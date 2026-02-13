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
- **Heurystyki solution/module:** katalog z `.sln` jest traktowany jako jeden projekt; `*.csproj/*.vcxproj/*.vcproj` pod nim są modułami (nie osobnymi sugestiami), z wyjątkiem zagnieżdżonych `.sln` (osobny projekt).
- **Status sugestii:** enum `Pending` / `Accepted` / `Rejected`.
- **Persistencja decyzji:** sugestia ma fingerprint; odrzucone (`Rejected`) wpisy z tym samym (`path`,`kind`,`fingerprint`) są automatycznie pomijane przy kolejnych skanach.
- **Kasowanie archiwum:** usunięcie wpisu z archiwum zdejmuje baseline odrzucenia (sugestia może wrócić przy kolejnym skanie).
- **Tryby skanu:** `roots`, `changed`, `whole`.
- **Harmonogram:** per‑disk lock; whole‑scan blokuje inne skany.
- **UI:** Scan view z start/stop/pause/resume, stanami i kolejką.
- **Scan UI:** ETA działa (wyliczane runtime), długie `Current path` ma poziomy scroll, a lista rootów pokazuje badge (`Projects`, `Pending`) i podsumowanie ostatniego skanu.
- **Live Results / Suggestions cards:** lista sugestii jest zasilana z SQLite przez IPC; `Accept/Reject` zapisuje status; `Reason` is click-to-copy, `Path` has context menu (`Copy path`, `Open in Explorer`), and grid card size is adjustable via slider.

## 3. Architektura (FE/BE)
- **Engine**: logika domenowa i skanowanie (docelowo heurystyki detekcji i tagów).
- **AppHost**: host desktopowy + IPC + persystencja (EF Core / SQLite).
- **UI (Angular)**: widoki i interakcja z AppHost przez IPC.
- **IPC suggestions:** `suggestions.list`, `suggestions.setStatus`, `suggestions.exportArchive`, `suggestions.openArchiveFolder`, `suggestions.openPath`.
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
- ignorowanie artefaktów build/IDE jest już wdrożone.

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

Backfill:
- po dodaniu nowego taga (manualnie lub po akceptacji AI) uruchamiamy backfill:
  - heurystyczny zawsze,
  - AI opcjonalnie.
- backfill ma być asynchroniczny i idempotentny (bez duplikatów sugestii).

Konfigurowalność:
- rozważamy GUI/YAML dla reguł heurystyk (wymaga parsera + runtime).

## 7. Model danych (v1)
Minimalny zestaw:
- **Root**: ścieżka, status, statystyki skanu.
- **ScanSession**: tryb, stan, liczba plików, postęp, output JSON.
- **ProjectSuggestion**: kandydat projektu + metadane wykrycia + status decyzji; wpisy są archiwalne per skan (pod regresję i audyt).
- **ProjectSuggestion.Fingerprint**: deterministyczny podpis heurystyki dla konkretnego kandydata (używany do suppress/restore po `Reject/Delete`).
- **Project**: zaakceptowany projekt.
- **Tag** i **TagSuggestion**: tagowanie i sugestie.
- **ProjectTag**: relacja N:M między `Project` i `Tag`.
- **TagSuggestion** powinien mieć:
  - typ: `AssignExisting` / `CreateNew`,
  - źródło: `Heuristic` / `AI`,
  - status: `Pending` / `Accepted` / `Rejected`,
  - fingerprint do suppress powtórek.

Uwagi modelowe:
- Dla sugestii opartych o `.sln` planujemy przechowywać także metadane modułów (np. liczba/ścieżki), ale nadal jako jeden wpis projektu.

## 8. UI / widoki
Główne zakładki:
- **Scan**: rooty, start skanu, kolejka, postęp.
- **Project Organizer**: lista projektów, filtry, szczegóły.
- **Suggestions**: akceptacja/odrzucanie sugestii projektów i tagów.
- **Suggestions / Project suggestions**: przełącznik `Pending` (ze wszystkich skanów) / `Archive` (Accepted+Rejected), eksport archiwum do JSON i szybkie otwieranie folderu eksportów.
- **Suggestions / Regression**: dostępne akcje `Run regression report` oraz `Export regression JSON` (replay historycznych decyzji usera na `scan-<id>.json`).
- **Suggestions / panel actions**: akcje działają per panel (Project vs Tag), a bulk `Accept all` / `Reject all` są zabezpieczone dialogiem potwierdzenia.
- **Suggestions / Project suggestions**: w archiwum `Reject` jest ukryty; `Accept` może odwrócić wcześniejszy `Rejected`.
- **Project acceptance flow**: po `Accept` projektu otwieramy dialog uruchomienia heurystyk/AI tagów.
- **Project Organizer**: akcje na projekcie `Run tag heuristics` i `Run AI tag suggestions`.
- **Tags**: zarządzanie tagami i backfill.
- **Recent**: last_viewed / last_opened.

Makiety (Excalidraw) trzymamy w `docs/excalidraw/`, a PNG w `docs/images/`.

## 9. Testy
Piramida testów:
- **Unit (Engine)**: logika heurystyk, policy dla próbek, filtrowanie.
- **Integration (AppHost)**: DB + skan + JSON.
- **Integration (AppHost)**: DB + skan + JSON + regresja heurystyk względem historycznych decyzji (`Accepted`/`Rejected`) powiązanych z konkretnym `ScanSessionId` i jego `scan-<id>.json`.
- **E2E/Visual (Playwright)**: kluczowe ścieżki UI + snapshoty.
- **FE unit/component tests (ng test)**: pokrywają logikę bulk akcji (`setPendingStatusForAll`) i potwierdzenia dialogowe na stronie Suggestions.

Pre-commit jest realizowany przez `.githooks/pre-commit` (wymaga `core.hooksPath=.githooks`).
User-data replay regression jest osobną kategorią testów (`Category=UserDataRegression`) i jest uruchamiany ręcznie na realnej bazie użytkownika.

## 10. Roadmapa
Najbliższe i średnie kroki są w `BACKLOG.md`. Skrót:
- **Near Term (kolejność wdrożenia tagów):**
  1. `ProjectSuggestion -> Project` po `Accept` (znika z `Pending`, jest widoczny w `Project Organizer`).
  2. CRUD tagów (manualne dodawanie/edycja/usuwanie), aby istniał słownik tagów dla heurystyk i AI.
  3. Dialog po akceptacji projektu: `Run tag heuristics` / `Run AI tag suggestions` / `Skip`.
  4. Te same akcje uruchamiane ręcznie z `Project Organizer` dla istniejących projektów.
  5. `TagSuggestion` dla istniejących tagów (`AssignExisting`) i nowych propozycji (`CreateNew`), ze statusem i fingerprintem.
  6. Backfill po dodaniu nowego taga (manualnie lub po akceptacji AI): heurystyki zawsze, AI opcjonalnie.
- **Mid Term:** AI tag suggestions z wykorzystaniem historii odrzuceń, split/merge projektów, incremental scan.
- **Later:** AI do „one project vs many”, sync z backendem.

## 11. Zasady aktualizacji dokumentów
- `PLAN.md` opisuje aktualną specyfikację i stan projektu.
- `BACKLOG.md` to źródło zadań i kolejnych kroków.
- Makiety: Excalidraw -> PNG (opis w `AGENTS.md`).
