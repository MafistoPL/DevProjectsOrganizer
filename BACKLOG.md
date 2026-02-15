# Backlog

## W trakcie
* (pusto)

## Teraz (najbliższy tydzień)
1. Dodać ręczne podpinanie tagów do istniejącego projektu oraz ręczne odpinanie tagów od projektu.
     * UI w `Project Organizer` powinno umożliwiać dodanie taga z listy istniejących tagów.
     * UI powinno umożliwiać odpięcie taga od projektu bez usuwania samego taga.
     * Dodać testy FE + AppHost integration dla flow attach/detach.
2. Dodać podstawowe wyszukiwanie projektów po tagach (prosty iloczyn / AND).
     * Wyszukiwanie ma zwracać projekty, które mają wszystkie wybrane tagi.
     * Na razie bez rankingów i bez zaawansowanej składni.
3. Wdrożyć flow ponownego przetwarzania tagów zgodny z aktualnym zachowaniem.
     * Heurystyki powinny pozostać ograniczone do sygnałów seedowanych/systemowych; nie sugerować automatycznego dopasowania dla tagów custom po ich utworzeniu.
     * Dopasowanie tagów custom powinno być AI-driven (manualny trigger), a nie automatyczny heurystyczny backfill.
4. Zdefiniować UX wsparcia AI dla tagów custom.
     * Dodać/potwierdzić manualną akcję dopasowania tagów utworzonych przez użytkownika do istniejących projektów przez AI.
     * Zachować gwarancje idempotencji i deduplikacji dla tworzonych wpisów `tag_suggestions`.
     * Zachować jawny krok akceptacji użytkownika przed przypięciem tagów.
5. Zwiększyć głębokość próbek treści skanu.
     * Zwiększyć liczbę próbkowanych linii na plik z `30` do `100` albo zrobić to konfigurowalne przy starcie skanu.
     * Jeśli konfigurowalne: wystawić opcję w Scan UI, utrwalać w metadanych scan request/snapshot i zabezpieczyć testami.

## Wkrótce (2-4 tygodnie)
* Pre-commit: zweryfikować konfigurację w dokumentacji (`core.hooksPath=.githooks`) dla każdego klonu/środowiska.

## Wkrótce (1-3 miesiące)
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
