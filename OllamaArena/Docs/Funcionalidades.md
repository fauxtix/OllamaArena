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
| Outros | ClosedXML (export Excel), Markdig (markdown), Chart.js via JS interop |

**Organização do projeto:**
- `Components/Pages/*.razor` — páginas com rota
- `Components/Pages/Components/*.razor` — componentes reutilizáveis (painéis, diálogos)
- `Components/Layout/` — `MainLayout`, `NavMenu`, `ReconnectModal`
- `Services/` — interfaces, implementações, repositórios, helpers (`ChatComposerService`, `ChatMeasureTemperature`, `MessageFormatter`, `EvaluationParser`, `ScoreCalculator`, `PromptFilesService`, `PromptSummarizer`, `AutomatedJudgeService`, `OllamaChecker`, `InternetConnectivityService`, `ConversationRepository`, `DatabaseSchemaInitializer`, `OllamaOptions`)
- `Models/DTO` — shapes da API; `Models/Entities` — mapeamento SQLite (Dapper)
- `Prompts/*.txt` — ficheiros de prompt editáveis em runtime (Content files)
- `PromptTemplates/` — construtores de prompt em C# (`EvaluatePromptTemplate`, `ChatInstructionsPrompt`)
- `PlanoDeTestes/*.md` — documentos do plano de testes (PT + EN), lidos pela página `/plano-de-testes`
- `ComoCompararModelos/*.md` — guia "como comparar modelos" (PT + EN), lido pela página `/comparar-modelos`
- `wwwroot/fonts/` — TTF embebidos no relatório PDF (`Inter-{Regular,Bold}`, `RobotoMono-Regular`) + `NOTICE.md`/`OFL.txt` (proveniência e licença)
- `Screenshots/` — capturas de ecrã da app (PNG) + apresentação gerada `OllamaArena.pptx` + script `gerar_apresentacao.ps1` (COM do PowerPoint; ver `Guia_Apresentacao.md`)

> **Nota de build:** a solução (`OllamaArena.slnx`) contém o projeto da aplicação (`OllamaArena/`) e o projeto de testes (`OllamaArena.Tests/`, xUnit). Faça um build limpo com `dotnet build OllamaArena.slnx`.
>
> O número exacto de testes pode variar ao longo do desenvolvimento; para obter a contagem actual execute `dotnet test --list-tests` ou abra o projecto `OllamaArena.Tests` no IDE. Um antigo `Services/Services.csproj` na raiz (refactor abandonado que não compilava) foi **removido**.
>
> A Ollama comunica por **HTTP direto** (cliente nomeado `"Ollama"`); o pacote NuGet `OllamaSharp` chegou a estar referenciado mas deixou de ser usado e foi removido do `.csproj`.

---

## 2. Navegação — funcionalidades listadas no `NavMenu`

O menu lateral (`Components/Layout/NavMenu.razor`) expõe as funcionalidades principais, cada uma com a sua rota (os rótulos abaixo são os valores reais dos recursos `LoadedModels`, `EditPrompts`, `SystemLogs`, …):

| Item do menu | Rota | Funcionalidade |
|---|---|---|
| Home | `/` | Landing page + cache de modelos + preview do README |
| Chat | `/chat` | Chat com streaming, barra de contexto e benchmark automático |
| Benchmarks | `/benchmark-evaluations` | Tabela consolidada de avaliações + export Excel + análise local |
| Qualidade e Métricas | `/benchmarks` | Split-view por prompt: métricas, gráficos, avaliação por juiz |
| Dashboard | `/dashboard` | Ranking agregado por modelo (score ponderado de 12 métricas) |
| Modelo Carregados | `/modelos-ollama` | Grelha de modelos locais com compatibilidade GPU |
| Prompts | `/edit-prompts` | Editor runtime dos ficheiros `Prompts/*.txt` |
| Logs do sistema | `/system-logs` | Visualização/filtro/apagamento dos logs Serilog |
| Definições | `/settings` | Configuração dos juízes (chaves, modelo, reasoning) — no **menu de rodapé** (`.navmenu-footer`, segundo `FluentNavMenu` do mesmo componente) |

> **Responsividade:** em ecrãs **≤768px** o `NavMenu` deixa de ocupar o lado esquerdo e passa a ser um **drawer deslizante** (`min(80vw, 280px)`) com overlay escurecido, aberto/fechado pelo ícone de hambúrguer no cabeçalho (`MainLayout`); os links fecham o drawer após a navegação.

Duas páginas de **apenas leitura** não vivem no menu lateral mas sim em **botões do cabeçalho** (`MainLayout.razor`), com **ícone e legenda visível** (`.header-btn-label`, classe `Header.*` nos recursos) para não obrigar a hover; o `title` e o `aria-label` mantêm o texto longo e garantem o nome acessível quando a legenda está recolhida:

| Botão do cabeçalho | Rota | Funcionalidade |
|---|---|---|
| Plano de Testes | `/plano-de-testes` | Leitura dos documentos de sugestões de teste (`PlanoDeTestes/*.md`, PT/EN) |
| Comparar Modelos | `/comparar-modelos` | Leitura do guia "como comparar modelos" (`ComoCompararModelos/*.md`, PT/EN) |

**Appbar** (`.header-actions`): todos os controlos de acção levam **ícone + legenda visível** — *README* (`DocumentBulletList`, abre o `ReadmePreview`), *Testes* (`CheckmarkCircle`), *Comparar* (`ArrowSwap`), comutador de tema (emoji `🌙`/`☀️` + legenda do tema **alvo**, p. ex. "Tema escuro", alinhada com o emoji; o `title` mantém a frase "Mudar para tema escuro") e o seletor de idioma (`CultureSelector`, PT/EN). As legendas são recursos `Header.*` (`README`/`Testes`/`Comparar`/`Tema escuro`/`Tema claro`) e vivem em `.header-btn-label`; o `title` e o `aria-label` de cada botão mantêm o texto longo (`ReadmeViewer`, `TestPlan`, `CompareModels`, `Theme.To*`) — o `aria-label` é indispensável porque abaixo de 768px a legenda está em `display: none` e sai da árvore de acessibilidade. **Responsividade em 3 níveis, por ordem de sacrifício:** ≥1100px título + legendas; 769–1100px o título (`.app-title`) recolhe e as legendas mantêm-se; ≤768px as legendas recolhem e ficam só os ícones (com o `title` a descrever). O `NavMenu` fixo consome ~280px em desktop, daí os 1100px — com legendas curtas as acções ocupam ~550px e abaixo desse valor o título só apareceria elipsado. Cada botão tem ícone próprio: "README" e "Comparar" usavam ambos `DocumentBulletList`, indistinguíveis sem hover. O layout também inicializa os provedores UI usados pela app — `FluentToastProvider`, `FluentDialogProvider`, `FluentTooltipProvider` e `FluentMenuProvider` — e inclui links de rodapé para o repositório e para a documentação do Ollama.

---

## 3. Funcionalidades por página

### 3.1 Home (`/`)

- **Propósito:** página de entrada; verifica se o Ollama está ativo e pré-carrega a cache de modelos.
- **Fluxo:**
  1. `OllamaChecker.IsOllamaRunningAsync()` → se o Ollama não responde na base configurada (`Ollama:BaseUrl`), mostra `FluentMessageBar` de erro.
  2. `OnAfterRenderAsync` limpa a cache de modelos (`localStorage`) uma vez por sessão (`sessionStorage "ollamaCacheCleared"`) e, se vazia, popula `ollamaModels` e `ollamaModelsFullV2` a partir de `GET /api/tags` + `POST /api/show` (metadados: contexto nativo, ano de treino, capacidade de reasoning).
  3. Botão **Readme** → `ReadmePreview` (descarrega o README do GitHub e renderiza markdown); sem internet, mostra o diálogo de erro "Internet indisponível" (ver 4.2).
- **Componentes:** `FluentStack`, `FluentCard`, `FluentButton`, `FluentMessageBar`.
- **Outputs:** cards com imagens; botão de preview do README; barra de erro se o Ollama não estiver em execução.

### 3.2 Chat (`/chat`)

Chat com streaming em tempo real e recolha automática de métricas de benchmark. Detalhe técnico na secção 7.

- **Propósito:** conversar com o modelo local selecionado e, simultaneamente, registar cada troca como resposta de benchmark.
- **Fluxo principal (`SendMessage` em `Chat.razor.cs`):**
  1. **Persiste a mensagem do utilizador** (`PersistUserMessageAsync`): cria a conversa na BD (`Conversas`, título = primeiros 60 chars do prompt) se ainda não existir e grava a mensagem (`ConversaMensagens`).
  2. Calcula o contexto efetivo se necessário (`GetContextLengthAsync`, secção 7) e estima os tokens do novo prompt.
  3. **`ChatComposerService.Prepare`** constrói o payload: lê as instruções de sistema (`Prompts/system-prompt.txt` + `Prompts/ChatInstructionsPrompt`, com o token `{{TARGET_LANGUAGE}}` substituído pelo idioma da sessão via `TargetLanguageResolver.GetTargetLanguage()` — `pt` → "European Portuguese (pt-PT)", `en` → "English (en-US)", default pt; sem token é no-op), monta o histórico (omite vazios, `...` e a saudação "Olá!", funde mensagens consecutivas do mesmo papel, remove a mensagem final de "assistant"), **apara a ~60%** do contexto (`HistoryBudgetFraction`), determina a **temperatura** via `ChatMeasureTemperature` (0.10–0.90), o `num_predict` base (1800 se `FitsInGpu`, senão 1200; ×1.15 a temp ≤0.25 / ×0.90 a ≥0.75) **limitado** para caber no contexto (reserva de 25%, mínimo 32) e os parâmetros (`num_ctx`, `repeat_penalty` 1.1, `top_k`, `top_p`) — todos os defaults vêm de `OllamaOptions` (configuráveis em `appsettings.json`).
  4. Envia o payload para `/api/chat` (cliente nomeado `"Ollama"`) — inclui `think` (reasoning) **apenas para modelos com capacidade `thinking`** (deteção via `/api/show` + `ModelSupportsThinkingAsync`, cacheada por modelo), com o valor **explícito** resolvido pelo `ChatComposerService` a partir da BD (`Chat:EnableReasoning`, página Settings) → configuração → default **false**: `true` quando ativo, `false` quando desativado (omitir o campo faz o Ollama pensar por omissão); em modelos normais o campo é omitido (senão o Ollama devolve 400 "does not support thinking"). Com reasoning desativado, o raciocínio que o modelo devolver **não é capturado nem guardado** (modelos tipo `deepseek-r1` geram-no sempre e não respeitam `think: false`).
  5. **Streaming NDJSON** com `StreamReader.ReadLineAsync` (1 linha = 1 token): concatena `message.content` em `aiMessage.Text` e captura o reasoning para `aiMessage.Reasoning` — aceita **`reasoning_content` ou `thinking`** conforme a versão do Ollama (também lido da mensagem final `done` — modelos tipo qwq/deepseek-r1); atualiza a **barra de contexto** por estimativa (`text.Length/4`) e faz auto-scroll.
  6. No `done`, desserializa `OllamaMetrics` (`PromptEvalCount` + `EvalCount`); no `finally` grava o benchmark na BD (se `evalCount > 0 && evalDurationNs > 0`) e persiste a resposta do assistente com o reasoning e o tempo decorrido (`PersistAssistantMessageAsync`).
- **UI extra do Chat:**
  - Seleção de modelo (`FluentSelect`), badge de GPU ("GPU OK" verde vs "CPU Fallback" âmbar, via `GpuInfoDialog`).
  - **Bloco de reasoning** (`<details class="reasoning-block">`, colapsado por defeito, com label "Pensamento") quando o modelo devolveu reasoning (`reasoning_content`/`thinking`) **e** o reasoning está ativo na Settings; o reasoning também é gravado na BD (`ConversaMensagens.Reasoning`).
  - **Sondagem local (Ollama) sobre `think: false`**: `qwen3` e `smollm3` **respeitam-no** — saltam a fase de pensamento quando o reasoning está desligado (`qwen3:1.7b` e `smollm3`: ~11–15 s → ~0,05 s); `deepseek-r1` **ignora-o** — gera sempre o reasoning (a app apenas deixa de o capturar/guardar; sem ganho de velocidade).
  - **Barra de contexto**: `FluentProgress` com `Contexto: X / Y tokens` e "Z restantes" (fica âmbar a ≥90%).
  - Botões **Novo Chat** (descarta a sessão, fecha a conversa ativa e descarrega o modelo com `keep_alive=0`) e **Histórico** (`HistoryPanel` — histórico de execuções + conversas, ver secção 4.1).
  - **Cancelar** durante o streaming (token de cancelamento; mensagem termina com `*(Cancelado)*`).
   - **Título curto** de cada prompt: a descrição é gravada na coluna `Prompts.Descricao` pelo `BenchmarkCommandRepository.CreatePromptAsync` / `GetOrCreatePromptIdAsync` (ver secção 5) e exibida na grelha de `/benchmark-evaluations` e no `HistoryPanel`.
- **Outputs:** mensagens markdown formatadas (`MessageFormatter.FormatMessagePlus`), tempo decorrido em tempo real (timer de 150 ms até ao 1.º token), badge de temperatura.
- **Logs técnicos:** tempo até o Ollama começar a responder (time-to-first-byte) e tempo até ao 1.º token.

> **Responsividade:** em mobile o card do chat ocupa a largura total (`100%` / `calc(100dvh - 120px)`), o cabeçalho com o seletor de modelo e os badges de GPU fazem *wrap* e a barra de erro limita-se a `min(500px, 90vw)`.

### 3.3 Benchmarks — Avaliações (`/benchmark-evaluations`)

- **Propósito:** visão consolidada de todas as respostas de benchmark com as avaliações dos juízes (Gemini/OpenRouter) e métricas de desempenho.
- **Cabeçalho:** `PageHeader` com `BenchmarkEvaluations.PageTitle` ("Avaliações de Benchmarks"), seguido da barra de ferramentas *sticky*.
- **Fluxo:**
  1. `GetAllBenchmarks()` → `BenchmarkQueryRepository.BenchmarkResponseEvaluationAsync()`.
  2. Pesquisa livre (`FluentSearch`) e filtros **Todos / Por Avaliar / Avaliados** — "Por Avaliar" = qualquer avaliação em falta (Gemini **OU** OpenRouter); "Avaliados" = ambas presentes (definição uniformizada com a contagem e o aviso de exportação PDF).
  3. Duplo clique ou botão "olho" → `BenchmarkEvaluationDetail` (diálogo 900px com as tabs **Métricas** e **Feedbacks dos Juízes**; conteúdo detalhado na secção 4.1).
  4. **Exportar para Excel** → `ExportarParaExcel()` (ClosedXML): agrupa por `PromptId`, cabeçalho de prompt a azul, linhas por modelo; download via JS `downloadFileFromBase64`.
  5. **Análise IA Local** → `FluentDialog` (800px) com `BenchmarkAnalysisViewer`; valida com confirmações o nº de grupos de prompts (>1) e benchmarks ainda não avaliados; executa `LocalAnalysisService.AnalisarBenchmarksAsync` (100% local em C#, cancelável) e apresenta sumário executivo, métricas rápidas e análise técnica (ver 4.1).
  6. **Apagar Benchmarks** — menu (`FluentMenu`) com 4 âmbitos e contagens: **filtrados** (o que está visível com o filtro/pesquisa ativos), **por avaliar**, **avaliados** e **todos** (`DeleteByScopeAsync`); âmbitos com 0 registos ficam desativados. Apagar por âmbito usa `DeleteResponsesAsync` (transação única + limpeza dos prompts que fiquem sem respostas); "todos" usa `DeleteAllPromptsAsync` (os prompts e, por `ON DELETE CASCADE`, as respostas). Apagar linha individual (apaga também o prompt se for a única resposta). Ambas as vias com confirmações via `IDialogService` (ver 4.2).
- **Outputs:** `FluentDataGrid` (Modelo, Descrição, Gemini [Factual/Formato/Global], OpenRouter [Factual/Formato/Global], Tokens/s, Tempo, Carga, Tokens, Ações) + paginação (10/página); ficheiro Excel; diálogo de detalhe com tabs; painel de análise local com streaming de log.

> **Responsividade:** a barra de ferramentas faz *wrap* (pesquisa a largura total em mobile) e a grelha fica num contentor com `overflow-x: auto` para deslocamento horizontal. O mesmo padrão (`overflow-x: auto` + larguras mínimas) aplica-se às grelhas de Modelos, Qualidade e Métricas e Logs.

### 3.4 Qualidade e Métricas (`/benchmarks`)

- **Propósito:** análise **split-view** por prompt (cards à esquerda) com as respostas de cada modelo avaliadas (à direita), gráficos e edição de avaliações.
- **Fluxo:**
  1. `GetDataAsync()` → `BenchmarkQueryRepository.GetAllBenchmarksAsync()` (prompts + respostas).
  2. Esquerda: cards de prompt com badge `#id`, data e nº de modelos; botões **Gráfico** (`BenchmarkChartDialog` — 2 tabs: **Desempenho** com 4 gráficos Chart.js e **Qualidade** com 2 radares por juiz, todos com download de imagem, ver 4.1) e apagar (cascade, com confirmação "Apagar Benchmark", ver 4.2).
   3. Direita: card por resposta com badges `Tokens/s`, `Eval`, `Load`, `Tokens`; ratings Gemini/OpenRouter; **resposta do modelo colapsada por defeito** (ver nota abaixo); botões **Avaliação** (`BenchmarkEvaluationDialog` — formulário 12 métricas 1–5 + tradução de feedback, ver 4.1) e **Avaliar automaticamente** (`AvaliarAutomaticamenteAsync` — gera o prompt do juiz com `EvaluatePromptTemplate` + metadados do modelo via `/api/show`, submete-o aos juízes Gemini (`gemini-flash-latest`) e OpenRouter (default `openrouter/free`) via `AutomatedJudgeService`, preenche as métricas com `EvaluationParser` e abre o diálogo já preenchido). As chaves de API dos juízes vêm da **página Settings** (BD, tabela `Configuracoes`; fallback em user-secrets `ApiKeys:Gemini` / `ApiKeys:OpenRouter`); o serviço impõe um **intervalo mínimo entre avaliações** (`AutomatedJudge:MinIntervalSeconds`, default 60 s) marcado **apenas após sucesso** (falhas não bloqueiam novas tentativas) e mostra o diálogo `Benchmarks.QuotaLimitMessage` quando o limite é atingido. Antes de submeter, `AvaliarAutomaticamenteAsync` verifica a ligação à internet via `InternetConnectivityService.HasInternetAsync()` e mostra o erro `Benchmarks.NoInternetError` se não houver (os juízes são serviços em nuvem). O botão **Copiar prompt** (`CopiarPromptAvaliacaoAsync`) mantém o fallback manual (só copia o prompt gerado, sem precisar de internet). **Falha total (nenhum juiz respondeu):** mostra o diálogo de erro `Benchmarks.AutomatedJudgeErrorTitle` com o corpo `Benchmarks.AutomatedJudgesNoResponseMessage` seguido dos erros de ambos, e **não abre o diálogo de avaliação**. **Falha parcial (pelo menos um respondeu):** abre o diálogo com um aviso que identifica qual(ais) falhou(ram) e deixa os campos desse(s) juiz(es) **editáveis** — o utilizador tem três opções: **aguardar** e tentar mais tarde, **aproveitar a avaliação do juiz que respondeu** e preencher manualmente o outro, ou **fazer toda a avaliação manual** (com 'Copiar prompt').
   4. `SaveEvaluationAsync` valida ratings 1–5 (diálogo "Erro de Validação" se fora do intervalo, ver 4.2) e grava com `UpdateResponseEvaluationAsync` (diálogo "Sucesso").

> **Feedback de progresso e cancelamento da avaliação automática:** como os juízes são serviços em nuvem (e o intervalo mínimo entre avaliações é de 60 s), `AvaliarAutomaticamenteAsync` expõe o estado real da operação em vez de um "loading" genérico. Uma **barra de estado** por cima da lista (`_avaliacaoEmCurso`, `.barra-avaliacao`) mostra o modelo em avaliação, os **segundos decorridos** (cronómetro de 1 s com `PeriodicTimer`, atualizado via `InvokeAsync`), um botão **Cancelar** e uma lista de **4 passos**: `Preparar prompt` (obter `/api/show` + construir o prompt) → **`juiz Gemini`** → **`juiz OpenRouter`** → `Validar notas`. Cada passo tem estado próprio (`a aguardar` / `a processar…` / `concluído` / `falhou`, com o detalhe do erro no `title`), tempo próprio (congelado quando termina) e marcador próprio (⟳ a girar enquanto ativo).
> Os passos dos juízes não são estimativas: `AutomatedJudgeService.GetFeedbacksAutomatizadosAsync` recebe um `Action<JudgeProgressEvent>` opcional (`reportarProgresso`) e emite `DefinicoesResolvidas`, `QuotaVerificada`, `JuizIniciado`, `JuizConcluido`, `JuizFalhou` (com a mensagem de erro) e `ModeloAlternativo` (quando o OpenRouter cai no fallback `openrouter/free`), terminando em `Concluido` — **por juiz**, já que as chamadas continuam em paralelo. Um juiz sem chave configurada reporta `JuizFalhou` imediatamente, sem gastar rede. O serviço também aceita `CancellationToken`: o botão **Cancelar** aborta os pedidos HTTP em curso e o `catch (OperationCanceledException)` do componente distingue "o utilizador cancelou" (sem diálogo de erro, apenas log) de "os juízes falharam". Em paralelo, o **card da resposta em avaliação** ganha a classe `.cartao-avaliando` (borda esquerda de destaque) e o botão "Avaliar automaticamente" é substituído por um `FluentProgressRing` + "A avaliar...". O cronómetro é cancelado no `finally` do método e em `DisposeAsync` (navegação a meio da avaliação); enquanto houver operação em curso (`OperacaoEmCurso`), os botões "Avaliar automaticamente", "Copiar prompt" e "Avaliação" ficam desabilitados. Os `return` antecipados (sem internet, quota, juízes sem resposta) passam todos pelo `finally`, pelo que a barra desaparece em qualquer cenário.

> **Respostas colapsadas por defeito:** cada card do painel direito mostra a resposta do modelo dentro de uma caixa colapsável `.resposta-collapse-box` (variante neutra, barra esquerda accent). O cabeçalho é um disclosure acessível (`role="button"`, `tabindex="0"`, `aria-expanded`, ícone de documento + chevron que roda entre `ChevronUp`/`ChevronDown`, rótulo `Benchmarks.ShowResponseLabel` = "Ver resposta") e o markdown só é renderizado quando a caixa está aberta. O estado vive em `HashSet<int> _respostasAbertas` (por `ResponseId`) e é limpo ao mudar de prompt; <kbd>Espaço</kbd>/<kbd>Enter</kbd> alternam a caixa.

> **Responsividade:** o `FluentSplitter` deteta a largura do viewport via **JS interop** (`appViewport.getWidth` + `subscribeResize` no primeiro render; `unsubscribeResize` em `DisposeAsync`) e aplica `AplicarLayoutCompacto` — em ecrãs **≤768px** a orientação passa a **vertical** (painéis empilhados, `_panel1MinSize` 120px / `_panel2MinSize` 200px), acima disso mantém-se **horizontal** (250px/400px). Se o JS falhar, mantém o estado predefinido (horizontal).

- **Outputs:** cards de prompt; cards de resposta com markdown formatado; diálogo de avaliação com o formulário dos juízes e tradução; `BenchmarkChartDialog` com duas tabs — **Desempenho** (4 gráficos de barras Chart.js: Tempo de Execução/Eval, Tempo de Carga/Load, Velocidade (Tokens/s), Total de Tokens) e **Qualidade** (2 radares por juiz, Gemini/OpenRouter, com as 12 métricas na escala 0–5) — todos com download de imagem (conteúdo detalhado na secção 4.1).

### 3.5 Modelos Ollama (`/modelos-ollama`)

- **Propósito:** grelha dos modelos locais instalados com o contexto nativo e a compatibilidade estimada com a GPU.
- **Fluxo:** carrega `localStorage "ollamaModelsFullV2"` (cache-first; a chave v1 `ollamaModelsFull` deixou de ser usada porque não guardava a capacidade de reasoning); se vazio, combina `GET /api/tags` com `POST /api/show` (metadados) e grava a cache.
- **Toolbar:** barra *sticky* acima da grelha (`.toolbar` / `.toolbar-content` / `.toolbar-row`, mesmo padrão de `/benchmark-evaluations`) com a contagem de modelos instalados à esquerda e, à direita, o botão **"O que significa cada coluna?"** (`Icons.Regular.Size20.Info`, `Appearance.Outline`) que abre o `ModelColumnsInfoDialog`.
- **Outputs:** `FluentDataGrid` com Nome, Família, Parâmetros, Quantização, Ano de Treino, Contexto Máximo (tokens), Tamanho no Disco (GB), badge de **Raciocínio** (verde "Suporta" / neutro "Não suporta" / neutro "Desconhecido" quando o Ollama não devolve `capabilities`) e badge de Compatibilidade GPU (verde "Compatível" / âmbar "Excede VRAM" / neutro "Leitura VRAM Indisponível").

### 3.6 Editar Prompts (`/edit-prompts`)

- **Propósito:** edição em runtime dos ficheiros `Prompts/*.txt` (Content files copiados para output — editáveis sem recompilar).
- **Fluxo:** `FluentSelect` para escolher ficheiro; `FluentTextArea` para editar; **Gravar** (`PromptFilesService.SavePromptFileAsync`), **Cancelar** (recarrega conteúdo) e **Sair** (volta a `/`); aviso de navegação com alterações pendentes (`NavigationManager.RegisterLocationChangingHandler` + diálogo `ConfirmDiscardAsync`).
- **Outputs:** toasts de sucesso/erro; diálogo "Alterações não gravadas".

### 3.7 Páginas de leitura de documentos (`/plano-de-testes`, `/comparar-modelos`)

Duas páginas **apenas leitura**, ambas acedidas por **botões do cabeçalho** (`MainLayout.razor`, ícone + legenda) e não pelo menu lateral. Partilham a mesma implementação: `PromptFilesService` resolve o ficheiro pelo idioma da UI (cookie PT/EN, com fallback ao ficheiro base) e o `MarkdownRenderer` (Markdig) converte o Markdown para HTML, renderizado estilizado numa `div` `.testplan-markdown` (títulos, tabelas, checklists, prompts em blocos de código). Sem gravação nem aviso de alterações pendentes; badge "Apenas leitura" (`TestPlan.ReadOnly`), estado de carregamento (`FluentProgressRing`) e botão **Sair** (`NavigateTo("/")`).

| Página | Pasta / ficheiro | Conteúdo |
|---|---|---|
| **Plano de Testes** (`/plano-de-testes`) | `PlanoDeTestes/Prompts_Teste_Juiz.md` (PT) + `.en.md` (EN) | catálogo de **15 casos** para avaliar juízes/modelos: controlo benigno, segurança/veto, contexto legítimo, língua errada, truncamento/formatação, compliance, deteção de loop, 6 jailbreak/engano e 2 temas (criativo, conversacional) |
| **Comparar Modelos** (`/comparar-modelos`) | `ComoCompararModelos/Guia_Como_Comparar_Modelos.md` (PT) + `.en.md` (EN) | guia passo-a-passo para comparar modelos de forma justa (mesmo prompt, mesmo contexto, mesma temperatura, leitura dos scores) |

- **Fluxo técnico:** `PromptFilesService.GetPromptFiles(<pasta>)` → resolução do nome por `CultureInfo.CurrentUICulture.TwoLetterISOLanguageName` (PT → ficheiro base; EN → `.<código>.md` com fallback ao base) → `GetPromptFileContentAsync(file, <pasta>)` → `MarkdownRenderer.ToHtml`. Toasts de erro via `TestPlan.InitError` / `TestPlan.LoadError` (plano de testes) e `CompareModels.InitError` / `CompareModels.LoadError` (comparar modelos).
- **Âncoras internas:** cliques em `](#…)` são interceptados por `wwwroot/js/markdown-anchors.js` (ver 9.1) — scroll suave com correspondência de id insensível a acentos e `stopPropagation()` para que o Blazor não trate a âncora como navegação SPA.

### 3.8 Logs do Sistema (`/system-logs`)

- **Propósito:** consulta, filtro e eliminação dos logs Serilog (tabela SQLite `Logs`).
- **Fluxo:** `LogRepository.GetAllLogsAsync()`; filtros por nível (Informações/Alertas/Erros/Todos) e pesquisa de texto; duplo clique → `LogDetailDialog` (mensagem + exceção); limpar visíveis/filtrados e apagar por linha (com confirmações e diálogos info/erro via `IDialogService`, ver 4.2).
- **Outputs:** `FluentDataGrid` (ID, Data/Hora em badge, Nível em badge colorido por severidade, Mensagem, Ações) com `FluentPaginator` (10/página).

### 3.9 Dashboard (`/dashboard`)

- **Propósito:** ranking agregado por modelo com visualizações gráficas — KPI cards, radares de qualidade por juiz, scatter plot de velocidade vs. qualidade e tabela classificativa.
- **Fluxo:** `OnInitializedAsync` → `BenchmarkStatsRepository.GetModelRankingAsync()` (agrega por `NomeModelo`, calcula o score com `ScoreCalculator`, ver secção 5) + `GetModelChartDataAsync()` (dados para gráficos) + `GetDashboardStatsAsync()` (total de prompts, data do último e cobertura de avaliação) + `GetScoreTimelineAsync()` (série temporal do score); erros silenciados (ranking vazio). `OnAfterRenderAsync` renderiza os gráficos Chart.js via JS interop (`benchmarkCharts.renderGrafico`/`renderScatter`/`renderLinha`) com retry para aguardar o canvas do DOM.
- **Outputs:**
  - **6 KPI cards** com ícones FluentUI: **Melhor Modelo** (nome + score, accent), **Modelos avaliados** (contagem, success), **Cobertura de avaliação** (% de respostas com os dois juízes + fração "x/y", warning), **Prompts testados** (contagem + data do último benchmark, accent), **Modelo mais rápido** (nome + tokens/s, success) e **Maior divergência juízes** (modelo com maior |Gemini − OpenRouter| + valor Δ, warning).
  - **2 radares de qualidade** (Chart.js, escala 0–5, 12 métricas) — um por juiz (`graficoRadarDashboardGemini` / `graficoRadarDashboardOpenRouter`); cada radar média as notas dos modelos que têm avaliação desse juiz; sem dados mostra "Sem avaliações para este juiz."; cada radar tem botão de download de imagem.
  - **1 scatter plot** (`graficoScatterTokens`) —.tokens/s (eixo X) vs. score (eixo Y), cor por modelo; download de imagem.
  - **1 gráfico de linhas "Evolução do score ao longo do tempo"** (`graficoEvolucaoScore`, via `renderLinha`) — score combinado por resposta (mesmo critério do ranking) com **média acumulada** por modelo, ordenado pela data do prompt; labels = união das datas de benchmark (modelos ausentes numa data levam `null`, `spanGaps` liga a linha); só aparece quando há dados; download de imagem.
  - **`FluentDataGrid` classificativa** — colunas: #, Modelo (sortable), Score (com `FluentProgress` 0–5 + valor), Gemini, OpenRouter, Tokens/s (média), Tempo médio (média de `TempoPuroMs`), Divergência (|Gemini − OpenRouter|; "—" quando só um juiz avaliou), Tokens/resposta (média de `TamanhoTokens`); todas as colunas são ordenáveis; `overflow-x: auto` em mobile.

### 3.10 Definições (`/settings`)

- **Propósito:** configuração dos juízes Gemini/OpenRouter (chaves de API), modelo do juiz OpenRouter (escolhido por **radio buttons** a partir do **catálogo público do OpenRouter** — `OpenRouterCatalogService` → `GET /api/v1/models`, só modelos gratuitos; `openrouter/free` fixo no topo; pesquisa; campo "ou escreve um ID à mão" para offline/custom) e toggle **Ativar reasoning (think)** (`Chat:EnableReasoning`, default **false**).
- **Persistência:** tudo gravado na BD (tabela `Configuracoes`) com dirty-tracking e diálogo de confirmação (resumo das alterações; chaves mostradas apenas como "definida/alterada" ou "removida", nunca o valor).

### Resumo conciso por página (rótulos de rota)

As descrições seguintes são resumos concisos, factuais e alinhados com a implementação actual. Servem como referência rápida das páginas rotadas da aplicação.

- Home (`/`): Verifica o estado do servidor Ollama, pré-carrega cache de modelos e mostra pré-visualização do README; fornece acessos rápidos a modelos e atalhos para páginas de leitura.
- Chat (`/chat`): Chat com streaming NDJSON contra o serviço Ollama; persistência de conversas (Conversas/ConversaMensagens), seleção de modelo, barra de contexto, captura opcional de reasoning e gravação automática de métricas de benchmark por troca.
- Benchmarks — Qualidade e Métricas (`/benchmarks`): Split-view por prompt com cards de prompts à esquerda e respostas por modelo à direita; edição manual de avaliações, gráficos (Chart.js), export e análise local (LocalAnalysisService).
- Benchmarks — Avaliações (`/benchmark-evaluations`): Grelha consolidada de todas as respostas com filtros (Todos / Por Avaliar / Avaliados), pesquisa, exportação Excel/PDF, detalhe por resposta (BenchmarkEvaluationDetail) e ações em massa (apagar/analisar).
- Dashboard (`/dashboard`): Visão agregada por modelo: ranking (ScoreCalculator), KPI cards, radares de qualidade por juiz, scatter tokens/s vs. score e evolução temporal; gráficos exportáveis via JS interop.
- Modelos Carregados (`/modelos-ollama`): Grelha de modelos instalados localmente com metadados (`/api/tags`, `/api/show`), contexto nativo, ano de treino, badge de suporte a reasoning e estimativa de compatibilidade GPU (GpuStatus).
- Editar Prompts (`/edit-prompts`): Editor runtime para os ficheiros Prompts/*.txt com guardar, cancelar e aviso de navegação para alterações não gravadas; alterações disponíveis sem recompilar.
- Logs do Sistema (`/system-logs`): Visualização, filtro e eliminação dos registos Serilog armazenados em SQLite (tabela Logs); detalhe de registo, pesquisa e operações com confirmação.
- Definições (`/settings`): Gestão de chaves Gemini/OpenRouter (persistidas em Configuracoes), selecção do modelo OpenRouter (catálogo público ou ID manual) e toggle de reasoning (Chat:EnableReasoning); alterações com dirty-tracking e confirmação.
- Plano de Testes (`/plano-de-testes`): Página de leitura (PT/EN) com casos de teste e sugestões para avaliar juízes e prompts; conteúdo em Markdown servido por PromptFilesService.
- Como Comparar Modelos (`/comparar-modelos`): Guia em-app com metodologia e boas práticas para comparar modelos de forma justa; leitura em Markdown.


## 4. Componentes partilhados

| Componente | Uso |
|---|---|
| `PageHeader` | Cabeçalho de página partilhado: `<h2>` com **gradiente no texto** (`#38BDF8 → #A78BFA` no tema escuro, `#0284C7 → #7C3AED` no tema claro via `body[data-theme="light"]`), barra de acento de 88×4 px e **linha divisória** (`--neutral-stroke-rest`); margem inferior de 30 px. Parâmetros `Title` (obrigatório) e `Subtitle` (opcional). Usado em `/settings`, `/system-logs`, `/modelos-ollama`, `/dashboard`, `/benchmarks` e `/benchmark-evaluations` |
| `HistoryPanel` | Painel lateral deslizante direito (`min(92vw, 860px)`×`100dvh`, com overlay, animação e `inert` enquanto fechado) com **duas tabs**: **Benchmarks** (linha de acções com **Apagar histórico** + grelha paginada 12/página: Descrição, Modelo, Data, Tempo Eval., Tempo Procº) e **Conversas** (linha de acções com **Exportar**/**Importar** JSON e **Apagar histórico** — este último desativado enquanto o chat tiver uma conversa aberta — mais a lista com **carregar** por linha) — ver 4.1 |
| `GpuInfoDialog` | Diálogo informativo (550px; **95vw em mobile**) sobre "CPU Fallback" (VRAM insuficiente, impacto na velocidade, dicas) — ver 4.1 |
| `ModelColumnsInfoDialog` | Diálogo informativo (600px; **95vw em mobile**) que explica o significado das **9 colunas** da grelha `/modelos-ollama` (uma secção por coluna, na mesma ordem) - ver 4.1 |
| `CultureSelector` | Comutação PT/EN (cookie de cultura via endpoint `Culture/Set`) |
| `BenchmarkEvaluation` | Formulário de avaliação por juiz: 12 métricas (Factual, Formatação, Compliance, Relevância, Tom, Concisão, Clareza, Legibilidade, Efeito Halo, Segurança, **Consistência Idiomática**, **Detecção de Loop**) + **Global**, escala 1–5; parse automático do output bruto com `EvaluationParser` (tags `*_SCORE:`, `FINAL_SCORE:`, `DESCRIPTION:`/`FEEDBACK:`, `RECOMMENDATION:`); caixa de recomendação do juiz (âmbar, colapsável, acionável por teclado) quando o campo `RECOMMENDATION` foi preenchido; **células de métricas fracas** destacadas a vermelho (fundo `rating-cell-weak`, borda esquerda 3px) — nota ≤3 para métricas normais, ≤2 para Segurança; **cabeçalhos de coluna dinâmicos** por `JudgeLabelResolver` (ver 4.1.1) |
| `BenchmarkEvaluationDialog` | Envelope do formulário + **tradução automática do feedback** via cadeia OpenRouter → Gemini → Ollama (`TranslationService`), com feedback do sistema em uso e pré-visualização (`TranslationPreviewDialog`) — ver 4.1 |
| `BenchmarkEvaluationDetail` | Diálogo 900px (**95vw em mobile**) com tabs **Métricas** (grelha Gemini vs OpenRouter) e **Feedbacks dos Juízes** — ver 4.1 |
| `BenchmarkChartDialog` | 2 tabs de gráficos Chart.js (4 barras de desempenho + 2 radares de qualidade por juiz) com download de imagem — ver 4.1 |
| `BenchmarkAnalysisViewer` | Painel de resultado da análise local (estados: vazio / streaming com log / concluído com sumário executivo) — ver 4.1 |
| `TranslationPreviewDialog` | Pré-visualização de tradução (modelo usado + tempo gasto) — ver 4.1 |
| `LogDetailDialog` | Detalhe de um log (mensagem + exceção) — ver 4.1 |
| `ReadmePreview` | Preview do README do GitHub (via `ReadMeService` + `MarkdownRenderer`) — ver 4.1 |

### 4.1 Conteúdo dos diálogos (detalhe)

- **`BenchmarkEvaluationDetail`** — o botão **"olho"** (ou duplo clique) na linha da grelha em `/benchmark-evaluations` abre este diálogo modal (900px; **95vw em mobile**). Cabeçalho com ícone olho, nome do modelo e o prompt executado em `blockquote` (com scroll). Tem **duas tabs**:
  - **Métricas** — grelha com colunas `Métrica | <rótulo do juiz 1> | <rótulo do juiz 2>` (cabeçalhos **dinâmicos** via `JudgeLabelResolver`, ver 4.3) e linhas Factual, Formatação, **Global** (linha destacada com bordas accent), Compliance, Relevância, Tom, Concisão, Clareza, Legibilidade, Efeito Halo, Segurança, Consistência Idiomática e Detecção de Loop; cada célula é a nota do juiz ou "—" quando ainda não avaliada (nota 0). Os dados vêm da consulta consolidada `BenchmarkResponseEvaluationAsync`.
  - **Feedbacks dos Juízes** — dois cards lado a lado (com títulos **dinâmicos**, ver 4.3) com o texto do feedback; o conteúdo é carregado assincronamente via `BenchmarkQueryRepository.GetBenchmarkJudgesFeedbackByIdAsync(ResponseId)` (progress ring durante a carga) e mostra "Nenhum feedback registado." se vazio. Quando o juiz devolveu `RECOMMENDATION`, cada card mostra ainda uma **caixa "Recomendação do Juiz"** (fundo âmbar) com o texto da recomendação.
- **`BenchmarkEvaluationDialog`** (aberto pelo botão "Avaliação" em `/benchmarks`; 1200px × `min(calc(100vh - 48px), 920px)`, **95vw em mobile**): cabeçalho com ícone `ClipboardCode` e nome do modelo (o prompt não é repetido, pois já é mostrado na página); corpo com o formulário `BenchmarkEvaluation` (12 métricas na escala 1–5, com parse automático do output dos juízes via `EvaluationParser`) e uma **barra de tradução fixa no fundo do corpo** (`position: sticky`; o corpo é a área com scroll), com os dois botões **Traduzir Feedback (<rótulo do juiz>)** (rótulo **dinâmico** via `JudgeLabelResolver`, ver 4.3) sempre visíveis mesmo com o formulário a exigir scroll. A tradução segue a língua da **sessão** através da cadeia **OpenRouter → Gemini → Ollama** (`TranslationService`) e abre o `TranslationPreviewDialog`; a UI mostra, em tempo real, **qual sistema e modelo** está a ser usado ("Sistema: OpenRouter · openrouter/free"), indicando a falha quando há mudança de sistema ("OpenRouter falhou, a usar o sistema seguinte."). Rodapé com **Guardar avaliação** (accent; oculto quando a avaliação já está gravada) e **Fechar**. Enquanto a tradução está em curso surge, por baixo dos botões, um bloco de estado (`role="status"`, `aria-live="polite"`) com progress ring, o texto **"A traduzir feedback de <juiz>… <mm:ss>"** (cronómetro atualizado a cada segundo) e o botão **Cancelar** (que passa a "A cancelar…" e fica desabilitado enquanto o pedido é abortado); durante a tradução os botões de traduzir, **Guardar** e **Fechar** ficam desabilitados — o cancelamento é feito pelo botão **Cancelar**, que aborta o pedido HTTP em curso (`CancellationToken` propagado por `TranslationService`) sem mostrar diálogo de erro.
  - **Botão de traduzir desabilitado quando a tradução é redundante** — cada botão avalia o *seu* feedback contra o idioma da aplicação (`CultureInfo.CurrentUICulture`, o mesmo para onde `TargetLanguageResolver` manda traduzir) com a heurística determinística `ResponseLanguageChecker.EstaNoIdiomaEsperado` (PT vs EN). Se o feedback estiver **comprovadamente** no idioma da aplicação, traduzi-lo devolveria o mesmo texto, pelo que o botão fica desabilitado, ganha um `title` explicativo ("O feedback já está no idioma da aplicação (…).") e aparece uma **nota visível** por baixo dos botões (`.nota-traducao-idioma`). Cada juiz é avaliado por separado (o feedback do Gemini em português e o do OpenRouter em inglês dão botões em estados diferentes). A botão também fica desabilitado sem feedback (`TraducaoIndisponivel`). **Deteção inconclusiva nunca desabilita**: num feedback curto (<12 palavras) ou ambíguo a heurística não sabe o idioma, e sem essa certeza o utilizador pode ainda querer o texto normalizado pelo modelo.
- **`TranslationPreviewDialog`** (60vw, modal): "Tradução do Feedback (<juiz>)", "Sistema e modelo usado" (`TranslationResult.EngineDescription`, ex.: "Gemini · gemini-flash-latest"), "Tempo gasto" (tempo do sistema que traduziu) e o texto traduzido numa caixa monoespaçada de **leitura** (`max-height: 400px`, scroll); **Sair** fecha. A tradução é apenas uma pré-visualização — o texto não é escrito de volta no campo do juiz.
- **`BenchmarkChartDialog`** (aberto pelo botão "Gráfico" em `/benchmarks`; 50vw×85vh, **94vw em mobile**): cabeçalho "Gráficos de Benchmark do Prompt #<id>" com **duas tabs**:
  - **Desempenho** (`tab-desempenho`) — quatro gráficos de barras Chart.js (cor por modelo; labels partidos em `:` ou `-`):
    - Tempo de Execução / Eval (ms) — `graficoEval`;
    - Tempo de Carga do Modelo / Load (ms) — `graficoLoad`;
    - Velocidade de Geração (Tokens/s) — `graficoVelocidade`;
    - Total de Tokens Gerados — `graficoTotalTokens`.
  - **Qualidade** (`tab-qualidade`) — dois **gráficos radar** (escala 0–5) com as 12 métricas de qualidade, um por juiz (`graficoRadarGemini` / `graficoRadarOpenRouter`); cada radar média as notas dos modelos que têm avaliação desse juiz e, sem dados, mostra "Sem avaliações para este juiz." (o radar só é renderizado quando o tab é aberto, com retry de até 8 tentativas enquanto o canvas do tab é montado).
  Cada gráfico tem botão de download da imagem (JS `benchmarkCharts.downloadGrafico`, ficheiro `benchmark_<métrica>_prompt_<id>`).
- **`LogDetailDialog`** (aberto pelo "olho" em `/system-logs`; 40vw×65vh, **94vw em mobile**): "Detalhes do Registo #<id>" com a caixa **Mensagem** (texto formatado, word-break) e, apenas quando existe, a caixa **Exceção / Erro** (fundo avermelhado, monospace, scroll até 250px).
- **`GpuInfoDialog`** (aberto pelo "?" de GPU no Chat; 550px, **95vw em mobile** — `width: min(550px, 95vw)`): "Informação do Sistema" com três blocos explicativos — "O que significa 'CPU fallback'?", "VRAM insuficiente" e "Impacto na velocidade" (lista de consequências na performance).
- **`ModelColumnsInfoDialog`** (aberto pelo botão "O que significa cada coluna?" na toolbar de `/modelos-ollama`; 600px, **95vw em mobile** — `width: min(600px, 95vw)`, `max-height: calc(100vh - 48px)` com corpo em scroll): "O que significa cada coluna?" com **nove blocos** explicativos — **uma secção por coluna da grelha, pela mesma ordem** — todos alinhados com o cálculo real do código: **Nome** (etiqueta do `/api/tags`, o identificador que o Chat envia ao Ollama), **Família** (`details.family`: arquitectura, tokenizer e template de prompt), **Parâmetros** (`details.parameter_size` do GGUF; B/M), **Quantização** (compressão dos pesos no GGUF: F32 → Q4/Q5/Q6…; menos bits = menos VRAM e mais velocidade, ao custo de qualidade), **Ano de Treino** (`model_info.*.general.date`; fallback por varrimento do Modelfile/template e, em último caso, "2024" — heurística, não garantia), **Contexto Máximo** (`model_info.*.context_length` de `/api/show`, fallback `DefaultContextLength` 2048, com lista sobre o `num_ctx` reduzido conforme a VRAM, o limite de 32 768 tokens da app e o truncamento do histórico), **Tamanho no Disco** (`SizeInGB`, divisões binárias de 1024 — GiB, não GB decimal), **Raciocínio** (capacidade `thinking` do array `capabilities`, com lista dos três estados do badge: Suporta / Não suporta / Desconhecido, e nota de que o campo `think` é enviado explicitamente aos que suportam e omitido nos restantes) e **Compatibilidade GPU** (estimativa `size × ModelLoadOverheadFactor` 1,2 contra a VRAM livre menos a reserva de 350 MB do Windows, com lista dos três estados do badge: Compatível / Excede VRAM / Leitura VRAM Indisponível, e nota de que é uma fotografia de momento da VRAM livre). Todo o texto vive nos recursos `Modelos.Help*` (PT + EN): uma chave vazia (`<value />`) aparece na UI como parágrafo em branco ou bullet vazio, por isso nenhuma pode ser deixada por preencher.
- **`HistoryPanel`** (aberto pelo botão "Histórico" no Chat): painel deslizante do lado direito (`min(92vw, 860px)`×`100dvh`) com overlay, animação e `inert` enquanto fechado (evita que os botões fora do ecrã continuem focáveis); o cabeçalho só tem o título e **Fechar** — a ação destrutiva fica no corpo de cada tab, sempre como último botão; **tab "Benchmarks"** com grelha paginada (12/página) de Descrição do Prompt (tooltip), Modelo, Data, Tempo Eval. e Tempo Procº (dados de `BenchmarkQueryRepository.HistoryResponseAsync()`, em scroll horizontal próprio; estados "A carregar..." e "Sem histórico."), com **Apagar histórico** em cima (`IBenchmarkCommandRepository.DeleteAllPromptsAsync`, confirmação por diálogo); **tab "Conversas"** com **Exportar JSON** (`ConversationRepository.GetAllForExportAsync`), **Importar JSON** (→ `ImportAsync`, transacional), **Apagar histórico** (`ClearAllConversationsAsync`, mensagem de confirmação própria) e a lista de conversas (`GetConversationsAsync()`, ordenada por última atividade) com **carregar** por linha. O botão de apagar conversas fica **desativado** enquanto `HasOpenConversation` for verdadeiro (o `Chat` passa `_conversationId > 0`): apagar tudo deixaria o `_conversationId` do chat a apontar para uma linha apagada e o próximo turno falharia na FK de `ConversaMensagens.ConversationId`. Os diálogos de confirmação são os do `DialogService` (provider no `MainLayout`); para ficarem **acima** do painel e do overlay — que usam `z-index` 9998/9999 — o `app.css` põe `fluent-dialog { position: relative; z-index: var(--z-modal) }`, já que a biblioteca não atribui `z-index` ao host do diálogo.
- **`ReadmePreview`** (aberto pelo botão "Readme" na Home; 50vw×85vh, **94vw em mobile**): "Preview README.md" com o markdown do README do GitHub renderizado via Markdig (`ReadMeService.LoadReadmeAsync`); "Loading..." enquanto carrega.
- **Diálogo "Análise IA Local"** — o botão **"Análise IA Local"** em `/benchmark-evaluations` abre um `FluentDialog` (800px; **95vw em mobile** — mesma classe CSS `evaluation-detail-benchmark` do `BenchmarkEvaluationDetail`) cujo corpo é o `BenchmarkAnalysisViewer`, com o cabeçalho **"Análise Analítica do Modelo Local"** e badge **Análise Offline Ativa** (sem tabs; o resultado é apresentado em secções empilhadas):
  - Estado vazio: "Pronto para analisar";
  - Durante o processamento: progress ring "A processar resposta..." + botão **Cancelar** e caixa de **streaming do log** (monospace, formatada por `MessageFormatter`);
  - Concluído: "Análise Concluída" e o resultado com **Sumário Executivo**, cards **⚡ Mais Rápido** (modelo + tokens/s) e **⭐ Melhor Avaliado**, e **🔍 Análise Técnica Detalhada** (linhas HTML);
  - Antes de executar valida com confirmações o nº de grupos de prompts ("Múltiplos Prompts Detetados", se >1) e benchmarks não avaliados; o cálculo é feito por `LocalAnalysisService.AnalisarBenchmarksAsync` (100% C#, sem LLM), cancelável, com estados de erro/cancelamento também apresentados no painel.

### 4.2 Diálogos de sistema (`IDialogService`)

Para além dos diálogos personalizados da secção 4.1, a app usa `IDialogService` (FluentUI) para **confirmações**, **informações** e **erros** (modais):

- **Confirmações de eliminação** (botões "Sim, Apagar" / "Cancelar"):
  - `/benchmark-evaluations`: **"Apagar todos os benchmarks"** (todos os prompts e respostas), as confirmações dos 4 âmbitos do menu — **filtrados / por avaliar / avaliados / todos** (indicam o nº de respostas a apagar) — e **"Apagar resposta do benchmark"** (linha individual).
  - `/benchmarks`: **"Apagar Benchmark"** / **"Aviso de Eliminação"** — indica o nº de respostas que serão removidas em cascade.
  - `/system-logs`: **"ATENÇÃO - VAI LIMPAR TODOS OS REGISTOS"** (limpeza total) ou **"Confirme Eliminação"** (apenas os filtrados) e **"Apagar registo"** (linha individual).
- **Confirmações da Análise IA Local** (descritas em 4.1): **"Múltiplos Prompts Detetados"** e **"Nem todos os benchmarks estão avaliados"**.
- **"Alterações não gravadas"** em `/edit-prompts` (Descartar / Cancelar; também disparado ao navegar para fora da página).
- **Informações/erros** (`ShowInfoAsync` / `ShowErrorAsync`):
  - `/system-logs`: **"Nenhum registo"** (apagar sem logs filtrados), **"Sucesso"** (n registos eliminados) e **"Erro ao eliminar"**.
  - `/benchmarks`: **"Erro de Validação"** (rating fora do intervalo 1–5) e **"Sucesso"** (avaliação gravada no SQLite).
  - `/benchmarks`: **"Erro na avaliação automática"** com o corpo `Benchmarks.AutomatedJudgesNoResponseMessage` + os erros de ambos os juiz — usado quando **nenhum** juiz devolveu avaliação; neste caso o diálogo de avaliação **não** é aberto (ver 4.1).
  - Home: **"Internet indisponível"** quando o botão Readme é clicado sem ligação.
  - `BenchmarkEvaluationDialog`: erros ao guardar a avaliação e ao traduzir feedback (Gemini/OpenRouter).

### 4.3 Origem real da avaliação por juiz (`JudgeOriginResolver` / `JudgeLabelResolver`)

Os rótulos dos dois \"juízes\" não são fixos: refletem **de onde veio o conteúdo de cada slot**, para que uma avaliação colada manualmente não seja apresentada como se tivesse vindo da nuvem.

**Valores de origem** (`OllamaArena/Services/Helpers/JudgeOriginResolver.cs`):

| Valor | Constante | Significado |
|---|---|---|
| `gemini` | `JudgeOriginResolver.Gemini` | slot preenchido pelo serviço automático do Gemini |
| `openrouter` | `JudgeOriginResolver.OpenRouter` | slot preenchido pelo serviço automático do OpenRouter |
| `externo` | `JudgeOriginResolver.Externo` | conteúdo colado/preenchido manualmente pelo utilizador |
| `null` | — | slot **vazio** (\"sem resposta\") **ou** registo legado com conteúdo mas sem origem registada |

- **Conteúdo** (`JudgeOriginResolver.TemConteudo`) = feedback não vazio **OU** nota global preenchida.
- **Persistência** (`CalcularOrigemSlot`): sem conteúdo → `null`; origem já registada → mantém-se; conteúdo presente à abertura mas sem origem gravada (legado) → `null` (nunca inventa origem); entrada manual nova → `externo`. As colunas `Respostas.Juiz1Origem` / `Juiz2Origem` (`TEXT`) são criadas pela migração `DatabaseSchemaInitializer` (ver secção 8).
- **Rótulos** (`JudgeLabelResolver.Rotulo`): `gemini` → *Google Gemini*; `openrouter` → *OpenRouter*; `externo` → *Juiz 1* / *Juiz 2* (consoante o slot); `null` com conteúdo → rótulo do juiz correspondente (fallback para registos legados); `null` sem conteúdo → **\"Sem resposta\"** (`Evaluation.NoResponse`), com estilo esbatido `.judge-label-muted` (itálico, borda tracejada, opacidade 0,55).
- **Onde aparece:** cabeçalhos das colunas na grelha de `/benchmarks`, cabeçalhos de coluna no formulário `BenchmarkEvaluation`, títulos dos cards e cabeçalhos de coluna no `BenchmarkEvaluationDetail`, legendas dos botões **\"Traduzir Feedback (<rótulo>)\"** e títulos dos blocos de feedback/recomendação na exportação PDF.

---

## 5. Camada de serviços

- **`OllamaGpuService`** — modelos locais (`/api/tags`), modelos em memória (`/api/ps`), metadados (`/api/show`: contexto nativo + ano de treino), compatibilidade GPU (`CheckGpuCompatibility`, **assíncrono** — `Task<GpuStatus>`), **contexto efetivo automático** (`GetRecommendedContextLengthAsync`) e unload de modelos (`keep_alive=0`).
- **`ChatComposerService`** — constrói o payload `/api/chat` a partir dos **`OllamaOptions`**: histórico com system prompt, fusão de mensagens consecutivas e trim a `HistoryBudgetFraction` (0.60), temperatura (`ChatMeasureTemperature`), `num_predict` base GPU/CPU com reserva de `ContextReserveFraction` (0.25), `num_ctx`, `top_k/top_p`, `repeat_penalty` e `think` (resolvido com prioridade BD `Chat:EnableReasoning` → `OllamaOptions.EnableReasoning` → default false). Inclui **extração de SUMMARY** (`ExtractSummaryFromResponse`) — procura o marcador `SUMMARY:` na primeira linha da resposta do modelo (após o streaming) para gerar uma descrição curta do prompt; e **fallback inteligente** (`SmartFallbackDescription`) — extrai a primeira frase significativa (até ao primeiro `.` ou quebra de linha) truncada a 50 carateres no limite de palavra, usada quando o SUMMARY não está disponível. `BuildHistory`, `EstimateTokens`, `ExtractSummaryFromResponse` e `SmartFallbackDescription` são estáticos e cobertos por testes.
- **`ScoreCalculator` / `JudgeScoreWeights`** — cálculo do **score final ponderado** a partir das **12 métricas** de um juiz: pesos configuráveis (`JudgeScoreWeights` em `appsettings.json`, injetados via `IOptions` no `BenchmarkStatsRepository` e no `LocalAnalysisService`), `HaloEffect` com peso **0** (métrica de controlo — não pesa no score, só na fiabilidade), renormalização sobre as métricas presentes; precisa de ≥8 métricas e soma de pesos ≥50 para produzir score (senão devolve `null` e o chamador usa o `FINAL_SCORE` declarado pelo juiz). **Recusas corretas**: quando o juiz marca `REFUSAL_HANDLED: yes`, as métricas Factual/Compliance/Relevance são excluídas da ponderação (renormalização sobre as restantes) — uma recusa correta deixa de penalizar o score do Dashboard. Usado pelo `BenchmarkStatsRepository.GetModelRankingAsync`/`GetModelChartDataAsync` (Dashboard) e pelo `LocalAnalysisService`.
- **`JudgeOriginResolver` / `JudgeLabelResolver`** — determinam a **origem** de cada avaliação (automática `gemini` / `openrouter`, `externo` colada manualmente, ou `null` = sem resposta / registo legado) e o **rótulo** que a UI mostra para cada slot (ver 4.3). Persistem em `Respostas.Juiz1Origem` / `Juiz2Origem`.
- **`PromptThemeClassifier`** — classificação lexical PT/EN do tema de um prompt (`Factual`, `Criativo`, `Conversacional`, `Codigo`, `Seguranca`; heurística de keywords sem acentos, prioridade Seguranca → Criativo → Codigo → Conversacional → Factual). Alimenta os tokens `{promptTheme}`/`{themeGuidance}` do template de avaliação (`EvaluatePromptTemplate.EvaluationCopyPromptAsync`, auto-deteta quando o tema não é indicado), tornando a recomendação dos juízes contextual por temática: em temas criativos/conversacionais Tom e Relevância pesam como o Factual.
- **Idioma esperado na avaliação** — o token `{expectedLanguage}` do template de avaliação é preenchido com o idioma da sessão **gravado na resposta** (`Respostas.IdiomaSessao`, coluna TEXT adicionada por migração; gravado em `PersistBenchmarkAsync` via `TargetLanguageResolver.GetTargetLanguage()`); respostas antigas (coluna NULL) recebem o idioma atual do seletor PT/EN marcado como *"assumed from the current app language"*. A métrica *Language Consistency* exige adesão à língua do prompt **e** a esse idioma esperado, com regra mecânica no template: resposta escrita noutra língua ⇒ nota 1-2 obrigatória; mistura de línguas ⇒ no máximo 3.
- **`JudgeLanguageGuard`** — rede de segurança determinística aplicada após o parse das notas dos juízes (fluxo automático em `PreencherAvaliacao` e colagem manual em `BenchmarkEvaluation`): deteta localmente o idioma dominante da resposta (`ResponseLanguageChecker`, PT vs EN) e, quando diverge claramente do idioma esperado (gravado ou, em falta, o da UI), **limita Language Consistency a ≤2 nos dois juízes** — mesmo que o LLM juiz tenha dado 5 — e acrescenta nota "[Verificação local]…" à recomendação. Novo ramo de **mistura de línguas** (simétrico EN↔PT): texto dominante no idioma certo mas com stopwords da outra língua significativas (`ContemMisturaSignificativa`: ≥4 ocorrências e ≥8% das palavras; "for" excluída por ser também portuguesa) limita a nota a ≤3 com nota local própria. Deteção inconclusiva nunca interfere.
- **`AutomatedJudgeService`** — avaliação automática pelos juízes Gemini/OpenRouter: chaves de API resolvidas com prioridade **BD (`Configuracoes`, página Settings) → configuração (user-secrets/appsettings)**, validadas **antes** de consumir quota; chamadas paralelas independentes (um juiz pode falhar sem bloquear o outro); controlo de quota por provedor (`AutomatedJudge:MinIntervalSeconds`, default 60 s) marcado **apenas após sucesso** (`QuotaLimitException`); modelo do juiz OpenRouter configurável (`AutomatedJudge:OpenRouterModel`, default `openrouter/free`, com **fallback automático** para `openrouter/free` quando o modelo configurado devolve 404); temperatura determinística **fixa em 0** (não configurável); parse com `EvaluationParser`.
- **`TranslationService`** — tradução de feedbacks para a **língua da sessão** com uma **cadeia de sistemas por ordem de preferência: OpenRouter → Gemini → Ollama (local)**. O prompt é o `translation-prompt.txt` editável (token `{{TARGET_LANGUAGE}}`, substituído por `GetTargetLanguage()`: `pt` → "European Portuguese (pt-PT)", `en` → "English (en-US)") e o pedido é não-streaming (`/api/chat` no Ollama; `chat/completions` no OpenRouter; `generateContent` no Gemini), sempre a `temperature 0`. Chaves e modelos vêm do `ApiCredentialsResolver` (BD → configuração → default, partilhado com `AutomatedJudgeService`): OpenRouter usa o modelo da página Settings (`AutomatedJudge:OpenRouterModel`, default `openrouter/free`), Gemini usa `gemini-flash-latest` e o Ollama usa o melhor modelo avaliado na BD (`GetBestModelAsync`). Uma resposta só é aceite se for uma tradução **válida** (JSON `{"translatedText", "confidence"}` como o prompt pede, ou texto simples traduzido com confiança 0.2); respostas vazias, iguais ao original ou iniciadas por frases de análise contam como falha e passam ao sistema seguinte. Se todos falharem é lançada `InvalidOperationException` com o motivo de cada um. `TranslateAsync` aceita um `Action<TranslationProgressEvent>` opcional (a UI mostra o sistema em uso e cada passagem de fallback) e um `CancellationToken` (o pedido em curso é abortado com ele); o cancelamento propaga como `OperationCanceledException` em vez de ser convertido em `InvalidOperationException`, para a UI distinguir "o utilizador cancelou" de "a tradução falhou". Notas: as traduções **não** consomem o intervalo mínimo de quota dos juízes automáticos (`AutomatedJudge:MinIntervalSeconds`), por serem um pedido manual e isolado; e o cancelamento é do lado do cliente — o Ollama pode continuar a gerar até detetar a queda da ligação.
- **`LocalAnalysisService`** — análise de benchmarks **100% local em C#** (sem LLM): sumário executivo, métricas rápidas ("Mais Rápido", "Melhor Avaliado"), **superfície de métricas fracas** (itera as 12 métricas × 2 juízes por benchmark, sinaliza com nota ≤3 — ≤2 para Segurança — agrupadas por modelo) e análise técnica detalhada; todas as strings estão localizadas via `IStringLocalizer<SharedResources>` (chaves `Analysis.*`, pt/en).
- **`PromptFilesService`** — leitura/gravação dos ficheiros `Prompts/*.txt` **e de outras pastas de documentos** (`PlanoDeTestes/`, `ComoCompararModelos/`), via `GetPromptFiles(folder)` / `GetPromptFileContentAsync(file, folder)`. O **system prompt** do chat é lido diretamente por `Chat.razor.cs` (`PromptFilesService.GetPromptFileContentAsync("system-prompt.txt")`) e combinado com `PromptTemplates/ChatInstructionsPrompt.cs` — já não existe um `SystemPromptService` dedicado.
- **`ChatMeasureTemperature`** — recomendação lexical de temperatura (PT+EN): análise de frases, termos, detecção de código e densidade de perguntas; mapeia o prompt para 0.10–0.90 (detalhe técnico na secção 7).
- **`MessageFormatter`** / **`CommonService`** — renderização/limpeza de markdown (fechar blocos, resgatar fences coladas por modelos pequenos, corrigir headings, reconstruir tabelas; pipeline Markdig). Implementado em **6 ficheiros `partial`** (`MessageFormatter.cs`, `.Normalization.cs`, `.Tables.cs`, `.Fences.cs`, `.CodeBreaking.cs`, `.Masking.cs`).
- **`EvaluationParser`** — parse do output estruturado dos juízes (linha-a-linha, com secções `DESCRIPTION`/`FEEDBACK` e `RECOMMENDATION` — corrigido para a `DESCRIPTION` não "engolir" a `RECOMMENDATION`; ver secção 8 para a persistência). Inclui a linha **`REFUSAL_HANDLED: yes/no`** (recusa correta; aceita yes/no, true/false, sim/não e 1/0 — valor desconhecido ou ausente devolve `null`, mantendo o comportamento antigo).
- **`InternetConnectivityService`** — verificação de ligação à internet; **`ReadMeService`** — leitura do README remoto; **`OllamaChecker`** — health check do Ollama.
- **`ConversationRepository`** (`IConversationRepository`) — persistência de conversas do chat em **SQLite** (tabelas `Conversas` / `ConversaMensagens`, **registado no DI**): criar/tocar/listar conversas, gravar/ler mensagens (inclui `Reasoning`, `Temperature`, `ElapsedTime`) e **exportar** (`GetAllForExportAsync`) / **importar** (`ImportAsync`, transacional) como JSON (ver `HistoryPanel`).

**`MessageFormatter` — pipeline de formatação markdown (`FormatMessagePlus`):**
1. **Placeholder de digitação** — se o conteúdo for `...`, devolve o HTML dos três pontos animados (`<div class='typing-dots'>`).
2. **`RemoveInternalReasoning`** — remove blocos `<think>...</think>` (modelos tipo DeepSeek/Phi/Qwen).
3. **`RescueGluedFence`** — resgata fences ``` com a tag de linguagem e/ou o fecho **colados ao código na mesma linha** (padrão de modelos pequenos: ```` ```csharpusing System;...;}}} ````). Separa tag/fecho do corpo, remove a tag repetida antes do fecho (`StripTrailingTag` — ex.: `... } } csharp``` `) e quebra o corpo de **linha única** consoante a linguagem da tag: `BreakSingleLineCode` (C#/JS — quebra em keywords e `;`, comenta `//`/`/* */` em caixa de código) ou `BreakSingleLinePython` (indentação por `:` e comentários `#`), decidido por `IsPythonTag`.
4. **`MaskCode` / `RestoreCode`** — isola código (fences ``` e spans inline `...`) para as heurísticas seguintes não corromperem indentação/identificadores dentro de código.
5. **`DetectNoise`** — decide se é precisa "formatação pesada": texto sem quebras de linha com >180 chars, ou padrões `|`, `%`, camelCase, `1.A`, números com percentagem, `||`, linhas a começar em `|`.
6. **`NormalizeBasic`** (sempre) — fecha blocos ``` não fechados; quebra linha antes de headings; corrige `#Título` → `# Título`; separa listas numeradas e com marcadores (`* - +`) após `:;.!?`; limpa espaços antes de negrito/itálico.
7. **`ApplyUniversalSeparations`** (só se `DetectNoise`) — separa `aB`, `%A`, `:A`, `palavra+%`.
8. **`ReconstructTables`** — agrupa linhas com `|`, estima o nº de colunas (`EstimateBestColumnCount`: nº de pipes − 1, mínimo 2), normaliza as células por linha e insere o separador `---` se faltar.
9. **Pipeline Markdig** — `UseAdvancedExtensions`, `UseSoftlineBreakAsHardlineBreak`, `UsePipeTables`, `UseTaskLists`, `UseAutoLinks`, `UseDefinitionLists`, `UseEmphasisExtras`.
10. **`ConvertParagraphsToDivs`** — converte `<p>...</p>` em `<div>...</div>` para compatibilidade com o CSS da Fluent UI.

**`PromptFilesService` — gestão dos ficheiros `Prompts/*.txt` (e de outras pastas de documentos):**
- `GetDirectory(folderName)` — `ContentRootPath/<pasta>` (dev) com fallback para `AppContext.BaseDirectory/<pasta>` (publish); sem parâmetro usa `Prompts`.
- `GetPromptFiles(folderName)` — lista `.txt` e `.md` da pasta; `GetPromptFileContentAsync(fileName, folderName)` — leitura com validação anti-**path traversal** (`Path.GetFullPath` + prefixo do diretório) e `IsValidPromptFilename` (sem carateres inválidos, sem `..`). Além de `Prompts/`, duas pastas são lidas em modo **apenas leitura** por páginas próprias: `PlanoDeTestes/` (`/plano-de-testes`) e `ComoCompararModelos/` (`/comparar-modelos`), ambas com variantes PT + `.en.md` resolvidas pelo idioma da UI.
- `SavePromptFileAsync` — limite de **200.000 chars**, cria o diretório se faltar e grava o ficheiro (as alterações ficam imediatamente ativas no chat/`PromptFilesService` na próxima leitura).
- **`analysis-prompt.txt` está DESCONTINUADO (mantido por compatibilidade):** nenhum código o lê (`GetPromptFileContentAsync`/`GetTemplateAsync` nunca o referenciam). É um vestígio de um desenho antigo em que a análise de resultados era gerada por LLM; hoje a análise é feita localmente pelo `LocalAnalysisService` a partir de recursos de localização (`Analysis.*`). Continua incluído no build e editável na página `EditPromptFiles`, mas sem efeito em runtime.

---

## 6. Integração com o Ollama (endpoints usados)

| Endpoint | Serviço | Uso |
|---|---|---|
| `GET /api/tags` | `OllamaGpuService.GetLocalModelsAsync` | Lista de modelos locais (Home, Modelos) |
| `POST /api/show` | `OllamaGpuService` | `model_info`: contexto nativo, arquitetura (KV cache), ano de treino |
| `GET /api/ps` | `OllamaGpuService.GetRunningModelsAsync` | Modelos carregados em RAM/VRAM (`size_vram`) — para o cálculo de contexto |
| `POST /api/chat` | `Chat` (streaming), `TranslationService` (fallback local) | Chat NDJSON e tradução de feedbacks |
| `POST https://openrouter.ai/api/v1/chat/completions` | `TranslationService` (1.ª preferência), `AutomatedJudgeService` | Tradução de feedbacks e avaliação automática |
| `POST https://generativelanguage.googleapis.com/v1beta/models/{modelo}:generateContent` | `TranslationService` (2.ª preferência), `AutomatedJudgeService` | Tradução de feedbacks e avaliação automática |
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
- O payload de `/api/chat` é montado pelo `ChatComposerService` (parâmetros base em `OllamaOptions`): `num_ctx` efetivo, `temperature`, `num_predict` (limitado: `contexto − histórico − 25%`), `repeat_penalty` 1.1, `top_k`, `top_p` e `think` — enviado **apenas para modelos com capacidade `thinking`** (via `/api/show`, cacheado por modelo), com valor **explícito**: `true` se o reasoning está ativo, `false` se está desativado (omitir o campo faz o Ollama pensar por omissão e devolver o reasoning); em modelos sem essa capacidade o campo é **omitido** (o Ollama 0.30+ devolve 400 "does not support thinking"). A flag é resolvida em `ChatComposerService` com prioridade **BD (`Configuracoes`, `Chat:EnableReasoning`) → configuração → default false**.
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
- `think` enviado **apenas para modelos com capacidade `thinking`**, com valor explícito (`true`/`false`) conforme o reasoning (`Chat:EnableReasoning`, default **false**; configurável na página Settings); omitido em modelos normais.
- A temperatura é gravada no registo do prompt (`CreatePromptAsync`) e exibida no chat ("Temp: X").

**Benchmark automático (gravado na BD):** com `PromptEvalCount` + `EvalCount`, `LoadDuration`, `EvalDuration` calcula `TokensPorSegundo`, `TempoPuroMs`, `TempoCargaMs`, `TempoProcessamento`, `TamanhoTokens` e associa ao prompt (`CreatePromptAsync` + `CreateResponseAsync`). Testes incompletos são descartados (log `[BENCHMARK]`).

**Fluxo completo do `SendMessage` (`Chat.razor.cs:213`):**
1. **Guardas** — se `_currentMessage` estiver vazio ou já em pensamento (`_isThinking`), retorna; inicia o `Stopwatch`.
2. **UI + persistência** — adiciona o balão "Tu", persiste a mensagem do utilizador (`PersistUserMessageAsync`: cria a conversa se `_conversationId == 0`, título = primeiros 60 chars), limpa o input (`_inputKey++` remonta o componente), liga `_isThinking` e a **barra de contexto**, faz auto-scroll; adiciona o balão "Ollama" com placeholder `...`; **timer de 150 ms** mostra o tempo decorrido em tempo real até ao 1.º token.
3. **Contexto** — se ainda não calculado, `GetContextLengthAsync(ModelName)` (secção 7 acima); estima os tokens do novo prompt (`text.Length / 4`) e soma-os à barra de contexto.
4. **Preparação** — `ChatComposerService.Prepare` (lê as instruções de sistema; `BuildHistory` omite vazios, `...` e a saudação "Olá!", funde mensagens consecutivas do mesmo papel e remove a mensagem final de "assistant"; **trim a 60%** — `HistoryBudgetFraction`; se a última mensagem não for de "user" aborta). O `PreparedChatRequest` devolve `History`, `Temperature`, `MaxTokens` e o `Payload` (`think` incluído).
5. **Pedido** — serializa o payload e faz `POST api/chat` com `HttpCompletionOption.ResponseHeadersRead` (cliente nomeado `"Ollama"` via `IHttpClientFactory`, timeout 10 min).
6. **Streaming NDJSON** — `StreamReader.ReadLineAsync`; por linha, `JsonDocument.Parse` e extrai `message.content` (formato chat) ou `response` (formato generate); concatena em `aiMessage.Text`; captura o reasoning (`reasoning_content` ou `thinking`, e o reasoning da mensagem final `done`) para `aiMessage.Reasoning`; atualiza a barra de contexto por estimativa; auto-scroll **condicional** (`chatScroll.scrollDuringStream` só cola quando a distância ao fundo é <120px).
7. **Métricas finais** — quando `done == true`, desserializa `OllamaMetrics` (`LoadDuration`, `EvalDuration`, `PromptEvalCount + EvalCount`) e mostra o total real de tokens na barra.
8. **`finally`** — para o stopwatch e o timer; formata o tempo final (2 casas <10 s, 1 casa acima); grava o **benchmark** se `evalCount > 0 && evalDurationNs > 0` (cria o prompt via `CreatePromptAsync` se `_currentPromptId == 0`, grava `BenchmarkResponse` via `CreateResponseAsync`; senão descarta com log `[BENCHMARK]`); **extrai o SUMMARY** da resposta do modelo via `ChatComposerService.ExtractSummaryFromResponse()` (procura o marcador `SUMMARY:` na primeira linha; se ausente, usa `SmartFallbackDescription()` que extrai a primeira frase significativa — até ao primeiro `.` ou quebra de linha — truncada a 50 carateres no limite de palavra); persiste a resposta do assistente (`PersistAssistantMessageAsync`, com `Reasoning`, temperatura e tempo decorrido) e faz `TouchConversationAsync`; **auto-save imediato** — guarda automaticamente a conversa (prompt + resposta) na BD sem diálogos de confirmação, usando o SUMMARY (ou fallback inteligente) como descrição do prompt; `_isThinking = false` e liberta o `CancellationTokenSource`.
9. **Erros** — `OperationCanceledException` → `⏱️ O tempo de resposta expirou.` (se nada recebeu) ou sufixo `*(Cancelado)*`; outras exceções → `❌ Erro inesperado: <mensagem>` (gravadas nos logs).

**Cancelamento e limpeza:**
- **`CancelRequest`** — cancela o `CancellationTokenSource` e, em background, envia `POST /api/generate` com `keep_alive = 0` para **libertar a GPU imediatamente**.
- **`NewChat`** — cancela o CTS, limpa as mensagens, repõe `_conversationId = 0` (a próxima mensagem abre uma conversa nova), faz o mesmo unload (`keep_alive = 0`) e mostra "Olá! Como posso ajudar-te hoje?".
- **`Dispose`** — cancela/liberta o CTS e o `_dotNetRef` ao sair da página.

---

## 8. Base de dados (SQLite — `ollama_benchmark.db`)

Sem migrações clássicas — tabelas criadas de forma lazily (Dapper / sink Serilog), com **migração idempotente em código** no arranque: `DatabaseSchemaInitializer.EnsureSchema(IDapperContext)` (invocado em `Program.cs` após `builder.Build()`) executa `CREATE TABLE IF NOT EXISTS` para as 7 tabelas e depois migrações incrementais de colunas via `PRAGMA table_info` — renomeia `ChatGpt*` → `OpenRouter*` (a avaliação passou a ser feita via OpenRouter) e adiciona `GeminiRecommendation`/`OpenRouterRecommendation`, as colunas das novas métricas `LanguageConsistency`/`LoopDetection`, `IdiomaSessao` e a origem dos juízes `Juiz1Origem`/`Juiz2Origem` (ver 4.3). As ligações SQLite são abertas com **`PRAGMA foreign_keys = ON` e `busy_timeout = 5000`** (`DapperContext`), pelo que o `ON DELETE CASCADE` declarado no DDL é respeitado em runtime. Os **handlers globais do Dapper** (`Services/Helpers/SqliteTypeHandlers.cs`, registados em `Program.cs` com `SqliteTypeHandlers.Register()`) convertem os `INTEGER` do SQLite (ex.: 4) em `double`/`float`/nullable sem `InvalidCastException` — o SQLite guarda números como INTEGER mesmo em colunas REAL/NUMERIC (cobertos pelos testes `SqliteTypeHandlersTests`).

| Tabela | Conteúdo (colunas principais) |
|---|---|
| `Prompts` | `Id`, `Descricao`, `TextoPrompt`, `DataCriacao`, `Temperatura` |
| `Respostas` | `Id`, `PromptId` (FK), `NomeModelo`, `TextoResposta`, `TokensPorSegundo`, `TempoPuroMs`, `TempoCargaMs`, `TamanhoTokens`, `TempoProcessamento`, `IdiomaSessao`, ratings/feedbacks Gemini (`GeminiRating`, `GeminiFactualRating`, `GeminiFormattingRating`, `GeminiComplianceRating`, `GeminiRelevanceRating`, `GeminiToneRating`, `GeminiConcisenessRating`, `GeminiClarityRating`, `GeminiReadabilityRating`, `GeminiHaloEffectRating`, `GeminiSafetyRating`, `GeminiLanguageConsistencyRating`, `GeminiLoopDetectionRating`, `GeminiFeedback`, `GeminiRecommendation`), `Juiz1Origem`/`Juiz2Origem` (`gemini` / `openrouter` / `externo` / NULL) e equivalentes OpenRouter (incluindo `OpenRouterRecommendation`); as colunas de recomendação, das novas métricas, de `IdiomaSessao` e da origem dos juízes são adicionadas pela migração `DatabaseSchemaInitializer` |
| `Conversas` | `Id`, `Titulo` (primeiros 60 chars do 1.º prompt), `NomeModelo`, `DataCriacao`, `DataUltimaAtividade` |
| `ConversaMensagens` | `Id`, `ConversationId` (FK, `ON DELETE CASCADE`), `Role` (user/assistant), `Content`, `Reasoning`, `Temperature`, `ElapsedTime`, `Timestamp` |
| `Configuracoes` | `Chave` (PK) / `Valor` — definições gravadas pela página Settings (`ApiKeys:Gemini`, `ApiKeys:OpenRouter`, `AutomatedJudge:OpenRouterModel`, `Chat:EnableReasoning`) |
| `ModelosJuiz` | `Id`, `Nome` (UNIQUE) / `DataCriacao` — tabela **legada** de modelos `:free` do OpenRouter, **sem uso pela UI** desde que o Settings passou a usar o catálogo ao vivo (`OpenRouterCatalogService`); continua a ser semeada no primeiro arranque se vazia |
| `Logs` | criada pelo Serilog sink (`Id`, `Timestamp`, `Level`, `Exception`, `RenderedMessage`, `Properties`) |

- `DeletePromptAsync` / `DeleteAllPromptsAsync` dependem de `ON DELETE CASCADE` (declarado no DDL das `Respostas` e `ConversaMensagens` e activo pelo `PRAGMA foreign_keys = ON` que o `DapperContext` aplica a cada ligação).
- `DeleteSpecificResponseAsync` apaga a resposta e, se for a última do prompt, também o prompt (transação).
- `DeleteResponsesAsync` apaga várias respostas numa transação única e, no fim, remove os prompts afetados que ficaram sem qualquer resposta (órfãos).
- Path: `OllamaArena/ollama_benchmark.db` (connection string `SqliteConnection`, resolvida contra `ContentRootPath`).
- DDL completo das **sete** tabelas (`Prompts`, `Respostas`, `Conversas`, `ConversaMensagens`, `Configuracoes`, `ModelosJuiz`, `Logs`) disponível em `Docs/schema.sql` — o mesmo conjunto que `DatabaseSchemaInitializer` cria no arranque; executável numa nova base de dados antes da primeira utilização.

---

## 9. Persistência no browser (localStorage / sessionStorage)

| Chave | Conteúdo | Onde |
|---|---|---|
| `ollamaModels` | Lista de nomes de modelos | Chat, Home, Modelos |
| `ollamaModelsFullV2` | `ModelDetails` com metadados (`ContextLength`, `TrainingYear`, `SupportsThinking`) | Home, Modelos |
| `ollama_model` | Modelo selecionado no chat | Chat |
| `ollama_history` | Histórico de execuções (helper `window.ollamaHistory`, chat.js) | Chat (helper definido; sem chamadores no C# atualmente — as conversas são persistidas em SQLite) |
| `theme` | Tema FluentUI (`FluentDesignTheme StorageName`) | Global |
| `sessionStorage: ollamaCacheCleared` | Flag de limpeza da cache uma vez por sessão | Home |

As **definições dos juízes** (chaves Gemini/OpenRouter, modelo do juiz OpenRouter) **não** usam localStorage — ficam na tabela SQLite `Configuracoes`, editáveis pela página Settings (`/settings`), com prioridade **BD → configuração → default** no `AutomatedJudgeService` (a temperatura dos juízes é **fixa em 0** — determinística — e não é configurável). O botão **Guardar configuração dos juízes** só fica ativo quando há alterações (dirty state) e abre um diálogo de confirmação com o resumo do que vai mudar (a chave nunca é mostrada — só o estado "definida/alterada" ou "removida"). A **escolha do modelo do juiz OpenRouter** é feita por **radio buttons** a partir do **catálogo público do OpenRouter** (`OpenRouterCatalogService`, `GET /api/v1/models`, só gratuitos; `openrouter/free` sempre fixo no topo; pesquisa; campo "ou escreve um ID à mão" para offline/custom). A tabela legada `ModelosJuiz` deixou de ser usada pela UI (mantida por compatibilidade). A página Settings inclui ainda o toggle **Ativar reasoning (think)** (FluentSwitch), que grava `Chat:EnableReasoning` em `Configuracoes` (default **false**) e controla se o payload `/api/chat` envia `think: true`/`false` a modelos com capacidade `thinking` (ver secção 3).

### 9.1 Camada de JS interop (`wwwroot/js/`)

| Ficheiro | Objetos expostos | Uso |
|---|---|---|
| `chat.js` | `window.ollamaHistory` — `addEntry`/`getHistory`/`clear` sobre `localStorage "ollama_history"` | Helper definido mas **sem chamadores** no C# atualmente |
| | `window.chatInput` — `attachHandlers`/`detachHandlers` (Enter → envia via `invokeMethodAsync('OnEnterPressedFromJs')`; Shift+Enter → nova linha no caret; auto-resize; `resetHeight` com `requestAnimationFrame` ×3) | Input do chat (`chatInputRef`) |
| | `window.chatScroll` — `getScrollState`, `isAtBottom` (threshold 80px), `scrollToBottom` (duplo `requestAnimationFrame`), `scrollDuringStream` (auto-scroll "sticky" a <120px do fundo) | Auto-scroll do chat (stream e salto ao fundo) |
| `graficos.js` | `window.benchmarkCharts` — `renderGrafico` (cores do tema via CSS vars `--colorNeutralForeground1`/`--colorNeutralStroke2` com fallbacks, `Chart.defaults`, **destrói o gráfico anterior** em `window["_"+canvasId]`, tooltip `Valor: X <unidade>`, eixo Y com sufixo; suporta `bar` (padrão) e `radar` — escala 0–5, legenda por modelo; devolve `bool` para retry quando o canvas ainda não está montado) e `downloadGrafico` (`canvas.toDataURL("image/png")` + link) | Gráficos do `BenchmarkChartDialog` (tabs Desempenho/Qualidade) e `Benchmarks` |
| `excel.js` | `window.downloadFileFromBase64` (data URI base64 `.xlsx`) | Export Excel (`BenchmarkEvaluations.ExportarParaExcel`) |
| `pdf.js` | `window.gerarRelatorioPDF` (relatório PDF agrupado por prompt com desempenho, métricas, feedbacks e recomendações dos juízes) — usa os **vendors locais** `js/vendor/jspdf.min.js` + `js/vendor/jspdf.plugin.autotable.min.js` (sem CDN) e **embebe as fontes** `wwwroot/fonts/Inter-{Regular,Bold}.ttf` (texto) e `RobotoMono-Regular.ttf` (prompt), carregadas por `fetch` com `document.baseURI` e cacheadas em base64; se o carregamento falhar, degrada para `helvetica`/`courier` sem falhar a exportação. O texto é saneado antes de ser desenhado (NFC, espaços exóticos → espaço, setas/✓/† → ASCII, invisíveis removidos, caracteres fora do subset removidos) e os parágrafos são **paginados linha a linha**. A proveniência e a licença das fontes (SIL OFL 1.1) estão em `wwwroot/fonts/NOTICE.md` + `OFL.txt` | Export PDF (`BenchmarkEvaluations.ExportarParaPdfAsync`) |
| `clipboard.js` | `window.copyToClipboard` (`navigator.clipboard` com fallback para textarea + `execCommand('copy')`) | Botão de copiar avaliação (`Benchmarks.razor.cs`) |
| `conversations.js` | `window.conversations` — `downloadJson(fileName, jsonString)` (Blob + `<a download>` + `URL.revokeObjectURL`) e `pickJsonFile()` (input de ficheiro oculto, `FileReader` → Promise) | Exportar/importar conversas em JSON no `HistoryPanel` |
| `markdown-anchors.js` | **nenhum objeto global** (IIFE sem interop) | Intercepta cliques em `a[href^="#"]` na fase de *capture*, resolve o destino por `getElementById` e depois por comparação normalizada (NFD, sem diacríticos, minúsculas), e faz `scrollIntoView({ behavior: "smooth" })` com `preventDefault()` + `stopPropagation()` — o `stopPropagation` é o que impede o Blazor de tratar a âncora como navegação SPA e colapsar o conteúdo. `href="#"` faz scroll ao topo. Usado por `/plano-de-testes`, `/comparar-modelos` e pelo `ReadmePreview` |
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
- `OllamaOptions` (`Configure<OllamaOptions>`, secção `Ollama` do `appsettings.json`), `IDapperContext`, `IOllamaGpuService`, `IBenchmarkCommandRepository`, `IBenchmarkQueryRepository` e `IBenchmarkStatsRepository` (Scoped), `ISettingsRepository` (Scoped), `IModelosJuizRepository` (Scoped), `ILogRepository` (Scoped), `IConversationRepository` (Scoped)
- `PromptFilesService`, `ChatComposerService`, `EvaluatePromptTemplate`, `MarkdownRenderer`, `LocalAnalysisService`, `AutomatedJudgeService`, `ApiCredentialsResolver` (Scoped; chaves/modelos dos provedores de IA), `InternetConnectivityService`, `ReadMeService`
- Clientes HTTP: `IAnalysisService/LocalAnalysisService`, `InternetConnectivityService`, `ReadMeService`, `ITranslationService/TranslationService` (**timeout 4 min**, base de `Ollama:BaseUrl`, apenas para o fallback local da tradução), o cliente nomeado **`"Ollama"`** (**timeout 10 min**, base de `Ollama:BaseUrl`) usado no streaming do chat, e os clientes nomeados **`"Gemini"`** / **`"OpenRouter"`** (**timeout 120 s** cada) usados pelos juízes automáticos e pela tradução.
- Arranque: `SqliteTypeHandlers.Register()` (handlers Dapper de conversão numérica, antes da primeira query) e `DatabaseSchemaInitializer.EnsureSchema` (schema das 7 tabelas).
- Configuração: `appsettings.Local.json` opcional (não versionado) para overrides locais — incluindo `Security:EnableHttpsRedirection` (`false` para deploys HTTP-only, ver secção 12); chaves de API dos juízes **recomendadas via página Settings** (BD, tabela `Configuracoes`), com fallback para **user-secrets** (`ApiKeys:Gemini` / `ApiKeys:OpenRouter`) ou `appsettings.Local.json`.
- Localização: PT (`pt`) por defeito + `en`; cookie `RequestCultureProvider`. A **tradução de feedbacks**, a **análise local de resultados** e as **respostas do chat** (token `{{TARGET_LANGUAGE}}` no `system-prompt.txt`, substituído por `TargetLanguageResolver.GetTargetLanguage()`) seguem a língua da sessão.
- **Aviso local de idioma**: após cada resposta do chat (e ao carregar histórico) e nos cartões de resposta dos Benchmarks, o `ResponseLanguageChecker` deteta heuristicamente se o texto é PT ou EN (stopwords + diacríticos; ignora blocos de código e URLs; textos curtos/empatados são inconclusivos). Se conflituar com o idioma da sessão, mostra um badge ⚠ "Resposta possivelmente fora do idioma selecionado" — apenas informativo, não altera métricas nem grava nada na BD.

**Requisitos de runtime:** `ollama serve` ativo na base configurada (`Ollama:BaseUrl`); projeto de testes `OllamaArena.Tests` com **257 testes de unidade** em **20 classes** (ver nota de build na secção 1) — `dotnet test OllamaArena.slnx`; validação = build + testes + execução manual.

---

## 12. Deploy em IIS (GitHub Actions)

O repositório inclui um workflow GitHub Actions (`.github/workflows/deploy-iis.yml`) que publica a aplicação e faz o deploy para um **self-hosted runner** registado apenas na máquina do autor (a app fica em `http://localhost:4800`):

- **Triggers:** `push` para `master` ou `workflow_dispatch`.
- **Publicação:** `dotnet publish OllamaArena.csproj -c Release` (SDK .NET 10).
- **Deploy:** para `C:\inetpub\wwwroot\OllamaArena` (site) + app pool `OllamaArena` (configuráveis via secrets `IIS_SITE_PATH`/`IIS_APP_POOL`/`SITE_PORT`).
- **Sem interrupção de dados:** o app pool é parado antes da substituição dos ficheiros (evita DLLs bloqueadas) e re-iniciado no fim; a BD `ollama_benchmark.db` (e `-shm`/`-wal`/`-journal`) e o `appsettings.Local.json` são **preservados** entre deploys.
- **HTTP-only:** o workflow garante `Security:EnableHttpsRedirection = false` no `appsettings.Local.json` do site (sem certificado não há redirect/HSTS).
- **Smoke test:** após o arranque, valida `HTTP 200` em `http://localhost:4800`.

Em máquinas sem o runner, o workflow é irrelevante — a app corre localmente com `dotnet watch run` (ver README), sem tokens ou credenciais.
