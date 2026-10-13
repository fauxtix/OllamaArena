# How to Compare Local Models

 **Methodology guide to getting the most out of the benchmarking laboratory** - Ollama Chat & Benchmark Laboratory

---

 The purpose of this guide is to define a repeatable workflow for answering the question _"which is the best model for each topic?"_ (history, coding, translation, short stories, jokes...) and deciding which models are worth keeping loaded in Ollama for almost exclusive day-to-day use.

## 1. What the application measures

 The architecture is based on two independent axes:

 - **Speed** (`BenchmarkResponse`): tokens per second, model loading time, pure generation time, and response size in tokens.
- **Quality** (`BenchmarkEvaluation`): 12 metrics => Factual, Formatting, Compliance, Relevance, Tone, Conciseness, Clarity, Readability, Halo Effect, Safety, Language Consistency, and Loop Detection; plus an overall score, evaluated by two independent judges (Gemini and OpenRouter).

 There is also a third key mechanism: **automatic temperature adjustment** (`ChatMeasureTemperature`). The application classifies each prompt and automatically decides the temperature: `0.10/0.30` for factual requests (history, coding, translation) and `0.65/0.90` for creative tasks (short stories, jokes, roleplay). In other words, the app already answers the question _"is this factual or creative?"_ for us.

### 1.1 The 12 quality metrics (1 to 5 scale)

 Each judge evaluates the response against **12 independent criteria** (1 = poor / 5 = excellent), plus the **Overall** score:

 | # | Metric | What it evaluates |
| --- | --- | --- |
| 1 | **Factual** | Truthfulness, logical reasoning, and depth of information, considering knowledge up to the model's training cutoff year |
| 2 | **Formatting** | Markdown correctness, structure, compliance with word limits, non-truncated response |
| 3 | **Compliance** | Compliance with the prompt's explicit constraints (positive/negative) |
| 4 | **Relevance** | Directly and exclusively addresses the user's intent, without parallel topics |
| 5 | **Tone** | Appropriateness, professionalism, and alignment of style with what is expected |
| 6 | **Conciseness** | Efficiency of expression, without unnecessary digressions, repetition, or verbosity |
| 7 | **Clarity** | Ease of understanding, logical flow, and absence of ambiguity |
| 8 | **Readability** | Reading structure: sentences, paragraphs, and visual scannability |
| 9 | **Halo Effect** | Bias control: one score should not drag independent metrics up or down (weight 0 in the final score) |
| 10 | **Safety** | Guardrails: absence of hate, dangerous content, or harmful advice; refusal of malicious requests; defensive responses in legitimate contexts |
| 11 | **Language Consistency** | Strict adherence to the prompt's language, without switching languages midway |
| 12 | **Loop Detection** | Semantic health: penalizes loops, repeated sentences, and circular arguments |

The **Overall** score weights the 12 metrics (**Factual** has the highest weight, 20). Each judge's **recommendation** is primarily based on factual performance, but includes a **safety veto** (`SAFETY_SCORE ? 2`), alerts for critical failures (truncation, language, compliance, loops), a reference to the weakest metric, and **refusal** rules (correct refusals are rewarded for malicious requests; unjustified refusals are penalized for benign prompts).

## 2. Recommended methodology (5 steps)

### Step 1 - Define a prompt set for each topic

 The most common mistake is comparing answers to different questions. To make models comparable, each one must answer **the same questions**. Create 3 to 5 prompts per topic, designed to trigger different temperatures:

 | Topic | Example prompt (expected temperature) |
| --- | --- |
| Factual / history | "Explain what caused the fall of the Republic in Portugal in 1926" (\~0.10.0.20) |
| How-to / coding | "Write a C# function that calculates standard deviation" (\~0.10.0.20, code detection) |
| Translation | "Translate into English: ..." (\~0.24) |
| Creative / joke | "Tell me a programmer joke" (\~0.78) |
| Creative / short story | "Write a 200-word short story about an astronaut cat" (\~0.78�0.90) |

### Step 2 - Load the candidate set into Ollama

 - Run `ollama pull <model>` for the candidates. Installed models automatically appear in the chat selector; the **Settings** page (`/settings`) is not where models are added — there you only configure the **AI judges** (Gemini/OpenRouter keys, OpenRouter judge model) and the **reasoning toggle**.
- Include **the same family in different sizes** (e.g. `qwen3:4b`, `qwen3:8b`, `qwen3:14b`); the application's premise is that a smaller model may outperform a larger one on a specific topic.

### Step 3 - Run the benchmark

 For each model, answer **the same questions**, selecting the 'New Chat' option and changing the model in the chat selector. Each response is recorded in the database with its speed metrics, grouped by the same question (same `PromptId`).

### Step 4 - Evaluate each response

- Use **"Evaluate Automatically"** on the **Quality and Metrics** page (`/benchmarks`) - it submits the audit prompt to both judges (Gemini and OpenRouter), with critical context so that the model is not penalized for lacking post-training knowledge.
- If preferred, use **"Copy Prompt"** and paste the result manually. The two judge labels always show the **true origin** of each evaluation (automatic, external pasted by you, or "no response"), so never assume a filled-in field came from the API.
- Evaluate **all** responses in the benchmark, not just one.

### Step 5 - Draw conclusions in three places

 - **Benchmarks** (`/benchmark-evaluations`): consolidated table with filters, Excel export, and automatic local analysis.
- **Quality & Metrics** (`/benchmarks`): side-by-side comparison by question, with **per-prompt charts** and **Performance** tab (execution/load times, speed, and tokens, shown as bar charts) and **Quality** tab (**per-judge radar charts** with the 12 metrics on a 0 to 5 scale, showing each model's complete profile at a glance); all with image download.
- **Automatic analysis** (`LocalAnalysisService`): identifies the fastest model, the highest-rated model, the consensus between judges, and warns if all models score below 4.0/5.

## 3. How to read the results

 1. **Never look only at the final score.** The value lies in separating **Factual Score vs. Formatting Score**: a small model may have `Factual=5, Formatting=2` (brilliant at logic, weak at Markdown), while a large model may show the opposite. This is the signal to use when choosing a model by topic.
2. **Consensus between judges.** If Gemini and OpenRouter diverge significantly on `FactualRating`, be cautious about the isolated result (the app flags "divergence"). If both agree on 5, that is a strong signal.
3. **Cross-reference speed and quality.** For almost exclusive day-to-day use, the fastest model that maintains quality  4 for _your_ type of prompt is preferable to the highest-rated model if it takes 3x longer.
4. **VRAM compatibility** (GPU badge in the chat): determines whether the candidate runs on your GPU or CPU. A model that "fits on the GPU" is always the practical winner.
5. **Low overall quality warning.** If _no_ model exceeds 4/5 for a topic, the correct decision is to change family or size, not to accept the least bad option.
6. **Low Factual score = hallucination signal.** There is no dedicated hallucination metric: responses containing fabricated claims tend to receive a low `Factual` score. However, judges evaluate only against the **prompt + training year**, without external verification (no web search/ground truth) / internal contradictions and clearly false facts can be detected, while plausible but fabricated details (citations, statistics, URLs) may pass. Reinforce this with judge consensus (item 2): if both judges agree on a low `Factual` score, it is a strong signal.
7. **The radar chart is more informative than a single score.** In the per-judge radar chart (Quality tab), a "square" and balanced profile (all metrics high) is preferable to one with peaks and valleys; a spiky profile points to specific weaknesses (e.g. formatting or reasoning). The radar complements the weighted ranking average; use it to decide between models with similar final scores.

## 4. Topic Model Profile Map

 | Intended use | Metrics to weight most | What model to look for |
| --- | --- | --- |
| History / factual questions | Factual, Relevance, Speed | Small�medium solid factual model (e.g. phi4-mini, qwen3 4:8b) |
| Coding / instructions | Factual, Compliance, Formatting | Medium model with strong instruction following |
| Translation | Factual, Clarity | Small model is usually enough (low temperature) |
| Short stories / jokes / creative | Tone, Creativity, Formatting | Larger model or writing-focused model (temperature 0.65/0.90) |

**Expected outcome:** 1 "general-purpose" model (wins on the topics you use 80% of the time) + 1 backup "creative specialist". There is no need to keep 8 models loaded.

## 5. Current notes and limitations

 - The fixes associated with this guide ensure that grouping by question (`PromptId`) works in the Quality & Metrics grid, and that local analysis does not crash when there are no factual evaluations yet.
- Evaluation is **per response**, either manually or by cloud judges (limited to \~60 seconds between calls to protect free-tier limits).
- **Temperature is automatic**: to test the same model in creative vs. factual mode, the difference must come from the prompt itself -> this is how the application is designed.
- Chat conversation history is **persisted in the database** (SQLite, `Conversas`/`ConversaMensagens` tables) and can be **exported/imported as JSON** from the Chat History panel; evaluations and benchmarks also live in the same database.
