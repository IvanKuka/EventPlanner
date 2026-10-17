# Rozhodnutia a záväzný rámec

Stav: podrobný návrh na implementáciu, vytvorený 17. 10. 2026; aplikácia zatiaľ neexistuje.

| ID | Rozhodnutie | Pôvod a dopad |
|---|---|---|
| D01 | Samostatný desktop pre Plánovač podujatí | Používateľ preferuje vlastnú appku každého človeka, nie povinné webové účty |
| D02 | C#/.NET 10 + Avalonia + lokálny SQLite | Preferencia C#, bez databázového servera; SQLite bol používateľom povolený podľa vhodnosti |
| D03 | Windows x64 + Apple Silicon + Intel Mac | Výslovná požiadavka vrátane installerov a GitHub build/release |
| D04 | Bez platených podpisových certifikátov v prvej verzii | Používateľ potvrdené; distribučné upozornenia sú dokumentované |
| D05 | Offline a súkromné dáta na zariadení | Domácnosť, prípadný server cez LAN/VPN; žiadny povinný nový backend |
| D06 | Lokálny AI prepis, cloud API až neskôr | Výslovne potvrdené pre Media dielňu; ostatné appky AI nevyžadujú |
| D07 | Organizácia a ai/ podľa FinanceApp | Prieskum konkrétnych referenčných docs; prispôsobenie desktopu, nie slepá kópia webu |
| D08 | Snapshot prenos, automatický merge mimo MVP | Dizajnové rozhodnutie na zachovanie samostatnosti bez servera; vyplýva zo zmeny pôvodného webového návrhu |

## Čo nie je potvrdený fakt

Presná zostava servera bola čítaná z dokumentácie, nie z runtime. Konkrétne upstream endpointy, zvolené natívne binárky, prehrávač, PDF renderer a presné verzie NuGet balíkov sa overujú pred bootstrapom. Názvy modelov/portov v tomto balíku sú návrh nového softvéru. Minimálny OS musí prejsť overením pripnutých závislostí a čistého balíka; dokumentovaná cieľová matica sama nie je dôkaz testu.

## Rozsah oprávnenia

Tento balík pripravuje dokumentáciu. Neautorizuje deploy, nákup certifikátov, publikovanie GitHub release, kontaktovanie ľudí, spúšťanie služieb na serveri ani zásahy do súborov používateľa. Budúca implementácia musí rešpektovať konkrétne zadanie a bezpečné testovacie korene.
