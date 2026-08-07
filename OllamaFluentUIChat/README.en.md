# 🦙 Ollama FluentUI Chat & Benchmark Laboratory

![Ollama FluentUI Chat & Benchmark Laboratory](../assets/readme-banner-en.svg)

![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white) ![Blazor Server](https://img.shields.io/badge/Blazor-Server-512BD4?logo=blazor&logoColor=white) ![FluentUI Blazor v4](https://img.shields.io/badge/FluentUI_Blazor-v4.14.2-0078D4?logo=fluentui&logoColor=white) ![SQLite + Dapper](https://img.shields.io/badge/SQLite-Dapper-003B57?logo=sqlite&logoColor=white) ![Ollama](https://img.shields.io/badge/Ollama-Local_LLMs-7499FF?logo=ollama&logoColor=white) ![PT | EN](https://img.shields.io/badge/Lang-PT%20%7C%20EN-00897B) ![PRs Welcome](https://img.shields.io/badge/PRs-welcome-2ea44f)

> **PT:** [Ler este documento em português](README.md)

Your personal **100% local** artificial intelligence hub. Chat with AI models running on your own computer, benchmark each one's performance and find out which responds fastest and with the best quality — all through a modern, fluid interface available in Portuguese and English.

This application is more than a simple chat: it's an **experimentation laboratory** that helps you choose, compare and fine-tune the AI models already installed on your machine, without relying on external services or an internet connection.

---

## 🎯 Motivation, Philosophy and Selection Engineering

This laboratory was born to solve a practical, everyday challenge in the local ecosystem: **how do you choose the right AI model for the right task?** In the world of open models, size does not always dictate effectiveness. A `1.5B` or `3B` model can deliver better factual and formatting performance than a larger model for a given type of prompt.

Developed under a philosophy of **digital sovereignty and data-driven engineering**, this public project focuses on two fundamental pillars:

### 🔬 1. Scientific Selection of the Ideal Model (The Core of the App)
The app lets you cross-reference data and benchmarks so that the user can empirically discover which local model offers the best balance for their specific prompts:
- **Isolated Scoring:** By surgically separating the *Factual Score*, the *Formatting Score* and the *Final Score* (alongside other metrics), the app reveals whether a small model is brilliant at logic (but weak at markdown) or merely produces pretty text without substance;
- **Fair Context (`[CRITICAL CONTEXT]`):** Protects old or small models from unfair evaluation by instructing the external judges (ChatGPT/Gemini) to rate responses strictly on the basis of the local model's training year;
- **Hybrid Architecture:** The user acts as a manual, secure and free bridge to gather advanced feedback from the cloud without spending a single cent on subscriptions or expensive API keys.

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
- **Assess quality with two AI judges** — rate your models' responses from 1 to 5 across 11 criteria (factuality, formatting, clarity, safety, etc.) using the **ChatGPT** and **Gemini** judges, also saving each judge's **recommendation**;
- **Analyse results 100% locally** — the app produces a smart summary with conclusions and performance recommendations (pure C# analysis, no cloud dependency);
- **Export results to Excel**, with reports grouped by question, ready to share;
- Compare the responses of **several models side by side** for the same question, in the Quality and Metrics panel;
- Manage your records, deleting individual tests or the entire history whenever you want.

## ⚖️ Evaluation by the Two Judges (ChatGPT & Gemini)

The app integrates a structured external audit process in the **Quality and Metrics** panel to evaluate and score the responses given by the local Ollama models, using ChatGPT and Gemini as quality judges:

1. **Copy for Evaluation:** The user accesses the response of a specific model and clicks the **"Copy for evaluation"** button. The app internally generates the audit prompt and places it on the clipboard.
2. **Critical Context Injected:** The generated prompt automatically includes smart metadata (such as the local model's training year) under the `[CRITICAL CONTEXT]` marker, instructing the external judge not to penalize the model for lacking knowledge of future events.
3. **Browser Processing:** The user manually opens their web browser, accesses the ChatGPT and Gemini sessions, and presses <kbd>Ctrl</kbd>+<kbd>V</kbd> to submit the prompt to both judges.
4. **Opening the Evaluation Screen:** After obtaining the judges' responses in the browser, the user returns to the app and clicks the **"Evaluation"** button (located next to the judges' counters).
5. **Pasting the Raw Output:** On the evaluation screen, the user pastes the text copied from the browser directly into the **"Feedback (Paste the raw Gemini/ChatGPT output here)"** fields.
6. **Automatic Metrics Extraction:** The system analyses the pasted raw text and automatically fills in the **Evaluation Metrics (1-5 scale)**, including criteria such as *Factual, Formatting, Compliance, Relevance, Tone, Conciseness, Clarity, Readability, Halo Effect, Safety* and the *Global* score; it also extracts each judge's **RECOMMENDATION**, which is stored in the Database and shown highlighted in the evaluation form and in the feedback dialog.
7. **Translation and Consolidation:** The user can use the **"Translate Feedback"** option to convert the analyses into the language currently active in the interface (Portuguese or English) and, finally, click **"Save evaluation"** to persist all data permanently in the Database.

## 💬 Chat Interface

- **Real-time responses (streaming)** — the text appears on screen word by word, as the model generates it;
- **Formatting** — headings, lists, tables and code blocks are rendered cleanly and legibly;
- **Conversation history** — saves and reopens your previous conversations at any time;
- **New chat with one click** — starts a conversation from scratch instantly, freeing up computer resources;
- **Built-in stopwatch** — each response shows how long it took, to monitor performance;
- **Cancel at any time** — interrupt a response;
- **Graphics compatibility indicator** — the app warns you whether the model fits in your graphics card memory or will run slower on the CPU;
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

Navigation is simple. The side menu shows every section of the app: **Chat**, **Benchmarks**, **Quality and Metrics**, **Loaded Models**, **Prompts** and **Logs**.

- **Chat**: open the Chat, type your message in the text box and press <kbd>Enter</kbd> or the send button. Responses appear in real time and each one shows how long it took. Use **History** to resume previous conversations and **New Chat** to start over.
- **Judge Evaluation Process:** For each response obtained, select the **"Copy for evaluation"** option. Two new tabs will open in the browser. Open each one and press <kbd>Ctrl</kbd>+<kbd>V</kbd>. Next, select the **"Evaluation"** option in the app. Copy the first judge's response in the browser and paste it into the app's evaluation screen. Repeat the process for the second judge to save the verdicts in the DB.
- **Light/Dark Theme**: switch between light and dark themes whenever you prefer. Your choice is **saved in the browser** and restored automatically on your next visit.
- **Settings**: go to the Settings page to manage your AI models (set the default model and control which are available) and customize the app's appearance.

Start by sending a message in the Chat — the app handles everything else.

## ℹ️ A note on navigation

The repository is public and open to the community. Feel free to clone it, learn from the integration architecture and contribute to optimizing decision-making in the local ecosystem.
