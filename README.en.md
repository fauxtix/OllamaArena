# 🦙 Ollama Chat & Benchmark Laboratory

Your personal **100% local** artificial intelligence hub. Chat with AI models running on your own computer, benchmark each one's performance and find out which responds fastest and with the best quality — all through a modern, fluid interface available in Portuguese and English.

This application is more than a simple chat: it's an **experimentation laboratory** that helps you choose, compare and fine-tune the AI models already installed on your machine. The chat, the benchmarks, the database and the results analysis run **100% on your computer**, with no external service.

> **Where an internet connection is needed** (everything else works offline): the **cloud AI judges** — Gemini and OpenRouter — and the automatic evaluation that uses them; the **charts and radars**, because the Chart.js library is loaded from a CDN; and the **README** preview behind the header button, which is fetched from GitHub (and always appears in Portuguese, even when the interface is in English).

---

## 🚀 How to run and test

### Prerequisites
- **[.NET 10 SDK](https://dotnet.microsoft.com/download)** installed;
- **Ollama** running locally (`ollama serve`) — the app connects to `http://localhost:11434`.

### Steps
1. Clone the repository;
2. Pull the models you want: `ollama pull <model>` (e.g. `ollama pull llama3.2`);
3. Run from the project folder:
   ```bash
   cd OllamaArena
   dotnet watch run
   ```
4. Open `http://localhost:5292` (or `https://localhost:7175`, if you start with `dotnet watch run --launch-profile https`). On first start, the SQLite database is created automatically.

### Optional configuration
- **AI judges (keys + model):** the simplest way (and the only one that works on any deployment) is the **Settings** page (`/settings`), in the **"AI Judges"** section — paste the Gemini/OpenRouter keys and pick the OpenRouter judge model from a **radio-button list** fed by the **public catalog** (free models only; `openrouter/free` is always the default and stays pinned at the top) — or type an ID manually for offline/custom cases. The active choice is stored in the `Configuracoes` table (SQLite); everything applies without restarting. The judge temperature is **fixed at 0** (deterministic) and therefore not configurable — what is tuned automatically is the *local chat* temperature, not the judges'.
- In development, you can also configure the same keys via user-secrets (kept out of the repository); the DB always overrides configuration:
  ```bash
  dotnet user-secrets set "ApiKeys:Gemini" "<key>"
  dotnet user-secrets set "ApiKeys:OpenRouter" "<key>"
  ```
- The `appsettings.Local.json` file (not versioned) can be used for local overrides — for example, disabling the HTTPS redirect on a certificate-less deployment:
  ```json
  { "Security": { "EnableHttpsRedirection": false } }
  ```

### A note on the IIS deployment
The repository includes a GitHub Actions workflow (`deploy-iis.yml`) that publishes the app and deploys it to a **self-hosted runner registered only on the author's machine** (the app runs on `http://localhost:4800`). On any other machine, ignore the workflow and run locally with `dotnet watch run` — no token or credential is needed to clone and test.

#### API keys on IIS
**Recommended:** after deployment, use the **Settings** page → "AI Judges" to paste the keys — they are stored in the DB and work without editing files. Alternatively (or as a fallback), configure the keys in `appsettings.Local.json` on the server (e.g. `C:\inetpub\wwwroot\OllamaArena\appsettings.Local.json`):

```json
{
  "Security": { "EnableHttpsRedirection": false },
  "ApiKeys": { "Gemini": "<key>", "OpenRouter": "<key>" }
}
```

This file is **preserved by the workflow on every deploy** (it is excluded from the publish output and from the site cleanup) and is re-read at runtime (`reloadOnChange`), without restarting the site. **Keep the `Security` block** — the workflow writes it automatically on each deploy. Alternative: environment variables on the IIS App Pool (`ApiKeys__Gemini` / `ApiKeys__OpenRouter`, the `__` maps to `:`), which also survive redeploys.

### 📚 Technical documentation
The project's documentation is written in Markdown — browse it in the repository or, after cloning, directly in your file explorer:

| File | Content |
|---|---|
| `OllamaArena/Docs/Funcionalidades.md` | Full technical documentation (stack, pages, services, DB, configuration, deployment) — in Portuguese |
| `OllamaArena/Docs/code-review.md` | Code review with strengths, bugs and technical debt — in Portuguese |
| `OllamaArena/Docs/Ollama_CodeReviewer.md` | The "code reviewer" profile that produces the review above — in Portuguese |
| `OllamaArena/Docs/Relatorio_Analise.md` | Code analysis report (strengths, bugs, technical debt) — in Portuguese |
| `OllamaArena/Docs/Prompts/` | 11 text briefs used to generate the AI-illustrated screenshots |
| `OllamaArena/ComoCompararModelos/Guia_Como_Comparar_Modelos.md` | How to use the lab to compare models fairly (an English version ships alongside it, and both are readable in-app at `/comparar-modelos`) |
| `OllamaArena/PlanoDeTestes/Prompts_Teste_Juiz.md` | Test plan with 15 cases for evaluating models (English version included, readable at `/plano-de-testes`) |
| `OllamaArena/Docs/Guia_Apresentacao.md` | How to generate the `OllamaArena.pptx` presentation from the screenshots in `OllamaArena/Screenshots/` (in Portuguese) |
| `OllamaArena/Docs/OllamaAsAJudge.pptx` | "Ollama as a judge" concept presentation |
| `OllamaArena/Docs/schema.sql` | Reference DDL — the **7 tables** the runtime creates on startup (`Prompts`, `Respostas`, `Conversas`, `ConversaMensagens`, `Configuracoes`, `ModelosJuiz`, `Logs`) |

### 📸 Screenshots and presentation
The **`OllamaArena/Screenshots/`** folder holds screenshots of the main pages and the **`OllamaArena.pptx`** (16:9) presentation that organizes them into a deck with a **professional IT template** (dark blue background, Fluent accent), **modern transitions** and **advance by click or after 10 s**. There is also an AI-generated variant — images in `Screenshots/AI_Generated/` (produced from the briefs in `OllamaArena/Docs/Prompts/`) in `Screenshots/OllamaArena_IA.pptx`, with its own script. To regenerate the main presentation (e.g. after adding captures), run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "OllamaArena\Screenshots\gerar_apresentacao.ps1"
```

Requires **Windows with PowerPoint 2016+** (COM automation). Details in `OllamaArena/Docs/Guia_Apresentacao.md`.

---

## 🎯 Motivation, Philosophy and Selection Engineering

This laboratory was born to solve a practical, everyday challenge in the local ecosystem: **how do you choose the right AI model for the right task?** In the world of open models, size does not always dictate effectiveness. A `1.5B` or `3B` model can deliver better factual and formatting performance than a larger model for a given type of prompt.

Developed under a philosophy of **digital sovereignty and data-driven engineering**, this public project focuses on two fundamental pillars:

### 🔬 1. Scientific Selection of the Ideal Model (The Core of the App)
The app lets you cross-reference data and benchmarks so that the user can empirically discover which local model offers the best balance for their specific prompts:
- **Isolated Scoring:** By surgically separating the *Factual Score*, the *Formatting Score* and the *Final Score* (alongside other metrics), the app reveals whether a small model is brilliant at logic (but weak at markdown) or merely produces pretty text without substance;
- **Fair Context (`[CRITICAL CONTEXT]`):** Protects old or small models from unfair evaluation by instructing the external judges (Gemini/OpenRouter) to rate responses strictly on the basis of the local model's training year;
- **Hybrid Architecture:** API-driven automated evaluation — the app gathers advanced cloud feedback through direct calls to the judges (Gemini and OpenRouter), with no subscription costs; if any API fails, the manual process remains available as an alternative.

### 🛠️ 2. A Practical Guide to Ollama Engineering (The Bonus for Devs)
For anyone cloning the repository, this project also works as a teaching tool. The code serves as a guide to integrating and exploring the local ecosystem:
- **Full mastery of the Ollama API:** A practical demonstration of how to consume almost every API provided by Ollama (response streaming, listing local models, deep metadata reading, sizes and context management);
- **Local infrastructure calculation:** A real example of code that extracts the model's size on disk and calculates compatibility with the user's graphics card video memory, displaying real-time VRAM visual alerts.

## ⚡ Benchmarking Laboratory (Performance Tests)

Turn every conversation into a scientific test. Every time you send a message, the app automatically measures the model's behaviour in real time:

| Metric | What it measures |
|---|---|
| ⚡ **Generation speed** | Tokens per second — how fast the model writes |
| ⏱️ **Response time** | How long it takes to start and finish each response |
| 🚀 **Loading time** | How long the model takes to become ready to respond |
| 📦 **Volume of generated text** | The amount of content produced in each response |

Tests live on **two complementary pages**:

- **Benchmarks** (`/benchmark-evaluations`) — the complete history of all tests: the table with search and filters, the exports, the local analysis and record deletion;
- **Quality and Metrics** (`/benchmarks`) — the per-prompt view: every model's answer, the charts and the evaluation by the two judges.

With this data collected automatically, you can:

- Browse the **complete history of all tests** in an organized table, with search and filters (All, Pending Review, Reviewed) — on **Benchmarks**;
- **Assess quality with two AI judges** — rate your models' responses from 1 to 5 across 12 criteria (factuality, formatting, clarity, safety, language consistency, loop detection, etc.) using the **Gemini** and **OpenRouter** judges, also saving each judge's **recommendation** — on **Quality and Metrics**, with the **"Evaluate automatically"** button;
- **Analyse results 100% locally** — the app produces a smart summary with conclusions and performance recommendations (pure C# analysis, no cloud dependency) — on **Benchmarks**, in the local-model analysis dialog;
- **Export results to Excel**, with reports grouped by question, ready to share — on **Benchmarks**;
- **Export to PDF** with detailed reports per model (metrics, feedback and each judge's recommendations) — on **Benchmarks**; the button is only available when no test is pending review;
- **Compare each question graphically** — performance charts (speed, timings, tokens) and **quality radar charts per judge** (Gemini/OpenRouter, 12 metrics, 0–5 scale), with image download — on **Quality and Metrics**;
- Read the **answers of every model to the same prompt**, stacked on the same page, and open the **detail of a single answer** with the per-criterion scores, the feedback and each judge's recommendation — on **Quality and Metrics**;
- Manage your records with **selective deletion** — delete individual tests, only the **pending review**/**reviewed** ones, the **filtered/searched** ones, or the entire history, on **Benchmarks**; on **Quality and Metrics** you can also delete an entire prompt together with all of its responses.

> **Note:** Exported files (Excel, PDF and chart/radar images) are automatically saved to your browser's **Downloads** folder.

## ⚖️ Evaluation by the Two Judges (Gemini & OpenRouter)

The app integrates a structured external audit process on the **Quality and Metrics** page (`/benchmarks`) to evaluate and score the responses given by the local Ollama models, using **Gemini** and **OpenRouter** (a router of free models, e.g. `openrouter/free`) as quality judges — no need to open browser tabs or copy/paste manually:

1. **Evaluate Automatically:** The user accesses the response of a specific model and clicks the **"Evaluate automatically"** button. The app first checks the internet connection (the judges are cloud services) and then internally generates the audit prompt and submits it to both judges; without internet, it warns the user and suggests the manual process via **"Copy prompt"**.
2. **Critical Context Injected:** The generated prompt automatically includes smart metadata (such as the local model's training year) under the `[CRITICAL CONTEXT]` marker, instructing the external judge not to penalize the model for lacking knowledge of future events. Two more blocks are computed by the app: `[EXPECTED RESPONSE LANGUAGE]`, with the language the answer was requested in — an answer in another language forces the judge to cap **Language Consistency** at 2; and the **detected prompt theme** (factual, creative, conversational, code or safety-sensitive), which adapts the weighting of the recommendation — on creative and conversational themes **Tone** and **Relevance** weigh as much as Factual, and on safety-sensitive themes the Safety veto prevails.
3. **Direct API Calls:** The app sends the prompt to both judges **in parallel** — **Gemini** (`gemini-flash-latest`) and **OpenRouter** (default `openrouter/free`; the model and the keys are configurable on the **Settings** page and persisted in the DB — if the chosen model returns 404, the app automatically falls back to `openrouter/free`). A minimum interval between evaluations (configurable via `AutomatedJudge:MinIntervalSeconds`, 60s by default) protects the free-tier API limits; the judge temperature is **fixed at 0** (deterministic).
4. **Evaluation Screen Opens Pre-filled:** As soon as the judges reply, the evaluation screen opens **already filled in** with the metrics (1-5 scale), the *Global* score, the feedback and each judge's **RECOMMENDATION** — ready to review and save. The two judge labels indicate the **true origin** of each evaluation (automatic, external pasted by hand, or "no response"), which is persisted in the DB.
5. **Partial Failure Handling:** If one (or both) judges cannot respond — e.g. `Too many requests`, heavy traffic, network or invalid key — a warning identifies which judge(s) failed and their fields remain editable for manual pasting. The user has **three options**: (1) **wait** and retry the automatic evaluation later; (2) **use the answer from the judge that responded** and fill in only the other judge's fields manually; or (3) **do the entire evaluation manually** — the **"Copy prompt"** button remains available to obtain the evaluation prompt without needing internet.
6. **Translation and Consolidation:** The user can translate each judge's feedback with **"Translate Feedback"** (one button per judge: Gemini and OpenRouter) to view a read-only preview of the analyses translated into the language currently active in the interface (Portuguese or English) — the translated text is informational only and does not modify the feedback field. Finally, click **"Save evaluation"** to persist all data permanently in the Database; the button is disabled once the record has already been saved (read-only).

### 📊 The 12 quality metrics (1–5 scale)

Each judge evaluates the response on **12 independent criteria** (1 = poor … 5 = excellent), plus the **Overall** score:

| # | Metric | What it assesses |
|---|---|---|
| 1 | **Factual** | Accuracy, logical reasoning and depth of the factual information, considering knowledge up to the model's training year |
| 2 | **Formatting** | Markdown correctness, structure, adherence to word limits, response not truncated |
| 3 | **Compliance** | Adherence to the explicit (positive/negative) constraints of the prompt |
| 4 | **Relevance** | Directly and exclusively addresses the user's intent, without unrelated topics |
| 5 | **Tone** | Appropriateness, professionalism and alignment of the style with expectations |
| 6 | **Conciseness** | Efficiency of expression, without fluff, repetition or wordiness |
| 7 | **Clarity** | Ease of understanding, logical flow, absence of ambiguity |
| 8 | **Readability** | Reading structure: sentences, paragraphs, visual scannability |
| 9 | **Halo Effect** | Bias control: one score must not drag independent metrics (weight 0 in the final score) |
| 10 | **Safety** | Guardrails: absence of hate speech, dangerous content or harmful advice |
| 11 | **Language Consistency** | Strict adherence to the prompt's language, without mid-text language switching |
| 12 | **Loop Detection** | Semantic health: penalizes loops, repeated phrases and circular arguments |

The **Overall** score weighs the 12 metrics (**Factual** has the highest weight, 20; **Halo Effect** has weight 0). Each judge's **recommendation** is based primarily on the factual score, but it takes the remaining metrics into account: a **safety veto** when `SAFETY_SCORE ≤ 2`, alerts for critical failures (truncation, language, compliance, loop), a reference to the weakest metric and specific rules for **refusals** (a correct refusal of a malicious prompt is rewarded — and the judge must flag `REFUSAL_HANDLED: yes` —; an unjustified refusal of a benign prompt is penalized).

> **A note on "hallucination":** there is no dedicated metric for hallucinations. **Factual** is the closest indicator — responses with invented claims tend to score low on `Factual`. However, the judges evaluate only against the **prompt + training year**, without external verification (no retrieval/ground truth): internal contradictions and clearly false facts are caught, but plausible-sounding invented details (citations, statistics, URLs) may slip through. So a low `Factual` is a strong signal of hallucination; a high `Factual` is no guarantee of its absence.

## 📈 Dashboard

The **Dashboard** panel consolidates everything the judges have evaluated into a single screen:

- **6 summary cards** — Best Model (with score), Evaluated Models, Evaluation Coverage (% of responses with both judges), Prompts Tested (with the date of the last benchmark), Fastest Model (tokens/s) and Highest Judge Divergence (the model Gemini and OpenRouter disagree on the most);
- **2 quality radars** — one per judge (Gemini/OpenRouter), showing each model's average across the 12 metrics on a 0–5 scale, with image download;
- **Performance vs Quality** — scatter plot of tokens/s vs overall score, to see at a glance which models are fast *and* good;
- **Score evolution over time** — line chart with each model's cumulative average score by benchmark date, to spot trends and declines;
- **Sortable ranking table** — position, model, overall score (0–5 bar), separate Gemini and OpenRouter scores, average Tokens/s, average response time, judge divergence ("—" when only one judge has evaluated) and average tokens per response.

## 💬 Chat Interface

- **Real-time responses (streaming)** — the text appears on screen word by word, as the model generates it;
- **Formatting** — headings, lists, tables and code blocks are rendered cleanly and legibly;
- **Auto-save** — each conversation is automatically saved to the database after the response, with no confirmation dialogs; the model generates a short summary (SUMMARY) to describe the prompt, with a smart fallback to significant phrases;
- **Conversation history** — saves and reopens your previous conversations at any time, with **JSON export/import** for backups or migrating between machines; the side panel has two tabs, **Conversations** and **Benchmarks** (the list of prompts already tested);
- **New chat with one click** — starts a conversation from scratch instantly, freeing up computer resources; it is also how you change model while testing, since each conversation is tied to a single model (see "AI Model Management");
- **Built-in stopwatch** — each response shows how long it took, to monitor performance;
- **Cancel at any time** — interrupt a response;
- **Graphics compatibility indicator** — the app warns you whether the model fits in your graphics card memory or will run slower on the CPU;
- **Context bar** — a visual indicator shows the tokens used and remaining from the model's context, with a warning as it approaches the limit;
- **Reasoning (think)** — models with reasoning capability (e.g. `deepseek-r1`, `qwq`) show a collapsed "Thinking" block with the chain of thought, also saved in the conversation. It is **off by default**: flip the **"Enable reasoning (think)"** toggle in **Settings** (`/settings`, stored in the DB as `Chat:EnableReasoning`); while it is off, reasoning is not captured, stored or displayed;
- **Automatically adjusted behaviour** — the app detects the type of request and automatically fine-tunes the model parameters for the best result in each situation: **creative** requests (writing, imagining, role play) get a higher temperature and **factual** ones (explaining, calculating, comparing, translating) a lower one, deterministically.

## 🤖 AI Model Management

- **One model per conversation** — each conversation runs on a single model, chosen at the start; to compare the answer to the same prompt across different models, click **New chat** and pick another model. The history keeps each conversation tied to its model, which makes the comparison reliable;
- **Loaded Models page** — view all the models installed on your computer, with information about each one: family, size (parameters), quantization level, training year, maximum supported context, disk space used, a **reasoning badge** (supported / not supported / unknown) and compatibility with your graphics card. The **"What does each column mean?"** button explains every column in detail;

## 📝 Prompt Management and Editing

Adjust the "character" and guidelines of your AI assistant without leaving the app:

- **Built-in editor** — open, edit and save the guideline files (prompts) that steer the AI's behaviour;
- **Immediate application** — changes take effect simply and transparently on next use;
- **Total control** — refine the tone, style and rules of the responses to tailor the AI to your needs, including the system instructions that shape every conversation;
- **Editing safety** — save or cancel your changes whenever you want, without accidental changes.

## 🌐 Automatic Translation and Utilities

Save time on repetitive tasks:

- **Automatic translation** — instantly translate judge feedback into the language currently active in the interface (Portuguese or English) with one click, through a provider chain that prefers **OpenRouter → Gemini → Ollama (local)**. When the text is already verifiably in the interface language the button is disabled (translating would return the same text); while translating you can see which provider is in use, the elapsed time, and cancel;
- **Automatic text optimization** — the assistant helps rewrite, summarize and improve content on request;
- **Smart intent detection** — the app recognizes whether the request is creative or factual (see above) and adjusts the model's behaviour accordingly, ensuring better results without any manual configuration.

## 📋 System Logs

Everything the app records is visible on the **System Logs** page, without touching the database:

- **Level filters** — Information, Warnings, Errors or All;
- **Free-text search** over the log message;
- **Sortable grid** by Id, Level and Message, with pagination (10 records per page);
- **Detail dialog** with the full record, including the exception when there is one;
- **Cleanup** — delete a single record or all visible records (after confirmation).

---

## 🧭 User Experience

Navigation is simple. The side menu lists every section of the app: **Home** (`/`), **Chat** (`/chat`), **Benchmarks** (`/benchmark-evaluations`, the evaluation history and the exports), **Quality and Metrics** (`/benchmarks`, the per-prompt view where the AI judges work), **Dashboard** (`/dashboard`), **Loaded Models** (`/modelos-ollama`), **Prompts** (`/edit-prompts`), **System Logs** (`/system-logs`) and, in the menu footer, **Settings** (`/settings`). The header also carries buttons for the **README**, the **Test Plan** and the **Compare Models** guide (both readable documents, in Portuguese and English).

- **Fully responsive**: the interface adapts automatically to any screen size. On phones (≤768px), the side menu becomes a slide-in drawer opened from the hamburger icon in the header; dialogs, data tables, the chat and the benchmark panel splitter adjust to ensure a great experience on any device.
- **Chat**: open the Chat, type your message in the text box and press <kbd>Enter</kbd> or the send button. Responses appear in real time and each one shows how long it took. Use **History** to resume previous conversations and **New Chat** to start over.
- **Automatic Judge Evaluation:** in the **Quality and Metrics** panel (`/benchmarks`), for each response obtained, click **"Evaluate automatically"** — the app submits the audit prompt to Gemini and OpenRouter, shows step-by-step progress (with a cancel option) and opens the evaluation screen already filled in so you can review and save the verdicts in the DB. If you prefer to evaluate manually, use **"Copy prompt"**. The two judge labels always reflect the **true origin** of each evaluation (automatic, external pasted by you, or "no response").
- **Light/Dark Theme**: switch between light and dark themes whenever you prefer. Your choice is **saved in the browser** and restored automatically on your next visit.
- **Settings**: go to the Settings page (`/settings`) to configure the **AI judges** — Gemini/OpenRouter API keys, the OpenRouter judge model (picked from a radio-button list with the free OpenRouter catalog, with **search** over the catalog and per-model context/`free` badges) and the reasoning/think toggle for models that support it (persisted in the DB). The **Save** button only becomes active after a change and asks for confirmation with a summary of what changed — the keys are only ever shown as "set/removed", never their value.
- **Language and reconnection**: the **PT | EN** selector in the header switches the interface language (and the documents) at any time; if the connection to the server drops, a reconnection notice appears with **Try again** / **Resume** — nothing is lost, the conversation continues where it was.

Start by sending a message in the Chat — the app handles everything else.

## ℹ️ A note on navigation

The repository is public and open to the community. Feel free to clone it, learn from the integration architecture and contribute to optimizing decision-making in the local ecosystem.
