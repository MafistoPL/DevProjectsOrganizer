# DevProjectsOrganizer — Plan i Specyfikacja (aktualne)

## Spis treści
1. [Cel i zakres](#1-cel-i-zakres)
2. [Stan projektu (teraz)](#2-stan-projektu-teraz)
3. [Architektura (FE/BE)](#3-architektura-febe)
4. [Skanowanie](#4-skanowanie)
5. [Detekcja projektów](#5-detekcja-projektów)
6. [Tagowanie](#6-tagowanie)
7. [Model danych (v1)](#7-model-danych-v1)
8. [UI / widoki](#8-ui--widoki)
9. [Testy](#9-testy)
10. [Roadmapa](#10-roadmapa)
11. [Zasady aktualizacji dokumentów](#11-zasady-aktualizacji-dokumentów)

## 1. Cel i zakres
Program lokalny do porządkowania projektów na dysku:
- skanuje wskazane rooty (lub cały komputer),
- wykrywa kandydatów projektów i sugeruje ich klasyfikację,
- pozwala tagować, filtrować i zarządzać projektami w GUI,
- unika podwójnego skanowania tej samej fizycznej ścieżki,
- obsługuje skróty/reparse points w sposób bezpieczny (bez rekursji).

## 2. Stan projektu (teraz)
- **Stos:** WPF + WebView2 (AppHost), Angular (UI), .NET (Engine).
- **Dane:** SQLite w `%APPDATA%\DevProjectsOrganizer\data.db`.
- **Mapowanie EF:** konfiguracje encji są w osobnych klasach (`IEntityTypeConfiguration`) i ładowane przez `ApplyConfigurationsFromAssembly`.
- **Skanowanie:** uruchamiane z UI, zapisuje `ScanSession` w DB i generuje JSON
  (`%APPDATA%\DevProjectsOrganizer\scans\scan-<id>.json`).
- **Sugestie projektów:** marker heuristics działają po skanie i zapisują `ProjectSuggestion` do SQLite.
- **Materializacja projektu:** `ProjectSuggestion` po `Accept` jest materializowany do trwałego `Project` (upsert po `path+kind`).
- **Heurystyki solution/module:** katalog z `.sln` jest traktowany jako jeden projekt; `*.csproj/*.vcxproj/*.vcproj` pod nim są modułami (nie osobnymi sugestiami), z wyjątkiem zagnieżdżonych `.sln` (osobny projekt).
- **Status sugestii:** enum `Pending` / `Accepted` / `Rejected`.
- **Persistencja decyzji:** sugestia ma fingerprint; odrzucone (`Rejected`) wpisy z tym samym (`path`,`kind`,`fingerprint`) są automatycznie pomijane przy kolejnych skanach.
- **Kasowanie archiwum:** usunięcie wpisu z archiwum zdejmuje baseline odrzucenia (sugestia może wrócić przy kolejnym skanie).
- **Tryby skanu:** `roots`, `changed`, `whole`.
- **Harmonogram:** per‑disk lock; whole‑scan blokuje inne skany.
- **UI:** Scan view z start/stop/pause/resume, stanami i kolejką.
- **Scan UI:** ETA działa (wyliczane runtime), długie `Current path` ma poziomy scroll, a lista rootów pokazuje badge (`Projects`, `Pending`) i podsumowanie ostatniego skanu.
- **Scan UI (root selection):** każdy root ma checkbox do zbiorczego reskanu; akcja `Rescan selected roots` uruchamia `scan.start` tylko dla zaznaczonych rootów (zastępuje wcześniejszy placeholder `Import roots`), a po zaznaczeniu roota pojawia się per-root input `Depth limit` przekazywany do payloadu skanu; zaznaczenia rootów i `Depth limit` są trwałe między restartami aplikacji.
- **Live Results / Suggestions cards:** lista sugestii jest zasilana z SQLite przez IPC; `Accept/Reject` zapisuje status; `Reason` is click-to-copy, `Path` has context menu (`Copy path`, `Open in Explorer`), and grid card size is adjustable via slider.
- **Project Organizer:** zakładka jest podpięta pod realne dane `Project` przez IPC (`projects.list`).
- **Project Organizer (tag filter):** lista projektów wspiera podstawowe filtrowanie po wielu tagach (AND / iloczyn).
- **Project Organizer (description):** projekt ma pole opisu; opis można dodać podczas `Accept` sugestii i edytować później na zakładce `Project Organizer`.
- **Project Organizer (manual tags):** można ręcznie podpinać istniejące tagi do projektu i odpinać tagi z projektu (bez usuwania taga globalnie).
- **Project Organizer (file count + rescan):** projekt ma trwałe `FileCount`; liczba plików jest liczona przy materializacji projektu, a akcja `Rescan project` skanuje tylko ten projekt, odświeża metadane projektu i ponownie uruchamia heurystyki tagów dla tego projektu.
- **Post-accept actions:** po akceptacji sugestii projektu UI pokazuje dialog i może zlecić `Run tag heuristics` albo `Run AI tag suggestions` (IPC do AppHost).
- **Tags:** CRUD tagów jest podpięty pod SQLite przez IPC (`tags.list/add/update/delete`), z walidacją duplikatów nazw.
- **Tags (sorting):** lista tagów wspiera sortowanie po nazwie i po liczbie projektów (asc/desc).
- **Tags (UI):** seedowane/systemowe tagi są widoczne na liście jako `Seeded` i nie można ich usunąć.
- **Tags (usage):** każdy tag pokazuje licznik podpiętych projektów; kliknięcie otwiera modal z listą projektów (`tags.projects`).
- **Tags (delete UX):** usuwanie custom taga wymaga modalu potwierdzenia z przepisaniem nazwy taga.
- **Tags (reprocessing trigger):** `tags.add` tworzy tylko wpis taga; nie uruchamia automatycznie heurystyk ani AI backfill.
- **Tag suggestions (v1):** heurystyki tagów tworzą `AssignExisting` sugestie dla istniejących tagów systemowych (`isSystem=true`); sugestie są zapisywane w DB i obsługiwane przez IPC (`tagSuggestions.list`, `tagSuggestions.setStatus`).
  - Run heurystyk jest wyłącznie manualny (akcje usera: post-accept dialog, `Project Organizer`, globalne `Tags`).
  - Wykrywanie obejmuje też sygnały dla projektów beginner/sample: `hello-world` (path/name + kod) oraz `lorem-ipsum` (kod).
  - `low-level` jest wykrywany jako sygnał ASM (nie ogólny sygnał C/C++).
  - `winapi` jest wykrywany także po sygnale kodowym `WinMain()/WINMAIN`.
  - Dla projektów C/C++ z wykryciem wskaźników generowany jest tag `pointers`.
  - Heurystyki wyznaczają też tag rozmiaru projektu po liczbie linii (`lines-*`).
- **AI tag suggestions (current runtime):** `projects.runAiTagSuggestions` zwraca `AiTagSuggestionsQueued`; persystencja sugestii AI jest jeszcze backlogiem.
- **Tag suggestions (UX):** panel ma scope `Pending`/`Accepted`/`Rejected`, toolbar z wyszukiwarką zależną od pola sortowania (`Project`/`Tag`) oraz sortowaniem po projekcie/tagu/dacie (asc/desc); dla `Created` wyszukiwarka jest wyłączona, a domyślny kierunek to `desc` (najnowsze najpierw).
- **Tag suggestions (archive):** odrzucone sugestie (`Rejected`) można trwale usuwać z archiwum (per-item) z poziomu GUI.
- **Project tags:** akceptacja sugestii tagu przypina tag do projektu (`project_tags`).
- **Heurystyki tagów (dedupe):** ponowne uruchomienia nie generują duplikatów dla tagów już przypiętych do projektu ani dla semantycznie zaakceptowanych sugestii.
- **Project delete flow:** `Project Organizer` ma potwierdzenie usuwania przez przepisanie nazwy projektu (walidacja FE + BE); po usunięciu źródłowa sugestia trafia do `Rejected` w archiwum.
- **Project Organizer (tags):** karta projektu pokazuje przypięte tagi bezpośrednio pod metadanymi projektu.
- **Tag heuristics progress:** uruchomienie `Run tag heuristics` publikuje eventy progresu i jest widoczne w GUI (sekcja `Tag heuristics runs` na zakładce Scan).
- **Tag heuristics scan JSON:** każdy run heurystyk tagów zapisuje debugowy JSON do `%APPDATA%\DevProjectsOrganizer\scans\scan-tag-heur-<runId>.json`.
- **Active scans cleanup:** wpisy `Completed` (zarówno scan session, jak i tag heuristics run) można usunąć z karty `Active scans` po potwierdzeniu dialogu.
- **Suggestions (tooltips):** akcje `Run regression report`, `Export regression JSON`, `Export archive JSON` i `Open JSON folder` mają opisowe tooltipy.

## 3. Architektura (FE/BE)
- **Engine**: logika domenowa i skanowanie (docelowo heurystyki detekcji i tagów).
- **AppHost**: host desktopowy + IPC + persystencja (EF Core / SQLite).
- **UI (Angular)**: widoki i interakcja z AppHost przez IPC.
- **IPC suggestions:** `suggestions.list`, `suggestions.setStatus`, `suggestions.exportArchive`, `suggestions.openArchiveFolder`, `suggestions.openPath`.
  - `suggestions.setStatus` accepts optional `projectName` i `projectDescription` for `Accepted` flow.
- **IPC projects:** `projects.list`, `projects.update`, `projects.delete`, `projects.attachTag`, `projects.detachTag`, `projects.rescan`, `projects.runTagHeuristics`, `projects.runAiTagSuggestions`.
- **IPC tags:** `tags.list`, `tags.projects`, `tags.add`, `tags.update`, `tags.delete`.
- **IPC tag suggestions:** `tagSuggestions.list`, `tagSuggestions.setStatus`.
- **Refactor status**: execution flow is moved to `ScanExecutionService`; `ScanCoordinator` focuses on lifecycle, scheduling, and event relay.
- **State/event consistency**: scan states and event names are centralized in shared constants.

## 4. Skanowanie
Założenia:
- tryby `roots`, `changed`, `whole`,
- faza liczenia plików tylko dla skanów rootów (dla lepszej prognozy postępu),
- równolegle tylko skany na różnych dyskach,
- whole‑scan blokuje inne skany.

Snapshot JSON:
- drzewo folderów + pliki,
- próbki treści (limit linii i rozmiaru),
  - domyślny limit próbek treści: 100 linii na plik (z limitem znaków),
- ignorowanie artefaktów build/IDE jest już wdrożone.
- pliki wyjściowe:
  - skan projektów: `%APPDATA%\DevProjectsOrganizer\scans\scan-<id>.json`,
  - run heurystyk tagów: `%APPDATA%\DevProjectsOrganizer\scans\scan-tag-heur-<runId>.json`.

Uwaga: snapshot JSON to **feature debugowy** na czas dopracowania heurystyk.
Docelowym produktem skanowania są **ProjectSuggestion** (i później projekty + tagi),
nie sam JSON.

## 5. Detekcja projektów
Heurystyki (v1):
- markery: `.git`, `.sln`, `*.csproj`, `package.json`, `CMakeLists.txt`, `Makefile`, `pom.xml`, `build.gradle`.
- histogram rozszerzeń (np. przewaga `*.cs` -> `csharp`).
- struktura projektu (`src/`, `tests/`, `docs/`) — do rozszerzenia.

Semantyka (doprecyzowanie):
- W tym projekcie **solution = projekt** (jednostka biznesowa w organizerze).
- `*.csproj` / `*.vcxproj` / `*.vcproj` pod solution są traktowane jako **moduły** projektu, nie osobne projekty.
- Jeśli katalog ma `.sln`, to jest kanonicznym kandydatem `ProjectRoot`.
- Jeśli pod nim są podkatalogi z `*.csproj` / `*.vcxproj`, ich sugestie powinny być scalane/supresowane pod solution.
- Wyjątek: jeśli wewnątrz jest **osobna, zagnieżdżona `.sln`**, to jest osobny projekt.

Niejasne przypadki:
- folder bez markerów i wiele plików -> `Collection` + opcjonalne `SingleFileMiniProject`.
- opcjonalne rozstrzyganie z udziałem AI (limitowany kontekst).

## 6. Tagowanie
Heurystyki tagów (v1):
- markery i histogram rozszerzeń,
- nazwy katalogów (np. `course`, `tutorial`, `katas`),
- struktura (`src/`, `tests/`).
- heurystyki **nie tworzą nowych tagów**; generują tylko sugestie przypięcia istniejących tagów systemowych (`AssignExisting`).
- status flow sugestii tagów: `Pending` -> `Accepted`/`Rejected`; `Accepted` podpina tag do projektu.
- deduplikacja: ten sam kandydat (`project + tag + fingerprint`) nie jest dublowany; historycznie odrzucony fingerprint jest tłumiony przy kolejnych runach heurystyk.

Tag taxonomy v1 (draft):
- Canonical tags (heuristics-first): `csharp`, `dotnet`, `cpp`, `c`, `native`, `vs-solution`, `vs-project`, `node`, `react`, `angular`, `html`, `json`, `git`, `cmake`, `makefile`, `java`, `gradle`, `maven`, `python`, `rust`, `go`, `powershell`, `low-level`, `pointers`, `console`, `winapi`, `gui`, `hello-world`, `lorem-ipsum`, `lines-lt-100`, `lines-100-200`, `lines-200-500`, `lines-500-1k`, `lines-1k-2k`, `lines-2k-5k`, `lines-10k-20k`, `lines-20k-50k`, `lines-50k-100k`, `lines-gt-100k`.
- Naming policy:
  - lowercase kebab-case tags,
  - one canonical form per concept (no alias duplicates in DB),
  - aliases kept only as input-mapping (not stored as final tag names).
- Heuristics input sources:
  - marker files (`.sln`, `*.csproj`, `*.vcxproj`, `package.json`, `CMakeLists.txt`, `Makefile`, `pom.xml`, `build.gradle`, `.git`),
  - extension histogram (`*.ts`, `*.tsx`, `*.jsx`, `*.cpp`, `*.h`, `*.ps1`, `*.java`, etc.),
  - path/name hints (`winapi`, `gui`, `console`, `katas`, `tutorial`, beginner chapter patterns),
  - source content token hints (`hello world`, `lorem ipsum`, pointer syntax, asm keywords) scanned with bounded limits.
  - project size bucket from estimated source line count (`lines-*` tags).
- AI-first (not heuristics v1): semantic architecture/pattern tags such as `single-responsibility-principle`, deeper design-pattern tags, and advanced domain inference.
- Confidence policy (v1):
  - strong: direct marker match (e.g. `package.json` -> `node`),
  - medium: combined extension/path hints,
  - weak: path-only or single weak signal.
- Validation from current user scans (latest snapshot set):
  - high-confidence signals: `vs-solution`, `vs-project`, `cpp`, `native`, `java`, `single-file`,
  - good secondary signals: `design-patterns` (from path), `gui` (e.g. `Swing`), `winapi` (`windows.h` in sample lines),
  - noisy signal to gate in v1: `json` (tooling/config files produce false positives on native projects).

Lifecycle (docelowy):
- `ProjectSuggestion` po `Accept` materializuje się jako `Project`.
- Po akceptacji projektu pojawia się dialog z akcjami:
  - `Run tag heuristics` (domyślna ścieżka),
  - `Run AI tag suggestions` (opcjonalna),
  - `Skip for now`.
- Te same akcje muszą być dostępne z poziomu `Project Organizer` dla już istniejącego projektu.

AI (opcjonalnie, później):
- tylko `TagSuggestion` (bez auto‑aplikacji),
- AI dostaje listę istniejących tagów i ma preferować ich użycie,
- AI może zaproponować nowe tagi jako `CreateNewTagSuggestion`,
- AI może dostać historię odrzuconych sugestii tagów jako sygnał negatywny,
- limitowany kontekst (drzewo + krótkie próbki).
- akceptacja `CreateNewTagSuggestion` tworzy nowy tag i przypina go do projektu.

Tag governance (docelowo):
- `System tags` (seedowane pod heurystyki): niedeletowalne w UI.
- `Custom tags` (dodane ręcznie lub utworzone po akceptacji sugestii AI): deletowalne.
- Heurystyki działają wyłącznie na słowniku tagów systemowych; tagi użytkownika są obsługiwane ścieżką manualną/AI (`CreateNew` pozostaje domeną AI).

Backfill (current vs target):
- Obecnie wdrożone:
  - dodanie taga (`tags.add`) nie uruchamia automatycznego backfillu,
  - reprocess heurystyczny jest ręczny (per-projekt lub globalny run),
  - akcja AI jest ręczna i aktualnie tylko kolejkowana (`AiTagSuggestionsQueued`), bez zapisanych sugestii AI.
- Docelowo (backlog):
  - custom-tag matching ma być AI-driven na jawny trigger usera (bez automatycznego runu po `tags.add`),
  - flow ma pozostać idempotentny i deduplikowany.

Konfigurowalność:
- rozważamy GUI/YAML dla reguł heurystyk (wymaga parsera + runtime).

## 7. Model danych (v1)
Minimalny zestaw:
- **Root**: ścieżka, status, statystyki skanu.
- **ScanSession**: tryb, stan, liczba plików, postęp, output JSON.
- **ProjectSuggestion**: kandydat projektu + metadane wykrycia + status decyzji; wpisy są archiwalne per skan (pod regresję i audyt).
- **ProjectSuggestion.Fingerprint**: deterministyczny podpis heurystyki dla konkretnego kandydata (używany do suppress/restore po `Reject/Delete`).
- **Project**: zaakceptowany projekt.
- **Project.Description**: edytowalny opis projektu.
- **Project.FileCount**: liczba plików projektu (liczona przy materializacji + aktualizowana przez ręczny reskan projektu).
- **Tag** i **TagSuggestion**: tagowanie i sugestie.
- **ProjectTag**: relacja N:M między `Project` i `Tag`.
- **Tag** ma mieć klasyfikację źródła/typu (np. `System` vs `Custom`) i regułę usuwalności.
- **TagSuggestion** powinien mieć:
  - typ: `AssignExisting` / `CreateNew` (`CreateNew` zarezerwowane dla AI),
  - źródło: `Heuristic` / `AI`,
  - status: `Pending` / `Accepted` / `Rejected`,
  - fingerprint do suppress powtórek.

Uwagi modelowe:
- Dla sugestii opartych o `.sln` planujemy przechowywać także metadane modułów (np. liczba/ścieżki), ale nadal jako jeden wpis projektu.

## 8. UI / widoki
Główne zakładki:
- **Scan**: rooty, start skanu, kolejka, postęp.
- **Project Organizer**: lista projektów, filtry, szczegóły.
- **Project Organizer**: filtr tagów działa jako iloczyn (projekt musi mieć wszystkie wybrane tagi).
- **Project Organizer**: usuwanie projektu wymaga wpisania pełnej nazwy w modalu potwierdzenia (check FE + walidacja BE).
- **Project Organizer**: karta projektu pokazuje również przypięte tagi (chipy).
- **Project Organizer**: przy tagach dostępne są ręczne akcje `Attach tag` / `Detach` dla istniejących tagów.
- **Suggestions**: akceptacja/odrzucanie sugestii projektów i tagów.
- **Suggestions / Project suggestions**: przełącznik `Pending` / `Accepted` / `Rejected`, eksport archiwum do JSON i szybkie otwieranie folderu eksportów.
- **Suggestions / Project suggestions**: akcje regresji/eksportu mają tooltipy opisujące działanie, w tym gdzie pojawia się wynik raportu i jak przejść do folderu JSON (`Open JSON folder`).
- **Suggestions / Regression**: dostępne akcje `Run regression report` oraz `Export regression JSON` (replay historycznych decyzji usera na `scan-<id>.json`).
- **Suggestions / Regression**: po `Run regression report` GUI automatycznie przewija do panelu raportu na dole (również dla błędu).
- **Suggestions / panel actions**: akcje działają per panel (Project vs Tag); w `Pending` są `Accept all` / `Reject all`, a w `Rejected` są `Restore all` / `Delete all`; widok `Accepted` nie ma bulk mutacji.
- **Tag suggestions panel:** działa na realnych danych z DB (bez mocków), wspiera `Accept/Reject` per wpis i bulk.
- **Tag suggestions panel:** ma scope (`Pending/Accepted/Rejected`), wyszukiwarkę zależną od sortu (`Project` lub `Tag`) i sortowanie (`Project`, `Tag`, `Created`, `Asc/Desc`), a sterowanie layoutem (`List/Grid` + suwak `Card size`) jest w analogicznym miejscu i zachowuje się jak w `Project suggestions`.
- **Scan / status card:** zawiera także sekcję przebiegu heurystyk tagów (`Running/Completed/Failed`, progress, generated count).
- **Scan / status card:** wpisy `Completed` mają akcję `Clear` z potwierdzeniem (zarówno skany, jak i runy heurystyk tagów).
- **Suggestions / Project suggestions**: w widokach archiwalnych `Reject` jest ukryty; `Accept` może odwrócić `Rejected`; usuwanie dotyczy tylko `Rejected` (brak usuwania `Accepted`).
- **Project acceptance flow**: po `Accept` projektu otwieramy dialog uruchomienia heurystyk/AI tagów.
  - Dialog akceptacji pozwala edytować nazwę projektu i dodać opis przed finalnym `Accept` (z opcją: tylko zaakceptuj / zaakceptuj + heurystyki / zaakceptuj + AI).
- **Project Organizer**: akcje na projekcie `Run tag heuristics` i `Run AI tag suggestions`.
- **Project Organizer**: opis projektu jest widoczny i edytowalny inline (`Edit description` / `Save`).
- **Project Organizer**: karta projektu uwypukla liczbę plików (`Files N`) i ma akcję `Rescan project` (recount plików + odświeżenie rekomendacji tagów tylko dla tego projektu).
- **Tags**: zarządzanie tagami i backfill.
- **Tags**: działające CRUD (lista + add/edit/delete), z ochroną tagów systemowych (`Seeded` bez opcji `Delete`).
- **Tags**: licznik użycia (`Projects N`) i modal z listą projektów przypiętych do wybranego taga.
- **Tags**: lista wspiera sortowanie po `Name` oraz `Projects count` (asc/desc).
- **Tags**: globalna akcja `Apply latest heuristics to all projects` uruchamia heurystyki tagów dla wszystkich projektów z potwierdzeniem i statusem postępu w GUI.
- **Tags / global apply heuristics**: po zakończeniu runa generowany jest raport regresji tag heurystyk oparty o historyczne decyzje `Accepted`/`Rejected` (summary + per-project w GUI).
- **Recent**: last_viewed / last_opened.

Makiety (Excalidraw) trzymamy w `docs/excalidraw/`, a PNG w `docs/images/`.

## 9. Testy
Piramida testów:
- **Unit (Engine)**: logika heurystyk, policy dla próbek, filtrowanie.
- **Integration (AppHost)**: DB + skan + JSON.
- **Integration (AppHost)**: DB + skan + JSON + regresja heurystyk względem historycznych decyzji (`Accepted`/`Rejected`) powiązanych z konkretnym `ScanSessionId` i jego `scan-<id>.json`.
- **E2E/Visual (Playwright)**: kluczowe ścieżki UI + snapshoty.
- **FE unit/component tests (ng test)**: pokrywają logikę bulk akcji (`setPendingStatusForAll`) i potwierdzenia dialogowe na stronie Suggestions.
- **FE unit/component tests (ng test)**: obejmują też render przypiętych tagów w `Project Organizer`, filtrowanie/sortowanie/scope w `Tag suggestions` oraz widoczność panelu `Heuristics regression report` (success/failure).
- **FE unit/component tests (ng test)**: obejmują też ręczne attach/detach tagów w `Project Organizer` oraz dokładny payload IPC (`projects.attachTag`, `projects.detachTag`) w `ProjectsService`.
- **Integration (AppHost)**: payload parser dla mutacji tagów projektu akceptuje minimalny payload (`projectId`, `tagId`) i odrzuca niepoprawny.
- **IPC contract tests (FE+BE)**: `projects.rescan` ma test dokładnego payloadu w FE (`{ projectId }`) oraz test parsera payloadu w AppHost (accept minimal/reject invalid).
- **E2E layout guards (Playwright)**: po uruchomieniu regresji i głębokim scrollu nagłówki stron (`Scan`, `Project Organizer`, `Suggestions`, `Tags`, `Recent`) muszą pozostać w viewport po przełączaniu zakładek.
- **E2E layout guards (Playwright)**: nagłówek strony (`h1`) musi być renderowany poniżej paska zakładek (brak nakładania/ucięcia przez sticky tabs).
- **E2E regression guards (Playwright)**: panel `Heuristics regression report` musi zostać doscrollowany i widoczny w kontenerze GUI zarówno dla sukcesu raportu, jak i dla błędu.
- **E2E spacing guards (Playwright)**: widok `Suggestions` musi mieć dolny margines przy końcu scrolla zarówno bez raportu, jak i z panelem `Heuristics regression report`.
- **E2E responsiveness guards (Playwright)**: po kliknięciu `Run regression report` panel raportu ma pojawić się od razu (bez dodatkowej akcji użytkownika) i automatycznie przewinąć widok.
- **E2E layout guards (Playwright / report container)**: `regression-panel` musi mieć dodatnią wysokość i obejmować wyrenderowaną treść raportu (brak kolapsu kontenera i ucinania dzieci).
- **IPC contract tests (FE+BE)**: dla zmian payloadów IPC (np. `projects.delete`) wymagamy testu kształtu payloadu w FE oraz testu parsera/walidacji payloadu w AppHost.

Pre-commit jest realizowany przez `.githooks/pre-commit` (wymaga `core.hooksPath=.githooks`).
User-data replay regression jest osobną kategorią testów (`Category=UserDataRegression`) i jest uruchamiany ręcznie na realnej bazie użytkownika.

## 10. Roadmapa
Najbliższe i średnie kroki są w `BACKLOG.md`. Skrót:
- **Near Term (kolejność wdrożenia tagów):**
  1. (Done) `ProjectSuggestion -> Project` po `Accept` (znika z `Pending`, jest widoczny w `Project Organizer`).
  2. (Done) CRUD tagów (manualne dodawanie/edycja/usuwanie), aby istniał słownik tagów dla heurystyk i AI.
  3. (Done) Dialog po akceptacji projektu: `Run tag heuristics` / `Run AI tag suggestions` / `Skip`.
  4. (Done) Te same akcje uruchamiane ręcznie z `Project Organizer` dla istniejących projektów.
  5. (Done v1) `TagSuggestion` dla istniejących tagów (`AssignExisting`) ze statusem i fingerprintem.
  6. Jawny flow reprocessingu po dodaniu taga: bez auto-run po `tags.add`; manualne heurystyki/AI i jasna komunikacja w UI.
  7. `CreateNew` sugestie tagów (AI) + akceptacja tworząca nowy tag.
- **Mid Term:** AI tag suggestions z wykorzystaniem historii odrzuceń, split/merge projektów, incremental scan.
- **Later:** AI do „one project vs many”, sync z backendem.

## 11. Zasady aktualizacji dokumentów
- `PLAN.md` opisuje aktualną specyfikację i stan projektu.
- `BACKLOG.md` to źródło zadań i kolejnych kroków.
- Makiety: Excalidraw -> PNG (opis w `AGENTS.md`).
