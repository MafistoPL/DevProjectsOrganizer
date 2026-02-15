# Backlog

## W trakcie
* Brak aktywnych zadań.

## Teraz (najbliższy tydzień)
1. Wdrożyć flow ponownego przetwarzania tagów zgodny z aktualnym zachowaniem.
     * Heurystyki powinny pozostać ograniczone do sygnałów seedowanych/systemowych; nie sugerować automatycznego dopasowania dla tagów custom po ich utworzeniu.
     * Dopasowanie tagów custom powinno być AI-driven (manualny trigger), a nie automatyczny heurystyczny backfill.
2. Zdefiniować UX wsparcia AI dla tagów custom.
     * Dodać/potwierdzić manualną akcję dopasowania tagów utworzonych przez użytkownika do istniejących projektów przez AI.
     * Zachować gwarancje idempotencji i deduplikacji dla tworzonych wpisów `tag_suggestions`.
     * Zachować jawny krok akceptacji użytkownika przed przypięciem tagów.

## Wkrótce (2-4 tygodnie)
* Pre-commit: zweryfikować konfigurację w dokumentacji (`core.hooksPath=.githooks`) dla każdego klonu/środowiska.
* Tryb „nasłuchiwany root” + automatyczne reskany:
  * Możliwość oznaczenia rootów jako nasłuchiwane.
  * Automatyczny reskan nasłuchiwanych rootów po starcie aplikacji.
  * Dla rootów typu Desktop/duże katalogi: niski `depth limit` (np. `1-3`) konfigurowany per root.

## Wkrótce (1-3 miesiące)
* Nasłuchiwanie zmian w projektach i reskan per-projekt:
  * Nasłuchiwanie zmian plików dla zaakceptowanych projektów.
  * Po zmianach: uruchomienie reskanu tylko tego projektu, odświeżenie metadanych i sugestii tagów.
  * Po reskanie heurystycznym: sugestia/call-to-action czy uruchomić też skan AI tagów.
* Ścieżka docelowa przechowywania projektów + flow przenoszenia:
  * Ustawienie globalnej ścieżki „workspace/projects storage”.
  * Przy akceptacji sugestii projektu: pytanie czy przenieść projekt do tej ścieżki.
  * W `Project Organizer`: akcja `Move project` na kafelku projektu (ręczne przeniesienie istniejącego projektu).
* Sugestie tagów AI z użyciem: istniejących tagów + opcjonalnej historii odrzuconych propozycji nowych tagów.
* Ręczny split/merge wykrytych projektów wewnątrz roota.
* Lepszy ranking i filtrowanie sugestii (score, język, wiek).
* Skanowanie przyrostowe oparte o `last_modified_at` + cache.
* Konfigurowalne heurystyki (GUI lub YAML) + parser i runtime.
* Wyrównać interfejsy `Engine/Scanning` z realną implementacją runtime w AppHost (z czasem usunąć zduplikowane kontrakty skanu).

## Później
* AI dla rozstrzygania „jeden projekt vs wiele” z ograniczonym kontekstem.
* Zdalny backend + synchronizacja klienta.
* Wyszukiwanie pełnotekstowe po metadanych skanów.

## Do wydzielenia
* Dodać miejsce do przechowywania PAT do pracy z gh; trzeba ustalić bezpieczny sposób.

## Historia zrealizowanych zadań
Szczegółowa historia: `BACKLOG_DONE.md`.
