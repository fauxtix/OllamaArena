# AGENTS.md

Blazor Server (.NET 10) app — a local Ollama chat + LLM benchmark lab using FluentUI Blazor v4.14.2, Dapper, SQLite, Serilog, OllamaSharp. UI text and code comments are in Portuguese (PT). Single app project + a unit-test project; no lint/format config.

## Build status (important)

The solution builds as a **single app project** (`OllamaArena/`) plus a **test project** (`OllamaArena.Tests/`, xUnit). The root `Services/` folder (a stale, abandoned refactor with broken namespaces `MediaOrganizerApp.WebApi.Services`) was **removed**; the slnx only references `OllamaArena/` and `OllamaArena.Tests/`. If a `Services/Services.csproj` ever reappears, do not "fix" it — the live service layer is `OllamaArena/Services/` (namespaces `OllamaArena.Services.*`), wired up in `Program.cs`.

## Commands

- Run: `dotnet watch run` from `OllamaArena/` (launch profile `https`).
- Build: `dotnet build OllamaArena.slnx` (solution uses the new XML `.slnx` format).
- Test: `dotnet test OllamaArena.slnx` (93 tests in 8 classes; no test count guarantees, so don't hardcode a number).
- CI: `.github/workflows/deploy-iis.yml` publishes Release and deploys to the author's self-hosted IIS runner (port 4501); not relevant on other machines.
- Verification: successful build + passing tests + manual run.

## Runtime requirements

- Ollama daemon must be running (`ollama serve`); the app hardcodes `http://localhost:11434` (chat, translation, and keep-alive unload requests in `Chat.razor.cs`).
- No migrations: the SQLite schema is created lazily. DB lives at `OllamaArena/ollama_benchmark.db` (path from `appsettings.json` `SqliteConnection`, resolved against `ContentRootPath`). Serilog's SQLite sink writes to the same DB/table `Logs`.

## State & persistence gotchas

- `ollama_benchmark.db` is **not tracked** (gitignored along with `-shm`/`-wal`/`-journal` and `*.sqbpro`). The schema is created lazily on first run, so a fresh clone starts with an empty DB.
- Model list, selected model, and theme persist in browser **localStorage**, not the DB: keys `ollamaModels`, `ollama_model`, `theme`. New models only appear after `ollama pull <tag>` (the `ollamaModels` cache is refreshed by the Chat/Home pages).
- **Auto-save with SUMMARY**: after streaming completes, the model's response is parsed for a `SUMMARY: <3-5 word description>` marker (via `ChatComposerService.ExtractSummaryFromResponse()`). If absent, `SmartFallbackDescription()` extracts the first meaningful sentence (up to first `.` or newline), truncated at word boundary to 50 chars. The conversation is then **auto-saved immediately** (no ConfirmSaveDialog). The `ChatMessage` entity has a `Summary` property.
- **Judge settings** (Gemini/OpenRouter API keys, OpenRouter judge model) are configurable via the **Settings page** and persist in the DB table `Configuracoes` (key/value). Resolution priority in `AutomatedJudgeService`: **DB → configuration (user-secrets/appsettings) → defaults**. Keys can still be provided pre-publish via user-secrets `ApiKeys:Gemini`/`ApiKeys:OpenRouter` as fallback. The Settings save button is dirty-tracked and requires a confirmation dialog (summary of changes; keys are shown only as set/removed, never their value). The judge temperature is **fixed at 0** (deterministic; not configurable).
- The **reasoning (think)** toggle on the Settings page persists as `Chat:EnableReasoning` in `Configuracoes`; default is **false**. `ChatComposerService.Prepare` (async) resolves it itself with priority **DB → `OllamaOptions.EnableReasoning` config → default false** and, for models that support the `thinking` capability (via `/api/show`), sends the `think` value **explicitly** (`true`/`false`) — omitting the field makes Ollama think by default and return reasoning even when disabled; the field is omitted entirely for non-thinking models (Ollama returns 400 "does not support thinking"). When disabled, reasoning is **not captured/persisted/displayed** either (`PreparedChatRequest.EnableReasoning` gates the `Reasoning` capture in `Chat.razor.cs`) — models like `deepseek-r1` always generate thinking and ignore `think: false`.
- The **Settings page** picks the OpenRouter judge model from the **live OpenRouter catalog** (`OpenRouterCatalogService` → `GET https://openrouter.ai/api/v1/models`, free models only) via a **radio-button list** (`openrouter/free` is always pinned at the top), with a manual-ID fallback field for offline/custom IDs. The active model is stored in `Configuracoes` (`AutomatedJudge:OpenRouterModel`). The legacy `ModelosJuiz` table/seed is kept for compatibility but no longer used by the UI.

## Prompts

- `Prompts/*.txt` are `Content` files copied to output (`CopyToOutputDirectory=Always`); editable at runtime via the "EditPromptFiles" page. `PromptTemplates/` holds C# prompt builders.
- `Models/DTO` vs `Models/Entities`: DTOs are API shapes; Entities map to SQLite tables via Dapper.

## Conventions

- Deterministic LLM tuning: temperature ~0.3, `repeat_penalty` 1.1 (see `Chat.razor.cs` `ChatMeasureTemperature`).
- Blazor: buttons inside clickable cards need `@onclick:stopPropagation` (per README) to avoid cascading events.
