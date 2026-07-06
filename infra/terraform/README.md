# Terraform — Orquestrador de Jobs Distribuído (referência AWS)

Infraestrutura como código para o target de implantação escolhido em
[`ARCHITECTURE.md`](../../ARCHITECTURE.md) **ADR-008**: uma única instância EC2 rodando a mesma
topologia `docker-compose.yml` usada localmente (replica set MongoDB de nó único, RabbitMQ, Api,
Worker), provisionada via Terraform e inicializada com `cloud-init`.

Esta é uma implantação de **referência/demo**, não uma topologia de produção endurecida — veja
ADR-008 para entender por que essa forma foi escolhida em vez de ECS Fargate + DocumentDB +
Amazon MQ (resumo: a camada de compatibilidade MongoDB-API do Amazon DocumentDB não suporta de
forma confiável as transações ACID multi-documento das quais o Outbox transacional depende).

## O que é provisionado

- Uma VPC mínima: uma subnet pública, um internet gateway, uma route table.
- Um security group expondo a porta da API de Ingestão (`8080` por padrão), com SSH e a UI de
  gerenciamento do RabbitMQ fechados por padrão (habilitação opt-in via variáveis).
- Uma instância EC2 (Amazon Linux 2023), com um perfil IAM que concede acesso via SSM Session
  Manager (sem necessidade de abrir a porta 22 para acessá-la).
- Um Elastic IP para que o host tenha um endereço estável entre reinicializações.
- User data `cloud-init` (`templates/cloud-init.yaml.tpl`) que instala Docker + o plugin
  Compose, clona este repositório, grava um `.env` a partir das variáveis sensíveis do Terraform
  e executa `docker compose up -d`.

Intencionalmente **não** provisiona container registry, CI/CD, DNS ou terminação TLS — fora do
escopo deste entregável (o diferencial é o código IaC, não uma implantação real endurecida).

## Pré-requisitos

- [Terraform](https://developer.hashicorp.com/terraform/downloads) >= 1.5.0.
- Uma conta AWS e credenciais disponíveis para o provider AWS (ex.: `aws configure`, ou
  variáveis de ambiente `AWS_ACCESS_KEY_ID`/`AWS_SECRET_ACCESS_KEY`/`AWS_SESSION_TOKEN`).
- (Somente se você pretende executar `apply`) Uma imagem de container publicada para os hosts
  Api e Worker (`container_image_api` / `container_image_worker`) e este repositório acessível
  via `git clone` (`git_repository_url`).

## Variáveis obrigatórias antes do `apply`

As seguintes têm valores padrão de placeholder e **devem** ser substituídas — via `-var`, um
arquivo `*.tfvars` (ignorado pelo git se contiver segredos reais) ou variáveis de ambiente
`TF_VAR_*` — antes de aplicar de verdade:

| Variável | Finalidade |
|---|---|
| `git_repository_url` / `git_ref` | De onde o cloud-init clona o arquivo compose. |
| `container_image_api` / `container_image_worker` | Imagens publicadas para os dois hosts. |
| `api_key_secret` | Valor que os clientes enviam via `X-Api-Key`. **Sensível.** |
| `mongo_root_password` | Senha root para o container Mongo. **Sensível.** |
| `rabbitmq_password` | Senha para o usuário padrão do RabbitMQ. **Sensível.** |
| `ssh_key_name` / `allowed_ssh_cidr_blocks` | Necessário apenas se quiser acesso SSH além do SSM. |

Nunca commite valores reais para variáveis sensíveis — passe-os via `TF_VAR_api_key_secret`
etc., um arquivo `.tfvars` excluído pelo `.gitignore`, ou uma integração com gerenciador de
segredos.

## Uso

```bash
cd infra/terraform

terraform init

terraform validate

terraform plan \
  -var="git_repository_url=https://github.com/<você>/teste-f360.git" \
  -var="container_image_api=ghcr.io/<você>/job-orchestrator-api:latest" \
  -var="container_image_worker=ghcr.io/<você>/job-orchestrator-worker:latest" \
  -var="api_key_secret=$TF_VAR_api_key_secret" \
  -var="mongo_root_password=$TF_VAR_mongo_root_password" \
  -var="rabbitmq_password=$TF_VAR_rabbitmq_password"

# Somente se você realmente pretende subir infraestrutura AWS real:
terraform apply
```

`terraform init && terraform validate` é a barra de aceitação para este entregável — `apply` é
opcional e **não** foi executado contra uma conta real como parte desta entrega.

Após um `apply` bem-sucedido, `terraform output api_base_url` fornece a URL para chamar
`POST /jobs` (aguarde alguns minutos após o apply para o `cloud-init` terminar de baixar as
imagens e iniciar o stack). Use `terraform output ssm_session_command` para obter um shell no
host sem abrir SSH.

## Destruição

```bash
terraform destroy
```

Destrói todos os recursos criados por esta configuração (VPC, subnet, security group, instância
EC2, Elastic IP, IAM role). Nenhum recurso aqui é criado fora deste estado Terraform.

## Mapa de arquivos

| Arquivo | Finalidade |
|---|---|
| `main.tf` | Provider, VPC/rede, security group, IAM role, instância EC2 + EIP. |
| `variables.tf` | Todas as variáveis de entrada, com descrições e padrões (não sensíveis). |
| `outputs.tf` | IP público, URL base da API, helpers de acesso SSH/SSM. |
| `templates/cloud-init.yaml.tpl` | Template de user-data cloud-init que inicializa o Docker Compose no host. |
