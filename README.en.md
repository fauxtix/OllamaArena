# 🦙 Ollama FluentUI Chat & Benchmark Laboratory

![Ollama FluentUI Chat & Benchmark Laboratory](assets/readme-banner-en.svg)

![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white) ![Blazor Server](https://img.shields.io/badge/Blazor-Server-512BD4?logo=blazor&logoColor=white) ![FluentUI Blazor v4](https://img.shields.io/badge/FluentUI_Blazor-v4.14.2-0078D4?logo=fluentui&logoColor=white) ![SQLite + Dapper](https://img.shields.io/badge/SQLite-Dapper-003B57?logo=sqlite&logoColor=white) ![Ollama](https://img.shields.io/badge/Ollama-Local_LLMs-7499FF?logo=ollama&logoColor=white) ![PT | EN](https://img.shields.io/badge/Lang-PT%20%7C%20EN-00897B) ![PRs Welcome](https://img.shields.io/badge/PRs-welcome-2ea44f)

> **PT:** [Ler este documento em português](README.md)

Your personal **100% local** artificial intelligence hub. Chat with AI models running on your own computer, benchmark each one's performance and find out which responds fastest and with the best quality — all through a modern, fluid interface available in Portuguese and English.

This application is more than a simple chat: it's an **experimentation laboratory** that helps you choose, compare and fine-tune the AI models already installed on your machine, without relying on external services or an internet connection.

---

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

## 💬 Advanced Chat Interface

A modern and comfortable conversation experience:

- **Real-time responses (streaming)** — the text appears on screen word by word, as the model generates it;
- **Smart formatting** — headings, lists, tables and code blocks are rendered cleanly and legibly;
- **Conversation history** — saves and reopens your previous conversations at any time;
- **New chat with one click** — starts a conversation from scratch instantly, freeing up computer resources;
- **Built-in stopwatch** — each response shows how long it took, to monitor performance;
- **Cancel at any time** — interrupt a response with a simple button;
- **Graphics compatibility indicator** — the app warns you whether the model fits in your graphics card memory or will run slower on the CPU;
- **Automatically adjusted behaviour** — the app detects the type of request (creative, factual, translation) and automatically fine-tunes the model for the best result in each situation.

## 🤖 AI Model Management

No hassle, everything at the click of a button:

- **Instant model switching** — switch AI models mid-conversation, directly from the chat;
- **Loaded Models page** — view, at a glance, all the models installed on your computer, with useful information about each one: family, size (parameters), quantization level, training year, maximum supported context, disk space used and compatibility with your graphics card;
- **Centralized settings** — set the default model and control which models are available for use, all on the Settings page;
- **Preferences saved in the browser** — your favourite model is remembered between sessions.

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

Navigation is simple and intuitive. The side menu shows every section of the app: **Chat**, **Benchmarks**, **Quality and Metrics**, **Loaded Models**, **Prompts** and **Logs**.

- **Chat**: open the Chat, type your message in the text box and press <kbd>Enter</kbd> or the send button. Responses appear in real time and each one shows how long it took. Use **History** to resume previous conversations and **New Chat** to start over.
- **Light/Dark Theme**: switch between light and dark themes whenever you prefer. Your choice is **saved in the browser** and restored automatically on your next visit.
- **Settings**: go to the Settings page to manage your AI models (set the default model and control which are available) and customize the app's appearance.

Start by sending a message in the Chat — the app handles everything else.
