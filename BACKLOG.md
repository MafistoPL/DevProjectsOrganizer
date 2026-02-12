# Backlog

## Now (next 2-4 weeks)
- Pre-commit: verify setup in docs (`core.hooksPath=.githooks`) for every clone/environment.
- UI: expose action to run/export regression report against historical user decisions.
- Heurystyka: `solution = projekt`, a `*.csproj/*.vcxproj/*.vcproj` pod `.sln` traktować jako moduły (suppress/merge child suggestions).
- Heurystyka: wyjątek dla zagnieżdżonych `.sln` (nested solution = osobny projekt).
- Testy regresyjne heurystyk dla przypadku wrappera solution (`X\\` + `X\\X\\*.vcxproj`) oraz przypadku nested `.sln`.

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
- Suggestions: archived `Rejected` now suppresses re-adding the same candidate on future scans when (`path`,`kind`,`fingerprint`) is unchanged.
- Suggestions: archive supports explicit `Delete` (removes suppression baseline for that suggestion and allows re-adding on future scans).
- Persistence: `project_suggestions` now stores `Fingerprint` and uses latest non-pending decision per key for reinsert filtering.
- Scan UI: ETA is now computed and propagated from runtime to UI (`HH:MM:SS`).
- Scan UI: active scan `Current path` supports horizontal scroll for long paths.
- Scan UI: roots list now shows `Projects` + `Pending` badges.
- Scan UI: roots list now shows per-root last scan summary (`state`, `time`, `files`).
- Integration tests no longer clean global `%APPDATA%\\DevProjectsOrganizer\\scans`; scan snapshots in tests are now isolated to test-specific temp data directories.
- Replay regression now compares user decisions against matching `scan-<id>.json` per `ScanSessionId` (instead of mixing root-wide history into latest snapshot).
- UI: `Stop` action is now hidden for terminal scan states (e.g., `Completed`) and covered by Playwright test.
- UI: removed `Debug JSON` button from suggestion cards; `Reason` is now click-to-copy and `Path` opens context menu (`Copy path`, `Open in Explorer`).
- UI: project suggestions now support grid card-size slider (works in Scan + Suggestions views).
- UI: `Suggestions` page panels now stretch with viewport height similarly to Scan layout.
- UI: archive view actions are constrained (`Reject` hidden; `Accept` available for rejected entries to undo mistakes).
- UI: pending suggestions view now deduplicates newest item by (`path`,`kind`) to avoid repeated entries after multiple scans.
- IPC: added `suggestions.openPath` for opening selected suggestion path in Explorer.
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
