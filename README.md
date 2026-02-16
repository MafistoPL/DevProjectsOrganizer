# DevProjectsOrganizer

Windows-only project organizer with a fast .NET scanning engine and a web UI hosted in WebView2.

**Tech stack**
- Backend: .NET 10 (`src/Engine`)
- Desktop host: WPF + WebView2 (`src/AppHost`)
- Frontend: Angular + Angular Material (`ui/`)

**Repo layout**
- `src/Engine` — scanning engine, heuristics, data layer
- `src/AppHost` — WPF host that embeds the Angular UI
- `ui` — Angular app (Material UI)

**Prerequisites**
- Windows 10/11
- .NET SDK 10
- Node.js >= 20.19
- npm (bundled with Node)

**Run (development)**
1. Start Angular dev server:
   ```powershell
   cd ui
   npm install
   npm start
   ```
2. Run the WPF host:
   ```powershell
   dotnet run --project src/AppHost/AppHost.csproj
   ```
The app loads `http://localhost:4200/` by default in Debug.

**Run (production-like)**
1. Build Angular:
   ```powershell
   cd ui
   npm install
   npm run build
   ```
2. Run the host:
   ```powershell
   dotnet run --project src/AppHost/AppHost.csproj
   ```
In Release, AppHost tries to load `ui/dist/**/index.html`.

**Override UI URL**
You can point the host to a custom URL:
```powershell
$env:APPHOST_UI_URL = "http://localhost:4200/"
dotnet run --project src/AppHost/AppHost.csproj
```

**Notes**
- WebView2 uses the Edge (Chromium) runtime bundled with Windows.
- Angular Material theme is `indigo-pink` (see `ui/src/styles.scss`).

**Tests**
- Full automated set:
  ```powershell
  dotnet test DevProjectsOrganizer.slnx
  cd ui
  npm run test:pw
  ```
- User-data replay regression (reads real `%APPDATA%\DevProjectsOrganizer\data.db` + historical `scan-*.json`):
  ```powershell
  dotnet test tests/AppHost.IntegrationTests/AppHost.IntegrationTests.csproj --filter "Category=UserDataRegression"
  ```

**Tag Reprocessing (current behavior)**
- `tags.add` only creates a tag entry and reloads tag list; it does not trigger automatic heuristics or AI reprocessing.
- Tag heuristics run only when explicitly triggered by the user (`Run tag heuristics` per project or global `Apply latest heuristics to all projects` in `Tags`).
- Heuristics produce only `AssignExisting` suggestions and only for seeded/system tags (`isSystem=true`) matching supported heuristic signals.
- `projects.runAiTagSuggestions` is a manual per-project action; it generates `AssignExisting` suggestions for custom tags (`isSystem=false`) and persists them as `Pending` with `source=Ai`.
- AI reprocessing is source-scoped: rerun replaces only pending AI suggestions and does not remove pending heuristic suggestions.
- Practical consequence: adding a custom tag does not automatically backfill existing projects.

**IPC Contract Guardrails**
- For each IPC payload change between `ui/` and `src/AppHost`, add contract tests on both layers:
  - FE: service spec must assert the exact payload sent by `bridge.request(...)`.
  - BE: parser/handler test must verify required fields and reject malformed payloads.
- Example regression this prevents:
  - FE sends `projects.delete` with `{ projectId }`, while BE still requires extra `confirmName`.
  - Result: delete action appears broken in GUI even though FE flow looks correct.
