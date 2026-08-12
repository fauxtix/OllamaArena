# Relatório de Análise — Ollama Chat & Benchmark Laboratory

**Aplicação:** OllamaArena (Blazor Server, .NET 10) · **Data:** 10 de agosto de 2026

> **Nota de atualização:** após a geração inicial deste relatório foram corrigidos dois dos bugs identificados (secções 2.4 e 2.5). Este documento é a **fonte de verdade** em Markdown; o ficheiro `Docs/Relatorio_Analise.pdf` é o artefacto de distribuição correspondente.

---

## 1. Pontos fortes

- **Conceito diferenciado.** A ideia de "laboratório de benchmark" — medir automaticamente tokens/s, tempo de carga e de processamento em cada resposta, cruzar com avaliação de juízes externos (Gemini/OpenRouter) e gerar análise local — é sólida e bem documentada (README PT/EN, `Funcionalidades.md`, `schema.sql`).
- **Localização madura.** `SharedResources.resx`/`.en.resx` com 334 chaves cada, paridade total, e ~99% dos textos da interface passam por `L[...]`. Pouco comum.
- **SQL seguro.** Todo o acesso a dados usa Dapper com parâmetros `@`; não foi encontrada nenhuma SQL injection.
- **`ChatMeasureTemperature`** (`Services/ChatMeasureTemperature.cs`) — um dos melhores ficheiros do projeto: heurística lexical PT/EN para temperatura, deteção de código, densidade de perguntas; bem comentado, testável e sem dependências.
- **Arquitetura em camadas** com interfaces (Repositories/Services e distinção DTO vs Entities) — boa intenção que facilita a correção da dívida.
- **Acessibilidade parcial boa** — `aria-expanded` nos cabeçalhos colapsáveis, `@onclick:stopPropagation` nos cards, diálogos com `AriaLabel`.
- **Streaming de chat robusto** na via ativa (`SendMessage`, `Components/Pages/Chat.razor.cs:208`) — timeout alargado, cancelamento com descarga da VRAM e gestão de contexto dinâmico.

## 2. Bugs técnicos críticos

| # | Problema | Evidência | Estado |
|---|---|---|---|
| 1 | ~700 linhas de código duplicado: três implementações de envio de mensagem (`SendMessage` ativa; `SendMessageWithOllamaSharpAsync` e `SendMessageWithOllamaFastAsync` mortas). | `Chat.razor.cs:208, 868, 1094` | Em aberto |
| 2 | Serilog grava cada log 2 vezes — sink definido no JSON e novamente no Program.cs. | `Program.cs:32-39` + `appsettings.json:19-29` | Em aberto |
| 3 | `ON DELETE CASCADE` não funciona — `PRAGMA foreign_keys` nunca é ativado; apagar um prompt deixa `Respostas` órfãs. | `DapperContext.cs:25` vs `Docs/schema.sql:5` | Em aberto |
| 4 | Agrupamento por prompt quebrado — `BenchmarkResponseEvaluationAsync` nunca selecionava `R.PromptId`, logo `PromptId` era sempre 0. | `BenchmarkRepository.cs:368-385` | **Corrigido** |
| 5 | Crash na análise local quando todos os `*FactualRating` são NULL — `.First()` lançava exceção. | `LocalAnalysisService.cs:92-102` | **Corrigido** |
| 6 | Check de VRAM usa WMI `AdapterRAM` (uint32) que devolve 0 em GPUs > 4 GiB, ignorando o caminho `nvidia-smi` já implementado; além disso `nvidia-smi` bloqueia porque `ReadToEnd()` precede `WaitForExit`. | `OllamaGpuService.cs:170, 114-115` | Em aberto |
| 7 | `/culture-reload` — XSS refletido / open redirect: `redirectUri` interpolado cru em HTML/JS. | `Program.cs:114-120` | Em aberto |
| 8 | Rate-limit dos juízes consumido mesmo em falha e validado antes das chaves da API. | `AutomatedJudgeService.cs:102-103, 121-129` | Em aberto |
| 9 | Concorrência SQLite: `VACUUM` após cada delete + `batchSize:1` + sink duplicado; exceções "database is locked" são engolidas — perda silenciosa de dados. | `LogRepository.cs:38`, `BenchmarkRepository.cs:68-71`, `Chat.razor.cs:509-512` | Em aberto |
| 10 | `HttpClient` partilhado com `DefaultRequestHeaders` mutado nas pesquisas web — condição de corrida com a descarga de modelo em background. | `Chat.razor.cs:724-730, 552, 690` | Em aberto |
| 11 | Erros mascarados: `TranslationService` lança `InvalidOperationException("Sem informação de modelos")` para qualquer falha, sem exceção interior. | `TranslationService.cs:49-53` | Em aberto |

## 3. Dívida técnica / código morto

- **Páginas mortas e não ligadas no menu**: `Settings.razor` (lista de modelos fake hardcoded `phi4-mini/mistral/qwen3.5`), `Settings2.razor` (refactor abandonado a meio), `AnaliseBenchmark.razor` (substituída por `/benchmark-evaluations`).
- **`ConversationRepository`** chama stored procedures SQL Server (`usp_Conversation_*`) contra SQLite — rebentaria se fosse registado em DI.
- **DTOs/serviços nunca usados**: `TranslateClientService`, `ChatMessageDto`, `ChatMessageModel`, `HistoryEntry`, `ConversationItem`, `LanguageDetectionResult`, `TranslateRequest/ResponseDto`, `Entities\ChatMessage`, `QualityAnalysis.resx` inteiro.
- **`TranslatePromptTemplate.TranslationPrompt`** é um duplicado morto do `translation-prompt.txt`.
- **`PromptTemplateProvider._cache`** nunca é preenchido — I/O a disco em cada chamada.
- **`SystemPromptService`** registado em DI mas nunca injetado (o Chat lê o ficheiro diretamente).
- **RAG web morto**: `SearchWebContext_DuckDuckGo_Async` e `SearchWebContext_Wikipedia_Async` — funcionalidade completa, mas sem botão/ligação.
- **Interop JS roto**: `BenchmarkAnalysisViewer.razor:144` chama `scrollToBottom` inexistente; `Chat.razor.cs:597` chama `chatInput.handleKey` inexistente.
- **`SharpToken`** na csproj, mas a estimativa de tokens é `len/4` (`Chat.razor.cs:646-650`).

## 4. Funcional / Experiência de utilizador

- **Histórico fragmentado**: localStorage do browser (via JS) e `BenchmarkRepository` na BD — dois sistemas de memória de conversas.
- **Cores hardcoded** (116 ocorrências hex/rgba) quebram o tema escuro; o próprio `NavMenu.razor:53-56` usa `#f0f0f0` invisível no dark mode.
- **`FluentDesignTheme` duplicado** (MainLayout + Settings + Settings2 + ModelosOllama).
- **ModelosOllama** mostra "Nenhum modelo detetado" quando na verdade houve erro.
- **Botões só-ícone sem `aria-label`** (eliminar logs, downloads de gráficos).
- `appsettings.json` com `"DetailedErrors": true` e `AllowedHosts: "*"`.
- Chaves de API em texto simples em `appsettings.Local.json`; a chave Gemini viaja no query string da URL.
- **Ollama base URL hardcoded em 10+ sítios** (`localhost:11434`); zero configuração centralizada.

## 5. Plano de melhoria proposto

### Fase 1 — Estabilizar (bugs, sem mudança de comportamento)

1. Remover as duas implementações mortas de `SendMessage` e extrair a lógica de streaming, histórico e benchmark de `Chat.razor.cs` para um serviço injetável.
2. Corrigir o sink Serilog duplicado (deixar apenas o do `Program.cs`).
3. Ativar `PRAGMA foreign_keys=ON` + `busy_timeout` no `DapperContext`; substituir `VACUUM` por `DELETE` + `PRAGMA optimize`.
4. ~~Corrigir as projeções `R.PromptId` e `TempoProcessamento`~~ — **concluído** (projeção `R.PromptId` reparada; `TempoProcessamento` em aberto).
5. ~~Guarda contra `.First()` no `LocalAnalysisService`~~ — **concluído**.
6. `CheckGpuCompatibility` deve usar `GetUsableVramBytes()`; `nvidia-smi` async com `WaitForExit` antes da leitura.
7. Sanitizar `redirectUri` no `/culture-reload`; chaves API para user-secrets; `DetailedErrors=false`.
8. `AutomatedJudgeService`: validar chaves primeiro e só marcar timestamps após sucesso.

### Fase 2 — Consolidar (arquitetura)

9. Centralizar `Ollama:BaseUrl`, opções de chat, overhead de VRAM e contexto default no `appsettings.json` (via `IOptions`).
10. Remover dead code: Settings2, AnaliseBenchmark, ConversationRepository, DTOs mortos, TranslateClientService, cache morto do PromptTemplateProvider.
11. Fundir Settings/Settings2 numa página real (listar modelos reais do Ollama, gerir favoritos).
12. Migrations incrementais idempotentes (scripts SQL por versão executados no arranque) no lugar do `EnsureRecommendationColumns` ad-hoc.
13. Remover `ollama_benchmark.db` do git e gerá-la no arranque.

### Fase 3 — Features novas (maior valor)

14. Ativar RAG web no chat (a lógica DuckDuckGo/Wikipedia já existe) com botão/toggle e citações.
15. Suporte a modelos de reasoning (deepseek-r1, qwq) — mostrar `reasoning_content` separadamente.
16. Persistência real de conversas na BD (deixar de depender de localStorage) com export/import JSON.
17. Dashboard agregado de métricas (gráficos por modelo ao longo do tempo) em vez de só tabelas.
18. Testes unitários (`ChatMeasureTemperature`, `EvaluationParser`, `LocalAnalysisService`, repositórios com SQLite in-memory) + CI com GitHub Actions (`dotnet build`).
19. Tornar a recomendação dos juízes contextual por temática — ver secção 6.

### Fase 4 — UX

20. Substituir cores hardcoded por design tokens FluentUI; `aria-label` em botões-ícone; estados de erro reais nas páginas.

## 6. Como a recomendação dos juízes é gerada (e o seu viés factual)

A recomendação que aparece no ecrã de avaliação tem dois níveis distintos, ambos fáceis de confundir:

### Origem dinâmica (o texto em si)

O texto da `RECOMMENDATION:` é **escrito pelo próprio juiz** (Gemini/OpenRouter) em runtime — não existe nenhum texto fixo de recomendação na aplicação. A app apenas:

- **parseia** a secção `RECOMMENDATION:` da resposta do juiz (`Services/Helpers/EvaluationParser.cs:125-129` e `134-137`);
- **guarda-a** em `GeminiRecommendation`/`OpenRouterRecommendation` (entidade `BenchmarkResponse`);
- **exibe-a** na caixa colapsável "Recomendação" (`Components/Pages/Components/BenchmarkEvaluation.razor:72-89` e `160-178`).

### A regra estática que a molda (o que condiciona o conteúdo)

Apesar de o texto ser gerado pelo LLM, a **regra que o condiciona é estática** e vive no template do juiz — `Prompts/evaluation_prompt_template.txt:29`:

> *"Recommendation: Evaluate model suitability based on FACTUAL_SCORE. If FACTUAL_SCORE is 4 or 5, confirm {modelName}'s capability for this prompt type and suggest minor prompt/parameter tweaks. If FACTUAL_SCORE is <4, explicitly state that {modelName} is inadequate for this task and recommend upgrading to a higher-parameter model (e.g., 8B+ or 14B+ models)."*

Traduzindo a regra:

- A recomendação é **100% condicionada pelo `FACTUAL_SCORE`**;
- Limiar fixo: **≥ 4** → confirma capacidade e sugere afinações; **< 4** → declara o modelo inadequado e aconselha **subir para 8B+/14B+**;
- O formato de saída é obrigatório (`evaluation_prompt_template.txt:54`): "Actionable recommendation evaluating model suitability based on Factual Score, max 3 sentences".

### Implicações e pontos de melhoria

1. **Não pondera a temática.** Um modelo mau em factual mas excelente em contos/anedotas/roleplay recebe sempre "inadequado + subir de tamanho", porque a regra só olha para o `FACTUAL_SCORE`.
2. **Contradiz a tese central da app.** O conselho automático "upgrade para mais parâmetros" entra em conflito com o pilar do projeto (um 1.5B pode superar um modelo maior noutras métricas — README, secção Motivação).
3. **O viés factual é reforçado a jusante.** `GetBestModelAsync()` (`BenchmarkRepository.cs:413-429`, com `COALESCE` das métricas factuais) e a análise local (`LocalAnalysisService`) também pesam sobretudo o factual — o conjunto recomendação + ranking tende a privilegiar sempre o factual.
4. **Sugestão de melhoria (Fase 3, item 19):** tornar a recomendação contextual por temática — por exemplo, incluir métricas de Tom/Criatividade/Formatação na regra de recomendação, ou permitir editar o template de avaliação por tipo de prompt (o editor de prompts já existe na página EditPromptFiles).
