# AGENTS.md

Blazor Server (.NET 10) app — a local Ollama chat + LLM benchmark lab using FluentUI Blazor v4.14.2, Dapper, SQLite, Serilog, OllamaSharp. UI text and code comments are in Portuguese (PT). Single project; no tests, no CI, no lint/format config.

## Build status (important)

The solution does NOT build at HEAD. `dotnet build` fails with ~25 errors, all in the root `Services/` project:

- `Services/Services.csproj` (referenced from `OllamaFluentUIChat.csproj:47`) is a **stale, abandoned refactor** — broken namespaces (`MediaOrganizerApp.WebApi.Services`, references to nonexistent `Services.Models.*`). `Services/Backend.csproj` is an orphan (referenced by nothing).
- The **live** service layer is `OllamaFluentUIChat/Services/` (namespaces `OllamaFluentUIChat.Services.*`), wired up in `Program.cs`.
- There are duplicate types in both locations (`DapperContext`, `ConversationRepository`). The in-app ones are the real ones — do not "fix" the root `Services/` project thinking it's active.

If you need a buildable tree, remove the `<ProjectReference>` to `..\Services\Services.csproj` (and consider deleting the root `Services/` folder).

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
