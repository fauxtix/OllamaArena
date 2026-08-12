# AGENTS.md

Blazor Server (.NET 10) app — a local Ollama chat + LLM benchmark lab using FluentUI Blazor v4.14.2, Dapper, SQLite, Serilog, OllamaSharp. UI text and code comments are in Portuguese (PT). Single app project + a unit-test project; no lint/format config.

## Build status (important)

The solution builds as a **single app project** (`OllamaArena/`) plus a **test project** (`OllamaArena.Tests/`, xUnit). The root `Services/` folder (a stale, abandoned refactor with broken namespaces `MediaOrganizerApp.WebApi.Services`) was **removed**; the slnx only references `OllamaArena/` and `OllamaArena.Tests/`. If a `Services/Services.csproj` ever reappears, do not "fix" it — the live service layer is `OllamaArena/Services/` (namespaces `OllamaArena.Services.*`), wired up in `Program.cs`.

## Commands

- Run: `dotnet watch run` from `OllamaArena/` (launch profile `https`).
- Build: `dotnet build OllamaArena.slnx` (solution uses the new XML `.slnx` format).
- Test: `dotnet test OllamaArena.slnx` (47 cases / 43 methods; no test count guarantees, so don't hardcode a number).
- CI: `.github/workflows/deploy-iis.yml` publishes Release and deploys to the author's self-hosted IIS runner (port 4501); not relevant on other machines.
- Verification: successful build + passing tests + manual run.

## Runtime requirements

- Ollama daemon must be running (`ollama serve`); the app hardcodes `http://localhost:11434` (chat, translation, and keep-alive unload requests in `Chat.razor.cs`).
- No migrations: the SQLite schema is created lazily. DB lives at `OllamaArena/ollama_benchmark.db` (path from `appsettings.json` `SqliteConnection`, resolved against `ContentRootPath`). Serilog's SQLite sink writes to the same DB/table `Logs`.

## State & persistence gotchas

- `ollama_benchmark.db` is **not tracked** (gitignored along with `-shm`/`-wal`/`-journal` and `*.sqbpro`). The schema is created lazily on first run, so a fresh clone starts with an empty DB.
- Model list, selected model, theme, and settings persist in browser **localStorage**, not the DB: keys `ollamaModels`, `ollama_model`, `theme`. New models only appear after `ollama pull <tag>` + adding them on the Settings page.

## Prompts

- `Prompts/*.txt` are `Content` files copied to output (`CopyToOutputDirectory=Always`); editable at runtime via the "EditPromptFiles" page. `PromptTemplates/` holds C# prompt builders.
- `Models/DTO` vs `Models/Entities`: DTOs are API shapes; Entities map to SQLite tables via Dapper.

## Conventions

- Deterministic LLM tuning: temperature ~0.3, `repeat_penalty` 1.1 (see `Chat.razor.cs` `ChatMeasureTemperature`).
- Blazor: buttons inside clickable cards need `@onclick:stopPropagation` (per README) to avoid cascading events.
