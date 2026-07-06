# ADR-008 — Target Terraform: host Docker Compose único em EC2 na AWS

**Status:** Aceito

**Contexto.** O projeto precisa de um target de infraestrutura demonstrável para a avaliação.
Opções consideradas: Kubernetes (EKS), ECS Fargate, VM única com Docker Compose, Lambda (sem
estado persistente — não adequado). O requisito é demonstrar que a infraestrutura está descrita
como código e pode ser provisionada de forma repetível.

**Decisão.** Usar **Terraform** para provisionar uma única instância EC2 na AWS com Docker
Engine instalado via `user_data`. O `docker-compose.yml` deste repositório é copiado para a
instância via `file` provisioner (ou S3) e o stack é iniciado com `docker compose up -d`. A
gestão de estado Terraform usa S3 backend + DynamoDB lock (configurado em
`infra/terraform/backend.tf`).

A escolha de EC2 única versus ECS/EKS é deliberada para este contexto: o foco da avaliação é
a arquitetura da aplicação e as garantias de confiabilidade, não a orquestração de containers.
Uma instância única com Compose é mais transparente para avaliadores e evita custo de EKS.

**Consequências.**
- (+) Infraestrutura totalmente descrita em código (`infra/terraform/`); `terraform plan` +
  `apply` reproduzem o ambiente.
- (+) Sem gerenciamento de cluster; qualquer engenheiro familiarizado com EC2 pode operar.
- (+) Replica set MongoDB de nó único funciona sem configuração adicional (veja
  [ADR-001](./ADR-001-mongodb-nosql.md)).
- (−) Sem alta disponibilidade: falha da instância EC2 derruba o stack. Aceito para este
  contexto de avaliação; produção real exigiria pelo menos ECS ou múltiplas AZs.
- (−) Escalonamento horizontal requer refatorar o Compose para múltiplas instâncias e adicionar
  um load balancer — não abordado nesta versão.

---

*Voltar para [`ARCHITECTURE.md §5`](../../ARCHITECTURE.md#5-registros-de-decisão-de-arquitetura-adrs)*
