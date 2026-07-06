# Orquestrador de Jobs Distribuído

Um orquestrador de jobs distribuído de alta disponibilidade, construído em C#/.NET 9 com Clean
Architecture e DDD. Ele ingere submissões de jobs via HTTP e os processa de forma assíncrona com
um pool de workers, garantindo que **nenhum job seja perdido** — mesmo que o processo da API, o
message broker ou o banco de dados falhem durante a execução.

Para a narrativa completa da arquitetura — o mapa de camadas da Clean Architecture, o Outbox
transacional, o mecanismo de claim distribuído, os diagramas C4 e o log de ADRs por trás de
cada decisão técnica — veja **[`ARCHITECTURE.md`](./ARCHITECTURE.md)**.

## Como funciona, em um parágrafo

Um cliente chama `POST /jobs` com um `Idempotency-Key`. A API valida o payload e grava o novo
`Job` **e** um registro de outbox em uma única transação MongoDB — nunca "salva e depois publica"
sem garantia. Um Outbox Dispatcher separado varre as linhas de outbox não enviadas e as publica
no RabbitMQ (com suporte a prioridade, via `x-max-priority`). Os workers consomem essas
mensagens, reivindicam o job atomicamente (`Queued → Processing`, protegido por uma atualização
condicional atômica para que dois workers nunca reivindiquem o mesmo job), executam o handler e
registram o resultado. Falhas são reprocessadas com backoff até um limite; quando esgotado (ou em
caso de mensagem inválida), o job vai para a fila de mensagens mortas e a mensagem é roteada
para a fila `_error` do RabbitMQ. Cada requisição carrega um `CorrelationId` que flui do log da
API pelos cabeçalhos da mensagem no outbox até os logs do worker, permitindo rastrear toda a
requisição de ponta a ponta.

## Pré-requisitos

- [Docker](https://docs.docker.com/get-docker/) e Docker Compose (v2, o plugin CLI `docker compose`
  — incluído no Docker Desktop).
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (veja [`global.json`](./global.json)
  para a versão exata fixada) — necessário apenas para build/execução/debug fora de containers
  ou para rodar os testes localmente.

## Início rápido

```bash
docker compose up
```

Isso sobe:

- **MongoDB**, inicializado como **replica set** de nó único (necessário para as transações
  multi-documento que o padrão Outbox usa — veja `ARCHITECTURE.md` ADR-001). O healthcheck do
  Compose executa `rs.initiate()` na primeira inicialização antes de permitir que a API e o
  Worker subam.
- **RabbitMQ**, com a UI de gerenciamento exposta (padrão `http://localhost:15672`,
  `guest`/`guest` no arquivo local) e uma fila com prioridade habilitada (`x-max-priority`) para
  mensagens `JobQueued`.
- **Api** (`JobOrchestrator.Api`), ouvindo em `http://localhost:8080`.
- **Worker** (`JobOrchestrator.Worker`), executando o Outbox Dispatcher, o liberador de jobs
  agendados e o consumer de `JobQueued` como background services, além dos próprios endpoints
  `/health/live` e `/health/ready` em `http://localhost:8081`.

Se as portas `8080`/`8081` já estiverem em uso, remapeie o lado esquerdo nas entradas `ports:`
do `docker-compose.yml`.

Com o stack saudável, confirme que ambos os hosts estão no ar:

```bash
curl http://localhost:8080/health/live
curl http://localhost:8080/health/ready   # 200 somente quando Mongo + RabbitMQ estão acessíveis
curl http://localhost:8081/health/live
curl http://localhost:8081/health/ready
```

### Executando sem Docker

```bash
dotnet build JobOrchestrator.slnx
dotnet run --project src/JobOrchestrator.Api
dotnet run --project src/JobOrchestrator.Worker
```

Você vai precisar de um replica set MongoDB local e RabbitMQ acessíveis nas connection strings de
`src/JobOrchestrator.Api/appsettings.Development.json` / `src/JobOrchestrator.Worker/appsettings.json`
(substitua via variáveis de ambiente ou user secrets — nunca commite connection strings reais).

### Executando os testes

```bash
dotnet test JobOrchestrator.slnx
```

Testes unitários (`tests/JobOrchestrator.UnitTests`) rodam sem infraestrutura externa — incluindo
um teste de arquitetura que quebra o build se `Domain` referenciar `Infrastructure`, MongoDB ou
MassTransit. Testes de integração (`tests/JobOrchestrator.IntegrationTests`) sobem um replica set
MongoDB via Testcontainers (e RabbitMQ, para as suites de mensageria) automaticamente — somente
o Docker é necessário, não um stack em execução.

## Autenticação

Todo endpoint `/jobs` requer autenticação; apenas os endpoints de health/liveness são anônimos.
Dois esquemas são aceitos — envie **um deles**:

- **API Key** — header `X-Api-Key: <chave>`. Na configuração local do Compose/dev, a chave
  pré-configurada é `local-dev-api-key` (veja `src/JobOrchestrator.Api/appsettings.json`, seção
  `ApiKeys`). Em qualquer implantação real, deve ser substituída via configuração/ambiente, nunca
  deixada como valor de exemplo.
- **JWT bearer** — header `Authorization: Bearer <jwt>`, validado contra a seção `Jwt` da
  configuração (`Issuer`, `Audience`, `SigningKey`). A emissão de tokens é externa a este sistema
  — a API valida tokens, não os emite.

Uma requisição não autenticada ou com credenciais inválidas para qualquer rota `/jobs` retorna
`401`.

## Usando a API

Contrato completo da API disponível via Swagger UI em `/swagger` ao rodar em modo Development.

### Enviar um job

`Idempotency-Key` é **obrigatório** — retentar com a mesma chave e mesmo body retorna o job
original (nunca um duplicado); reutilizá-la com um body *diferente* retorna `409`.

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

Uma resposta bem-sucedida é `202 Accepted` com um header `Location` e body:

```json
{
  "jobId": "b2b1a7e0-....",
  "status": "Queued",
  "correlationId": "demo-correlation-001"
}
```

(Um job imediato — sem `scheduledAt` — é enfileirado para entrega imediata. Um job com
`scheduledAt` futuro retorna `"status": "Scheduled"`.)

Para agendar um job para o futuro ao invés de executar imediatamente, adicione `"scheduledAt"`
(ISO-8601, ex.: `"2026-07-03T10:00:00Z"`) ao body — o job fica `Scheduled` até o vencimento.

### Consultar status do job

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

IDs desconhecidos retornam `404`.

### Cancelar um job

Permitido enquanto o job estiver `Scheduled`, `Queued` ou `Processing`; cancelar um job já
`Cancelled` é bem-sucedido sem efeito, e cancelar um job em estado terminal (`Completed` ou
`DeadLettered`) retorna `409`.

```bash
curl -i -X POST http://localhost:8080/jobs/b2b1a7e0-.../cancel \
  -H "X-Api-Key: local-dev-api-key"
```

Uma requisição bem-sucedida retorna `202 Accepted`. Se o job estiver `Processing`, o cancelamento
é cooperativo: o `CancellationToken` do worker é sinalizado e o job transita para `Cancelled`
no próximo ponto seguro de verificação.

## Estrutura do projeto

```
src/
  JobOrchestrator.Domain          # Entidades, value objects, a máquina de estados do Job — sem deps externas
  JobOrchestrator.Application     # Comandos/queries CQRS (MediatR), ports (interfaces), validadores
  JobOrchestrator.Infrastructure  # Repositórios Mongo/UoW, MassTransit, Polly, implementações Serilog
  JobOrchestrator.Api             # Host da API de ingestão — composition root, endpoints, auth
  JobOrchestrator.Worker          # Host do Worker — composition root, Outbox Dispatcher, consumers
tests/
  JobOrchestrator.UnitTests        # Testes de domínio + regras de arquitetura, sem infraestrutura externa
  JobOrchestrator.IntegrationTests # Testes de integração com Mongo/RabbitMQ via Testcontainers
infra/terraform/                  # IaC para a implantação de referência na AWS (veja ARCHITECTURE.md ADR-008)
docs/diagrams/                    # Cópia autônoma dos diagramas C4
```

## Leitura adicional

- **[`ARCHITECTURE.md`](./ARCHITECTURE.md)** — visão geral do sistema, mapa de camadas da Clean
  Architecture, a história de confiabilidade (Outbox/claim/idempotência/DLQ), diagramas C4 e o
  log completo de ADRs.
- **[`infra/terraform/README.md`](./infra/terraform/README.md)** — pré-requisitos e o fluxo
  init/validate/plan/apply do Terraform IaC.
