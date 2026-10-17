# Desktop a infraštruktúra

Stav: Etapa 1 a 2 implementované (17. 10. 2026). UI pravidlá: [obrazovky](../specs/05-screens.md), [spoločné UI](../specs/10-shared-ui.md); úložisko: [lokálne dáta](../specs/09-storage-portability.md); distribúcia: [release](../deployment/01-desktop-release.md).

## Infrastructure

| Oblasť | Súbor | Poznámka |
|---|---|---|
| DbContext | `Infrastructure/Data/EventPlannerDbContext.cs`, `Data/Configurations/*` | jedna konfigurácia na entitu, enumy ako string, `Revision` concurrency token |
| Migrácie | `Infrastructure/Data/Migrations/` | `InitialProfile` (Etapa 1), `VendorsAndBudget` (Etapa 2, len pridáva tabuľky Vendors, Quotes, BudgetItems, Payments), `VendorRevisionAndDecisions` (stĺpec `Vendors.Revision` s predvolenou 1 a tabuľka `QuoteDecisions`). `dotnet ef migrations add <Name> --project EventPlanner.Infrastructure --output-dir Data/Migrations` |
| Session | `Data/DataSession.cs`, `Data/{EventProject,Guest,Vendor,Budget}Store.cs` | concurrency aj SQLite unique/PK violation → `SaveOutcome.Conflict` |
| Pripojenie | `Data/ProfileDatabase.cs` | foreign keys on, pooling off, busy timeout 5 s |
| SQLite údržba | `Data/SqliteMaintenance.cs` | online backup API, integrity/FK check, WAL |
| Dátový koreň | `Profiles/DataRoot.cs` | `profiles/<name>/eventplanner.db`, `active-profile.txt`, `logs/`, `staging/` |
| Lock | `Profiles/ProfileLock.cs` | exkluzívny `profile.lock` (Windows share-deny, macOS flock) |
| Štart profilu | `Profiles/ProfileOpener.cs` | lock → novšia schéma? → space check + predmigračná záloha (5 posledných) → migrate → WAL → integrity → Ready |
| Záloha | `Backup/ProfileBackupService.cs`, `BackupArchiveReader.cs`, `BackupManifest.cs` | `.epbackup` ZIP: `manifest.json` + `data/eventplanner.db`; overenie pred publikovaním, obnova do nového profilu |

## Desktop

| Oblasť | Súbor |
|---|---|
| Vstup, `--smoke-test` | `Desktop/Program.cs`, `Composition/AppOptions.cs`, `App.axaml.cs` |
| DI a logy (Serilog file, 7 dní, 3 MB/súbor) | `Composition/AppServices.cs` |
| Shell, štart a recovery | `ViewModels/Shell/ShellViewModel.cs`, `Views/MainWindow.axaml` |
| Stránky | `ViewModels/{Events,Overview,Guests,Vendors,Budget,Backup}/`, `Views/*View.axaml` (mapované v `App.axaml`) |
| Peniaze v UI | `Localization/MoneyText.cs` – presné parsovanie do minor units (viac desatinných miest = chyba, nie zaokrúhlenie), prázdne = neznáme |
| Texty | `Localization/Strings.resx` (strongly typed), `ErrorText.cs` (chyby, pravidlá, varovania), `EnumText.cs` |
| Stavové farby | `App.axaml` (`StatusErrorBrush`, `StatusNoticeBrush`, `StatusPositiveBrush` pre Light/Dark), použité v `Styles/AppStyles.axaml` (`field-error`, `notice`, `positive`); Fluent `SystemFillColor*Brush` sa vo svetlej téme nevyriešil |
| OS integrácia | `Platform/IPlatformServices.cs`, `AvaloniaPlatformServices.cs` (picker, launcher, shutdown) |

## Testy

- `Integration/Storage/ProfileOpenerTests.cs` – lock, novšia schéma bez zmeny súboru, poškodená DB, WAL/FK.
- `Integration/Storage/ProfileBackupServiceTests.cs` – záloha → obnova → čítanie entít; traversal, hash, neznáma verzia/schéma/appId.
- `Integration/Storage/ProfileMigrationTests.cs` – profily zo zmrazených schém `Tests/Fixtures/Schemas/InitialProfile.sql` a `VendorsAndBudget.sql` (+ `.seed.sql`) → overená predmigračná záloha so starou schémou a dátami → migrácia → zachované entity a funkčný rozpočet; druhé otvorenie bez ďalšej zálohy; `.epbackup` so schémou `InitialProfile` (manifest zapísaný doslovne) sa obnoví do nového profilu a zmigruje. Fixture sa nikdy negeneruje znova; každá ďalšia migrácia pridá zmrazenú schému predchádzajúcej verzie.
- `Integration/Storage/BudgetPerformanceTests.cs` – E-A09 časť rozpočtu: prehľad 1000 platieb (50 položiek) s presnými súčtami; hranica 1 s je ochrana pred regresiou (N+1), namerané časy sú vo výstupe testu (lokálne cca 40 ms studený / 15 ms teplý).
- `Ui/StatusColorTests.cs` – triedy `field-error`, `notice`, `positive` majú vo svetlej aj tmavej téme vlastnú farbu.
- `Ui/ShellFlowTests.cs` – Avalonia headless tok projekt → hostia → skupinové RSVP → prehľad → záloha; tok dodávatelia → ponuky v dvoch menách → výber do rozpočtu → záloha/doplatok/refund/preplatok; zamknutý profil. `EVENTPLANNER_SCREENSHOTS=<dir>` uloží snímky.

## Známe medzery (neoverené alebo neimplementované)

- Druhé spustenie neaktivuje existujúce okno, len zobrazí stav Locked.
- Prepnutie na obnovený profil vyžaduje reštart aplikácie.
- Dialóg Uložiť/Zahodiť/Zrušiť pri neuložených úpravách nie je implementovaný.
- Detail dodávateľa zatiaľ nemá prílohy (05-screens; čakajú na interný blob store podľa 09, Etapa 4/5). História rozhodnutí je implementovaná; ponuky vybrané pred migráciou `VendorRevisionAndDecisions` v nej nemajú záznam.
- E-A09 je overené iba pre rozpočet (1000 platieb); hostia 2000, úlohy, prílohy 1 GB a streaming export čakajú na príslušné moduly.
- macOS balíky a Inno Setup installer neboli zostavené lokálne (iba v CI na natívnych runneroch). Lokálny git repozitár (vetva `main`, workflowy spúšťa push na `main`) je založený 17. 10. 2026 bez commitu a bez remote, takže CI zatiaľ nebežalo. `.gitattributes` drží `*.sh` v LF.
