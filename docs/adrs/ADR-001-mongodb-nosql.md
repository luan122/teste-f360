# ADR-001 — MongoDB como banco de dados NoSQL

**Status:** Aceito

**Contexto.** O sistema usa um banco de dados NoSQL. A doutrina de confiabilidade exige um
Outbox transacional: o job e a "intenção de publicar" devem ser confirmados atomicamente ou não
serem confirmados. A maioria dos stores NoSQL abre mão de transações ACID multi-documento em
favor de escala; sem elas, o padrão Outbox degenera em "salvar e depois publicar" — o que viola
a garantia central de durabilidade.

**Decisão.** Usar **MongoDB**, executado como **replica set** (mesmo nó único), especificamente
porque as transações multi-documento do MongoDB exigem um replica set — um `mongod` standalone
rejeita `session.WithTransactionAsync(...)` com "Transaction numbers are only allowed on a
replica set member". Por isso o `docker-compose.yml` e os fixtures de testes de integração com
Testcontainers neste repositório explicitamente executam `rs.initiate()` contra um replica set
de nó único antes de a aplicação (ou os testes) se conectarem — pular esse passo anula
silenciosamente a garantia do Outbox (a chamada de transação lança em runtime, não em tempo de
compilação). O modelo de documento flexível do MongoDB também se adapta bem a `Job.Payload`/`Result`
(JSON opaco) e índices TTL atendem à expiração de chaves de idempotência e (opcionalmente) à
retenção de jobs.

**Consequências.**
- (+) Uma escrita transacional cobre job + linha de outbox; nenhum two-phase commit necessário.
- (+) Índices TTL oferecem expiração gratuita para registros de idempotência.
- (+) O shape de documento mapeia naturalmente para os agregados `Job`/`OutboxMessage`.
- (−) O bootstrap do replica set é mais uma peça móvel no Compose/Testcontainers/produção
  (veja [ADR-008](./ADR-008-terraform-ec2.md) para como o target Terraform lida com isso em
  ambiente gerenciado).
- (−) Transações multi-documento têm um pequeno custo de desempenho versus escritas de documento
  único; aceitável aqui porque a escrita transacional são dois documentos pequenos, não um batch.

---

*Voltar para [`ARCHITECTURE.md §5`](../../ARCHITECTURE.md#5-registros-de-decisão-de-arquitetura-adrs)*
