# ADR-006 — Fila com prioridade do RabbitMQ (`x-max-priority`)

**Status:** Aceito

**Contexto.** O sistema deve suportar roteamento de jobs de alta prioridade para frente da fila
sem uma fila separada por tier de prioridade (o que multiplicaria a configuração de consumers
e complicaria o gerenciamento de workers).

**Decisão.** Usar o plugin de fila com prioridade nativo do RabbitMQ via o argumento de
declaração de fila `x-max-priority`. A topologia é configurada no `EndpointConfigurator` do
MassTransit ao configurar o `ReceiveEndpoint`. A prioridade é transportada como propriedade de
cabeçalho da mensagem e mapeada a partir do campo `Job.Priority` (`High = 10`, `Normal = 5`,
`Low = 1`) em `JobCreatedEventConsumerDefinition`.

**Consequências.**
- (+) Consumers únicos e tópico único — sem gerenciamento de múltiplos Consumer per-priority.
- (+) A semântica de prioridade é transparente para o código de negócio: definir `Job.Priority`
  é suficiente; a infraestrutura cuida do cabeçalho e da declaração da fila.
- (−) `x-max-priority` tem um custo de memória/CPU no broker proporcional ao número de buckets
  de prioridade (documentado pelo RabbitMQ: manter `x-max-priority` ≤ 10 para evitar overhead).
- (−) A fila deve ser declarada com `x-max-priority` na criação; alterá-lo depois requer
  deletar e recriar a fila.

---

*Voltar para [`ARCHITECTURE.md §5`](../../ARCHITECTURE.md#5-registros-de-decisão-de-arquitetura-adrs)*
