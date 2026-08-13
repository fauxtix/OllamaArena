# OllamaArena — Funcionalidades (Documentação Técnica)

Aplicação **Blazor Server** (.NET 10) que funciona como um **chat local com Ollama** e como um **laboratório de benchmarks "LLM-as-a-judge"** — responde a prompts, mede o desempenho de modelos abertos gratuitos e permite avaliar a qualidade das respostas usando um "juiz" (modelo Gemini e/ou OpenRouter) e uma análise local em C#.

- **UI:** FluentUI Blazor v4 (FluentDesignTheme, FluentDataGrid, FluentDialog, FluentSplitter, FluentNavMenu, etc.)
- **Persistência:** SQLite via Dapper (schema criado de forma lazily + migração de schema idempotente em código no arranque)
- **Logging:** Serilog (console + sink SQLite)
- **Ollama:** comunicação direta por HTTP com a base configurada em `appsettings.json` (`Ollama:BaseUrl`, default `http://localhost:11434`) (streaming NDJSON em `/api/chat`)
- **Idiomas de UI:** Português / Inglês (localização via cookie, `CultureSelector`)

---

## 1. Stack e arquitetura

| Camada | Tecnologia |
|---|---|
| Frontend/Backend | Blazor Server (Interactive Server render mode), Razor Components |
| UI | Microsoft FluentUI.AspNetCore.Components v4.14.2 |
| Dados | Dapper + Microsoft.Data.Sqlite (`ollama_benchmark.db`) |
| Logging | Serilog (Console + Serilog.Sinks.SQLite, tabela `Logs`) |
| LLM | Ollama local (base `Ollama:BaseUrl` em `appsettings.json`, default `http://localhost:11434`) — `/api/chat`, `/api/tags`, `/api/show`, `/api/ps` |
| Outros | HtmlAgilityPack (RAG web no chat — Wikipedia + DuckDuckGo, com toggle), ClosedXML (export Excel), Markdig (markdown), Chart.js via JS interop |

**Organização do projeto:**
- `Components/Pages/*.razor` — páginas com rota
- `Components/Pages/Components/*.razor` — componentes reutilizáveis (painéis, diálogos)
- `Components/Layout/` — `MainLayout`, `NavMenu`, `ReconnectModal`
- `Services/` — interfaces, implementações, repositórios, helpers (`ChatComposerService`, `ChatMeasureTemperature`, `MessageFormatter`, `EvaluationParser`, `ScoreCalculator`, `PromptFilesService`, `PromptSummarizer`, `AutomatedJudgeService`, `OllamaChecker`, `InternetConnectivityService`, `ConversationRepository`, `DatabaseSchemaInitializer`, `OllamaOptions`)
- `Models/DTO` — shapes da API; `Models/Entities` — mapeamento SQLite (Dapper)
- `Prompts/*.txt` — ficheiros de prompt editáveis em runtime (Content files)
- `PromptTemplates/` — construtores de prompt em C# (`EvaluatePromptTemplate`, `TranslatePromptTemplate`, `ChatInstructionsPrompt`)

> **Nota de build:** a solução (`OllamaArena.slnx`) é de **projeto único** (`OllamaArena/`) + projeto de **testes** (`OllamaArena.Tests/`, 47 testes de unidade — 43 métodos). Um antigo `Services/Services.csproj` na raiz (refactor abandonado que não compilava) foi **removido**; o build limpo faz-se com `dotnet build OllamaArena.slnx`.

---

## 2. Navegação — funcionalidades listadas no `NavMenu`

O menu lateral (`Components/Layout/NavMenu.razor`) expõe as funcionalidades principais, cada uma com a sua rota:

| Item do menu | Rota | Funcionalidade |
|---|---|---|
| Home | `/` | Landing page + cache de modelos + preview do README |
| Chat | `/chat` | Chat com streaming, barra de contexto e benchmark automático |
| Benchmarks | `/benchmark-evaluations` | Tabela consolidada de avaliações + export Excel + análise local |
| Qualidade e Métricas | `/benchmarks` | Split-view por prompt: métricas, gráficos, avaliação por juiz |
| Dashboard | `/dashboard` | Ranking agregado por modelo (score ponderado de 12 métricas) |
| Modelos Ollama | `/modelos-ollama` | Grelha de modelos locais com compatibilidade GPU |
| Editar Prompts | `/edit-prompts` | Editor runtime dos ficheiros `Prompts/*.txt` |
| Logs do Sistema | `/system-logs` | Visualização/filtro/apagamento dos logs Serilog |

> **Responsividade:** em ecrãs **≤768px** o `NavMenu` deixa de ocupar o lado esquerdo e passa a ser um **drawer deslizante** (`min(80vw, 280px)`) com overlay escurecido, aberto/fechado pelo ícone de hambúrguer no cabeçalho (`MainLayout`); os links fecham o drawer após a navegação.

Existe ainda uma página auxiliar fora do menu: `/settings` (configuração dos juízes Gemini/OpenRouter).

---

## 3. Funcionalidades por página

### 3.1 Home (`/`)

- **Propósito:** página de entrada; verifica se o Ollama está ativo e pré-carrega a cache de modelos.
- **Fluxo:**
  1. `OllamaChecker.IsOllamaRunningAsync()` → se o Ollama não responde na base configurada (`Ollama:BaseUrl`), mostra `FluentMessageBar` de erro.
  2. `OnAfterRenderAsync` limpa a cache de modelos (`localStorage`) uma vez por sessão (`sessionStorage "ollamaCacheCleared"`) e, se vazia, popula `ollamaModels` e `ollamaModelsFull` a partir de `GET /api/tags` + `POST /api/show` (metadados: contexto nativo, ano de treino).
  3. Botão **Readme** → `ReadmePreview` (descarrega o README do GitHub e renderiza markdown); sem internet, mostra o diálogo de erro "Internet indisponível" (ver 4.2).
- **Componentes:** `FluentStack`, `FluentCard`, `FluentButton`, `FluentMessageBar`.
- **Outputs:** cards com imagens; botão de preview do README; barra de erro se o Ollama não estiver em execução.

### 3.2 Chat (`/chat`)

Chat com streaming em tempo real e recolha automática de métricas de benchmark. Detalhe técnico na secção 7.

- **Propósito:** conversar com o modelo local selecionado e, simultaneamente, registar cada troca como resposta de benchmark.
- **Fluxo principal (`SendMessage` em `Chat.razor.cs`):**
  1. **Persiste a mensagem do utilizador** (`PersistUserMessageAsync`): cria a conversa na BD (`Conversas`, título = primeiros 60 chars do prompt) se ainda não existir e grava a mensagem (`ConversaMensagens`).
  2. Calcula o contexto efetivo se necessário (`GetContextLengthAsync`, secção 7) e estima os tokens do novo prompt.
  3. **`ChatComposerService.Prepare`** constrói o payload: lê o system prompt (`Prompts/system-prompt.txt`), monta o histórico (omite vazios, `...` e a saudação "Olá!", funde mensagens consecutivas do mesmo papel, remove a mensagem final de "assistant"), **apara a ~60%** do contexto (`HistoryBudgetFraction`), determina a **temperatura** via `ChatMeasureTemperature` (0.10–0.90), o `num_predict` base (1800 se `FitsInGpu`, senão 1200; ×1.15 a temp ≤0.25 / ×0.90 a ≥0.75) **limitado** para caber no contexto (reserva de 25%, mínimo 32) e os parâmetros (`num_ctx`, `repeat_penalty` 1.1, `top_k`, `top_p`) — todos os defaults vêm de `OllamaOptions` (configuráveis em `appsettings.json`).
  4. **Web search (toggle)**: se `_webSearchEnabled` estiver ligado, injeta no índice 1 uma mensagem de sistema com o contexto pesquisado (`BuscarContextoWebAsync` → Wikipedia + DuckDuckGo).
  5. Envia o payload para `/api/chat` (cliente nomeado `"Ollama"`) — inclui `think` (reasoning) apenas quando o modelo suporta a capacidade `thinking` (deteção via `/api/show` + `ModelSupportsThinkingAsync`, cacheada por modelo) **e** `OllamaOptions.EnableReasoning` está ativo; noutros modelos o campo é omitido (senão o Ollama devolve 400 "does not support thinking").
  6. **Streaming NDJSON** com `StreamReader.ReadLineAsync` (1 linha = 1 token): concatena `message.content` em `aiMessage.Text` e captura o reasoning para `aiMessage.Reasoning` — aceita **`reasoning_content` ou `thinking`** conforme a versão do Ollama (também lido da mensagem final `done` — modelos tipo qwq/deepseek-r1); atualiza a **barra de contexto** por estimativa (`text.Length/4`) e faz auto-scroll.
  7. No `done`, desserializa `OllamaMetrics` (`PromptEvalCount` + `EvalCount`); no `finally` grava o benchmark na BD (se `evalCount > 0 && evalDurationNs > 0`) e persiste a resposta do assistente com o reasoning e o tempo decorrido (`PersistAssistantMessageAsync`).
- **UI extra do Chat:**
  - Seleção de modelo (`FluentSelect`), badge de GPU ("GPU OK" verde vs "CPU Fallback" âmbar, via `GpuInfoDialog`).
  - **Toggle "Pesquisa web"** (`FluentSwitch`, `_webSearchEnabled`) — ativa a pesquisa web (Wikipedia funciona; o DuckDuckGo passou a usar um mecanismo **anti-bot** e pode devolver resultados vazios).
  - **Bloco de reasoning** (`<details class="reasoning-block">`, colapsado por defeito, com label "Pensamento") quando o modelo devolveu reasoning (`reasoning_content`/`thinking`); o reasoning também é gravado na BD (`ConversaMensagens.Reasoning`).
  - **Barra de contexto**: `FluentProgress` com `Contexto: X / Y tokens` e "Z restantes" (fica âmbar a ≥90%).
  - Botões **Novo Chat** (descarta a sessão, fecha a conversa ativa e descarrega o modelo com `keep_alive=0`) e **Histórico** (`HistoryPanel` — histórico de execuções + conversas, ver secção 4.1).
  - **Cancelar** durante o streaming (token de cancelamento; mensagem termina com `*(Cancelado)*`).
   - **Título curto** de cada prompt: gerado automaticamente por `PromptSummarizer.ExtractDescription` (**C# puro, sem chamadas LLM**) no momento em que o prompt é gravado (`CreatePromptAsync`/`UpdatePromptAsync`), e guardado na coluna `Prompts.Descricao` (ver secção 5).
- **Outputs:** mensagens markdown formatadas (`MessageFormatter.FormatMessagePlus`), tempo decorrido em tempo real (timer de 150 ms até ao 1.º token), badge de temperatura.
- **Logs técnicos:** tempo até o Ollama começar a responder (time-to-first-byte) e tempo até ao 1.º token.

> **Responsividade:** em mobile o card do chat ocupa a largura total (`100%` / `calc(100dvh - 120px)`), o cabeçalho com o seletor de modelo e os badges de GPU fazem *wrap* e a barra de erro limita-se a `min(500px, 90vw)`.

### 3.3 Benchmarks — Avaliações (`/benchmark-evaluations`)

- **Propósito:** visão consolidada de todas as respostas de benchmark com as avaliações dos juízes (Gemini/OpenRouter) e métricas de desempenho.
- **Fluxo:**
  1. `GetAllBenchmarks()` → `BenchmarkRepository.BenchmarkResponseEvaluationAsync()`.
  2. Pesquisa livre (`FluentSearch`) e filtros **Todos / Por Avaliar / Avaliados**.
  3. Duplo clique ou botão "olho" → `BenchmarkEvaluationDetail` (diálogo 900px com as tabs **Métricas** e **Feedbacks dos Juízes**; conteúdo detalhado na secção 4.1).
  4. **Exportar para Excel** → `ExportarParaExcel()` (ClosedXML): agrupa por `PromptId`, cabeçalho de prompt a azul, linhas por modelo; download via JS `downloadFileFromBase64`.
  5. **Análise IA Local** → `FluentDialog` (800px) com `BenchmarkAnalysisViewer`; valida com confirmações o nº de grupos de prompts (>1) e benchmarks ainda não avaliados; executa `LocalAnalysisService.AnalisarBenchmarksAsync` (100% local em C#, cancelável) e apresenta sumário executivo, métricas rápidas e análise técnica (ver 4.1).
  6. **Apagar Benchmarks** (tudo) e apagar linha individual (apaga também o prompt se for a única resposta) — com confirmações via `IDialogService` (ver 4.2).
- **Outputs:** `FluentDataGrid` (Modelo, Descrição, Gemini [Factual/Formato/Global], OpenRouter [Factual/Formato/Global], Tokens/s, Tempo, Carga, Tokens, Ações) + paginação (10/página); ficheiro Excel; diálogo de detalhe com tabs; painel de análise local com streaming de log.

> **Responsividade:** a barra de ferramentas faz *wrap* (pesquisa a largura total em mobile) e a grelha fica num contentor com `overflow-x: auto` para deslocamento horizontal. O mesmo padrão (`overflow-x: auto` + larguras mínimas) aplica-se às grelhas de Modelos, Qualidade e Métricas e Logs.

### 3.4 Qualidade e Métricas (`/benchmarks`)

- **Propósito:** análise **split-view** por prompt (cards à esquerda) com as respostas de cada modelo avaliadas (à direita), gráficos e edição de avaliações.
- **Fluxo:**
  1. `GetDataAsync()` → `BenchmarkRepository.GetAllBenchmarksAsync()` (prompts + respostas).
  2. Esquerda: cards de prompt com badge `#id`, data e nº de modelos; botões **Gráfico** (`BenchmarkChartDialog` — 4 gráficos Chart.js com download de imagem, ver 4.1) e apagar (cascade, com confirmação "Apagar Benchmark", ver 4.2).
   3. Direita: card por resposta com badges `Tokens/s`, `Eval`, `Load`, `Tokens`; ratings Gemini/OpenRouter; botões **Avaliação** (`BenchmarkEvaluationDialog` — formulário 12 métricas 1–5 + tradução de feedback, ver 4.1) e **Avaliar automaticamente** (`AvaliarAutomaticamenteAsync` — gera o prompt do juiz com `EvaluatePromptTemplate` + metadados do modelo via `/api/show`, submete-o aos juízes Gemini (`gemini-flash-latest`) e OpenRouter (default `openrouter/free`) via `AutomatedJudgeService`, preenche as métricas com `EvaluationParser` e abre o diálogo já preenchido). As chaves de API dos juízes vêm da **página Settings** (BD, tabela `Configuracoes`; fallback em user-secrets `ApiKeys:Gemini` / `ApiKeys:OpenRouter`); o serviço impõe um **intervalo mínimo entre avaliações** (`AutomatedJudge:MinIntervalSeconds`, default 60 s) marcado **apenas após sucesso** (falhas não bloqueiam novas tentativas) e mostra o diálogo `Benchmarks.QuotaLimitMessage` quando o limite é atingido. Antes de submeter, `AvaliarAutomaticamenteAsync` verifica a ligação à internet via `InternetConnectivityService.HasInternetAsync()` e mostra o erro `Benchmarks.NoInternetError` se não houver (os juízes são serviços em nuvem). O botão **Copiar prompt** (`CopiarPromptAvaliacaoAsync`) mantém o fallback manual (só copia o prompt gerado, sem precisar de internet).
  4. `SaveEvaluationAsync` valida ratings 1–5 (diálogo "Erro de Validação" se fora do intervalo, ver 4.2) e grava com `UpdateResponseEvaluationAsync` (diálogo "Sucesso").

> **Responsividade:** o `FluentSplitter` deteta a largura do viewport via **JS interop** (`appViewport.getWidth` + `subscribeResize` no primeiro render; `unsubscribeResize` em `DisposeAsync`) e aplica `AplicarLayoutCompacto` — em ecrãs **≤768px** a orientação passa a **vertical** (painéis empilhados, `_panel1MinSize` 120px / `_panel2MinSize` 200px), acima disso mantém-se **horizontal** (250px/400px). Se o JS falhar, mantém o estado predefinido (horizontal).

- **Outputs:** cards de prompt; cards de resposta com markdown formatado; diálogo de avaliação com o formulário dos juízes e tradução; 4 gráficos Chart.js por prompt (`BenchmarkChartDialog`): Tempo de Execução/Eval, Tempo de Carga/Load, Velocidade (Tokens/s), Total de Tokens — com download de imagem (conteúdo detalhado na secção 4.1).

### 3.5 Modelos Ollama (`/modelos-ollama`)

- **Propósito:** grelha dos modelos locais instalados com o contexto nativo e a compatibilidade estimada com a GPU.
- **Fluxo:** carrega `localStorage "ollamaModelsFull"` (cache-first); se vazio, combina `GET /api/tags` com `POST /api/show` (metadados) e grava a cache.
- **Outputs:** `FluentDataGrid` com Nome, Família, Parâmetros, Quantização, Ano de Treino, Contexto Máximo (tokens), Tamanho no Disco (GB) e badge de Compatibilidade GPU (verde "Compatível" / âmbar "Excede VRAM" / neutro "Leitura VRAM Indisponível").

### 3.6 Editar Prompts (`/edit-prompts`)

- **Propósito:** edição em runtime dos ficheiros `Prompts/*.txt` (Content files copiados para output — editáveis sem recompilar).
- **Fluxo:** `FluentSelect` para escolher ficheiro; `FluentTextArea` para editar; **Gravar** (`PromptFilesService.SavePromptFileAsync`), **Cancelar** (recarrega conteúdo) e **Sair** (volta a `/`); aviso de navegação com alterações pendentes (`NavigationManager.RegisterLocationChangingHandler` + diálogo `ConfirmDiscardAsync`).
- **Outputs:** toasts de sucesso/erro; diálogo "Alterações não gravadas".

### 3.7 Logs do Sistema (`/system-logs`)

- **Propósito:** consulta, filtro e eliminação dos logs Serilog (tabela SQLite `Logs`).
- **Fluxo:** `LogRepository.GetAllLogsAsync()`; filtros por nível (Informações/Alertas/Erros/Todos) e pesquisa de texto; duplo clique → `LogDetailDialog` (mensagem + exceção); limpar visíveis/filtrados e apagar por linha (com confirmações e diálogos info/erro via `IDialogService`, ver 4.2).
- **Outputs:** `FluentDataGrid` (ID, Data/Hora em badge, Nível em badge colorido por severidade, Mensagem, Ações) com `FluentPaginator` (10/página).

### 3.8 Dashboard (`/dashboard`)

- **Propósito:** ranking agregado por modelo — o melhor modelo geral e a posição relativa de cada um, calculados a partir das avaliações dos juízes.
- **Fluxo:** `OnInitializedAsync` → `BenchmarkRepository.GetModelRankingAsync()` (agrega por `NomeModelo` e calcula o score com `ScoreCalculator`, ver secção 5); erros silenciados (ranking vazio).
- **Outputs:** cards de estatística — **Melhor Modelo** (nome + score), **Modelos avaliados** (contagem) e **Avaliações** (total de respostas com juiz) — e `FluentDataGrid` (Posição, Modelo, Score com `FluentProgress` 0–5, Gemini, OpenRouter, Respostas, Avaliadas) com `overflow-x: auto` em mobile.

### 3.9 Páginas auxiliares (fora do menu)

- **`/settings`** — configuração dos juízes Gemini/OpenRouter: chaves de API, modelo do juiz OpenRouter e temperatura — gravadas na BD (tabela `Configuracoes`). O modelo do juiz é escolhido por **radio buttons** a partir do **catálogo público do OpenRouter** (`OpenRouterCatalogService` → `GET /api/v1/models`, só modelos gratuitos; `openrouter/free` fixo no topo; pesquisa; campo "ou escreve um ID à mão" para casos offline/custom).

---

## 4. Componentes partilhados

| Componente | Uso |
|---|---|
| `HistoryPanel` | Painel lateral deslizante direito (50vw×80vh, com overlay; em mobile `min(92vw, 560px)`×`100dvh`) com **duas tabs**: **Benchmarks** (`HistoryResponseAsync`; grelha paginada 12/página: Descrição, Modelo, Data, Tempo Eval., Tempo Procº) e **Conversas** (lista de conversas SQLite com **exportar/importar** JSON, carregar e apagar) — ver 4.1 |
| `GpuInfoDialog` | Diálogo informativo (550px; **95vw em mobile**) sobre "CPU Fallback" (VRAM insuficiente, impacto na velocidade, dicas) — ver 4.1 |
| `CultureSelector` | Comutação PT/EN (cookie de cultura via endpoint `Culture/Set`) |
| `BenchmarkEvaluation` | Formulário de avaliação por juiz: 12 métricas (Factual, Formatação, Compliance, Relevância, Tom, Concisão, Clareza, Legibilidade, Efeito Halo, Segurança, **Consistência Idiomática**, **Detecção de Loop**) + **Global**, escala 1–5; parse automático do output bruto com `EvaluationParser` (tags `*_SCORE:`, `FINAL_SCORE:`, `DESCRIPTION:`/`FEEDBACK:`, `RECOMMENDATION:`); caixa de recomendação do juiz (âmbar) quando o campo `RECOMMENDATION` foi preenchido |
| `BenchmarkEvaluationDialog` | Envelope do formulário + **tradução automática do feedback** via modelo local (`TranslationService`), com pré-visualização (`TranslationPreviewDialog`) — ver 4.1 |
| `BenchmarkEvaluationDetail` | Diálogo 900px (**95vw em mobile**) com tabs **Métricas** (grelha Gemini vs OpenRouter) e **Feedbacks dos Juízes** — ver 4.1 |
| `BenchmarkChartDialog` | 4 gráficos Chart.js com download de imagem — ver 4.1 |
| `BenchmarkAnalysisViewer` | Painel de resultado da análise local (estados: vazio / streaming com log / concluído com sumário executivo) — ver 4.1 |
| `TranslationPreviewDialog` | Pré-visualização de tradução (modelo usado + tempo gasto) — ver 4.1 |
| `LogDetailDialog` | Detalhe de um log (mensagem + exceção) — ver 4.1 |
| `ReadmePreview` | Preview do README do GitHub (via `ReadMeService` + `MarkdownRenderer`) — ver 4.1 |

### 4.1 Conteúdo dos diálogos (detalhe)

- **`BenchmarkEvaluationDetail`** — o botão **"olho"** (ou duplo clique) na linha da grelha em `/benchmark-evaluations` abre este diálogo modal (900px; **95vw em mobile**). Cabeçalho com ícone olho, nome do modelo e o prompt executado em `blockquote` (com scroll). Tem **duas tabs**:
  - **Métricas** — grelha com colunas `Métrica | Google Gemini | OpenRouter` e linhas Factual, Formatação, **Global** (linha destacada com bordas accent), Compliance, Relevância, Tom, Concisão, Clareza, Legibilidade, Efeito Halo e Segurança; cada célula é a nota do juiz ou "—" quando ainda não avaliada (nota 0). Os dados vêm da consulta consolidada `BenchmarkResponseEvaluationAsync`.
  - **Feedbacks dos Juízes** — dois cards lado a lado (Google Gemini / OpenRouter) com o texto do feedback; o conteúdo é carregado assincronamente via `BenchmarkRepository.GetBenchmarkJudgesFeedbackByIdAsync(ResponseId)` (progress ring durante a carga) e mostra "Nenhum feedback registado." se vazio. Quando o juiz devolveu `RECOMMENDATION`, cada card mostra ainda uma **caixa "Recomendação do Juiz"** (fundo âmbar) com o texto da recomendação.
- **`BenchmarkEvaluationDialog`** (aberto pelo botão "Avaliação" em `/benchmarks`; 1200px, **95vw em mobile**): cabeçalho com ícone `ClipboardCode` e nome do modelo (o prompt não é repetido, pois já é mostrado na página); corpo com o formulário `BenchmarkEvaluation` (12 métricas na escala 1–5, com parse automático do output dos juízes via `EvaluationParser`) e dois botões **Traduzir Feedback (Gemini)** / **Traduzir Feedback (OpenRouter)** que traduzem o feedback para a **língua da sessão** com o modelo local (`TranslationService`, cliente com timeout de 4 min) e abrem `TranslationPreviewDialog`. Rodapé com **Guardar avaliação** (accent; oculto quando a avaliação já está gravada) e **Fechar** (progress ring enquanto traduz).
- **`TranslationPreviewDialog`**: "Tradução do Feedback (<juiz>)", "Modelo usado", "Tempo gasto" e o texto traduzido num `FluentTextArea` editável; **Aceitar e continuar** aplica o texto ao campo do juiz; **Sair** descarta.
- **`BenchmarkChartDialog`** (aberto pelo botão "Gráfico" em `/benchmarks`; 50vw×85vh, **94vw em mobile**): cabeçalho "Gráficos de Benchmark do Prompt #<id>"; quatro gráficos de barras Chart.js (cor por modelo; labels partidos em `:` ou `-`):
  - Tempo de Execução / Eval (ms) — `graficoEval`;
  - Tempo de Carga do Modelo / Load (ms) — `graficoLoad`;
  - Velocidade de Geração (Tokens/s) — `graficoVelocidade`;
  - Total de Tokens Gerados — `graficoTotalTokens`.
  Cada gráfico tem botão de download da imagem (JS `benchmarkCharts.downloadGrafico`, ficheiro `benchmark_<métrica>_prompt_<id>`).
- **`LogDetailDialog`** (aberto pelo "olho" em `/system-logs`; 40vw×65vh, **94vw em mobile**): "Detalhes do Registo #<id>" com a caixa **Mensagem** (texto formatado, word-break) e, apenas quando existe, a caixa **Exceção / Erro** (fundo avermelhado, monospace, scroll até 250px).
- **`GpuInfoDialog`** (aberto pelo "?" de GPU no Chat; 550px, **95vw em mobile** — `width: min(550px, 95vw)`): "Informação do Sistema" com três blocos explicativos — "O que significa 'CPU fallback'?", "VRAM insuficiente" e "Impacto na velocidade" (lista de consequências na performance).
- **`HistoryPanel`** (aberto pelo botão "Histórico" no Chat): painel deslizante do lado direito (50vw×80vh; em mobile `min(92vw, 560px)`×`100dvh`) com overlay e animação; **tab "Benchmarks"** com grelha paginada (12/página) de Descrição do Prompt (tooltip), Modelo, Data, Tempo Eval. e Tempo Procº (dados de `BenchmarkRepository.HistoryResponseAsync()`; estados "A carregar..." e "Sem histórico."); **tab "Conversas"** com a lista de conversas (`ConversationRepository.GetConversationsAsync()`, ordenada por última atividade), botões **Exportar** (JSON descarregado via `ConversationRepository.GetAllForExportAsync`) e **Importar** (JSON colado → `ImportAsync`, transacional), e por linha **carregar** (recarrega a conversa no chat) e **apagar**.
- **`ReadmePreview`** (aberto pelo botão "Readme" na Home; 50vw×85vh, **94vw em mobile**): "Preview README.md" com o markdown do README do GitHub renderizado via Markdig (`ReadMeService.LoadReadmeAsync`); "Loading..." enquanto carrega.
- **Diálogo "Análise IA Local"** — o botão **"Análise IA Local"** em `/benchmark-evaluations` abre um `FluentDialog` (800px; **95vw em mobile** — mesma classe CSS `evaluation-detail-benchmark` do `BenchmarkEvaluationDetail`) cujo corpo é o `BenchmarkAnalysisViewer`, com o cabeçalho **"Análise Analítica do Modelo Local"** e badge **Análise Offline Ativa** (sem tabs; o resultado é apresentado em secções empilhadas):
  - Estado vazio: "Pronto para analisar";
  - Durante o processamento: progress ring "A processar resposta..." + botão **Cancelar** e caixa de **streaming do log** (monospace, formatada por `MessageFormatter`);
  - Concluído: "Análise Concluída" e o resultado com **Sumário Executivo**, cards **⚡ Mais Rápido** (modelo + tokens/s) e **⭐ Melhor Avaliado**, e **🔍 Análise Técnica Detalhada** (linhas HTML);
  - Antes de executar valida com confirmações o nº de grupos de prompts ("Múltiplos Prompts Detetados", se >1) e benchmarks não avaliados; o cálculo é feito por `LocalAnalysisService.AnalisarBenchmarksAsync` (100% C#, sem LLM), cancelável, com estados de erro/cancelamento também apresentados no painel.

### 4.2 Diálogos de sistema (`IDialogService`)

Para além dos diálogos personalizados da secção 4.1, a app usa `IDialogService` (FluentUI) para **confirmações**, **informações** e **erros** (modais):

- **Confirmações de eliminação** (botões "Sim, Apagar" / "Cancelar"):
  - `/benchmark-evaluations`: **"Apagar todos os benchmarks"** (todos os prompts e respostas) e **"Apagar resposta do benchmark"** (linha individual).
  - `/benchmarks`: **"Apagar Benchmark"** / **"Aviso de Eliminação"** — indica o nº de respostas que serão removidas em cascade.
  - `/system-logs`: **"ATENÇÃO - VAI LIMPAR TODOS OS REGISTOS"** (limpeza total) ou **"Confirme Eliminação"** (apenas os filtrados) e **"Apagar registo"** (linha individual).
- **Confirmações da Análise IA Local** (descritas em 4.1): **"Múltiplos Prompts Detetados"** e **"Nem todos os benchmarks estão avaliados"**.
- **"Alterações não gravadas"** em `/edit-prompts` (Descartar / Cancelar; também disparado ao navegar para fora da página).
- **Informações/erros** (`ShowInfoAsync` / `ShowErrorAsync`):
  - `/system-logs`: **"Nenhum registo"** (apagar sem logs filtrados), **"Sucesso"** (n registos eliminados) e **"Erro ao eliminar"**.
  - `/benchmarks`: **"Erro de Validação"** (rating fora do intervalo 1–5) e **"Sucesso"** (avaliação gravada no SQLite).
  - Home: **"Internet indisponível"** quando o botão Readme é clicado sem ligação.
  - `BenchmarkEvaluationDialog`: erros ao guardar a avaliação e ao traduzir feedback (Gemini/OpenRouter).

---

## 5. Camada de serviços

- **`OllamaGpuService`** — modelos locais (`/api/tags`), modelos em memória (`/api/ps`), metadados (`/api/show`: contexto nativo + ano de treino), compatibilidade GPU (`CheckGpuCompatibility`, **assíncrono** — `Task<GpuStatus>`), **contexto efetivo automático** (`GetRecommendedContextLengthAsync`) e unload de modelos (`keep_alive=0`).
- **`ChatComposerService`** — constrói o payload `/api/chat` a partir dos **`OllamaOptions`**: histórico com system prompt, fusão de mensagens consecutivas e trim a `HistoryBudgetFraction` (0.60), temperatura (`ChatMeasureTemperature`), `num_predict` base GPU/CPU com reserva de `ContextReserveFraction` (0.25), `num_ctx`, `top_k/top_p`, `repeat_penalty` e `think` (`EnableReasoning`). `BuildHistory` e `EstimateTokens` são estáticos e cobertos por testes.
- **`ScoreCalculator` / `JudgeScoreWeights`** — cálculo do **score final ponderado** a partir das **12 métricas** de um juiz: pesos configuráveis (`JudgeScoreWeights` em `appsettings.json`), `HaloEffect` com peso **0** (métrica de controlo — não pesa no score, só na fiabilidade), renormalização sobre as métricas presentes; precisa de ≥8 métricas e soma de pesos ≥50 para produzir score (senão devolve `null` e o chamador usa o `FINAL_SCORE` declarado pelo juiz). Usado pelo `BenchmarkRepository.GetModelRankingAsync` (Dashboard).
- **`AutomatedJudgeService`** — avaliação automática pelos juízes Gemini/OpenRouter: chaves de API resolvidas com prioridade **BD (`Configuracoes`, página Settings) → configuração (user-secrets/appsettings)**, validadas **antes** de consumir quota; chamadas paralelas independentes (um juiz pode falhar sem bloquear o outro); controlo de quota por provedor (`AutomatedJudge:MinIntervalSeconds`, default 60 s) marcado **apenas após sucesso** (`QuotaLimitException`); modelo do juiz OpenRouter configurável (`AutomatedJudge:OpenRouterModel`, default `openrouter/free`, com **fallback automático** para `openrouter/free` quando o modelo configurado devolve 404); temperatura determinística **fixa em 0** (não configurável); parse com `EvaluationParser`.
- **`TranslationService`** — tradução de feedbacks para a **língua da sessão** via `/api/chat` (stream=false), usando o modelo melhor avaliado (`GetBestModelAsync`) e o prompt `translation-prompt.txt` (token `{{TARGET_LANGUAGE}}`, substituído por `GetTargetLanguage()`: `pt` → "European Portuguese (pt-PT)", `en` → "English (en-US)"). Cliente HTTP tipado com timeout de 4 min.
- **`LocalAnalysisService`** — análise de benchmarks **100% local em C#** (sem LLM): sumário executivo, métricas rápidas ("Mais Rápido", "Melhor Avaliado") e análise técnica detalhada; todas as strings estão localizadas via `IStringLocalizer<SharedResources>` (chaves `Analysis.*`, pt/en).
- **`PromptFilesService`** — leitura/gravação dos ficheiros `Prompts/*.txt`; **`SystemPromptService`** — o system prompt do chat.
- **`ChatMeasureTemperature`** — recomendação lexical de temperatura (PT+EN): análise de frases, termos, detecção de código e densidade de perguntas; mapeia o prompt para 0.10–0.90 (detalhe técnico na secção 7).
- **`MessageFormatter`** / **`CommonService`** — renderização/limpeza de markdown (fechar blocos, corrigir headings, reconstruir tabelas; pipeline Markdig).
- **`EvaluationParser`** — parse do output estruturado dos juízes (linha-a-linha, com secções `DESCRIPTION`/`FEEDBACK` e `RECOMMENDATION` — corrigido para a `DESCRIPTION` não "engolir" a `RECOMMENDATION`; ver secção 8 para a persistência).
- **`InternetConnectivityService`** — verificação de ligação à internet; **`ReadMeService`** — leitura do README remoto; **`OllamaChecker`** — health check do Ollama.
- **`ConversationRepository`** (`IConversationRepository`) — persistência de conversas do chat em **SQLite** (tabelas `Conversas` / `ConversaMensagens`, **registado no DI**): criar/tocar/listar conversas, gravar/ler mensagens (inclui `Reasoning`, `Temperature`, `ElapsedTime`) e **exportar** (`GetAllForExportAsync`) / **importar** (`ImportAsync`, transacional) como JSON (ver `HistoryPanel`).
- **`PromptSummarizer`** — extrai a descrição curta de cada prompt.

**`MessageFormatter` — pipeline de formatação markdown (`FormatMessagePlus`):**
1. **Placeholder de digitação** — se o conteúdo for `...`, devolve o HTML dos três pontos animados (`<div class='typing-dots'>`).
2. **`RemoveInternalReasoning`** — remove blocos `<think>...</think>` (modelos tipo DeepSeek/Phi/Qwen).
3. **`DetectNoise`** — decide se é precisa "formatação pesada": texto sem quebras de linha com >180 chars, ou padrões `|`, `%`, camelCase, `1.A`, números com percentagem, `||`, linhas a começar em `|`.
4. **`NormalizeBasic`** (sempre) — fecha blocos ``` não fechados; quebra linha antes de headings; corrige `#Título` → `# Título`; separa listas numeradas e com marcadores (`* - +`) após `:;.!?`; limpa espaços antes de negrito/itálico.
5. **`ApplyUniversalSeparations`** (só se `DetectNoise`) — separa `aB`, `%A`, `:A`, `palavra+%`.
6. **`ReconstructTables`** — agrupa linhas com `|`, estima o nº de colunas (`EstimateBestColumnCount`: nº de pipes − 1, mínimo 2), normaliza as células por linha e insere o separador `---` se faltar.
7. **Pipeline Markdig** — `UseAdvancedExtensions`, `UseSoftlineBreakAsHardlineBreak`, `UsePipeTables`, `UseTaskLists`, `UseAutoLinks`, `UseDefinitionLists`, `UseEmphasisExtras`.
8. **`ConvertParagraphsToDivs`** — converte `<p>...</p>` em `<div>...</div>` para compatibilidade com o CSS da Fluent UI.

**`PromptSummarizer` — descrição curta dos prompts (`ExtractDescription`):**
- Limpa a pontuação mas **preserva maiúsculas** (acrónimos "API"/"GPT") e termos técnicos ("c#", "c++", "react.js"); filtra com a **união dos dicionários de stopwords PT + EN** (`AllStopWords`, inclui `WeakWords`) — agnóstico ao idioma (a app é bilingue), sem deteção heurística de língua.
- Gera **bigramas** (bónus posicional `1/(1+índice)` + bónus técnico) e **unigramas** (frequência ×1.8 + posição ×3.2 + bónus técnico); seleciona sem repetir palavras até `maxWords` (padrão 3) e capitaliza.
- **Bónus técnico** (`GetTechnicalBonus`): palavras ≥7 chars (+1.2), com hífen ou dígitos (+1.8), sufixos técnicos/médicos `ine/oid/osis/itis/emia/vaccine` (+1.5).
- Usado por `CreatePromptAsync` e `UpdatePromptAsync` (`BenchmarkRepository`) para preencher a coluna `Prompts.Descricao`; a descrição é exibida na grelha de `/benchmark-evaluations` e no `HistoryPanel`.

**`PromptFilesService` — gestão dos ficheiros `Prompts/*.txt`:**
- `GetPromptsDirectory` — `ContentRootPath/Prompts` (dev) com fallback para `AppContext.BaseDirectory/Prompts` (publish).
- `GetPromptFiles` — lista `.txt` e `.md`; `GetPromptFileContentAsync` — leitura com validação anti-**path traversal** (`Path.GetFullPath` + prefixo do diretório) e `IsValidPromptFilename` (sem carateres inválidos, sem `..`).
- `SavePromptFileAsync` — limite de **200.000 chars**, cria o diretório se faltar e grava o ficheiro (as alterações ficam imediatamente ativas no chat/`SystemPromptService` na próxima leitura).
- **`analysis-prompt.txt` está DESCONTINUADO (mantido por compatibilidade):** nenhum código o lê (`GetPromptFileContentAsync`/`GetTemplateAsync` nunca o referenciam). É um vestígio de um desenho antigo em que a análise de resultados era gerada por LLM; hoje a análise é feita localmente pelo `LocalAnalysisService` a partir de recursos de localização (`Analysis.*`). Continua incluído no build e editável na página `EditPromptFiles`, mas sem efeito em runtime.

---

## 6. Integração com o Ollama (endpoints usados)

| Endpoint | Serviço | Uso |
|---|---|---|
| `GET /api/tags` | `OllamaGpuService.GetLocalModelsAsync` | Lista de modelos locais (Home, Modelos) |
| `POST /api/show` | `OllamaGpuService` | `model_info`: contexto nativo, arquitetura (KV cache), ano de treino |
| `GET /api/ps` | `OllamaGpuService.GetRunningModelsAsync` | Modelos carregados em RAM/VRAM (`size_vram`) — para o cálculo de contexto |
| `POST /api/chat` | `Chat` (streaming), `TranslationService` | Chat NDJSON e tradução de feedbacks |
| `POST /api/generate` | `Chat` (unload) | Descarregar modelo (`keep_alive=0`) em "Novo Chat"/cancelamento |
| `GET /` (health) | `OllamaChecker` | Verificação de que o Ollama está ativo |

---

## 7. Streaming de chat e gestão de contexto (detalhe técnico)

**Cálculo do contexto efetivo (`num_ctx`)** — automático e transparente (`GetRecommendedContextLengthAsync`):
1. **VRAM livre** em camadas: `nvidia-smi` (Windows/Linux NVIDIA) → WMI (`Win32_VideoController.AdapterRAM`, apenas Windows) → `0` se nada detetar.
2. **Footprint do modelo**: `size_vram` do `/api/ps` (se o modelo estiver carregado) ou `SizeInBytes × 1.2`.
3. **KV cache por token** pela arquitetura (`/api/show`): `4 × block_count × head_count_kv × head_dim` bytes/token (cross-platform).
4. `contexto = clamp( (VRAM − footprint − 350 MB − reserva 10%) / KV, 1024, min(contextoNativo, 32768) )`.
5. Sem deteção de VRAM ou footprint > VRAM → **fallback seguro 2048** (funciona em qualquer máquina).

**Streaming e proteções:**
- O payload de `/api/chat` é montado pelo `ChatComposerService` (parâmetros base em `OllamaOptions`): `num_ctx` efetivo, `temperature`, `num_predict` (limitado: `contexto − histórico − 25%`), `repeat_penalty` 1.1, `top_k`, `top_p` e `think` — enviado **apenas se o modelo suporta `thinking`** (via `/api/show`, cacheado por modelo) e `EnableReasoning` (default true); em modelos sem essa capacidade o campo é **omitido** (o Ollama 0.30+ devolve 400 "does not support thinking").
- Histórico enviado aparado a ~60% do contexto (UI intacta); balões mantêm o texto completo.
- Cliente HTTP nomeado **`"Ollama"` com timeout de 10 min** (o timeout padrão de 100 s era a causa de "tempo expirou" em máquinas com pouca VRAM).
- Leitura linha-a-linha (`StreamReader.ReadLineAsync`) — NDJSON, 1 linha por token; `JsonDocument` por linha; `done == true` → métricas finais.
- Logs de diagnóstico: tempo-ao-primeiro-byte e tempo-ao-primeiro-token.
- Cancelamento: `CancellationTokenSource`; o texto termina com `*(Cancelado)*`.

**Cálculo da temperatura (`temperature`)** — `ChatMeasureTemperature.ObterTemperaturaRecomendada(prompt)` (PT+EN) devolve um valor entre **0.10 e 0.90**, decidindo entre rigor factual e criatividade:
1. **Normalização** — remove acentos (`RemoverAcentos`) e converte para minúsculas.
2. **Detecção de código** — densidade de símbolos (`{ } ( ) [ ] ; = < > # @ $ \ | &`) > 4.5% do comprimento, ou keywords fortes (`public class`, `function `, `select `, `=>`, `console.log`, …); se for código → forte boost factual (+8 + densidade×12) e criatividade ×0.35.
3. **Frases multi-palavra** (peso alto, verificadas primeiro): factuais ("como funciona" 5, "passo a passo" 4.5, "how to" 4.5, "explica-me" 3.5, …) e criativas ("role play" 6, "act as" 6, "inventa uma" 5, "what if" 5, …).
4. **Tokenização + termos de palavra única** com pesos: factuais ("código" 4.5, "erro"/"bug"/"exception" 4.5, "explica" 3.5, "média" 2.4, "guerra" 3.2, "capital" 3.2, "vacina" 3.0, …) e criativos ("poema" 5.5, "piada" 5.5, "imagina"/"inventa" 4.5, "story" 4.5, …).
5. **Densidade de perguntas** (sinal `?`) → boost factual ×4.
6. **Modificadores estruturais** — prompts >600 chars (+2.5 factual) ou >300 (+1.2); prompts curtos (<80 chars) com intenção criativa +2.0; ≤4 palavras e criativo +1.5.
7. **Mapeamento score→temperatura** — `net = factual − criativo`; `normalizado = clamp(net / max(total,1) × (1/0.65), −1.5, 1.5)`; por faixas → **0.10** (factual forte) / **0.20** / **0.30** / **0.42** (neutro) / **0.65** / **0.78** / **0.90** (criativo forte). Prompt vazio → **0.20**.

**Parâmetros de geração enviados no `/api/chat`** (derivados da temperatura; valores base em `OllamaOptions`, configuráveis em `appsettings.json`):
- `temperature` arredondada a 2 casas; `repeat_penalty` fixo **1.1**.
- `num_predict` base: **1800** se o modelo cabe na GPU (`FitsInGpu`), senão **1200**; ajustado a **×1.15** se temp ≤0.25, **×0.90** se ≥0.75; depois **limitado para caber no contexto** (`contexto − histórico − reserva 25%`, mínimo 32).
- `top_k` **40** se temp ≤0.25, senão **600**; `top_p` **0.85** se temp ≤0.30, senão **0.92**.
- `think` enviado só para modelos com capacidade `thinking` e com `EnableReasoning` ativo.
- A temperatura é gravada no registo do prompt (`CreatePromptAsync`) e exibida no chat ("Temp: X").

**Benchmark automático (gravado na BD):** com `PromptEvalCount` + `EvalCount`, `LoadDuration`, `EvalDuration` calcula `TokensPorSegundo`, `TempoPuroMs`, `TempoCargaMs`, `TempoProcessamento`, `TamanhoTokens` e associa ao prompt (`CreatePromptAsync` + `CreateResponseAsync`). Testes incompletos são descartados (log `[BENCHMARK]`).

**Fluxo completo do `SendMessage` (`Chat.razor.cs:213`):**
1. **Guardas** — se `_currentMessage` estiver vazio ou já em pensamento (`_isThinking`), retorna; inicia o `Stopwatch`.
2. **UI + persistência** — adiciona o balão "Tu", persiste a mensagem do utilizador (`PersistUserMessageAsync`: cria a conversa se `_conversationId == 0`, título = primeiros 60 chars), limpa o input (`_inputKey++` remonta o componente), liga `_isThinking` e a **barra de contexto**, faz auto-scroll; adiciona o balão "Ollama" com placeholder `...`; **timer de 150 ms** mostra o tempo decorrido em tempo real até ao 1.º token.
3. **Contexto** — se ainda não calculado, `GetContextLengthAsync(ModelName)` (secção 7 acima); estima os tokens do novo prompt (`text.Length / 4`) e soma-os à barra de contexto.
4. **Preparação** — `ChatComposerService.Prepare` (lê o system prompt; `BuildHistory` omite vazios, `...` e a saudação "Olá!", funde mensagens consecutivas do mesmo papel e remove a mensagem final de "assistant"; **trim a 60%** — `HistoryBudgetFraction`; se a última mensagem não for de "user" aborta). O `PreparedChatRequest` devolve `History`, `Temperature`, `MaxTokens` e o `Payload` (`think` incluído).
5. **Web search (toggle)** — se `_webSearchEnabled`, chama `BuscarContextoWebAsync(userPrompt)` (Wikipedia + DuckDuckGo, cabeçalhos de browser via `CriarRequestWeb`) e injeta o contexto como mensagem de sistema no índice 1 do payload.
6. **Pedido** — serializa o payload e faz `POST api/chat` com `HttpCompletionOption.ResponseHeadersRead` (cliente nomeado `"Ollama"` via `IHttpClientFactory`, timeout 10 min).
7. **Streaming NDJSON** — `StreamReader.ReadLineAsync`; por linha, `JsonDocument.Parse` e extrai `message.content` (formato chat) ou `response` (formato generate); concatena em `aiMessage.Text`; captura o reasoning (`reasoning_content` ou `thinking`, e o reasoning da mensagem final `done`) para `aiMessage.Reasoning`; atualiza a barra de contexto por estimativa; auto-scroll **condicional** (`chatScroll.scrollDuringStream` só cola quando a distância ao fundo é <120px).
8. **Métricas finais** — quando `done == true`, desserializa `OllamaMetrics` (`LoadDuration`, `EvalDuration`, `PromptEvalCount + EvalCount`) e mostra o total real de tokens na barra.
9. **`finally`** — para o stopwatch e o timer; formata o tempo final (2 casas <10 s, 1 casa acima); grava o **benchmark** se `evalCount > 0 && evalDurationNs > 0` (cria o prompt via `CreatePromptAsync` se `_currentPromptId == 0`, grava `BenchmarkResponse` via `CreateResponseAsync`; senão descarta com log `[BENCHMARK]`); persiste a resposta do assistente (`PersistAssistantMessageAsync`, com `Reasoning`, temperatura e tempo decorrido) e faz `TouchConversationAsync`; `_isThinking = false` e liberta o `CancellationTokenSource`.
10. **Erros** — `OperationCanceledException` → `⏱️ O tempo de resposta expirou.` (se nada recebeu) ou sufixo `*(Cancelado)*`; outras exceções → `❌ Erro inesperado: <mensagem>` (gravadas nos logs).

**Cancelamento e limpeza:**
- **`CancelRequest`** — cancela o `CancellationTokenSource` e, em background, envia `POST /api/generate` com `keep_alive = 0` para **libertar a GPU imediatamente**.
- **`NewChat`** — cancela o CTS, limpa as mensagens, repõe `_conversationId = 0` (a próxima mensagem abre uma conversa nova), faz o mesmo unload (`keep_alive = 0`) e mostra "Olá! Como posso ajudar-te hoje?".
- **`Dispose`** — cancela/liberta o CTS e o `_dotNetRef` ao sair da página.

---

## 8. Base de dados (SQLite — `ollama_benchmark.db`)

Sem migrações clássicas — tabelas criadas de forma lazily (Dapper / sink Serilog), com **migração idempotente em código** no arranque: `DatabaseSchemaInitializer.EnsureSchema(IDapperContext)` (invocado em `Program.cs` após `builder.Build()`) executa `CREATE TABLE IF NOT EXISTS` para as 7 tabelas e depois migrações incrementais de colunas via `PRAGMA table_info` — renomeia `ChatGpt*` → `OpenRouter*` (a avaliação passou a ser feita via OpenRouter) e adiciona `GeminiRecommendation`/`OpenRouterRecommendation` + as colunas das novas métricas `LanguageConsistency`/`LoopDetection`. As ligações SQLite são abertas com **`PRAGMA foreign_keys = ON` e `busy_timeout = 5000`** (`DapperContext`), pelo que o `ON DELETE CASCADE` declarado no DDL é respeitado em runtime.

| Tabela | Conteúdo (colunas principais) |
|---|---|
| `Prompts` | `Id`, `Descricao`, `TextoPrompt`, `DataCriacao`, `Temperatura` |
| `Respostas` | `Id`, `PromptId` (FK), `NomeModelo`, `TextoResposta`, `TokensPorSegundo`, `TempoPuroMs`, `TempoCargaMs`, `TamanhoTokens`, `TempoProcessamento`, ratings/feedbacks Gemini (`GeminiRating`, `GeminiFactualRating`, `GeminiFormattingRating`, `GeminiComplianceRating`, `GeminiRelevanceRating`, `GeminiToneRating`, `GeminiConcisenessRating`, `GeminiClarityRating`, `GeminiReadabilityRating`, `GeminiHaloEffectRating`, `GeminiSafetyRating`, `GeminiLanguageConsistencyRating`, `GeminiLoopDetectionRating`, `GeminiFeedback`, `GeminiRecommendation`) e equivalentes OpenRouter (incluindo `OpenRouterRecommendation`); as colunas de recomendação e das novas métricas são adicionadas pela migração `DatabaseSchemaInitializer` |
| `Conversas` | `Id`, `Titulo` (primeiros 60 chars do 1.º prompt), `NomeModelo`, `DataCriacao`, `DataUltimaAtividade` |
| `ConversaMensagens` | `Id`, `ConversationId` (FK, `ON DELETE CASCADE`), `Role` (user/assistant), `Content`, `Reasoning`, `Temperature`, `ElapsedTime`, `Timestamp` |
| `Configuracoes` | `Chave` (PK) / `Valor` — definições dos juízes gravadas pela página Settings (`ApiKeys:Gemini`, `ApiKeys:OpenRouter`, `AutomatedJudge:OpenRouterModel`) |
| `ModelosJuiz` | `Id`, `Nome` (UNIQUE) / `DataCriacao` — tabela **legada** de modelos `:free` do OpenRouter, **sem uso pela UI** desde que o Settings passou a usar o catálogo ao vivo (`OpenRouterCatalogService`); continua a ser semeada no primeiro arranque se vazia |
| `Logs` | criada pelo Serilog sink (`Id`, `Timestamp`, `Level`, `Exception`, `RenderedMessage`, `Properties`) |

- `DeletePromptAndHistoryAsync` / `DeleteAllPromptsAndHistoryAsync` dependem de `ON DELETE CASCADE` (declarado no DDL das `Respostas` e `ConversaMensagens`).
- `DeleteSpecificResponseAsync` apaga a resposta e, se for a última do prompt, também o prompt (transação).
- Path: `OllamaArena/ollama_benchmark.db` (connection string `SqliteConnection`, resolvida contra `ContentRootPath`).
- DDL completo das cinco tabelas (`Prompts`, `Respostas`, `Conversas`, `ConversaMensagens`, `Logs`) disponível em `Docs/schema.sql` — executável numa nova base de dados antes da primeira utilização.

---

## 9. Persistência no browser (localStorage / sessionStorage)

| Chave | Conteúdo | Onde |
|---|---|---|
| `ollamaModels` | Lista de nomes de modelos | Chat, Home, Modelos |
| `ollamaModelsFull` | `ModelDetails` com metadados (`ContextLength`, `TrainingYear`) | Home, Modelos |
| `ollama_model` | Modelo selecionado no chat | Chat |
| `ollama_history` | Histórico de execuções (helper `window.ollamaHistory`, chat.js) | Chat (helper definido; sem chamadores no C# atualmente — as conversas são persistidas em SQLite) |
| `theme` | Tema FluentUI (`FluentDesignTheme StorageName`) | Global |
| `sessionStorage: ollamaCacheCleared` | Flag de limpeza da cache uma vez por sessão | Home |

As **definições dos juízes** (chaves Gemini/OpenRouter, modelo do juiz OpenRouter) **não** usam localStorage — ficam na tabela SQLite `Configuracoes`, editáveis pela página Settings (`/settings`), com prioridade **BD → configuração → default** no `AutomatedJudgeService` (a temperatura dos juízes é **fixa em 0** — determinística — e não é configurável). O botão **Guardar configuração dos juízes** só fica ativo quando há alterações (dirty state) e abre um diálogo de confirmação com o resumo do que vai mudar (a chave nunca é mostrada — só o estado "definida/alterada" ou "removida"). A **escolha do modelo do juiz OpenRouter** é feita por **radio buttons** a partir do **catálogo público do OpenRouter** (`OpenRouterCatalogService`, `GET /api/v1/models`, só gratuitos; `openrouter/free` sempre fixo no topo; pesquisa; campo "ou escreve um ID à mão" para offline/custom). A tabela legada `ModelosJuiz` deixou de ser usada pela UI (mantida por compatibilidade).

### 9.1 Camada de JS interop (`wwwroot/js/`)

| Ficheiro | Objetos expostos | Uso |
|---|---|---|
| `chat.js` | `window.ollamaHistory` — `addEntry`/`getHistory`/`clear` sobre `localStorage "ollama_history"` | Helper definido mas **sem chamadores** no C# atualmente |
| | `window.chatInput` — `attachHandlers`/`detachHandlers` (Enter → envia via `invokeMethodAsync('OnEnterPressedFromJs')`; Shift+Enter → nova linha no caret; auto-resize; `resetHeight` com `requestAnimationFrame` ×3) | Input do chat (`chatInputRef`) |
| | `window.chatScroll` — `getScrollState`, `isAtBottom` (threshold 80px), `scrollToBottom` (duplo `requestAnimationFrame`), `scrollDuringStream` (auto-scroll "sticky" a <120px do fundo) | Auto-scroll do chat (stream e salto ao fundo) |
| `graficos.js` | `window.benchmarkCharts` — `renderGrafico` (cores do tema via CSS vars `--colorNeutralForeground1`/`--colorNeutralStroke2` com fallbacks, `Chart.defaults`, **destrói o gráfico anterior** em `window["_"+canvasId]`, tooltip `Valor: X <unidade>`, eixo Y com sufixo) e `downloadGrafico` (`canvas.toDataURL("image/png")` + link) | 4 gráficos do `BenchmarkChartDialog` e `Benchmarks` |
| `excel.js` | `window.downloadFileFromBase64` (data URI base64 `.xlsx`) | Export Excel (`BenchmarkEvaluations.ExportarParaExcel`) |
| `clipboard.js` | `window.copyToClipboard` (`navigator.clipboard` com fallback para textarea + `execCommand('copy')`) | Botão de copiar avaliação (`Benchmarks.razor.cs`) |
| `viewport.js` | `window.appViewport` — `getWidth` (largura do viewport) / `subscribeResize` (listener `resize` que chama `invokeMethodAsync('OnViewportResized')`) / `unsubscribeResize` (remove o listener) | Splitter responsivo de `/benchmarks` (`AplicarLayoutCompacto`: vertical ≤768px) |

> Nota: o ficheiro `ollama_benchmark.db` **não está tracked no git** (gitignored juntamente com `-shm`/`-wal`/`-journal` e `*.sqbpro`) — a BD é criada de forma lazily no arranque, pelo que um clone novo começa com a base vazia.

---

## 10. Logging (Serilog)

- Bootstrap logger no terminal (`WriteTo.Console`).
- Sink SQLite: `sqliteDbPath=ollama_benchmark.db`, `tableName="Logs"`, `autoCreateSqliteTable=true`, `batchSize=100`.
- Nível mínimo `Warning` (`appsettings.json`); erros e cancelamentos ficam gravados e consultáveis em `/system-logs`.

---

## 11. Configuração — `Program.cs` / `appsettings.json`

**Registos de DI relevantes:**
- `OllamaOptions` (`Configure<OllamaOptions>`, secção `Ollama` do `appsettings.json`), `IDapperContext`, `IOllamaGpuService`, `IBenchmarkRepository` (Scoped), `ISettingsRepository` (Scoped), `IModelosJuizRepository` (Scoped), `ILogRepository` (Scoped), `IConversationRepository` (Scoped)
- `PromptFilesService`, `ChatComposerService`, `SystemPromptService`, `EvaluatePromptTemplate`, `MarkdownRenderer`, `LocalAnalysisService`, `AutomatedJudgeService`, `InternetConnectivityService`, `ReadMeService`
- Clientes HTTP: `IAnalysisService/LocalAnalysisService`, `InternetConnectivityService`, `ReadMeService`, `ITranslationService/TranslationService` (**timeout 4 min**, base de `Ollama:BaseUrl`), o cliente nomeado **`"Ollama"`** (**timeout 10 min**, base de `Ollama:BaseUrl`) usado no streaming do chat, e os clientes nomeados **`"Gemini"`** (timeout 30 s) / **`"OpenRouter"`** (timeout 120 s) para os juízes automáticos.
- Configuração: `appsettings.Local.json` opcional (não versionado) para overrides locais — incluindo `Security:EnableHttpsRedirection` (`false` para deploys HTTP-only, ver secção 12); chaves de API dos juízes **recomendadas via página Settings** (BD, tabela `Configuracoes`), com fallback para **user-secrets** (`ApiKeys:Gemini` / `ApiKeys:OpenRouter`) ou `appsettings.Local.json`.
- Localização: PT (`pt`) por defeito + `en`; cookie `RequestCultureProvider`. A **tradução de feedbacks** e a **análise local de resultados** seguem a língua da sessão (o system-prompt do chat mantém-se neutro).

**Requisitos de runtime:** `ollama serve` ativo na base configurada (`Ollama:BaseUrl`); projeto de testes `OllamaArena.Tests` com **47 testes de unidade** (43 métodos: ScoreCalculator, EvaluationParser, ChatMeasureTemperature, ChatComposerService, LocalAnalysisService) — `dotnet test`; validação = build + testes + execução manual.

---

## 12. Deploy em IIS (GitHub Actions)

O repositório inclui um workflow GitHub Actions (`.github/workflows/deploy-iis.yml`) que publica a aplicação e faz o deploy para um **self-hosted runner** registado apenas na máquina do autor (a app fica em `http://localhost:4501`):

- **Triggers:** `push` para `master` ou `workflow_dispatch`.
- **Publicação:** `dotnet publish OllamaArena.csproj -c Release` (SDK .NET 10).
- **Deploy:** para `C:\inetpub\wwwroot\OllamaArena` (site) + app pool `OllamaArena` (configuráveis via secrets `IIS_SITE_PATH`/`IIS_APP_POOL`/`SITE_PORT`).
- **Sem interrupção de dados:** o app pool é parado antes da substituição dos ficheiros (evita DLLs bloqueadas) e re-iniciado no fim; a BD `ollama_benchmark.db` (e `-shm`/`-wal`/`-journal`) e o `appsettings.Local.json` são **preservados** entre deploys.
- **HTTP-only:** o workflow garante `Security:EnableHttpsRedirection = false` no `appsettings.Local.json` do site (sem certificado não há redirect/HSTS).
- **Smoke test:** após o arranque, valida `HTTP 200` em `http://localhost:4501`.

Em máquinas sem o runner, o workflow é irrelevante — a app corre localmente com `dotnet watch run` (ver README), sem tokens ou credenciais.

---

*Documentação gerada a partir do código da solução (working tree). Caminhos relativos a `OllamaArena/`.*
