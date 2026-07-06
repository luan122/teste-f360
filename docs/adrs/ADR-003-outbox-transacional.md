# ADR-003 — Outbox transacional implementado manualmente em vez do outbox embutido do MassTransit

**Status:** Aceito

**Contexto.** O MassTransit oferece suas próprias integrações de outbox (ex.: o outbox EF Core,
e um outbox in-memory/bus para garantir publish-after-consume). O padrão Outbox deve ser
demonstrado como uma garantia transacional real e visível — não escondido dentro de um recurso
de framework que um avaliador não consegue inspecionar.

**Decisão.** Implementar o Outbox **manualmente** contra o MongoDB: `Job` e `OutboxMessage` são
gravados na mesma transação `IUnitOfWork.ExecuteInTransactionAsync` (`MongoUnitOfWork` +
`MongoSessionAccessor`), e um hosted service dedicado `OutboxDispatcher` varre, faz lease,
publica e marca as linhas como enviadas. O suporte de outbox próprio do MassTransit é voltado
principalmente para provedores EF Core/relacionais e obscureceria o mecanismo exato que este
projeto precisa demonstrar.

**Consequências.**
- (+) A garantia transacional é código explícito e inspecionável (`MongoUnitOfWork`,
  `OutboxMessage.Lease`/`MarkSent`, `MongoOutboxRepository`) em vez de uma caixa-preta —
  responde diretamente ao requisito.
- (+) Controle total sobre a semântica de lease/claim para múltiplas instâncias de dispatcher.
- (−) Mais código para manter e testar do que adotar um recurso de framework (mitigado: coberto
  por testes de integração em `tests/JobOrchestrator.IntegrationTests`).
- (−) Publicações duplicadas são possíveis na janela de falha entre publicar e marcar como sent;
  isso é aceito e tornado inofensivo pela idempotência do consumer em vez de evitado.

---

*Voltar para [`ARCHITECTURE.md §5`](../../ARCHITECTURE.md#5-registros-de-decisão-de-arquitetura-adrs)*
