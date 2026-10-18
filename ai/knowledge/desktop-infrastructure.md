# Desktop a infraštruktúra

Stav: Etapa 1 a 2 implementované (17. 10. 2026). UI pravidlá: [obrazovky](../specs/05-screens.md), [spoločné UI](../specs/10-shared-ui.md); úložisko: [lokálne dáta](../specs/09-storage-portability.md); distribúcia: [release](../deployment/01-desktop-release.md).

## Infrastructure

| Oblasť | Súbor | Poznámka |
|---|---|---|
| DbContext | `Infrastructure/Data/EventPlannerDbContext.cs`, `Data/Configurations/*` | jedna konfigurácia na entitu, enumy ako string, `Revision` concurrency token |
| Migrácie | `Infrastructure/Data/Migrations/` | `InitialProfile` (Etapa 1), `VendorsAndBudget` (Etapa 2, len pridáva tabuľky Vendors, Quotes, BudgetItems, Payments), `VendorRevisionAndDecisions` (stĺpec `Vendors.Revision` s predvolenou 1 a tabuľka `QuoteDecisions`), `Tasks` (tabuľka úloh, len pridáva). `dotnet ef migrations add <Name> --project EventPlanner.Infrastructure --output-dir Data/Migrations` |
| Session | `Data/DataSession.cs`, `Data/{EventProject,Guest,Vendor,Budget}Store.cs` | concurrency aj SQLite unique/PK/FK violation → `SaveOutcome.Conflict` |
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
| Neočakávané chyby | `App.axaml.cs` (`Dispatcher.UIThread.UnhandledException`), `Composition/UnexpectedErrors.cs` – každá sa zaloguje; prežiteľná (všetko okrem runtime porúch ako OutOfMemory, AccessViolation, TypeLoad) sa označí ako handled, shell ukáže pruh `UnexpectedErrorMessage` a znova načíta otvorenú stránku; zápisy sú transakčné, takže dáta ostanú konzistentné |
| Shell, štart a recovery | `ViewModels/Shell/ShellViewModel.cs`, `Views/MainWindow.axaml(.cs)` – záložky `TabPages` (Prehľad, Hostia, Dodávatelia, Rozpočet; `ListBox.tabs` cez `SelectedTab`), tlačidlo Projekty vpravo (Flyout nad `ProjectsPage.Projects`), Záloha a Vzhľad; pod záložkami riadok aktívneho projektu (`ShowProjectBar`, názov, stav, typ, dátum, miesto, Upraviť projekt) a názov projektu v titulku okna; projekty a záloha sú stránky mimo záložiek (`ProjectsPage`, `BackupPage`). Po zmene aktívneho projektu shell znova aktivuje otvorenú záložku (stránky čítajú dáta len v `ActivateAsync`). Bez projektu sa otvorí editor projektu; Nový projekt z inej stránky drží prázdny formulár (`EventsViewModel` pri aktivácii neprepíše rozpracovaný nový projekt). Ikony záložiek: `Views/PageIconConverter.cs` |
| Stránky | `ViewModels/{Events,Overview,Guests,Vendors,Budget,Tasks,Backup}/`, `Views/*View.axaml` (mapované v `App.axaml`) |
| Peniaze v UI | `Localization/MoneyText.cs` – presné parsovanie do minor units (viac desatinných miest = chyba, nie zaokrúhlenie), prázdne = neznáme |
| Texty | `Localization/Strings.resx` (strongly typed), `ErrorText.cs` (chyby, pravidlá, varovania), `EnumText.cs` |
| Voľba témy | `Composition/ThemePreferenceStore.cs` (`AppTheme` System/Light/Dark v `<data root>/ui-theme.txt`, mimo profilov, atomický zápis, neznáma hodnota = System), `ShellViewModel.SetThemeCommand`, aplikuje `IPlatformServices.ApplyTheme` (`Application.RequestedThemeVariant`) ešte pred otvorením profilu; tlačidlo Vzhľad v hlavičke |
| Téma | `App.axaml` – paleta `App*Brush`, `Message*Brush` a `Status*Brush` pre Light/Dark (sivé/biele plochy, akcent `#BE185D`) a `FluentTheme.Palettes`; `Styles/AppStyles.axaml` – tlačidlá, polia, zoznamy, karty, záložky; stavy pointerover/focus/selected sa štýlujú na častiach šablóny Fluent (`/template/ ContentPresenter#PART_ContentPresenter`, `Border#PART_BorderElement`, `Border#Background`). Fluent `SystemFillColor*Brush` sa vo svetlej téme nevyriešil, preto vlastné stavové farby. |
| Ikony | `Styles/Icons.axaml` (vlastné čiarové `StreamGeometry` na mriežke 16 px), `Path.icon` kreslí zdedenou farbou textu (`$self.(TextElement.Foreground)`; `$parent[ContentPresenter].Foreground` nefungoval); tlačidlo s ikonou: `views:ButtonIcon.Data` (`Views/ButtonIcon.cs`) |
| Pás nástrojov | `Border.ribbon` so skupinami `StackPanel.ribbon-group` (popis `TextBlock.ribbon-caption`, oddeľovač `Border.ribbon-divider`) a tlačidlami `Button.command` + `views:ButtonIcon.Data` (veľká ikona nad textom); hlavné akcie stránky (nový, zmazať, zrušiť platbu, záloha/obnova) sú v páse, ukladanie a potvrdenie zmazania ostávajú pri formulári |
| OS integrácia | `Platform/IPlatformServices.cs`, `AvaloniaPlatformServices.cs` (picker, launcher, téma, shutdown) |
| Jedna inštancia | `Platform/SingleInstanceChannel.cs` – lokálna named pipe (macOS: Unix socket) len pre aktuálneho používateľa, názov z hashu cesty profilu; otvorený profil počúva hneď po získaní zámku, druhé spustenie pri `Locked` požiada o zobrazenie okna (obnoví z minimalizácie, `Activate`) a skončí s 0; bez odpovede zobrazí Locked. Prijíma sa len pevný token, nič sa nepoužije ako dáta |

## Testy

- `Integration/Storage/ProfileOpenerTests.cs` – lock, novšia schéma bez zmeny súboru, poškodená DB, WAL/FK.
- `Integration/Storage/ProfileBackupServiceTests.cs` – záloha → obnova → čítanie entít; traversal, hash, neznáma verzia/schéma/appId, poškodený komprimovaný obsah (`CorruptData`, nie výnimka). Manifest sa číta ohraničene (max 1 MB skutočne prečítaných bajtov, nie deklarovaná dĺžka).
- `Ui/BackupViewModelTests.cs` – nezapisovateľný cieľ zálohy je hlásenie, nie pád aplikácie.
- `Ui/UnexpectedErrorTests.cs` – ktoré výnimky aplikácia prežije, pruh s hlásením a jeho zavretie. Napojenie na dispatcher (`App.OnFrameworkInitializationCompleted`) sa v headless testoch nespúšťa.
- Mazanie, ktoré odstráni aj ďalšie údaje (projekt, dodávateľ, ponuka, skupina, položka rozpočtu, hosť), sa vždy najprv potvrdzuje; projekt navyše napísaním názvu.
- `Integration/Storage/ProfileMigrationTests.cs` – profily zo zmrazených schém `Tests/Fixtures/Schemas/InitialProfile.sql`, `VendorsAndBudget.sql` a `VendorRevisionAndDecisions.sql` (vydanie 1.0.0) (+ `.seed.sql`) → overená predmigračná záloha so starou schémou a dátami → migrácia → zachované entity a funkčný rozpočet; druhé otvorenie bez ďalšej zálohy; `.epbackup` so schémou `InitialProfile` (manifest zapísaný doslovne) sa obnoví do nového profilu a zmigruje. Fixture sa nikdy negeneruje znova; každá ďalšia migrácia pridá zmrazenú schému predchádzajúcej verzie.
- `Integration/Storage/BudgetPerformanceTests.cs` – E-A09 časť rozpočtu: prehľad 1000 platieb (50 položiek) s presnými súčtami; hranica 1 s je ochrana pred regresiou (N+1), namerané časy sú vo výstupe testu (lokálne cca 40 ms studený / 15 ms teplý).
- `Ui/StatusColorTests.cs` – triedy `field-error`, `notice`, `positive` majú vo svetlej aj tmavej téme vlastnú farbu.
- `Integration/Platform/SingleInstanceChannelTests.cs` – skutočná pipe medzi dvoma kanálmi rovnakého profilu (dve žiadosti po sebe), bez bežiacej inštancie `false`, iný profil = iný kanál; `ShellFlowTests` – druhé spustenie zamknutého profilu ukončí appku bez chyby, otvorený profil počúva.
- `Ui/ThemePreferenceTests.cs` – predvolená systémová téma, okamžité použitie a obnovenie po reštarte, neznáma uložená hodnota.
- Snímky v testoch používajú rovnaké písmo Inter ako aplikácia (`TestAppBuilder.WithInterFont`); bez neho sa orezanie textu na snímkach neprejavilo.
- `Ui/IconTests.cs` – každá ikona záložiek a tlačidiel má geometriu aj farbu čiary (prázdna väzba by ikonu potichu skryla).
- `Ui/ShellFlowTests.cs` – prepnutie projektu v hlavičke znova načíta otvorenú záložku, bez projektu sa otvorí editor projektu mimo záložiek; Avalonia headless tok projekt → hostia → skupinové RSVP → prehľad → záloha; tok dodávatelia → ponuky v dvoch menách → výber do rozpočtu → záloha/doplatok/refund/preplatok; zamknutý profil. `EVENTPLANNER_SCREENSHOTS=<dir>` uloží snímky.

## Známe medzery (neoverené alebo neimplementované)

- Druhé spustenie aktivuje bežiace okno len pri verzii so `SingleInstanceChannel`; inštancia staršieho buildu neodpovie a druhé spustenie zobrazí stav Locked. Prenesenie okna dopredu na macOS nebolo overené na reálnom Macu.
- Prepnutie na obnovený profil vyžaduje reštart aplikácie.
- Dialóg Uložiť/Zahodiť/Zrušiť pri neuložených úpravách nie je implementovaný.
- Detail dodávateľa zatiaľ nemá prílohy (05-screens; čakajú na interný blob store podľa 09, Etapa 4/5). História rozhodnutí je implementovaná; ponuky vybrané pred migráciou `VendorRevisionAndDecisions` v nej nemajú záznam.
- E-A09 je overené iba pre rozpočet (1000 platieb); hostia 2000, úlohy, prílohy 1 GB a streaming export čakajú na príslušné moduly.
- macOS balíky a Inno Setup installer neboli zostavené lokálne (iba v CI na natívnych runneroch). Lokálny git repozitár (vetva `main`, workflowy spúšťa push na `main`) je založený 17. 10. 2026 bez commitu a bez remote, takže CI zatiaľ nebežalo. `.gitattributes` drží `*.sh` v LF.
