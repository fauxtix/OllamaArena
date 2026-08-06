# AGENTS.md

Blazor Server (.NET 10) app — a local Ollama chat + LLM benchmark lab using FluentUI Blazor v4.14.2, Dapper, SQLite, Serilog, OllamaSharp. UI text and code comments are in Portuguese (PT). Single project; no tests, no CI, no lint/format config.

## Build status (important)

The solution builds as a **single project**. The root `Services/` folder (a stale, abandoned refactor with broken namespaces `MediaOrganizerApp.WebApi.Services`) was **removed**; the slnx only references `OllamaFluentUIChat/`. If a `Services/Services.csproj` ever reappears, do not "fix" it — the live service layer is `OllamaFluentUIChat/Services/` (namespaces `OllamaFluentUIChat.Services.*`), wired up in `Program.cs`.

## Commands

- Run: `dotnet watch run` from `OllamaFluentUIChat/` (launch profile `https`).
- Build: `dotnet build OllamaFluentUIChat.slnx` (solution uses the new XML `.slnx` format).
- No test project exists; verification is a successful build + manual run.

## Runtime requirements

- Ollama daemon must be running (`ollama serve`); the app hardcodes `http://localhost:11434` (chat, translation, and keep-alive unload requests in `Chat.razor.cs`).
- No migrations: the SQLite schema is created lazily. DB lives at `OllamaFluentUIChat/ollama_benchmark.db` (path from `appsettings.json` `SqliteConnection`, resolved against `ContentRootPath`). Serilog's SQLite sink writes to the same DB/table `Logs`.

## State & persistence gotchas

- `ollama_benchmark.db` is **tracked in git** (currently shows as modified + deleted `-shm`/`-wal` files). Don't commit DB churn; run with it or revert before committing.
- Model list, selected model, theme, and settings persist in browser **localStorage**, not the DB: keys `ollamaModels`, `ollama_model`, `theme`. New models only appear after `ollama pull <tag>` + adding them on the Settings page.

## Prompts

- `Prompts/*.txt` are `Content` files copied to output (`CopyToOutputDirectory=Always`); editable at runtime via the "EditPromptFiles" page. `PromptTemplates/` holds C# prompt builders.
- `Models/DTO` vs `Models/Entities`: DTOs are API shapes; Entities map to SQLite tables via Dapper.

## Conventions

- Deterministic LLM tuning: temperature ~0.3, `repeat_penalty` 1.1 (see `Chat.razor.cs` `ChatMeasureTemperature`).
- Blazor: buttons inside clickable cards need `@onclick:stopPropagation` (per README) to avoid cascading events.
