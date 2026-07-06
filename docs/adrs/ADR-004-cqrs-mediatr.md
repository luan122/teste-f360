# ADR-004 — CQRS via MediatR

**Status:** Aceito

**Contexto.** Separar o caminho de escrita (crítico para durabilidade: criar/cancelar) do caminho
de leitura (consulta de status) esclarece quais operações devem respeitar a doutrina de
confiabilidade e permite que cada um seja testado independentemente.

**Decisão.** Usar **MediatR** como mediador in-process. Commands (`CreateJobCommand`,
`CancelJobCommand`) e Queries (`GetJobStatusQuery`) ficam em `Application/Features/Jobs`, cada
um com seu próprio handler — commands e queries nunca compartilham um handler.
`ApplicationServiceCollectionExtensions.AddApplication()` registra o MediatR e varre o assembly
em busca de handlers/validadores, mais um comportamento de pipeline `ValidationBehavior<,>` que
executa o FluentValidation antes de qualquer handler.

**Consequências.**
- (+) Separação clara: um avaliador pode ver de relance que `GetJobStatusQueryHandler` nunca
  muta estado.
- (+) O comportamento de pipeline de validação é transversal — novos commands recebem validação
  gratuitamente ao registrar um validador, sem boilerplate no handler.
- (−) Uma camada extra de indireção (dispatch do mediador) versus chamar serviços de aplicação
  diretamente; considerado válido pela clareza que o CQRS oferece para avaliadores.

---

*Voltar para [`ARCHITECTURE.md §5`](../../ARCHITECTURE.md#5-registros-de-decisão-de-arquitetura-adrs)*
