# 🦙 Ollama Chat & Benchmark Laboratory

![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white) ![Blazor Server](https://img.shields.io/badge/Blazor-Server-512BD4?logo=blazor&logoColor=white) ![FluentUI Blazor v4](https://img.shields.io/badge/FluentUI_Blazor-v4.14.2-0078D4?logo=fluentui&logoColor=white) ![SQLite + Dapper](https://img.shields.io/badge/SQLite-Dapper-003B57?logo=sqlite&logoColor=white) ![Ollama](https://img.shields.io/badge/Ollama-Local_LLMs-7499FF?logo=ollama&logoColor=white) ![PT | EN](https://img.shields.io/badge/Lang-PT%20%7C%20EN-00897B) ![PRs Welcome](https://img.shields.io/badge/PRs-welcome-2ea44f)

> **PT:** [Ler este documento em português](README.md)

Your personal **100% local** artificial intelligence hub. Chat with AI models running on your own computer, benchmark each one's performance and find out which responds fastest and with the best quality — all through a modern, fluid interface available in Portuguese and English.

This application is more than a simple chat: it's an **experimentation laboratory** that helps you choose, compare and fine-tune the AI models already installed on your machine, without relying on external services or an internet connection.

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
4. Open `https://localhost:7175` (or `http://localhost:5292`). On first start, the SQLite database is created automatically.

### Optional configuration
- **AI judges (keys + model + temperature):** the simplest way (and the only one that works on any deployment) is the **Settings** page (`/settings`), in the **"AI Judges"** section — paste the Gemini/OpenRouter keys, pick the OpenRouter judge model from a **radio-button list** fed by the **public catalog** (free models only; `openrouter/free` is always the default and stays pinned at the top) — or type an ID manually for offline/custom cases — and adjust the temperature. The active choice is stored in the `Configuracoes` table (SQLite); everything applies without restarting.
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
The repository includes a GitHub Actions workflow (`deploy-iis.yml`) that publishes the app and deploys it to a **self-hosted runner registered only on the author's machine** (the app runs on `http://localhost:4501`). On any other machine, ignore the workflow and run locally with `dotnet watch run` — no token or credential is needed to clone and test.

#### API keys on IIS
**Recommended:** after deployment, use the **Settings** page → "AI Judges" to paste the keys — they are stored in the DB and work without editing files. Alternatively (or as a fallback), configure the keys in `appsettings.Local.json` on the server (e.g. `C:\inetpub\wwwroot\OllamaArena\appsettings.Local.json`):

```json
{
  "Security": { "EnableHttpsRedirection": false },
  "ApiKeys": { "Gemini": "<key>", "OpenRouter": "<key>" }
}
```

This file is **preserved by the workflow on every deploy** (it is excluded from the publish output and from the site cleanup) and is re-read at runtime (`reloadOnChange`), without restarting the site. **Keep the `Security` block** — the workflow writes it automatically on each deploy. Alternative: environment variables on the IIS App Pool (`ApiKeys__Gemini` / `ApiKeys__OpenRouter`, the `__` maps to `:`), which also survive redeploys.

### 📚 Technical documentation (the `Docs` folder)
The **`OllamaArena/Docs/`** folder gathers the project's technical documentation in Markdown — you can browse it in the repository or, after cloning, directly in your file explorer:

| File | Content |
|---|---|
| `Funcionalidades.md` | Full technical documentation (stack, pages, services, DB, configuration, deployment) |
| `Guia_Testes.md` | Manual validation checklist (build + unit tests + feature smoke tests) |
| `Guia_Como_Comparar_Modelos.md` | How to use the lab to compare models fairly |
| `Guia_Apresentacao.md` | How to generate the `OllamaArena.pptx` presentation from the screenshots in `OllamaArena/Screenshots/` |
| `Relatorio_Analise.md` | Code analysis report (strengths, bugs, technical debt) |
| `schema.sql` | Reference DDL (5 tables; the runtime creates 7 — adding `Configuracoes` and `ModelosJuiz` on startup) |

### 📸 Screenshots and presentation
The **`OllamaArena/Screenshots/`** folder holds screenshots of the main pages and the **`OllamaArena.pptx`** (16:9) presentation that organizes them into a deck with a **professional IT template** (dark blue background, Fluent accent), **modern transitions** and **advance by click or after 10 s**. To regenerate (e.g. after adding captures), run:

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

With this data collected automatically, you can:

- Browse the **complete history of all tests** in an organized table, with search and filters (All, Pending Review, Reviewed);
- **Assess quality with two AI judges** — rate your models' responses from 1 to 5 across 12 criteria (factuality, formatting, clarity, safety, language consistency, loop detection, etc.) using the **Gemini** and **OpenRouter** judges, also saving each judge's **recommendation**;
- **Analyse results 100% locally** — the app produces a smart summary with conclusions and performance recommendations (pure C# analysis, no cloud dependency);
- **Export results to Excel**, with reports grouped by question, ready to share;
- **Compare each question graphically** — performance charts (speed, timings, tokens) and **quality radar charts per judge** (Gemini/OpenRouter, 12 metrics, 0–5 scale), with image download;
- Compare the responses of **several models side by side** for the same question, in the Quality and Metrics panel;
- Manage your records with **selective deletion** — delete individual tests, only the **pending review**/**reviewed** ones, the **filtered/searched** ones, or the entire history.

## ⚖️ Evaluation by the Two Judges (Gemini & OpenRouter)

The app integrates a structured external audit process in the **Benchmarks** panel to evaluate and score the responses given by the local Ollama models, using **Gemini** and **OpenRouter** (a router of free models, e.g. `openrouter/free`) as quality judges — no need to open browser tabs or copy/paste manually:

1. **Evaluate Automatically:** The user accesses the response of a specific model and clicks the **"Evaluate automatically"** button. The app first checks the internet connection (the judges are cloud services) and then internally generates the audit prompt and submits it to both judges; without internet, it warns the user and suggests the manual process via **"Copy prompt"**.
2. **Critical Context Injected:** The generated prompt automatically includes smart metadata (such as the local model's training year) under the `[CRITICAL CONTEXT]` marker, instructing the external judge not to penalize the model for lacking knowledge of future events.
3. **Direct API Calls:** The app sends the prompt to both judges **in parallel** — **Gemini** (`gemini-flash-latest`) and **OpenRouter** (default `openrouter/free`; the model and the keys are configurable on the **Settings** page and persisted in the DB — if the chosen model returns 404, the app automatically falls back to `openrouter/free`). A minimum interval between evaluations (configurable via `AutomatedJudge:MinIntervalSeconds`, 60s by default) protects the free-tier API limits; the judge temperature is **fixed at 0** (deterministic).
4. **Evaluation Screen Opens Pre-filled:** As soon as the judges reply, the evaluation screen opens **already filled in** with the metrics (1-5 scale), the *Global* score, the feedback and each judge's **RECOMMENDATION** — ready to review and save.
5. **Partial Failure Handling:** If one (or both) judges cannot respond — e.g. `Too many requests`, heavy traffic, network or invalid key — a warning identifies which judge(s) failed and their fields remain editable for manual pasting. The user has **three options**: (1) **wait** and retry the automatic evaluation later; (2) **use the answer from the judge that responded** and fill in only the other judge's fields manually; or (3) **do the entire evaluation manually** — the **"Copy prompt"** button remains available to obtain the evaluation prompt without needing internet.
6. **Translation and Consolidation:** The user can use the **"Translate Feedback"** option to view a read-only preview of the analyses translated into the language currently active in the interface (Portuguese or English) — the translated text is informational only and does not modify the feedback field. Finally, click **"Save evaluation"** to persist all data permanently in the Database; the button is disabled once the record has already been saved (read-only).

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

The **Overall** score weighs the 12 metrics (**Factual** has the highest weight, 20) and each judge's **recommendation** is based on the factual score.

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
- **Conversation history** — saves and reopens your previous conversations at any time, with **JSON export/import** for backups or migrating between machines;
- **New chat with one click** — starts a conversation from scratch instantly, freeing up computer resources;
- **Built-in stopwatch** — each response shows how long it took, to monitor performance;
- **Cancel at any time** — interrupt a response;
- **Graphics compatibility indicator** — the app warns you whether the model fits in your graphics card memory or will run slower on the CPU;
- **Context bar** — a visual indicator shows the tokens used and remaining from the model's context, with a warning as it approaches the limit;
- **Visible reasoning** — reasoning models (e.g. `deepseek-r1`, `qwq`) show a collapsed "Thinking" block with the chain of thought, also saved in the conversation;
- **Web search** — the toggle enables internet search (Wikipedia + DuckDuckGo) to give the model fresh external context before answering;
- **Automatically adjusted behaviour** — the app detects the type of request (creative, factual, translation) and automatically fine-tunes the model for the best result in each situation.

## 🤖 AI Model Management

- **Instant model switching** — switch AI models mid-conversation, directly from the chat;
- **Loaded Models page** — view all the models installed on your computer, with information about each one: family, size (parameters), quantization level, training year, maximum supported context, disk space used and compatibility with your graphics card;

## 📝 Prompt Management and Editing

Adjust the "character" and guidelines of your AI assistant without leaving the app:

- **Built-in editor** — open, edit and save the guideline files (prompts) that steer the AI's behaviour;
- **Immediate application** — changes take effect simply and transparently on next use;
- **Total control** — refine the tone, style and rules of the responses to tailor the AI to your needs, including the system instructions that shape every conversation;
- **Editing safety** — save or cancel your changes whenever you want, without accidental changes.

## 🌐 Automatic Translation and Utilities

Save time on repetitive tasks:

- **Automatic translation** — instantly translate texts and reviews into the language currently active in the interface (Portuguese or English) with one click, using your local model;
- **Automatic text optimization** — the assistant helps rewrite, summarize and improve content on request;
- **Smart intent detection** — the app recognizes when you're asking for a translation or a creative task and adjusts the model's behaviour accordingly, ensuring better results without any manual configuration.

---

## 🧭 User Experience

Navigation is simple. The side menu shows every section of the app: **Chat**, **Benchmarks**, **Quality and Metrics**, **Dashboard**, **Loaded Models**, **Prompts**, **Logs** and **Settings**.

- **Fully responsive**: the interface adapts automatically to any screen size. On phones (≤768px), the side menu becomes a slide-in drawer opened from the hamburger icon in the header; dialogs, data tables, the chat and the benchmark panel splitter adjust to ensure a great experience on any device.
- **Chat**: open the Chat, type your message in the text box and press <kbd>Enter</kbd> or the send button. Responses appear in real time and each one shows how long it took. Use **History** to resume previous conversations and **New Chat** to start over.
- **Automatic Judge Evaluation:** For each response obtained, click **"Evaluate automatically"** — the app submits the audit prompt to Gemini and OpenRouter, opens the evaluation screen already filled in and, after your review, saves the verdicts in the DB. If you prefer to evaluate manually, use **"Copy prompt"** to place the audit prompt on the clipboard.
- **Light/Dark Theme**: switch between light and dark themes whenever you prefer. Your choice is **saved in the browser** and restored automatically on your next visit.
- **Settings**: go to the Settings page (`/settings`) to configure the **AI judges** — Gemini/OpenRouter API keys, the OpenRouter judge model (picked from a radio-button list with the free OpenRouter catalog) and the reasoning/think toggle for models that support it (persisted in the DB).

Start by sending a message in the Chat — the app handles everything else.

## ℹ️ A note on navigation

The repository is public and open to the community. Feel free to clone it, learn from the integration architecture and contribute to optimizing decision-making in the local ecosystem.
