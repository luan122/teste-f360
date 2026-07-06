# ADR-005 — Claim distribuído via atualização condicional atômica do Mongo

**Status:** Aceito

**Contexto.** Múltiplas instâncias do Worker consomem a mesma fila RabbitMQ. Quando uma mensagem
é entregue a um Worker e ele falha ou demora, o RabbitMQ pode reentregá-la a outro Worker — que
pode então tentar processar o mesmo job concorrentemente com o primeiro Worker (se este for
retomado). O sistema precisa garantir que exatamente um Worker "possua" o processamento de um
job em um dado momento, sem um lock distribuído separado ou um coordenador externo.

**Decisão.** Usar um `UpdateOneAsync` condicional do MongoDB como operação de claim atômica:
`{ Status: Queued } → { Status: Processing, WorkerId: thisWorker, ClaimedAt: now }`. Se a
operação retorna `MatchedCount == 0`, outro Worker já reclamou o job, e o Consumer atual
descarta a mensagem silenciosamente (ack sem processar). Isso é implementado em
`MongoJobRepository.TryClaimAsync`. O campo `WorkerId` permite que a extensão de heartbeat
identifique o "proprietário" e stale-job cleanup pode expirar claims presos.

**Consequências.**
- (+) Sem lock distribuído separado, sem ZooKeeper/Redis, sem coordenador externo — a atomicidade
  de documento único do MongoDB serve como árbitro.
- (+) Corridas são resolvidas de forma limpa: exatamente um winner, perdedores descartam
  silenciosamente.
- (−) Requer disciplina dos callers: todo código que processa um job *deve* passar por
  `TryClaimAsync` e verificar o resultado — não há enforcement em tempo de compilação.
- (−) Stale claims (Worker crashou com status `Processing`) precisam de um job de cleanup
  separado (não implementado nesta versão; documentado como gap).

---

*Voltar para [`ARCHITECTURE.md §5`](../../ARCHITECTURE.md#5-registros-de-decisão-de-arquitetura-adrs)*
