# Backlog

## Now (najbliższe 2–4 tygodnie)
- Dodać ignorowanie artefaktów build/IDE (`bin/`, `obj/`, `.vs/`, `.idea/`, `node_modules/`, `*.pdb`, `*.obj`, `*.tlog`, `*.exe`, `*.suo`).
- Heurystyki wykrywania projektów (markery: `.sln`, `.csproj`, `package.json`, `CMakeLists.txt`, `Makefile`, `pom.xml`, `build.gradle`, `.git`).
- Pipeline: scan JSON -> `ProjectSuggestion` + zapis do SQLite + UI listy sugestii.
- Root‑badges w Scan: liczba projektów + liczba ongoing sugestii.
- Podsumowanie skanu per root (ostatni stan, czas, liczba plików).
- UI kolejki skanów i `queue reason` (jeśli nie jest jeszcze w pełni widoczne w UI).
- Po zakończonym skanie przycisk `Stop` powinien znikać (potwierdzić UX i domknąć).
- Pre‑commit: uruchamianie testów i blokada commita, jeśli testy nie przechodzą.

## Soon (1–3 miesiące)
- Tag suggestions (heurystyki, bez AI na start).
- Manual split/merge projektów w obrębie rootów.
- Lepsze rankowanie i filtrowanie sugestii (score, język, wiek).
- Incremental scan po `last_modified_at` + cache.
- Konfigurowalne heurystyki (GUI lub YAML) + parser i runtime dla reguł.

## Later
- AI do rozstrzygania „one project vs many” z limitem kontekstu.
- Zdalny backend + synchronizacja danych klienta.
- Full‑text search po metadanych skanów.

## Done (recent)
- IPC dla skanów (start/stop/pause/resume) + ScanSession w SQLite.
- Per‑disk lock i blokada skanów podczas whole‑scan.
- Snapshot skanu do JSON z próbkami treści.
- Pulsujący pasek postępu dla skanu `Running`.
