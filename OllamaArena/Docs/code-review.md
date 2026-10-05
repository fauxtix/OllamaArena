# Code Review — Feature "Benchmarks" (`/benchmark-evaluations`)

Auditoria externa e independente (Sem padrões internos do repositório), feita sobre o
conjunto de componentes que constitui a feature:

| Componente | Ficheiro |
|---|---|
| Página | `Components/Pages/BenchmarkEvaluations.razor` + `.razor.cs` |
| Diálogo de detalhe | `Components/Pages/Components/BenchmarkEvaluationDetail.razor` |
| Visualizador de análise | `Components/Pages/Components/BenchmarkAnalysisViewer.razor` |
| Repositório | `Services/Implementations/Repositories/{BenchmarkCommandRepository,BenchmarkQueryRepository,BenchmarkStatsRepository}.cs` + as interfaces homónimas em `Services/Interfaces/Repositories/` |
| Análise local | `Services/Implementations/Services/LocalAnalysisService.cs` + `IAnalysisService` |
| Apoio | `Services/Helpers/{JudgeOriginResolver,ScoreCalculator,MessageFormatter,HtmlOutputSanitizer}.cs`, `wwwroot/js/{pdf,excel}.js`, `Program.cs` |

## Como continuar este documento

Cada passo tem estado. `pendente` → `feito` / `parcial`. Um passo só passa a `feito`
quando o build passa (`dotnet build OllamaArena.slnx`) **e** os testes passam
(`dotnet test OllamaArena.slnx`).

---

## Fase 0 — Scope ✅ feito

Mapeamento do fluxo:

```
BenchmarkEvaluations.razor
  ├─ QueryRepo.BenchmarkResponseEvaluationAsync()   ──► BenchmarkQueryRepository (SQLite/Dapper)
  ├─ _filtroConsulta (cache) / BenchmarkEvaluationRules / CommandRepo.Delete*Async ──► BenchmarkCommandRepository
  ├─ AnalysisService.AnalisarBenchmarksAsync()      ──► LocalAnalysisService (ScoreCalculator)
  │                                                      └─ devolve rótulo + texto simples
  │                                                         └─► BenchmarkAnalysisViewer (texto escapado)
  ├─ ExportarParaExcel()  ──► BenchmarkExcelExporter ──► GET /api/export/benchmarks/{id} ──► JS downloadBenchmarksExcel
  └─ ExportarParaPdfAsync() ──► JS gerarRelatorioPDF (pdf.js, fontes embebidas)
BenchmarkEvaluationDetail.razor ──► QueryRepo.GetBenchmarkJudgesFeedbackByIdAsync()
```

## Fase 1 — Arquitetura e Design

| # | Achado | Decisão |
|---|---|---|
| A1 | `LocalAnalysisService` (domínio) devolve **strings com HTML** (`<strong>`, `<em>`, `&le;`) → obriga a `MarkupString` na vista e é a causa-raiz do XSS | `feito` — `BenchmarkAnalysisLine` separa rótulo de texto; o serviço devolve dados, a vista monta a marcação e o Blazor escapa o texto, pelo que deixou de ser preciso sanear |
| A2 | Catálogo das 12 métricas declarado **4×** (repositório, export PDF, análise local, diálogo de detalhe) | `feito` — `JudgeMetricCatalog` é agora a fonte única (`JudgeMetric`, `JudgeMetricKey`, limiares por métrica, `ScoreDoModelo`/`ScoreDaEntidade`); o `JudgeScoreInput` é indexado por `JudgeMetricKey`, pelo que a ordem dos campos deixou de ser significativa |
| A3 | Rótulo de juiz duplicado: `RotuloJuiz` local no export PDF reimplementa `JudgeLabelResolver.Rotulo` | `feito` — `JudgeLabelResolver.RotuloComOrigem`, usado pelo PDF e pelos diálogos |
| A4 | Diálogo de detalhe mostra **10 de 12** métricas (faltam Language Consistency e Loop Detection) | `feito` |
| A5 | `BenchmarkRepository` (~850 linhas) misturava read models, agregações e comandos | `feito` — repartido em `BenchmarkCommandRepository`, `BenchmarkQueryRepository` e `BenchmarkStatsRepository`, cada um com a sua interface; o SQL não foi reescrito |
| A6 | `IAnalysisService` registado com `AddHttpClient` mas **nunca faz HTTP**; `HttpClient` do construtor é descartado | `feito` |
| A7 | `IQueryable` sobre lista em memória (com método local no predicado, intraduzível) | `feito` — filtro materializado em `List<T>` e exposto como `IQueryable` **uma vez** por recálculo (`_filtroConsulta`); a grelha deixa de enumerar a cadeia de `Where` a cada render |

## Fase 2 — Qualidade C# / .NET

| # | Achado | Decisão |
|---|---|---|
| Q1 | `DeleteSpecificResponseAsync` é `async Task` mas o corpo é Dapper **síncrono** → bloqueia thread-pool | `feito` |
| Q2 | `AnalisarBenchmarksAsync` devolve `Task.FromResult` (síncrona) e **ignora o `CancellationToken`** → o botão "Cancelar" não funciona | `feito` — genuinamente assíncrona, cede a thread entre fases e valida o token no ciclo das métricas |
| Q3 | Sem `IDisposable` na página → `_cts` não é libertado | `feito` — `Dispose` cancela e liberta; token novo por execução (reutilizar um token já cancelado faria a 2.ª análise abortar de imediato) |
| Q4 | `_streamingLog`/`OnPartial`/`StreamingLog`/`FormatMessage`/`ConvertMarkdownToHtml` inalcançáveis | `feito` |
| Q5 | `EventCallback` (`IsAnalyzingChanged`, `AnaliseResultadoChanged`) declarados e nunca invocados; `@bind` não honrado | `feito` — o viewer só recebe `IsAnalyzing`, `AnaliseResultado` e `OnCancelarAnalise` |
| Q6 | `AlterarItensPorPagina` inalcançável | `feito` |
| Q7 | Export Excel gera o workbook de forma CPU-bound no sync context do circuito (congela a UI) | `feito` — `Task.Run` + `try/catch` + botão desativado durante a exportação |

## Fase 3 — Robustez

| # | Achado | Decisão |
|---|---|---|
| R1 | **3 definições conflitantes de "avaliado"** (`HasValue` / `> 0` / `== 0`) → diálogo diz "0 benchmark(s) ainda não avaliados" | `feito` — `BenchmarkEvaluationRules` |
| R2 | Ordenação **lexicográfica** das colunas de tempo (`"10:00" < "9:00"`) | `feito` — `DurationTextComparer` compara `m:ss` numericamente; a coluna mantém o formato visível e passa a ordenar por tempo real |
| R3 | `OnParametersSetAsync` consulta a BD em **cada** render do pai; `_lastResponseId` atribuído mas nunca lido | `feito` — só consulta quando `Visible` e o `ResponseId` mudam |
| R4 | Linhas órfãs (`LEFT JOIN`, `ResponseId = 0`) contam nos totais e a eliminação é um no-op silencioso | `feito` — `INNER JOIN` |
| R5 | Paginação não reposta nem limitada após eliminação/filtro/pesquisa | `feito` — `AjustarPaginaAsync` (via `SetCurrentPageIndexAsync`, que é a única forma de escrever em `PaginationState`) |
| R6 | `ExportarParaExcel` sem `try/catch` (ao contrário do PDF) | `feito` |
| R7 | Erros engolidos sem feedback ao utilizador; `GetBenchmarkJudgesFeedbackByIdAsync` devolve vazio em erro → erro apresentado como "sem dados" | `feito` — falhas de eliminação e exportação mostram diálogo; o repositório propaga o erro de leitura e o detalhe mostra um aviso em vez de "sem feedback" |
| R8 | `DeleteScope.Todos` apaga **toda** a tabela `Prompts` com contagem obsoleta do cache | `feito` — a eliminação total recarrega do BD (`GetAllBenchmarks`) em vez de filtrar a cache, para não divergir do que outro separador entretanto escreveu |

## Fase 4 — Segurança e Performance

| # | Achado | Decisão |
|---|---|---|
| S1 | **XSS armazenado**: `LinhasAnaliseDetalhada` renderizada com `(MarkupString)` sem allow-list; as linhas interpolam `NomeModelo` (tag do modelo persistida) | `feito` — as linhas deixaram de ser HTML (`BenchmarkAnalysisLine`) e são renderizadas como texto, que o Blazor escapa; o nome do modelo aparece literalmente. `HtmlOutputSanitizer` ficou apenas para o texto estático de estado |
| S2 | Base64 do `.xlsx` inteiro atravessa o circuito SignalR (limite 32 MB) | `feito` — o workbook vai para um ficheiro temporário e a página descarrega-o por `fetch`/Blob contra `GET /api/export/benchmarks/{id}`; o limite de 32 MB deixa de se aplicar à exportação |
| S3 | `DataCriacao` gravada em UTC e **exibida sem conversão** (dois fusos na mesma UI) | `feito` — `DataCriacaoFormatada` converte para hora local, com guarda para não deslocar duas vezes |
| S4 | N varreduras O(n) por render (`_quantidadePorAvaliar`/`Avaliados`/`Filtrados` + getter `FilteredBenchmarks`) | `feito` — `RecalcularAsync` calcula lista e contagens uma vez, por mudança de estado |
| S5 | Pesquisa numérica em `InvariantCulture` → "12,5" (pt) nunca encontra | `feito` — compara em ambas as notações |
| S6 | `catch { }` a engolir falhas de JS interop no scroll | removido com o código morto |

---

## Dívidas registadas — fechadas

Todas as dívidas que ficaram em aberto na ronda anterior foram implementadas e
verificadas.

| # | Dívida | Como ficou |
|---|--------|------------|
| D1 | A1: `LocalAnalysisService` devolvia texto com HTML | `BenchmarkAnalysisLine` (`Models/DTO/`) separa **rótulo** de **texto**; o serviço devolve dados e a vista monta a marcação. As linhas deixaram de passar por `MarkupString`/`HtmlOutputSanitizer`, porque o Blazor escapa o texto — um nome de modelo vindo da BD é apresentado literalmente em vez de ser interpretado |
| D2 | A5: `BenchmarkRepository` concentrava read models, agregações e comandos | Interface segregada em `IBenchmarkCommandRepository`, `IBenchmarkQueryRepository` e `IBenchmarkStatsRepository`, com uma implementação por responsabilidade (`BenchmarkCommandRepository`, `BenchmarkQueryRepository`, `BenchmarkStatsRepository`). Cada consumidor injecta só o que usa — `TranslationService` passou a depender só das agregações e `OllamaGpuService` deixou de injectar o repositório que nunca usou |
| D3 | S2: `.xlsx` gerado em memória e enviado em base64 | O exportador escreve para `%TEMP%/ollamaarena-exports/{Guid:N}.xlsx` e a página descarrega por `fetch`/Blob contra `GET /api/export/benchmarks/{identificador}`. O ficheiro apaga-se em `HttpContext.Response.OnCompleted`; identificadores que não sejam Guid "N" dão 404, por isso não há forma de sair da pasta temporária |
| D4 | `ScoreCalculator`/`JudgeScoreInput`: 12 parâmetros posicionais | `JudgeScoreInput` é indexado por `JudgeMetricKey` e `JudgeScoreWeights.Peso(JudgeMetricKey)` é a fonte única dos pesos; a posição deixou de ser significativa |
| D5 | Duração ordenadas lexicograficamente no `HistoryPanel` | `DurationTextComparer` aceite `m:ss` e `HH:mm:ss`, aplicado também ao `HistoryPanel` |
| D6 | R8: "apagar todos" eliminava a tabela `Prompts` inteira sem confirmação própria | Diálogo próprio, com título e texto distintos por âmbito (filtrado, não avaliados, avaliados, tudo) e confirmação explícita para a opção destrutiva |
| — | Falta de feedback ao copiar para a área de transferência | Toast de sucesso/erro, em PT e EN |

## Estado

Build e testes verdes na última execução:

- `dotnet build OllamaArena.slnx` → 0 avisos, 0 erros
- `dotnet test OllamaArena.slnx` → 410 testes, 0 falhas

Verificação manual do endpoint de exportação (app em `http://localhost:5292`):

- identificador inválido (`nao-existe`) → 404
- ficheiro temporário válido → 200, `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`, apagado no fim da resposta

Testes novos desta ronda:

| Ficheiro | Cobre |
|---|---|
| `DurationTextComparerTests.cs` | Ordenação numérica de `m:ss`, valores ausentes, texto não interpretável |
| `DurationTextComparerTests.cs` (`BenchmarkEvaluationModelDateTests`) | UTC → hora local sem dupla conversão |
| `LocalAnalysisServiceTests.cs` | Notas 0 como "não avaliadas", token já cancelado, cancelamento a meio da análise (determinístico), acumulação de métricas fracas entre prompts, linhas sem marcação HTML, nome de modelo hostil preservado como texto |
| `BenchmarkAnalysisLineTests.cs` | Separação rótulo/texto a partir das strings reais dos recursos: marcador, emoji, linha só com rótulo, frase sem `<strong>`, `<em>` no corpo, `&le;`/`&ge;`/`&amp;`/`&nbsp;`, colapso de espaços, nome de modelo hostil, entradas vazias |
| `BenchmarkExcelExporterTests.cs` | `.xlsx` gerado e válido, folhas/celulas esperadas, dados vazios e nulos, identificadores distintos, cancelamento, identificador malicioso rejeitado, limpeza de ficheiros expirados |
| `ScoreCalculatorTests.cs` / `JudgeMetricCatalogTests.cs` | 12 métricas e ordem, chaves de recurso únicas, limiar de `Safety`, acessores entidade/modelo, `ParesEntidade`, média ponderada por métrica |

## Resultado

`feito`: A1, A2, A3, A4, A5, A6, A7, Q1–Q7, R1–R8, S1, S2, S3, S4, S5.
Sem dívidas registadas em aberto.