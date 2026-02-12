# Backlog

## Now (next 2-4 weeks)
- GUI: Completed scan shouldn't have "Stop" button visible. It should have dismiss button or something like this.
- GUI: Active scans current path if dont fit into window it should have horizontal scroll.
- ETA doesn't work
- Root badges in Scan: project count + ongoing suggestions count.
- Scan summary per root (last state, time, files count).
- Queue visibility in Scan UI with `queue reason` (if still incomplete).
- Hide `Stop` button after scan is finished (confirm UX and close).
- Pre-commit: verify setup in docs (`core.hooksPath=.githooks`) for every clone/environment.
- UI: expose action to run/export regression report against historical user decisions.

## Soon (1-3 months)
- Tag suggestions (heuristics first, no AI by default).
- Manual split/merge of detected projects inside a root.
- Better ranking and filtering of suggestions (score, language, age).
- Incremental scan based on `last_modified_at` + cache.
- Configurable heuristics (GUI or YAML) + parser and runtime.
- Align `Engine/Scanning` interfaces with real runtime implementation in AppHost (remove duplicated scan contracts over time).

## Later
- AI for "one project vs many" with bounded context.
- Remote backend + client sync.
- Full-text search over scan metadata.

## To segregate
- Add place to keep PAT to interact with gh, need to figureout how to keep it safely.

## Done (recent)
- UI `Live results` now loads real `ProjectSuggestion` rows from SQLite through IPC (`suggestions.list`).
- `Accept` / `Reject` in UI now persist suggestion status in DB (`suggestions.setStatus`).
- `Debug JSON` action now exports suggestion debug payload in-memory and copies it to clipboard with confirmation bubble.
- `Suggestions` view now supports `Pending` (all scans) and `Archive` (Accepted+Rejected) scopes.
- Added archive export action (`suggestions.exportArchive`) and open export folder action (`suggestions.openArchiveFolder`) in Suggestions UI.
- Marker heuristics for project detection (`.sln`, `.csproj`, `package.json`, `CMakeLists.txt`, `Makefile`, `pom.xml`, `build.gradle`, `.git`) + save suggestions to SQLite.
- `ProjectSuggestion` status moved from free `string` to enum (`Pending`/`Accepted`/`Rejected`).
- EF entity mapping moved to dedicated configuration classes + `ApplyConfigurationsFromAssembly`.
- Heuristics regression analyzer (integration-level) comparing current run vs historical accepted/rejected suggestions per root.
- User-data replay regression test (`Category=UserDataRegression`) reads real `%APPDATA%` DB + historical scan JSON snapshots (no mocks/seeding).
- Historical suggestions persisted per scan (`project_suggestions` includes `RootPath`) for regression and audit.
- Pre-commit script exists in `.githooks/pre-commit` and blocks commit on test failures when `core.hooksPath` is configured.
- Ignore build/IDE artifacts in scan (`bin/`, `obj/`, `.vs/`, `.idea/`, `node_modules/`, `*.pdb`, `*.obj`, `*.tlog`, `*.exe`, `*.suo`).
- Split `MainWindow` request handling into dedicated partial files for web message dispatch, roots handlers, and scan handlers.
- Extracted `ScanRuntime` and snapshot building/writing into dedicated classes (`ScanRuntime`, `ScanSnapshotBuilder`, `ScanSnapshotWriter`).
- Extracted scan execution flow from `ScanCoordinator` to `ScanExecutionService`.
- Standardized scan state and event names via shared constants (`ScanSessionStates`, `ScanEventTypes`).
- IPC for scans (start/stop/pause/resume) + `ScanSession` in SQLite.
- Per-disk lock and whole-scan blocking.
- Scan snapshot to JSON with content samples.
- Pulsing progress bar for `Running` scan.
