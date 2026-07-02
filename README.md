# Ollama FluentUI Chat & Benchmark Laboratory 🚀

Uma aplicação web desenvolvida em **.NET 10 / Blazor** que funciona como um laboratório de testes locais para Modelos de Linguagem Pequenos (SLMs); poderá evoluir para modelos maiores (nº de parâmetros), dependendo da capacidade da sua GPU (VRAM).
A aplicação combina uma interface de chat em tempo real com um painel de telemetria e avaliação de qualidade (*LLM-as-a-Judge*).

---

## 💻 Notas Importantes sobre o Hardware e Desempenho

### Ambiente de Teste Inicial (Máquina Antiga)
Este laboratório foi projetado e testado inicialmente numa **máquina antiga limitada a apenas 1GB de VRAM**. Sob estas restrições estritas, o sistema funciona de forma híbrida: o **Ollama** faz a gestão inteligente da memória, enviando o que não cabe na placa gráfica para processamento direto na memória RAM e processador (CPU) do computador.

### Upgrade Recomendado para Modelos Maiores
Para obter respostas com maior maturidade intelectual e menor índice de alucinações, **devem ser utilizadas placas gráficas (GPUs) modernas e dedicadas**. Um upgrade de hardware permitirá carregar localmente modelos muito mais potentes, que exigem maior capacidade de processamento gráfico para entregar resultados de qualidade superior em tempo útil.

---

## 🧠 Características Principais

### 1. Chat Local em Tempo Real (Modo Offline)
- Interface de chat interativa desenvolvida com **Microsoft FluentUI Blazor Components v4.14.2**.
- Processamento de texto por fluxo de rede (*HTTP streaming / chunked*) de alto desempenho com processamento por blocos (`buffer`).
- Configuração determinista controlada (`temperature: 0.3` / `repeat_penalty: 1.2`), mitigando alucinações ou loops infinitos de texto em modelos de pequena escala.

### 2. Laboratório de Benchmarks & Telemetria
- Captura das métricas oficiais do Ollama quando o fluxo termina (`done = true`).
- Gravação automática de estatísticas críticas na base de dados:
  - **Tokens por Segundo (T/s)**: Desempenho real de geração de texto.
  - **Tempo Puro (Eval Ms)**: Velocidade estrita do processamento de tokens.
  - **Tempo de Carga (Load Ms)**: O tempo que o Ollama demora a carregar/paginar o modelo para a memória.
  - **Tamanho (Count)**: Contagem total de tokens gerados.

### 3. Painel de Análise Master-Detail (Layout Proporcional 1/3 e 2/3)
- **Painel Esquerdo**: Lista cronológica de prompts executados, com informação sobre o nº de modelos usados; opções para visualizar resultados em forma de gráfico e para apagar prompt.
- **Painel Direito**: Cabeçalho fixo com o prompt selecionado e área de scroll independente para os cartões de resposta das IAs lado a lado.

### 4. Avaliação Cruzada (LLM-as-a-Judge)
- Painel integrado para introdução de métricas de qualidade baseadas em modelos de fronteira (**Google Gemini** e **OpenAI ChatGPT**).
- Permite atribuir classificações e justificações de erros/alucinações; os campos de avaliação persistem no SQLite e aparecem nas tabelas de avaliação.
- Persistência via **Dapper** para fechar o ciclo de análise.

---

## 🛠️ Stack Tecnológica

- **Frontend**: Blazor Server (InteractiveServer Mode)
- **Componentes UI**: Microsoft FluentUI Blazor Library v4.14.2
- **Motor Local de IA**: Ollama API (`http://localhost:11434`)
- **Base de Dados**: SQLite
- **Micro-ORM**: Dapper (Mapeamento por reflexão de objetos)
- **Outras libs importantes**: Serilog (sink SQLite), Markdig, Syncfusion.Blazor

---

## API / Integração com Ollama (endpoints usados pela aplicação)

A aplicação comunica com o Ollama local a partir do serviço `OllamaGpuService` (ver `OllamaFluentUIChat/Services/Implementations/Services/OllamaGpuService.cs`). O URL base esperado por padrão é `http://localhost:11434`.

Resumo dos endpoints usados e contratos observados no código:

1) GET /api/tags
- Utilizado por: `GetLocalModelsAsync()` em `OllamaGpuService` e pela página de Settings para obter a lista de modelos.
- Resposta esperada: envelope JSON com lista de modelos, compatível com o DTO `OllamaModels.OllamaResponse`:

  {
    "models": [
      { "name": "phi4-mini:latest", "model": "phi4-mini", "size": 123456789, "digest": "...", "details": { ... } },
      ...
    ]
  }

- Uso na UI: popular dropdowns/selects de modelos disponíveis.

2) GET /api/ps
- Utilizado por: `GetRunningModelsAsync()` em `OllamaGpuService`.
- Propósito: listar os modelos actualmente carregados em memória (RAM/VRAM) e o seu estado.
- Resposta: semelhante a `/api/tags`, mas pode incluir propriedades adicionais como `expires_at` e `size_vram` (visto no DTO `ModelDetails`).

3) POST /api/show
- Utilizado por: `GetModelContextLengthAsync(string modelName)`.
- Payload (JSON): { "name": "<modelo>" }
- Resposta esperada: corresponde ao DTO `OllamaModels.OllamaShowResponse` com, entre outros, `model_info` — um dicionário onde existe uma chave que pode terminar em `.context_length`. O serviço tenta descobrir essa chave para extrair o tamanho do contexto do modelo.

4) POST /api/chat
- Utilizado para: conversas e geração de texto.
- Payload (DTO `OllamaChatPayload`):

  {
    "model": "phi4-mini:latest",
    "messages": [ { "role": "system|user|assistant", "content": "..." }, ... ],
    "stream": true|false,
    "options": { "temperature": 0.2, "num_predict": 800 }
  }

- Modos de resposta:
  - Streaming (stream = true): o Ollama envia uma sequência de objetos JSON, tipicamente um objeto JSON por linha (newline-delimited JSON). O cliente no projeto lê o corpo em modo `ResponseHeadersRead` e faz leitura por blocos, separando por quebras de linha e parseando cada objecto. Cada objecto pode conter:
    - `message.content` (conteúdo incremental)
    - `response` (algumas variantes)
    - um objecto final com `done: true` e métricas como `load_duration`, `eval_duration`, `eval_count`.

    Exemplo de fragmento de streaming (cada linha é um JSON válido):

    { "message": { "content": "Olá" } }
    { "message": { "content": " tudo bem?" } }
    { "done": true, "load_duration": 12345678, "eval_duration": 9876543, "eval_count": 512 }

    Observação importante: no código local (Chat.razor.cs), `load_duration` e `eval_duration` são interpretados como valores em nanosegundos — o código divide por 1_000_000 para obter milissegundos (ms).

  - Não-streaming (stream = false): o Ollama responde com um envelope JSON final, mapeável para `OllamaChatResponse`:

    {
      "model": "phi4-mini:latest",
      "created_at": "2026-...",
      "message": { "content": "Resposta completa do modelo" }
    }

- Consumo no projeto:
  - `Chat.razor.cs` usa leitura por blocos, parse por linha e actualiza o card do chat à medida que chegam os fragmentos.
  - `TranslationService` (e outras rotinas) usam chamadas `stream = false` e desserializam o JSON completo em `OllamaChatResponse`.

5) POST /api/generate (usado em alguns pontos)
- Observado em `Chat.razor.cs` para pedidos de descarregamento/expurgo de modelos da memória: o código envia payloads com `model` e `keep_alive = 0` para forçar libertação de memória.
- Exemplo de payload para descarregar: { "model": "phi4-mini:latest", "keep_alive": 0 }
- Nota: a aplicação também usa `/api/chat` com um payload contendo `keep_alive = 0` como alternativa — ambos os usos aparecem no código.

---

## NavMenu & fluxo de modelos

Esta secção descreve exatamente o que o menu de navegação (NavMenu) mostra e como os modelos do Ollama são carregados e persistidos na aplicação.

O que o NavMenu expõe (rotas principais):
- Home → `/` (página inicial)
- Chat → `/chat` (componente de chat que usa `OllamaChatPayload` e stream)
- Qualidade e Métricas → `/benchmarks` (vista master/detail para prompts e respostas)
- Benchmarks → `/benchmark-evaluations` (tabela de avaliações)
- Modelos → `/settings2` (Settings2: gestão e preferências locais de modelos e prompt juiz)
- Modelos carregados → `/modelos-ollama` (lista dos modelos locais obtidos via Ollama)
- Logs → `/system-logs` (visualizador de logs do Serilog)

**Nota importante:** em algumas versões do código a entrada `/settings2` está comentada no NavMenu; a página de referência para gestão/inspeção de modelos é `/modelos-ollama` (ModelosOlama.razor). Se vês que `settings2` não aparece no teu menu, usa `/modelos-ollama`.

**Nota:** A página `Benchmark Evaluations` (rota `/benchmark-evaluations`) inclui um botão "Exportar Excel" na toolbar que gera um ficheiro .xlsx com prompts agrupados e métricas (Gemini/ChatGPT, Tokens/s, Tempo, Tokens). Este ficheiro Excel pode ser usado como backup externo dos resultados e para análises posteriores em ferramentas como Excel ou Power BI.

Onde os modelos vêm e como circulam na app:
1. A app lê a lista de modelos directamente do Ollama invocando `OllamaGpuService.GetLocalModelsAsync()` (GET `/api/tags`).
   - Código: `OllamaFluentUIChat/Services/Implementations/Services/OllamaGpuService.cs`.
2. Páginas que consomem essa lista:
   - `Settings2.razor` — quando presente/activada, preenche a lista de `Models` e permite ao utilizador adicionar/remover entradas na UI (persistência local).
   - `ModelosOlama.razor` — mostra os modelos detectados no disco, apresenta `ContextLength` (obtido via POST `/api/show`) e calcula compatibilidade GPU. Esta é a página canónica para inspecionar metadados extraídos do ficheiro do modelo.
   - `Chat.razor` — ao carregar, chama `GpuService.GetLocalModelsAsync()` para recuperar detalhes e aferir compatibilidade; o `ModelName` seleccionado é usado para chamadas a `/api/chat`.
3. Persistência local na UI (localStorage):
   - `ollama_models` — lista JSON de modelos adicionados/personalizados pela UI (Settings2).
   - `ollama_model` — modelo actualmente seleccionado (Settings / Chat).
   - `juiz_ai_prompt` — prompt do juiz salvo pelo utilizador (Settings2).
   - `ollama_history` — historial de interacções mantido pelo JS helper (`wwwroot/js/chat.js`).
   Exemplos de leitura/gravação no código: `JS.InvokeAsync<string>("localStorage.getItem", "ollama_models")` e `JS.InvokeVoidAsync("localStorage.setItem", "ollama_model", ModelName)`.
4. Notas operacionais:
   - A app NÃO faz `ollama pull` automaticamente — o utilizador deve executar `ollama pull <model>` localmente para adicionar os ficheiros do modelo ao Ollama.
   - Se um modelo não existir localmente, as chamadas a `/api/chat` irão falhar no Ollama; o README deve instruir o utilizador a usar `ollama list` / `ollama pull`.
   - Para forçar libertação de VRAM, a app envia payloads com `keep_alive = 0` para `/api/generate` (ou usa `/api/chat` com payloads específicos) — isso faz com que o Ollama descarregue o modelo da memória.

Extras: campos adicionais extraídos do Ollama e apresentados na UI (ModelosOlama)
- Para além do nome e do tamanho, o componente `ModelosOlama.razor` exibe metadados adicionais obtidos do endpoint `/api/tags` e `/api/show`:
  - details.family (família do modelo)
  - details.parameter_size (número/descrição de parâmetros)
  - details.quantization_level (nível de quantização)
  - SizeInGB (conversão legível do campo `size` do Ollama)
  - ContextLength (extraído via `/api/show` a partir de model_info, quando disponível)
  - SizeInVram / GpuOffloadPercentage (quando `/api/ps` fornece size_vram; usado para estimar percentagem em VRAM e compatibilidade GPU)

Arquivos relevantes (links):
- NavMenu.razor: `Components/Layout/NavMenu.razor` — lista as rotas do menu.
  https://github.com/fauxtix/OllamaFluentUIChat/blob/master/OllamaFluentUIChat/Components/Layout/NavMenu.razor
- OllamaGpuService: chamada aos endpoints do Ollama.
  https://github.com/fauxtix/OllamaFluentUIChat/blob/master/OllamaFluentUIChat/Services/Implementations/Services/OllamaGpuService.cs
- Modelos página: `Components/Pages/ModelosOlama.razor` — mostra modelos locais, contexto e compatibilidade GPU.
  https://github.com/fauxtix/OllamaFluentUIChat/blob/master/OllamaFluentUIChat/Components/Pages/ModelosOlama.razor
- Settings2: `Components/Pages/Settings2.razor` — gestão de modelos e prompt juiz (quando utilizado).
  https://github.com/fauxtix/OllamaFluentUIChat/blob/master/OllamaFluentUIChat/Components/Pages/Settings2.razor
- Chat: `Components/Pages/Chat.razor.cs` — construções de payload, streaming e persistência de métricas.
  https://github.com/fauxtix/OllamaFluentUIChat/blob/master/OllamaFluentUIChat/Components/Pages/Chat.razor.cs

Recomendações para documentação:
- Acrescentar no README uma breve nota explicando que os modelos visíveis na UI provêm do Ollama local (GET `/api/tags`) e que o utilizador deve correr `ollama pull <model>` caso o modelo não exista.
- Documentar as chaves `localStorage` para facilitar debugging e recuperação de preferências.

---

## Avaliação automática / Prompt do Juiz (Gemini / ChatGPT)

A aplicação é pensada para funcionar offline/localmente; por essa razão a integração automática com serviços externos (OpenAI / Google) NÃO está incluida por defeito. O processo actual assume avaliação manual pelo utilizador usando interfaces externas (p.ex. Gemini ou ChatGPT no browser) e posterior colagem das notas na aplicação.

O projecto inclui um template de prompt (EvaluatePromptTemplate) usado para pedir a um modelo de fronteira que acts como "juiz" e avalie as respostas geradas pelos modelos locais. O prompt força um formato estrito de saída com 3 rankings e uma breve descrição.

Formato exigido pelo prompt do juiz (must):
- FACTUAL_SCORE: [1-5]
- FORMATTING_SCORE: [1-5]
- FINAL_SCORE: [1-5]
- DESCRIPTION: [Breve descrição da avaliação — máximo 3 frases]

Significado das métricas pedidas ao juiz:
- Factual Score: precisão factual, lógica e correção das informações (1 a 5).
- Formatting Score: correção de Markdown, sintaxe de tabelas e estrutura/legibilidade (1 a 5).
- Final Score: avaliação global combinando factualidade e formatação (1 a 5).

Como este prompt é usado na aplicação:
- O utilizador pode copiar o prompt padrão a partir da UI (Settings2) e submetê‑lo em interfaces externas (p.ex. Gemini web UI ou ChatGPT) para obter a avaliação. Depois cola as notas (FACTUAL_SCORE/FORMATTING_SCORE/FINAL_SCORE e DESCRIPTION) nos campos de avaliação da app.
- As colunas `GeminiRating` e `ChatGptRating` (ou campos equivalentes) nas tabelas de avaliação persistem essas notas no SQLite.

---

## Inspecionar `ollama_benchmark.db` (DB Browser for SQLite)

Se precisares apenas de consultar ou exportar resultados para análise, prefira usar a funcionalidade de exportação para Excel integrada na aplicação em vez de apagar a base de dados. A exportação gera um ficheiro .xlsx agrupado por Prompt (nome do ficheiro: Benchmarks_Agrupados_yyyyMMdd_HHmmss.xlsx) que serve como backup portátil e pode ser carregado no Excel / Power BI para análise avançada.

1. Exportar via UI (recomendado — backup)
- Navega para **Benchmarks → Benchmark Evaluations** (`/benchmark-evaluations`) e clica em **Exportar Excel** na toolbar. O ficheiro descarregado contém prompts agrupados e métricas (Gemini/ChatGPT, Tokens/s, Tempo, Tokens).
- Nome do ficheiro: `Benchmarks_Agrupados_{timestamp}.xlsx`.

2. Localização do ficheiro de BD (para leitura/inspeção somente)
- A base de dados SQLite usada pela aplicação encontra‑se em: `OllamaFluentUIChat/ollama_benchmark.db` (ou na pasta de execução da app, conforme `appsettings`).
- Incluímos também um ficheiro de projecto para DB Browser for SQLite: `OllamaFluentUIChat/ollama_benchmark.sqbpro` (abre‑o no DB Browser para ter as vistas/configurações).

3. Abrir o ficheiro com DB Browser for SQLite (opcional)
- Descarrega e instala DB Browser for SQLite: https://sqlitebrowser.org/
- Abra a aplicação e escolha `Open Database` → seleccione `ollama_benchmark.db` no repositório/na pasta do projecto.

4. Consultas úteis (SQL tab)
- Ver prompts:
  SELECT * FROM Prompts ORDER BY Id DESC;
- Ver respostas:
  SELECT * FROM Respostas ORDER BY PromptId DESC, TokensPorSegundo DESC;
- Exportar uma tabela para CSV: clique em `Browse Data`, seleccione a tabela (Prompts ou Respostas) e escolha `Export` → `Table(s) as CSV file`.

5. Aviso sobre remoção de dados
- Recomendamos NÃO apagar diretamente o ficheiro `ollama_benchmark.db` nem executar comandos destrutivos sem efectuar primeiro um backup. A exportação para Excel funciona como o mecanismo de backup preferido — gera um ficheiro legível que pode ser arquivado e analisado fora da aplicação.
- Se, ainda assim, precisares de reiniciar os dados por razões específicas, faz primeiro uma cópia do ficheiro `.db` (ex.: `ollama_benchmark.db.bak`). Apagar a DB ou executar `DELETE FROM Prompts;` é uma operação irreversível e deve ser evitada em fluxos normais de utilização.

---

## Exemplos (curl)

- Chamada não-streaming (ex.: tradução):

```bash
curl -X POST http://localhost:11434/api/chat \
  -H "Content-Type: application/json" \
  -d '{"model":"phi4-mini:latest","messages":[{"role":"user","content":"Traduz isto para PT"}],"stream":false}'
```

- Chamada streaming (ver output linha-a-linha; usar `-N` para manter o output em tempo real):

```bash
curl -N -X POST http://localhost:11434/api/chat \
  -H "Content-Type: application/json" \
  -d '{"model":"phi4-mini:latest","messages":[{"role":"user","content":"Olá"}],"stream":true}'
```

- Forçar descarregamento de modelo (exemplo usado pelo UI):

```bash
curl -X POST http://localhost:11434/api/generate \
  -H "Content-Type: application/json" \
  -d '{"model":"phi4-mini:latest","keep_alive":0}'
```

---

## Observações sobre como o projeto consome a API

- O serviço `OllamaGpuService` encapsula chamadas a `/api/tags`, `/api/ps`, `/api/show` e `/api/chat` via `HttpClient`.
- O componente de UI `Chat.razor` faz `HttpClient.SendAsync(..., HttpCompletionOption.ResponseHeadersRead)` e lê o corpo em fluxo para suportar a experiência token-a-token.
- Ao finalizar um stream, o código tenta extrair métricas (`load_duration`, `eval_duration`, `eval_count`) e as persiste como métricas de benchmark no SQLite (`ollama_benchmark.db`).

---

## 📊 Modelos Utilizados nos Testes

Os seguintes modelos de pequena escala (SLMs) foram escolhidos especificamente para avaliar o comportamento do ecossistema sob cenários de baixa memória e paginação por CPU:
- **`qwen2.5:0.5b`**
- **`llama3.2:1b`**
- **`qwen2.5:1.5b`**
- **`gemma2:2b`**
- **`phi4-mini:latest`**

(Ver `ollama pull <model>` para descarregar os modelos localmente.)

---

## ⚙️ Guia de Configuração do Ambiente

1. Instale o Ollama seguindo https://ollama.com e inicie o daemon local:

```bash
ollama serve
```

2. Restaure e execute a aplicação:

```bash
dotnet restore
dotnet build
dotnet watch run --project OllamaFluentUIChat/OllamaFluentUIChat.csproj
```

3. Abra a URL apresentada no terminal (ex.: https://localhost:5001).

---

## Notas finais e próximos passos

- Documentei as informações adicionais extraídas do Ollama e adicionei nota sobre o template do juiz.
- A exportação para Excel foi destacada como o método preferido de backup/arquivamento dos resultados.
- Posso ainda:
  - Inserir um pequeno aviso no UI de `ModelosOlama.razor` (ex.: "Se o modelo não aparece: execute `ollama pull <model>`") e abrir PR;
  - Implementar um utilitário UI para colar a resposta do juiz e parsear automaticamente os 3 scores antes de gravar (opção offline-only).

Se quiseres que eu aplique alguma dessas alterações adicionais, diz qual e eu procedo.
