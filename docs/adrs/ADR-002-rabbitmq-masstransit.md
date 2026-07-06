# ADR-002 — RabbitMQ + MassTransit como abstração de mensageria

**Status:** Aceito

**Contexto.** O sistema usa RabbitMQ como message broker. Uma camada de abstração como o
MassTransit evita acoplar ao client library do RabbitMQ em todo o código.

**Decisão.** Usar **RabbitMQ** como broker e **MassTransit** como abstração .NET sobre ele
(`IPublishEndpoint` para publicação, `IConsumer<T>` para consumo). O MassTransit também fornece
middleware de retry/redelivery e as convenções de fila `_error`/`_skipped` usadas pela estratégia
de DLQ ([ADR-007](./ADR-007-dead-letter-queue.md)) e pela topologia de fila com prioridade
([ADR-006](./ADR-006-fila-prioridade.md)).

**Consequências.**
- (+) Consumers e publishers são testáveis contra um transporte in-memory em testes unitários, e
  contra um broker real via Testcontainers em testes de integração.
- (+) Retry/redelivery, roteamento de DLQ e cabeçalhos de mensagem (usados para transportar
  `CorrelationId`) são configurados declarativamente em vez de implementados manualmente.
- (−) Adiciona uma dependência e sua própria superfície de configuração (`RabbitMqOptions` em
  `Infrastructure/Messaging`); o time deve entender a distinção entre retry e redelivery do
  MassTransit para configurar o backoff corretamente (veja [ADR-007](./ADR-007-dead-letter-queue.md)).

---

*Voltar para [`ARCHITECTURE.md §5`](../../ARCHITECTURE.md#5-registros-de-decisão-de-arquitetura-adrs)*
