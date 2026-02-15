# Backlog

## Now (next 1 week)
1. Define tag taxonomy + ownership (heuristics vs AI) before coding tag heuristics.
     * Bootstrap candidate tags from current signals: markers/tech hints (e.g. `cpp`, `native`, `.net`, `.sln`).
     * Define normalized tag set and naming policy (avoid duplicates like `vs-project` vs `vs-solution` unless both are intentional).
     * Classify tags by source:
       * Heuristics-first candidates: `html`, `angular`, `react`, `node`, `json`, `git`, `vs-project`, `vs-solution`, `low-level`, `native`, `cpp`.
       * AI-only / advanced semantic candidates (for now): e.g. `SingleResponsibilityPrinciple`, deeper architecture/pattern tags.
     * Add mapping rules draft (marker/ext/path/file-name -> tag suggestion) and confidence tiers.
     * Decide which tags are inferred from project metadata only vs requiring source/content analysis.
     * Draft v1 canonical set to validate on scan data:
       `csharp`, `dotnet`, `cpp`, `c`, `native`, `vs-solution`, `vs-project`, `node`, `react`, `angular`, `html`, `json`, `git`, `cmake`, `makefile`, `java`, `gradle`, `maven`, `python`, `rust`, `go`, `powershell`, `low-level`, `console`, `winapi`, `gui`.
     * Validation notes from current scan snapshots:
       * strong and stable: `vs-solution`, `vs-project`, `cpp`, `native`, `java`, `single-file`.
       * useful path/content hints: `design-patterns`, `gui` (`Swing`), `winapi` (`windows.h` in sample lines).
       * noise to control: `json` appears often from tooling files and should require stronger co-signal or be disabled in v1.
2. Backfill after creating a new tag.
     * BE: async and idempotent, heuristics always, AI optional.
     * UI: lightweight status feedback (toast/status).
3. Tag governance model.
     * Seed system tags used by heuristics.
     * System tags are not deletable.
     * User-created / AI-created tags are deletable.
     * `CreateNew` tag suggestions are AI-only; accepting one creates the tag and attaches it.

## Soon (2-4 weeks)
* Pre-commit: verify setup in docs (`core.hooksPath=.githooks`) for every clone/environment.

## Soon (1-3 months)
* AI tag suggestions using: existing tags + optional history of rejected new-tag proposals.
* Manual split/merge of detected projects inside a root.
* Better ranking and filtering of suggestions (score, language, age).
* Incremental scan based on `last_modified_at` + cache.
* Configurable heuristics (GUI or YAML) + parser and runtime.
* Align `Engine/Scanning` interfaces with real runtime implementation in AppHost (remove duplicated scan contracts over time).

## Later
* AI for "one project vs many" with bounded context.
* Remote backend + client sync.
* Full-text search over scan metadata.

## To segregate
* Add place to keep PAT to interact with gh, need to figureout how to keep it safely.

## Done (recent)
* Suggestions archive is split into separate scopes:
  * `Pending` / `Accepted` / `Rejected` toggle in Project suggestions.
  * Bulk actions are scope-bound: `Accept all` / `Reject all` only in `Pending`, `Restore all` / `Delete all` only in `Rejected`.
  * `Accepted` scope has no mutating bulk actions and no per-item delete.
  * Tests: updated Playwright (`suggestions`, `apphost-bridge-refresh`) and unit specs for the new scope behavior.
* Tags delete now requires typed-name confirmation:
  * UI: custom tag `Delete` opens modal and requires exact tag name before enabling confirmation.
  * Tests: updated tags unit + Playwright CRUD flow to validate dialog behavior.
* Suggestions archive bulk actions are now scope-aware:
  * `Pending` scope: `Accept all` / `Reject all`.
  * `Archive` scope: `Restore all` / `Delete all`, both targeting only `Rejected`.
  * `Accepted` archived suggestions are no longer deletable from UI.
  * Tests: updated Suggestions unit/service tests and Playwright archive scenarios.
* Added regression guard for `projects.delete` contract mismatch (FE vs BE):
  * BE: introduced `ProjectsDeletePayloadParser` tests to enforce `{ projectId }` payload acceptance and malformed payload rejection.
  * FE: `ProjectsService` spec asserts exact delete payload shape (`{ projectId }`).
  * Docs: added IPC contract-testing guardrails in `README.md`, `PLAN.md`, and agent workflow rule in `AGENTS.md`.
* Tags/Organizer UX updates are implemented:
  * Tags list now shows seeded/system tags as `Seeded` and hides `Delete` for them.
  * Each tag row has `Projects N` usage bubble; clicking opens modal with linked projects.
  * Project delete in Organizer now requires typed-name confirmation in modal (`confirmName` is sent to IPC).
  * Backend `projects.delete` validates `confirmName` against current project name; deleting project marks source archived suggestion as `Rejected`.
  * Tests: updated AppHost integration, Angular unit tests, and Playwright (`tags`, `organizer`) for the new flows.
* Tag heuristics run now writes JSON scan artifact:
  * BE: `projects.runTagHeuristics` persists `%APPDATA%\\DevProjectsOrganizer\\scans\\scan-tag-heur-<runId>.json`.
  * Artifact includes run metadata (`project`, `started/finished`, counts) and detected tag suggestions.
  * Tests: added `TagHeuristicsScanWriterTests` integration coverage for file naming/content roundtrip.
* Scan UI supports clearing completed items in `Active scans`:
  * `Completed` project scans can be removed from the list via `Clear` action with confirmation.
  * `Completed` tag heuristics runs can be removed from `Tag heuristics runs` via `Clear` action with confirmation.
  * Tests: added FE unit coverage (`ScanService.clearCompleted`, `TagHeuristicsRunsService.clearCompleted`) and Playwright coverage for both clear flows.
* Tag heuristics runs are now visible in GUI:
  * BE emits `tagHeuristics.progress` events (`Running`/`Completed`/`Failed`) during `projects.runTagHeuristics`.
  * Scan view (`Active scans` card) shows a dedicated `Tag heuristics runs` section with progress and generated-count summary.
  * FE mock bridge emits the same events for Playwright and local browser mode.
  * Tests: added FE unit test for event-driven runs service and Playwright scenario covering heuristics run visibility after project accept.
* Tag suggestions v1 (heuristics first) is implemented:
  * BE: `TagSuggestionHeuristicsService` generates `AssignExisting` suggestions for existing tags only.
  * Persistence: new `tag_suggestions` + `project_tags` tables with status (`Pending/Accepted/Rejected`), source/type, fingerprint and dedupe/suppress flow.
  * IPC: `tagSuggestions.list` and `tagSuggestions.setStatus` handlers; accepting attaches tag to project.
  * UI: `Tag suggestions` panel is wired to real DB-backed data and supports per-item + bulk `Accept/Reject` with confirmation.
  * Tests: added AppHost integration coverage (`TagSuggestionHeuristicsService`, `TagSuggestionStore`, `ProjectTagStore`) and FE unit tests for tag suggestions service/component.
* Post-accept project dialog is implemented:
  * UI: after project suggestion `Accept`, dialog offers `Run tag heuristics` / `Run AI tag suggestions` / `Skip`.
  * BE IPC: `projects.runTagHeuristics`, `projects.runAiTagSuggestions` (validated and queued response).
  * FE: actions wired through `ProjectsService` and covered by unit + Playwright tests.
* Tags CRUD (minimum working) is implemented:
  * BE: `TagEntity` + `TagStore` (`list/add/update/delete`) with duplicate validation by normalized name.
  * IPC: `tags.list`, `tags.add`, `tags.update`, `tags.delete`.
  * UI: `Tags` page now has real list + add/edit/delete form actions.
  * Tests: new integration tests (`TagStoreTests`), FE unit tests (`TagsService`, `TagsPage`), and Playwright CRUD flow (`ui/tests/tags.spec.ts`).
* `ProjectSuggestion -> Project` on `Accept` is implemented:
  * BE: new `projects` table + `ProjectStore.UpsertFromSuggestionAsync`.
  * IPC: new `projects.list`.
  * UI: `Project Organizer` now renders real accepted projects from DB.
  * UI refresh: accepting suggestion triggers projects refresh (`projects.changed` + service reload).
* FE tests: added unit coverage for `SuggestionsService.setPendingStatusForAll` and component-level integration tests for project bulk confirm flows in `SuggestionsPage`.
* Suggestions UI: actions were moved from page-level header into per-panel toolbars (`Project suggestions`, `Tag suggestions`).
* Bulk actions `Accept all` / `Reject all` now require confirmation dialogs before execution.
* UI: Suggestions now exposes `Run regression report` and `Export regression JSON` actions based on historical user decisions + scan snapshots.
* Heurystyki: `solution = projekt` jest wdrożone; `*.csproj/*.vcxproj/*.vcproj` pod `.sln` są traktowane jako moduły (suppress child + merge marker hints do sugestii solution).
* Heurystyki: wyjątek dla zagnieżdżonych `.sln` jest wdrożony (nested solution pozostaje osobnym projektem).
* Testy regresyjne heurystyk dodane dla wrappera solution (`X\\` + `X\\X\\*.vcxproj`) i dla nested `.sln`.
* Suggestions: archived `Rejected` now suppresses re-adding the same candidate on future scans when (`path`,`kind`,`fingerprint`) is unchanged.
* Suggestions: archive supports explicit `Delete` (removes suppression baseline for that suggestion and allows re-adding on future scans).
* Persistence: `project_suggestions` now stores `Fingerprint` and uses latest non-pending decision per key for reinsert filtering.
* Scan UI: ETA is now computed and propagated from runtime to UI (`HH:MM:SS`).
* Scan UI: active scan `Current path` supports horizontal scroll for long paths.
* Scan UI: roots list now shows `Projects` + `Pending` badges.
* Scan UI: roots list now shows per-root last scan summary (`state`, `time`, `files`).
* Integration tests no longer clean global `%APPDATA%\\DevProjectsOrganizer\\scans`; scan snapshots in tests are now isolated to test-specific temp data directories.
* Replay regression now compares user decisions against matching `scan-<id>.json` per `ScanSessionId` (instead of mixing root-wide history into latest snapshot).
* UI: `Stop` action is now hidden for terminal scan states (e.g., `Completed`) and covered by Playwright test.
* UI: removed `Debug JSON` button from suggestion cards; `Reason` is now click-to-copy and `Path` opens context menu (`Copy path`, `Open in Explorer`).
* UI: project suggestions now support grid card-size slider (works in Scan + Suggestions views).
* UI: `Suggestions` page panels now stretch with viewport height similarly to Scan layout.
* UI: archive view actions are constrained (`Reject` hidden; `Accept` available for rejected entries to undo mistakes).
* UI: pending suggestions view now deduplicates newest item by (`path`,`kind`) to avoid repeated entries after multiple scans.
* IPC: added `suggestions.openPath` for opening selected suggestion path in Explorer.
* UI `Live results` now loads real `ProjectSuggestion` rows from SQLite through IPC (`suggestions.list`).
* `Accept` / `Reject` in UI now persist suggestion status in DB (`suggestions.setStatus`).
* `Debug JSON` action now exports suggestion debug payload in-memory and copies it to clipboard with confirmation bubble.
* `Suggestions` view now supports `Pending` (all scans) and `Archive` (Accepted+Rejected) scopes.
* Added archive export action (`suggestions.exportArchive`) and open export folder action (`suggestions.openArchiveFolder`) in Suggestions UI.
* Marker heuristics for project detection (`.sln`, `.csproj`, `package.json`, `CMakeLists.txt`, `Makefile`, `pom.xml`, `build.gradle`, `.git`) + save suggestions to SQLite.
* `ProjectSuggestion` status moved from free `string` to enum (`Pending`/`Accepted`/`Rejected`).
* EF entity mapping moved to dedicated configuration classes + `ApplyConfigurationsFromAssembly`.
* Heuristics regression analyzer (integration-level) comparing current run vs historical accepted/rejected suggestions per root.
* User-data replay regression test (`Category=UserDataRegression`) reads real `%APPDATA%` DB + historical scan JSON snapshots (no mocks/seeding).
* Historical suggestions persisted per scan (`project_suggestions` includes `RootPath`) for regression and audit.
* Pre-commit script exists in `.githooks/pre-commit` and blocks commit on test failures when `core.hooksPath` is configured.
* Ignore build/IDE artifacts in scan (`bin/`, `obj/`, `.vs/`, `.idea/`, `node_modules/`, `*.pdb`, `*.obj`, `*.tlog`, `*.exe`, `*.suo`).
* Split `MainWindow` request handling into dedicated partial files for web message dispatch, roots handlers, and scan handlers.
* Extracted `ScanRuntime` and snapshot building/writing into dedicated classes (`ScanRuntime`, `ScanSnapshotBuilder`, `ScanSnapshotWriter`).
* Extracted scan execution flow from `ScanCoordinator` to `ScanExecutionService`.
* Standardized scan state and event names via shared constants (`ScanSessionStates`, `ScanEventTypes`).
* IPC for scans (start/stop/pause/resume) + `ScanSession` in SQLite.
* Per-disk lock and whole-scan blocking.
* Scan snapshot to JSON with content samples.
* Pulsing progress bar for `Running` scan.
