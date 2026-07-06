# ADR-007 — DLQ via fila `_error` do MassTransit + status `DeadLettered`

**Status:** Aceito

**Contexto.** Jobs que falham repetidamente no processamento não devem ser silenciosamente
descartados nem bloquear a fila principal para sempre. O sistema precisa de uma estratégia de
Dead Letter que seja inspecionável (operadores podem ver os jobs mortos) e que altere o estado
do job de forma que seja legível via API.

**Decisão.** Combinar dois mecanismos:
1. **Fila `_error` do MassTransit**: quando todos os retries se esgotam, o MassTransit move a
   mensagem automaticamente para a fila `job-orchestrator_error` (convenção MassTransit). Isso
   requer zero código customizado.
2. **Status `DeadLettered`**: um consumer dedicado `DeadLetterConsumer` escuta a fila `_error`,
   lê o `CorrelationId` do cabeçalho da mensagem e faz `UpdateOneAsync` no job para
   `Status = DeadLettered`. Isso garante que `GET /jobs/{id}` retorne `DeadLettered` em vez de
   ficar preso em `Processing`.

A configuração de retry (tentativas + backoff) é em `WorkerServiceCollectionExtensions` via o
`UseMessageRetry` do MassTransit.

**Consequências.**
- (+) Sem código customizado de roteamento de DLQ — convenções MassTransit cuidam do
  roteamento; apenas o consumer de status precisa ser implementado.
- (+) Jobs mortos ficam visíveis via API (`GET /jobs/{id}` → `"status": "DeadLettered"`) e
  na fila `_error` para reprocessamento manual/replay.
- (−) O `DeadLetterConsumer` precisa ser resiliente a mensagens malformadas (sem
  `CorrelationId` válido) — tratadas com log + ack sem atualizar.
- (−) Reprocessamento de jobs mortos requer intervenção manual (mover da fila `_error` de volta
  para a fila principal); não há API de replay automático nesta versão.

---

*Voltar para [`ARCHITECTURE.md §5`](../../ARCHITECTURE.md#5-registros-de-decisão-de-arquitetura-adrs)*
