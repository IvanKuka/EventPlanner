# Štruktúra EventPlanner

Organizačný vzor: FinanceApp. Stav: **Etapa 1 a 2 implementované, z Etapy 3 úlohy** (projekt podujatia, hostia/RSVP, profil, záloha/obnova, desktop shell, packaging; dodávatelia, porovnanie ponúk, rozpočet a platby; zoznam úloh s termínmi). Ďalšie feature moduly sú plánované.

```text
EventPlanner/
  README.md / STRUCTURE.md / AGENTS.md / CLAUDE.md    vstupné dokumenty
  EventPlanner.sln
  global.json / Directory.Build.props / Directory.Packages.props / dotnet-tools.json (dotnet-ef)
  .editorconfig / .gitignore / .gitattributes (*.sh v LF)

  EventPlanner.Contracts/       Results (ErrorCode, Result<T>, ValidationIssue, Warning),
                                Money (MinorUnits), Events, Guests, Vendors, Budget, Tasks, Backup – príkazy a DTO
  EventPlanner.Core/            Models/{Events,Guests,Vendors,Budget,Tasks}, Services/{Events,Guests,Vendors,Budget,Tasks},
                                Validation (+ Events, Guests, Vendors, Budget, Tasks, MoneyValidation), Ports/IDataSession
  EventPlanner.Infrastructure/  Data (DbContext, Configurations, Migrations, stores, SqliteMaintenance),
                                Profiles (DataRoot, ProfileLock, ProfileOpener), Backup
  EventPlanner.Desktop/         Composition, Platform, Localization (Strings.resx), Styles,
                                ViewModels/{Shell,Events,Overview,Guests,Vendors,Budget,Tasks,Backup}, Views
  EventPlanner.Tests/           Unit, Integration/Storage, Ui (Avalonia headless), Fixtures (+ Schemas: zmrazené staré schémy)
  build/                        publish.ps1, smoke-test.ps1, package-windows.ps1,
                                package-macos.sh, validate-release.sh
  installer/                    windows/EventPlanner.iss, macos/Info.plist
  .github/workflows/            ci.yml, desktop-build.yml, release.yml
  ai/                           specs, knowledge, deployment, routing (pozri ai/structure.md)
```

## Vlastníctvo feature

| Modul | Stav | Kód |
|---|---|---|
| Events | Implementované | `EventPlanner.Core/Services/Events/`, `EventPlanner.Desktop/ViewModels/Events/` |
| Guests | Implementované | `EventPlanner.Core/Services/Guests/`, `EventPlanner.Desktop/ViewModels/Guests/` |
| Overview | Implementované | `EventPlanner.Desktop/ViewModels/Overview/` (počty z `GuestService`, súčty z `BudgetService`, najbližšie úlohy z `TaskService`) |
| Storage/Backup | Implementované | `EventPlanner.Infrastructure/{Data,Profiles,Backup}/`, `EventPlanner.Desktop/ViewModels/Backup/` |
| Vendors | Implementované | `EventPlanner.Core/Services/Vendors/` (`VendorService`, `QuoteComparisonService`), `EventPlanner.Desktop/ViewModels/Vendors/` |
| Budget | Implementované | `EventPlanner.Core/Services/Budget/` (`BudgetService`, `BudgetCalculator`), `EventPlanner.Desktop/ViewModels/Budget/` |
| Tasks | Implementované | `EventPlanner.Core/Services/Tasks/` (`TaskService`, `TaskPlanning`), `EventPlanner.Desktop/ViewModels/Tasks/` |
| Timeline, Seating | Plánované (Etapa 3) | – |
| Exports | Plánované (Etapa 4) | – |

Nezakladať priečinky plánovaných modulov naslepo; vytvárať ich až v ich implementačnom reze. Overené entry points: [ai/knowledge](ai/knowledge/core-boundaries.md).
