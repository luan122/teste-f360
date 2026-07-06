# Terraform — Distributed Job Orchestrator (AWS reference)

Infrastructure-as-code for the deployment target chosen in
[`ARCHITECTURE.md`](../../ARCHITECTURE.md) **ADR-008**: a single EC2 instance running the same
`docker-compose.yml` topology used locally (MongoDB single-node replica set, RabbitMQ, Api,
Worker), provisioned via Terraform and bootstrapped with `cloud-init`.

This is a **reference/demo** deployment, not a hardened production topology — see ADR-008 for
why this shape was chosen over an ECS Fargate + DocumentDB + Amazon MQ layout (short version:
Amazon DocumentDB's MongoDB-API compatibility layer does not reliably support the
multi-document ACID transactions the transactional Outbox depends on).

## What this provisions

- A minimal VPC: one public subnet, an internet gateway, a route table.
- A security group exposing the Ingestion API port (`8080` by default), with SSH and the
  RabbitMQ management UI closed by default (opt-in via variables).
- One EC2 instance (Amazon Linux 2023), with an IAM instance profile granting SSM Session
  Manager access (so you don't need to open port 22 to reach it).
- An Elastic IP so the host has a stable address across restarts.
- `cloud-init` user data (`templates/cloud-init.yaml.tpl`) that installs Docker + the Compose
  plugin, clones this repository, writes a `.env` from the sensitive Terraform variables, and
  runs `docker compose up -d`.

It deliberately does **not** provision a container registry, CI/CD, DNS, or TLS termination —
out of scope per `specs/007-deliverables/spec.md` (the differentiator is the IaC code, not a
live hardened deployment).

## Prerequisites

- [Terraform](https://developer.hashicorp.com/terraform/downloads) >= 1.5.0.
- An AWS account and credentials available to the AWS provider (e.g. `aws configure`, or
  `AWS_ACCESS_KEY_ID`/`AWS_SECRET_ACCESS_KEY`/`AWS_SESSION_TOKEN` environment variables).
- (Only if you intend to `apply`) A published container image for the Api and Worker hosts
  (`container_image_api` / `container_image_worker`) and this repository pushed somewhere
  `git clone` can reach (`git_repository_url`).

## Variables you must supply before `apply`

The following have placeholder defaults and **must** be overridden — via `-var`, a `*.tfvars`
file (git-ignored if it contains real secrets), or `TF_VAR_*` environment variables — before
this is applied for real:

| Variable | Purpose |
|---|---|
| `git_repository_url` / `git_ref` | Where cloud-init clones the compose file from. |
| `container_image_api` / `container_image_worker` | Published images for the two hosts. |
| `api_key_secret` | Value clients send via `X-Api-Key`. **Sensitive.** |
| `mongo_root_password` | Root password for the Mongo container. **Sensitive.** |
| `rabbitmq_password` | Password for the RabbitMQ default user. **Sensitive.** |
| `ssh_key_name` / `allowed_ssh_cidr_blocks` | Only needed if you want SSH access in addition to SSM. |

Never commit real values for the sensitive variables — pass them via `TF_VAR_api_key_secret`
etc., a `.tfvars` file excluded by `.gitignore`, or a secrets manager integration.

## Usage

```bash
cd infra/terraform

terraform init

terraform validate

terraform plan \
  -var="git_repository_url=https://github.com/<you>/teste-f360.git" \
  -var="container_image_api=ghcr.io/<you>/job-orchestrator-api:latest" \
  -var="container_image_worker=ghcr.io/<you>/job-orchestrator-worker:latest" \
  -var="api_key_secret=$TF_VAR_api_key_secret" \
  -var="mongo_root_password=$TF_VAR_mongo_root_password" \
  -var="rabbitmq_password=$TF_VAR_rabbitmq_password"

# Only if you actually intend to stand up real AWS infrastructure:
terraform apply
```

`terraform init && terraform validate` is the acceptance bar for this deliverable
(`specs/007-deliverables/spec.md`, AC-007-3) — `apply` is optional and was **not** run against a
live account as part of this submission.

After a successful `apply`, `terraform output api_base_url` gives the URL to hit
`POST /jobs` against (allow a few minutes after apply for `cloud-init` to finish pulling images
and starting the compose stack). Use `terraform output ssm_session_command` to get a shell on the
host without opening SSH.

## Tearing down

```bash
terraform destroy
```

Destroys every resource this configuration created (VPC, subnet, security group, EC2 instance,
Elastic IP, IAM role). No resources here are created outside of this Terraform state.

## File map

| File | Purpose |
|---|---|
| `main.tf` | Provider, VPC/networking, security group, IAM role, EC2 instance + EIP. |
| `variables.tf` | All input variables, with descriptions and (non-secret) defaults. |
| `outputs.tf` | Public IP, API base URL, SSH/SSM access helpers. |
| `templates/cloud-init.yaml.tpl` | Cloud-init user-data template that bootstraps Docker Compose on the host. |
