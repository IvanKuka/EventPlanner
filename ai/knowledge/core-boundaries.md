# Hranice jadra

Stav: Etapa 1 implementovaná a overená testami (17. 10. 2026); Etapa 2 (Vendors, Budget) implementovaná a overená testami (17. 10. 2026). Pravidlá: [model](../specs/02-domain-data.md), [workflow](../specs/03-processing.md).

## Vrstvy a závislosti

- `EventPlanner.Contracts` – bez závislostí. `Results/` (ErrorCode, OperationError, Result<T>, ValidationIssue + ValidationRules, Warning + WarningCodes), `Money/MinorUnits.cs` (ISO 4217 exponent, `MaxAmountMinor`), `Events/`, `Guests/`, `Vendors/`, `Budget/`, `Backup/`.
- `EventPlanner.Core` – závisí len na Contracts a `Microsoft.Extensions.Logging.Abstractions`. Žiadny EF, Avalonia ani filesystem.
- Port: `Core/Ports/IDataSession.cs` – `IDataSessionFactory.Open()` vráti krátku session s `Events` (IEventProjectStore), `Guests` (IGuestStore), `Vendors` (IVendorStore), `Budget` (IBudgetStore) a `SaveChangesAsync` → `SaveOutcome.Saved|Conflict` (Conflict = zastaraná revision alebo obsadený unikátny kľúč). Feature-špecifické metódy, nie generická repository.
- Spoločná kontrola zápisu: `Core/Services/Events/WritableEvent.cs` (existujúci, nearchivovaný projekt) – používajú ju Guests, Vendors aj Budget.

## Entry points

| Oblasť | Súbor | Poznámka |
|---|---|---|
| Projekt | `Core/Services/Events/EventProjectService.cs` | Create/Update/ChangeStatus, Add/Rename/RemoveProgramPart; kontrola revision a read-only archívu |
| Lifecycle | `Core/Models/Events/EventStatusRules.cs` | povolené prechody; Ready vyžaduje `ChecklistConfirmed` |
| Šablóny | `Core/Services/Events/EventTemplates.cs` | len predvolené časti programu |
| Hostia | `Core/Services/Guests/GuestService.cs` | CRUD hostí/skupín, `SetGroupRsvpAsync` (len vymenovaní členovia, all-or-nothing), varovanie duplicitného mena, zákaz cross-event väzieb |
| Počty | `Core/Services/Guests/GuestCountCalculator.cs` | počty podľa častí programu zo záznamov hostí |
| Dodávatelia | `Core/Services/Vendors/VendorService.cs` | CRUD dodávateľov (s `ExpectedRevision`) a ponúk, `DeclineQuoteAsync(note?)`, `ListDecisionsAsync(vendorId)` (najnovšie prvé); úprava prepojenej ponuky nemení rozpočet (`LinkedBudgetNotUpdated`) |
| Porovnanie/výber | `Core/Services/Vendors/QuoteComparisonService.cs`, `QuoteComparison.cs` | `CompareQuotesAsync(eventId, category)`, `SelectQuoteAsync(SelectQuote)` s `BudgetImpactPolicy` (Leave/Create/Update) a voliteľným dôvodom v jednej transakcii; zapíše `Selected` a pre nahradenú ponuku `Replaced` |
| Rozpočet | `Core/Services/Budget/BudgetService.cs` | `GetOverviewAsync`, CRUD položiek, `RecordPaymentAsync` (idempotentné podľa `OperationId`), `PostPaymentAsync`, `CancelPaymentAsync` |
| Súčty | `Core/Services/Budget/BudgetCalculator.cs` | čisté výpočty: paid = posted − refund, scheduled = planned, balance = committed − paid; súčty po mene |
| Modely | `Core/Models/Vendors/{Vendor,Quote,QuoteDecision}.cs`, `Core/Models/Budget/{BudgetItem,Payment}.cs` | `Quote.KnownTotalMinor` null pri neznámej doprave; `BudgetItem` je agregát platieb a pri každej zmene platby zvýši `Revision`; `QuoteDecision` je append-only záznam s cenou v čase rozhodnutia |
| Validácia | `Core/Validation/TextValidation.cs`, `MoneyValidation.cs`, `Validation/{Events,Guests,Vendors,Budget}/*Validator.cs` | limity 120/2000/100000, riadiace znaky, IANA zóna, ISO mena, sumy 0…`MaxAmountMinor` |

## Invarianty overené testami

- Revision: zastaraná `ExpectedRevision` → `Conflict` bez zápisu; súbeh dvoch sessions → druhý commit `Conflict` (EF concurrency token).
- Archivovaný projekt odmietne zmeny projektu, hostí, dodávateľov, ponúk a rozpočtu (`ReadOnly`); odomknutie = prechod Archived → Completed.
- Odstránenie časti programu ju odstráni z rozsahu všetkých hostí (DB cascade).
- Súkromná poznámka nie je v `GuestDto`; načíta sa len cez `GetGuestDetailAsync`.
- E-A04: plán 1000, záväzok 900, záloha 200, doplatok 700 (plánovaný → uhradený), refund 50 → uhradené 850, zostáva 50, plánované 0; preplatok je samostatná suma, nie nula a nezmenšuje dlh inej položky.
- Platba: rovnaký `OperationId` → jedna platba + `PaymentAlreadyRecorded`; iný obsah s tým istým id → `Conflict`; súbežný dvojitý submit → jedna platba (unikátny index + replay po konflikte); rovnaký dátum a suma s novým id je legitímna druhá platba. Refund len uhradený a nie vyšší ako uhradené platby; zrušenie platby, ktorá kryje refund, sa odmietne.
- E-A05: ponuky v dvoch menách sa neporovnávajú ani nesčítajú naprieč menou; neznáma doprava → `KnownTotalMinor = null` a `MissingFields`, nikdy nula; prázdne vylúčenia = „neuvedené“. Ponuka v cudzej mene v rozpočte ostane mimo súčtu projektu (`OtherCurrencyTotals`), `UpdateBudgetItem` s inou menou → `CurrencyMismatch`.
- Výber ponuky vráti predtým vybranú ponuku kategórie do Received; zmazanie dodávateľa zachová rozpočtovú položku (SET NULL na `SelectedQuoteId`).
- História rozhodnutí: výber/nahradenie/odmietnutie zapíše čas, cenu (`KnownTotalMinor` null pri neúplnej), menu, dopad na rozpočet a orezaný dôvod (prázdny = null, max 2000); neskoršia úprava ponuky históriu nezmení; opakované odmietnutie nepridá záznam. Popis v histórii je aktuálny názov ponuky. Zmazanie ponuky/dodávateľa zmaže aj jej históriu (cascade).
- Dodávateľ: zastaraná revízia pri úprave aj mazaní → `Conflict`, novšie dáta ostanú.

Testy: `EventPlanner.Tests/Unit/*` (vrátane `Unit/Budget/BudgetCalculatorTests.cs`, `Unit/Vendors/QuoteComparisonTests.cs`), `EventPlanner.Tests/Integration/Storage/EventProjectServiceTests.cs`, `GuestServiceTests.cs`, `BudgetServiceTests.cs`, `QuoteComparisonServiceTests.cs`, `BudgetPerformanceTests.cs`, `ProfileMigrationTests.cs`.
