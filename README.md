# Plánovač podujatí — EventPlanner

## Odporúčaný stack

**C# / .NET 10 LTS · Avalonia UI · MVVM · EF Core SQLite · lokálne súbory · xUnit · GitHub Actions.**

Forma: samostatný desktop pre Windows x64, macOS Apple Silicon a Intel. Bez databázového servera, webového backendu a registrácie. Každá inštalácia má vlastné dáta. SQLite spravuje aplikácia ako interný binárny súbor. Distribúcia: Windows Setup EXE/ZIP, dva Mac DMG/ZIP, self-contained runtime. Presné stabilné verzie sa pripnú pri bootstrap fáze.

Pripraviť svadbu, turnaj alebo oslavu od hostí a ponúk po harmonogram a odovzdanie pokynov.

Pôvodný výber: nápad 10. Stav: **Etapa 1 a 2 implementované, z Etapy 3 úlohy** – zoznam úloh s termínmi a závislosťami, projekty podujatí, hostia/skupiny/RSVP, počty podľa častí programu, lokálny SQLite profil, záloha a obnova, desktop pre Windows a macOS; dodávatelia s porovnaním ponúk, rozpočet (plán / dohodnuté / uhradené) a platby so zálohami, refundmi a preplatkami. Ďalšie etapy pozri v [roadmape](ai/specs/07-roadmap-decisions.md).

## Vývoj

```bash
dotnet restore --locked-mode
dotnet test
dotnet run --project EventPlanner.Desktop
```

`EventPlanner --smoke-test` overí spustenie v izolovanom dočasnom profile. Balíky: `build/publish.ps1`, `build/package-windows.ps1`, `build/package-macos.sh`. Vydania sú bez podpisového certifikátu: Windows SmartScreen a macOS Gatekeeper pri prvom spustení zobrazia upozornenie.

## Kde začať

1. [Rozhodnutia a rámec](ai/specs/00-decisions.md)
2. [Produkt a rozsah](ai/specs/01-product.md)
3. [Architektúra a stack](ai/specs/08-architecture.md)
4. [Model a pravidlá](ai/specs/02-domain-data.md), [workflow](ai/specs/03-processing.md)
5. [Obrazovky](ai/specs/05-screens.md), [rozhrania](ai/specs/04-interfaces-integrations.md)
6. [Úložisko a obnova](ai/specs/09-storage-portability.md)
7. [Testy](ai/specs/06-verification.md), [etapy](ai/specs/07-roadmap-decisions.md)
8. [Distribúcia a GitHub Actions](ai/deployment/01-desktop-release.md)

[Kompletný index špecifikácií](ai/specs/README.md) · [Štruktúra](STRUCTURE.md) · [Navigácia pre implementáciu](ai/task-routing.md)

Všetky názvy tried, dátové polia a interné kontrakty sú návrhy. Dokumenty explicitne oddeľujú potvrdené požiadavky, navrhované rozhodnutia a technické veci na overenie.
