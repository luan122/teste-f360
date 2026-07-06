# Distributed Job Orchestrator

A high-availability, distributed job orchestrator built in C#/.NET 9 using Clean Architecture
and DDD. It ingests job submissions over HTTP and processes them asynchronously with a pool of
workers, guaranteeing that **no job is ever lost** — even if the API process, the message
broker, or the database crashes mid-flight.

This repository is the implementation of the take-home challenge described in
[`GOAL.md`](../GOAL.md) (Portuguese, one directory above this repo root): design and build a
distributed job orchestrator that can absorb thousands of requests per minute, survive
infrastructure failures without losing work, support job priority ("Alta" jobs jump the queue),
scheduled execution, cooperative cancellation, and resilient processing (circuit breaker + Dead
Letter Queue), all on top of a NoSQL store and RabbitMQ.

For the full architecture narrative — the Clean Architecture layer map, the transactional
Outbox, the distributed claim mechanism, C4 diagrams, and the ADR log behind every major
technology choice — see **[`ARCHITECTURE.md`](./ARCHITECTURE.md)**. For the spec-driven-development
artifacts (per-feature `spec.md`/`plan.md`/`tasks.md` and the full GOAL.md traceability matrix),
see **[`README-SPECS.md`](./README-SPECS.md)** and the **[`specs/`](./specs/)** folder.

## How it works, in one paragraph

A client calls `POST /jobs` with an `Idempotency-Key`. The API validates the payload, and writes
the new `Job` **and** an outbox record in a single MongoDB transaction — never "save, then
publish" without a guarantee. A separate Outbox Dispatcher polls unsent outbox rows and publishes
them to RabbitMQ (priority-aware, via `x-max-priority`). Workers consume those messages,
atomically claim the job (`Queued → Processing`, guarded by an atomic conditional update so two
workers can never claim the same job), execute the handler, and record the outcome. Failures are
retried with backoff up to a budget; once exhausted (or on a poison message), the job is
dead-lettered and the message routed to RabbitMQ's `_error` queue. Every request carries a
`CorrelationId` that flows from the API log through the outbox message headers into the worker's
logs, so one request is traceable end-to-end.

## Prerequisites

- [Docker](https://docs.docker.com/get-docker/) and Docker Compose (v2, the `docker compose`
  CLI plugin — bundled with Docker Desktop).
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (see [`global.json`](./global.json)
  for the exact pinned version) — only needed if you want to build/run/debug outside containers,
  or run the test suites locally.

## Quick start

```bash
docker compose up
```

This brings up, per [`specs/001-solution-foundation`](./specs/001-solution-foundation/spec.md):

- **MongoDB**, initialized as a single-node **replica set** (required for the multi-document
  transactions the Outbox pattern relies on — see `ARCHITECTURE.md` ADR-001). The compose
  healthcheck runs `rs.initiate()` on first start before the API/Worker are allowed to start.
- **RabbitMQ**, with the management UI exposed (default `http://localhost:15672`, `guest`/`guest`
  in the local compose file) and a priority-enabled queue (`x-max-priority`) for `JobQueued`
  messages.
- **Api** (`JobOrchestrator.Api`), listening on `http://localhost:8080`.
- **Worker** (`JobOrchestrator.Worker`), running the Outbox Dispatcher, the scheduled-job
  releaser, and the `JobQueued` consumer as background services, plus its own `/health/live` and
  `/health/ready` endpoints on `http://localhost:8081`.

If port `8080`/`8081` is already taken on your machine, remap the left-hand side in
`docker-compose.yml`'s `ports:` entries.

Once the stack is healthy, confirm both hosts are up:

```bash
curl http://localhost:8080/health/live
curl http://localhost:8080/health/ready   # 200 only when Mongo + RabbitMQ are both reachable
curl http://localhost:8081/health/live
curl http://localhost:8081/health/ready
```

### Running without Docker

```bash
dotnet build JobOrchestrator.slnx
dotnet run --project src/JobOrchestrator.Api
dotnet run --project src/JobOrchestrator.Worker
```

You'll need a local MongoDB replica set and RabbitMQ reachable at the connection strings in
`src/JobOrchestrator.Api/appsettings.Development.json` / `src/JobOrchestrator.Worker/appsettings.json`
(override via environment variables or user secrets — never commit real connection strings, per
constitution §6).

### Running the tests

```bash
dotnet test JobOrchestrator.slnx
```

Unit tests (`tests/JobOrchestrator.UnitTests`) run with no external infrastructure — including an
architecture test that fails the build if `Domain` ever references `Infrastructure`, MongoDB, or
MassTransit. Integration tests (`tests/JobOrchestrator.IntegrationTests`) spin up a Testcontainers
MongoDB replica set (and RabbitMQ, for messaging-related suites) automatically — only Docker is
required, not a running compose stack.

## Authentication

Every `/jobs` endpoint requires authentication; only health/liveness endpoints are anonymous
(constitution §6). Two schemes are accepted — send **either**:

- **API Key** — header `X-Api-Key: <key>`. In the local compose/dev configuration, the seeded
  key is `local-dev-api-key` (see `src/JobOrchestrator.Api/appsettings.json`, section `ApiKeys`).
  In any real deployment this must be overridden via configuration/environment, never left as
  the sample value.
- **JWT bearer** — header `Authorization: Bearer <jwt>`, validated against the `Jwt` configuration
  section (`Issuer`, `Audience`, `SigningKey`). Token issuance is external to this system — the
  API validates tokens, it does not mint them.

An unauthenticated or invalid-credential request to any `/jobs` route returns `401`.

## Using the API

Full contract: [`specs/002-ingestion-api/contracts/openapi.yaml`](./specs/002-ingestion-api/contracts/openapi.yaml).

### Submit a job

`Idempotency-Key` is **required** — retrying the same key with the same body returns the
original job (never a duplicate); reusing it with a *different* body returns `409`.

```bash
curl -i -X POST http://localhost:8080/jobs \
  -H "X-Api-Key: local-dev-api-key" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: 3f1c9e2a-8b1d-4a5a-9c2e-6e6f1a2b3c4d" \
  -H "X-Correlation-Id: demo-correlation-001" \
  -d '{
        "type": "SendWelcomeEmail",
        "priority": "High",
        "maxAttempts": 5,
        "payload": { "userId": "u_123", "locale": "pt-BR" }
      }'
```

A successful response is `202 Accepted` with a `Location` header and body:

```json
{
  "jobId": "b2b1a7e0-....",
  "status": "Queued",
  "correlationId": "demo-correlation-001"
}
```

(An immediate job — no `scheduledAt` — is queued for delivery right away. A job with a future
`scheduledAt` instead comes back `"status": "Scheduled"`.)

To schedule a job for the future instead of running it immediately, add `"scheduledAt"`
(ISO-8601, e.g. `"2026-07-03T10:00:00Z"`) to the body — the job stays `Scheduled` until due, per
[`specs/003-task-management`](./specs/003-task-management/spec.md).

### Check job status

```bash
curl http://localhost:8080/jobs/b2b1a7e0-.... \
  -H "X-Api-Key: local-dev-api-key"
```

```json
{
  "jobId": "b2b1a7e0-....",
  "type": "SendWelcomeEmail",
  "priority": "High",
  "status": "Completed",
  "scheduledAt": null,
  "attempts": 1,
  "maxAttempts": 5,
  "correlationId": "demo-correlation-001",
  "error": null,
  "createdAt": "2026-07-02T12:00:00Z",
  "updatedAt": "2026-07-02T12:00:03Z"
}
```

Unknown ids return `404`.

### Cancel a job

Legal while the job is `Scheduled`, `Queued`, or `Processing`; cancelling an already-`Cancelled`
job is a no-op success, and cancelling a job in a terminal state (`Completed`/`DeadLettered`)
returns `409`.

```bash
curl -i -X POST http://localhost:8080/jobs/b2b1a7e0-.../cancel \
  -H "X-Api-Key: local-dev-api-key"
```

A successful request returns `202 Accepted`. If the job is `Processing`, cancellation is
cooperative: the worker's `CancellationToken` is signalled and the job transitions to `Cancelled`
at the next safe checkpoint (see [`specs/003-task-management`](./specs/003-task-management/spec.md)).

## Project layout

```
src/
  JobOrchestrator.Domain          # Entities, value objects, the Job state machine — no external deps
  JobOrchestrator.Application     # CQRS commands/queries (MediatR), ports (interfaces), validators
  JobOrchestrator.Infrastructure  # Mongo repositories/UoW, MassTransit, Polly, Serilog implementations
  JobOrchestrator.Api             # Ingestion API host — composition root, endpoints, auth
  JobOrchestrator.Worker          # Worker host — composition root, Outbox Dispatcher, consumers
tests/
  JobOrchestrator.UnitTests        # Domain + architecture-rule tests, no external infrastructure
  JobOrchestrator.IntegrationTests # Testcontainers-backed Mongo/RabbitMQ integration tests
specs/                            # Spec-driven-development artifacts (spec/plan/tasks per feature)
infra/terraform/                  # IaC for the reference AWS deployment (see ARCHITECTURE.md ADR-008)
docs/diagrams/                    # Standalone copy of the C4 diagrams
```

## Further reading

- **[`ARCHITECTURE.md`](./ARCHITECTURE.md)** — system overview, Clean Architecture layer map,
  the reliability story (Outbox/claim/idempotency/DLQ), C4 diagrams, and the full ADR log.
- **[`README-SPECS.md`](./README-SPECS.md)** — how the spec-driven-development artifacts are
  organized, the feature catalog, and the GOAL.md → spec → acceptance-criterion traceability
  matrix.
- **[`specs/`](./specs/)** — one folder per capability (`000-domain-model` through
  `007-deliverables`), each with `spec.md` (what/why) and `plan.md` (how).
- **[`infra/terraform/README.md`](./infra/terraform/README.md)** — prerequisites and the
  init/validate/plan/apply flow for the Terraform IaC.
