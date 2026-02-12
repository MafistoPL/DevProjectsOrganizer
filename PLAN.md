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
- **Status sugestii:** enum `Pending` / `Accepted` / `Rejected`.
- **Tryby skanu:** `roots`, `changed`, `whole`.
- **Harmonogram:** per‑disk lock; whole‑scan blokuje inne skany.
- **UI:** Scan view z start/stop/pause/resume, stanami i kolejką.

## 3. Architektura (FE/BE)
- **Engine**: logika domenowa i skanowanie (docelowo heurystyki detekcji i tagów).
- **AppHost**: host desktopowy + IPC + persystencja (EF Core / SQLite).
- **UI (Angular)**: widoki i interakcja z AppHost przez IPC.
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

Niejasne przypadki:
- folder bez markerów i wiele plików -> `Collection` + opcjonalne `SingleFileMiniProject`.
- opcjonalne rozstrzyganie z udziałem AI (limitowany kontekst).

## 6. Tagowanie
Heurystyki tagów (v1):
- markery i histogram rozszerzeń,
- nazwy katalogów (np. `course`, `tutorial`, `katas`),
- struktura (`src/`, `tests/`).

AI (opcjonalnie, później):
- tylko `TagSuggestion` (bez auto‑aplikacji),
- limitowany kontekst (drzewo + krótkie próbki).

Konfigurowalność:
- rozważamy GUI/YAML dla reguł heurystyk (wymaga parsera + runtime).

## 7. Model danych (v1)
Minimalny zestaw:
- **Root**: ścieżka, status, statystyki skanu.
- **ScanSession**: tryb, stan, liczba plików, postęp, output JSON.
- **ProjectSuggestion**: kandydat projektu + metadane wykrycia.
- **ProjectSuggestion**: kandydat projektu + status decyzji; wpisy są archiwalne per skan (pod regresję i audyt).
- **Project**: zaakceptowany projekt.
- **Tag** i **TagSuggestion**: tagowanie i sugestie.

## 8. UI / widoki
Główne zakładki:
- **Scan**: rooty, start skanu, kolejka, postęp.
- **Project Organizer**: lista projektów, filtry, szczegóły.
- **Suggestions**: akceptacja/odrzucanie sugestii projektów i tagów.
- **Tags**: zarządzanie tagami i backfill.
- **Recent**: last_viewed / last_opened.

Makiety (Excalidraw) trzymamy w `docs/excalidraw/`, a PNG w `docs/images/`.

## 9. Testy
Piramida testów:
- **Unit (Engine)**: logika heurystyk, policy dla próbek, filtrowanie.
- **Integration (AppHost)**: DB + skan + JSON.
- **Integration (AppHost)**: DB + skan + JSON + regresja heurystyk względem historycznych decyzji (`Accepted`/`Rejected`).
- **E2E/Visual (Playwright)**: kluczowe ścieżki UI + snapshoty.

Pre-commit jest realizowany przez `.githooks/pre-commit` (wymaga `core.hooksPath=.githooks`).

## 10. Roadmapa
Najbliższe i średnie kroki są w `BACKLOG.md`. Skrót:
- **Near Term:** UI pod realne sugestie z DB, akcje Accept/Reject + debug export, root‑badges.
- **Mid Term:** tag suggestions, split/merge projektów, incremental scan.
- **Later:** AI do „one project vs many”, sync z backendem.

## 11. Zasady aktualizacji dokumentów
- `PLAN.md` opisuje aktualną specyfikację i stan projektu.
- `BACKLOG.md` to źródło zadań i kolejnych kroków.
- Makiety: Excalidraw -> PNG (opis w `AGENTS.md`).
