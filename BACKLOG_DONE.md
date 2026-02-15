# Backlog - Zrobione

## Zrobione (ostatnio)
* Ręczne podpinanie i odpinanie tagów dla istniejącego projektu:
  * AppHost: dodano IPC `projects.attachTag` i `projects.detachTag` oraz parser payloadu (`projectId`, `tagId`).
  * UI (`Project Organizer`): dodano ręczne akcje `Attach tag` i `Detach` dla istniejących tagów.
  * Testy: AppHost integration (`ProjectTagStoreTests`, `ProjectTagMutationPayloadParserTests`) oraz FE unit (`ProjectsService`, `OrganizerPage`).
  * Dokumentacja: zaktualizowano `PLAN.md` o nowe IPC i flow ręcznego zarządzania tagami projektu.
* Reorganizacja dokumentacji backlogu:
  * Wydzielono historię wykonanych zadań do osobnego pliku BACKLOG_DONE.md.
  * Dodano do BACKLOG.md nowe wymagania: ręczne attach/detach tagów oraz podstawowe wyszukiwanie projektów po tagach (AND).
  * BACKLOG.md pozostawiono jako listę aktywnych i planowanych zadań.

* Uzupełniono zasady pracy w `AGENTS.md`:
  * Dodano regułę, że wiadomości commitów mają być po angielsku.
* Doprecyzowano aktualne zachowanie i dokumentację reprocessingu tagów:
  * Zweryfikowano ścieżkę implementacji: `tags.add` tylko tworzy tag; `projects.runTagHeuristics` jest manualne; `projects.runAiTagSuggestions` obecnie zwraca tylko potwierdzenie kolejki.
  * Zaktualizowano `README.md`, dodając jawną sekcję "Tag Reprocessing (current behavior)", aby uniknąć fałszywych oczekiwań.
  * Zaktualizowano `PLAN.md` (`Stan projektu` + `Tagowanie`) o semantykę backfillu current-vs-target oraz granice manualnych triggerów.
* Wykonano przebieg recovery/sync po przerwanej sesji:
  * Dodano regułę workflow w `AGENTS.md`, która wymaga cyklu `BACKLOG.md` na każdy przebieg (`In progress` na starcie, przeniesienie do `Done (recent)` na końcu, klasyfikacja pozostałego zakresu).
  * Dodano sekcję `## W trakcie` w `BACKLOG.md` i użyto jej w tym przebiegu.
  * Zsynchronizowano lokalną gałąź z GitHub: `git push origin main` (`8848235..15b6220`).
  * Walidacja smoke zakończona powodzeniem: `dotnet test DevProjectsOrganizer.slnx` (`88/88` testów zielone: `33` Engine unit + `55` AppHost integration).
* Heurystyki i metadane projektów (pakiet rozszerzeń):
  * `low-level` został zawężony do sygnałów ASM (hint/ext/kod), bez automatycznego podbijania od `c/cpp`.
  * Dodano heurystykę `pointers` dla projektów C/C++ z wykrytym użyciem wskaźników.
  * Dodano tagi rozmiaru projektu po liczbie linii: `lines-lt-100`, `lines-100-200`, `lines-200-500`, `lines-500-1k`, `lines-1k-2k`, `lines-2k-5k`, `lines-10k-20k`, `lines-20k-50k`, `lines-50k-100k`, `lines-gt-100k`.
  * Akceptacja sugestii projektu przyjmuje teraz opcjonalny opis (`projectDescription`), a `Project` ma trwałe pole `Description` (migracja DB + IPC `projects.update`).
  * `Project Organizer` wspiera edycję opisu istniejącego projektu.
  * Zakładka `Tags` wspiera sortowanie po liczbie projektów (`Projects count`, asc/desc).
  * Testy: AppHost integration (`TagSuggestionHeuristicsServiceTests`, `ProjectStoreTests`), Angular unit/component (`ProjectsService`, `SuggestionsService`, `OrganizerPage`, `TagsPage`) oraz Playwright (`suggestions`, `organizer`).
* Utwardzenie heurystyk tagów + sugestii tagów:
  * `Apply latest heuristics to all projects` no longer creates duplicates for already attached/semantically accepted tag suggestions.
  * `Tag suggestions` now allow permanent delete of `Rejected` entries (archive cleanup).
  * Global tag heuristics run now returns and displays regression report in `Tags` view, based on historical `Accepted`/`Rejected` decisions.
  * Tests: AppHost integration (`TagSuggestionStore`), FE unit/component (`ProjectsService`, `TagsPage`, `TagSuggestionsService`, `TagSuggestionList`), and Playwright (`tags`, `suggestions`).
* Dodano manualną globalną akcję odświeżania heurystyk w widoku `Tags`:
  * CTA: `Apply latest heuristics to all projects`.
  * Runs heuristics sequentially for all existing projects and reports progress/status in GUI.
  * Tests: FE unit (`TagsPage`, `ProjectsService`) and Playwright (`tags`) cover visibility, confirmation, and completion feedback.
* Heurystyki tagów rozszerzono dla projektów beginner/sample:
  * Added `hello-world` and `lorem-ipsum` as seeded system tags.
  * Heuristics now detect `hello-world` from beginner chapter naming/path patterns (including `Beginning_C ...\Chapter_01`-style paths).
  * Heuristics now scan source content for `hello world` and `lorem ipsum` patterns and emit dedicated tag suggestions when matching tags exist.
  * Tests: added AppHost integration coverage in `TagSuggestionHeuristicsServiceTests`.
* Bazowa taksonomia i ownership tagów dla v1 są już wdrożone:
  * Heuristics-first flow generates `AssignExisting` suggestions from existing tags/signals.
  * `CreateNew` remains AI-only by design (not produced by heuristics v1).
* Rdzeń governance tagów jest wdrożony:
  * Seeded/system tags are visible and protected from deletion.
  * User-created tags are deletable (with typed-name confirmation in UI).
* Archiwum sugestii jest rozdzielone na osobne scope:
  * `Pending` / `Accepted` / `Rejected` toggle in Project suggestions.
  * Bulk actions are scope-bound: `Accept all` / `Reject all` only in `Pending`, `Restore all` / `Delete all` only in `Rejected`.
  * `Accepted` scope has no mutating bulk actions and no per-item delete.
  * Tests: updated Playwright (`suggestions`, `apphost-bridge-refresh`) and unit specs for the new scope behavior.
* Usuwanie tagów wymaga teraz potwierdzenia przez wpisanie nazwy:
  * UI: custom tag `Delete` opens modal and requires exact tag name before enabling confirmation.
  * Tests: updated tags unit + Playwright CRUD flow to validate dialog behavior.
* Bulk akcje archiwum sugestii są teraz zależne od scope:
  * `Pending` scope: `Accept all` / `Reject all`.
  * `Archive` scope: `Restore all` / `Delete all`, both targeting only `Rejected`.
  * `Accepted` archived suggestions are no longer deletable from UI.
  * Tests: updated Suggestions unit/service tests and Playwright archive scenarios.
* Dodano zabezpieczenie regresyjne dla rozjazdu kontraktu `projects.delete` (FE vs BE):
  * BE: introduced `ProjectsDeletePayloadParser` tests to enforce `{ projectId }` payload acceptance and malformed payload rejection.
  * FE: `ProjectsService` spec asserts exact delete payload shape (`{ projectId }`).
  * Docs: added IPC contract-testing guardrails in `README.md`, `PLAN.md`, and agent workflow rule in `AGENTS.md`.
* Aktualizacje UX dla Tags/Organizer są wdrożone:
  * Tags list now shows seeded/system tags as `Seeded` and hides `Delete` for them.
  * Each tag row has `Projects N` usage bubble; clicking opens modal with linked projects.
  * Project delete in Organizer now requires typed-name confirmation in modal (`confirmName` is sent to IPC).
  * Backend `projects.delete` validates `confirmName` against current project name; deleting project marks source archived suggestion as `Rejected`.
  * Tests: updated AppHost integration, Angular unit tests, and Playwright (`tags`, `organizer`) for the new flows.
* Run heurystyk tagów zapisuje teraz artefakt skanu JSON:
  * BE: `projects.runTagHeuristics` persists `%APPDATA%\\DevProjectsOrganizer\\scans\\scan-tag-heur-<runId>.json`.
  * Artifact includes run metadata (`project`, `started/finished`, counts) and detected tag suggestions.
  * Tests: added `TagHeuristicsScanWriterTests` integration coverage for file naming/content roundtrip.
* Scan UI wspiera czyszczenie zakończonych wpisów w `Active scans`:
  * `Completed` project scans can be removed from the list via `Clear` action with confirmation.
  * `Completed` tag heuristics runs can be removed from `Tag heuristics runs` via `Clear` action with confirmation.
  * Tests: added FE unit coverage (`ScanService.clearCompleted`, `TagHeuristicsRunsService.clearCompleted`) and Playwright coverage for both clear flows.
* Runy heurystyk tagów są teraz widoczne w GUI:
  * BE emits `tagHeuristics.progress` events (`Running`/`Completed`/`Failed`) during `projects.runTagHeuristics`.
  * Scan view (`Active scans` card) shows a dedicated `Tag heuristics runs` section with progress and generated-count summary.
  * FE mock bridge emits the same events for Playwright and local browser mode.
  * Tests: added FE unit test for event-driven runs service and Playwright scenario covering heuristics run visibility after project accept.
* Tag suggestions v1 (heuristics-first) są wdrożone:
  * BE: `TagSuggestionHeuristicsService` generates `AssignExisting` suggestions for existing tags only.
  * Persistence: new `tag_suggestions` + `project_tags` tables with status (`Pending/Accepted/Rejected`), source/type, fingerprint and dedupe/suppress flow.
  * IPC: `tagSuggestions.list` and `tagSuggestions.setStatus` handlers; accepting attaches tag to project.
  * UI: `Tag suggestions` panel is wired to real DB-backed data and supports per-item + bulk `Accept/Reject` with confirmation.
  * Tests: added AppHost integration coverage (`TagSuggestionHeuristicsService`, `TagSuggestionStore`, `ProjectTagStore`) and FE unit tests for tag suggestions service/component.
* Dialog po akceptacji projektu jest wdrożony:
  * UI: after project suggestion `Accept`, dialog offers `Run tag heuristics` / `Run AI tag suggestions` / `Skip`.
  * BE IPC: `projects.runTagHeuristics`, `projects.runAiTagSuggestions` (validated and queued response).
  * FE: actions wired through `ProjectsService` and covered by unit + Playwright tests.
* CRUD tagów (minimum działające) jest wdrożony:
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

