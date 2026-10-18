# Sledovateľnosť požiadaviek a dodávky

Každý riadok je akceptovateľný výsledok, nie iba zoznam technických úloh. Test ID sú definované v [06-verification.md](06-verification.md). Stav pri riadkoch: Implemented/Verified/Planned podľa dôkazov.

| ID | Schopnosť | Scenáre | Vlastník | Podmienka vydania |
|---|---|---|---|---|
| E01 | Projekt podujatia | E-A06, E-A08 | Events | Časová zóna a prenos kópie – **Implemented** (projekt, IANA zóna, lifecycle); prenos kópie Planned |
| E02 | Hostia/RSVP | E-A01, E-A02 | Guests | Skupinové výnimky a privátne polia – **Verified** E-A01 (unit + UI headless); E-A02 čaká na Seating |
| E03 | Ponuky | E-A05 | Vendors | Neznáma cena sa nepovažuje za nulu – **Verified** E-A05 (unit + SQLite + UI headless); história rozhodnutia **Verified** (SQLite + UI headless); prílohy Planned (Etapa 5) |
| E04 | Rozpočet a platby | E-A04, E-A05 | Budget | Idempotencia a refund pravidlá – **Verified** E-A04, E-A05 (unit + SQLite + UI headless); E-A09 časť 1000 platieb **Verified** (SQLite), zvyšok E-A09 Planned |
| E05 | Úlohy/harmonogram | E-A06 | Tasks, Timeline | Zákaz cyklov, DST validácia – **Verified** úlohy a zákaz cyklov (unit + SQLite + UI headless); harmonogram a DST Planned |
| E06 | Stoly a kapacity | E-A02, E-A03 | Seating | Transakčná kapacita |
| E07 | Odovzdanie/export | E-A07, E-A08, E-A09, E-A10 | Exports | Allowlist súkromia a bezpečné HTML/CSV |

## Spoločné dodávky

| Oblasť | Povinný dôkaz |
|---|---|
| Bootstrap | Overené presné verzie, licencia, navrhnutá štruktúra, prvý funkčný build – **Verified** (.NET SDK 10.0.300, Avalonia 12.1.4, EF Core 10.0.13, xUnit v3 3.2.2; locked restore) |
| Úložisko | Uložiť/zavrieť/znovu otvoriť; starý profil migrovaný; zlyhanie zachová originál – round-trip, novšia schéma, poškodená DB a migrácia starého profilu (InitialProfile → VendorsAndBudget s overenou predmigračnou zálohou) **Verified** |
| Recovery | Fault fixture na kritických hraniciach a zdokumentovaný manuálny postup |
| Platformy | Windows a oba Macy, zabalený smoke aj clean-machine test – win-x64 publish + smoke **Verified** lokálne; osx-arm64/osx-x64 publish kompiluje, balík a smoke len v CI (Planned); clean-machine test Planned |
| Release | Šesť balíkov, manifest, hashe, licencie, kompletná test matrix |
| Dokumentácia | Aktuálne STRUCTURE, routing, index, knowledge a relevantné specs |

## Politika validácie kontraktov

Navrhované default limity: názov 1–120 znakov, krátky popis 0–2000, dlhá poznámka 0–100000; explicitné menšie/väčšie doménové limity majú prednosť. Text normalizovať iba pre porovnávanie, bez neočakávanej zmeny používateľského obsahu. Zakázaný NUL a nepodporované control characters; Markdown text môže obsahovať nové riadky a taby. UUID musí byť platné a každá väzba musí patriť rovnakému projektu/inventáru. Hodnota null znamená neznáme/nezadané, nie nula.

Error codes návrhu: ValidationFailed, Conflict, MissingSource, UnsupportedInput, PermissionDenied, InsufficientSpace, CorruptData, UnsupportedVersion, DependencyUnavailable, Interrupted a Cancelled. Dopĺňať úzke doménové kódy podľa workflow, nezamieňať konflikt s neočakávanou výnimkou. UI chybu preloží a priradí k poľu alebo položke, log zachová operationId bez payloadu.

Import je all-or-nothing v rámci aktivácie nového projektu. Dávka externých operácií môže skončiť čiastočne, ale musí presne evidovať položkové výsledky. Optimistic revision sa overuje pred commitom, nie len na UI. Úloha má idempotency key tam, kde opakované odoslanie môže vytvoriť duplicitný externý či finančný účinok.

## Technické riziká a ich uzavretie

- Neoverená knižnica alebo CLI: pripnúť verziu, primary docs a integračný fixture pred implementovaním závislej obrazovky.
- Platformový rozdiel: test na skutočnom RID; headless simulácia nestačí na inštalátor alebo systémový picker.
- Zmena schémy: predmigračný snapshot a restore test, nie iba successful migration log.
- Rozšírenie rozsahu: synchronizácia, platené API, cloud či serverový zápis vyžadujú nové produktové rozhodnutie.
- Falošná dokumentácia: po každej etape vyznačiť Implemented/Verified/Planned podľa dôkazov; netvrdiť hotovú funkciu len podľa názvu súboru.
