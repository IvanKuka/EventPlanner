# Lokálne dáta, migrácie, zálohy a prenos

## Rozhodnutie

SQLite cez EF Core SQLite provider zodpovedajúci .NET/EF Core 10; jedna lokálna databáza v profile aplikácie, žiadny PostgreSQL a žiadny sieťový databázový server. SQLite súbor je binárny, nie textový. Cesty k údajom sa získavajú platformovým API; Windows LocalApplicationData/<AppName>, macOS Application Support/<AppName>. Projektové prenosné súbory sa ukladajú tam, kam si používateľ vyberie, nie do Program Files alebo .app bundle.

V každej novej databáze zapnúť foreign keys, preveriť WAL režim, rozumný busy timeout a krátke transakcie. Jedna inštancia zapisovača na profil cez overený cross-platform profile lock; ďalšie spustenie aktivuje existujúce okno alebo bezpečne odmietne zápis. Zámok procesu nie je riešenie sieťovej spolupráce. Podpora viacerých okien musí ísť cez rovnakú aplikačnú službu a optimistic revision.

Živý profil musí byť na lokálnom volume. Nepodporovať živé SQLite na sieťovom share alebo v synchronizovanom priečinku ako rodinnú synchronizáciu. Záloha/export sa môže následne preniesť cez súborový sync, ale iba po uzavretí konzistentného snapshotu. Backup/transfer UI to jasne odlišuje.

## Technická správa schémy

Nové projekty používajú explicitné verzované EF Core migrácie a entity configuration po vzore organizačného členenia FinanceApp. Ide o vedomú odchýlku od jeho historických kompatibilitných SQL. Jedna konfigurácia na entitu, foreign keys a indexy v modeli. Žiadne univerzálne samovoľné EnsureCreated nad existujúcou produkčnou DB.

Spustenie: načítať profile metadata → overiť supported schema a app version → získať výlučný lock → pri upgrade vytvoriť konzistentný backup a overiť priestor → vykonať overené migrácie → integrity/foreign key kontrola → označiť profil Ready. Pred migráciou s rebuildom SQLite tabuliek overiť podporu konkrétnej operácie providerom na fixtures. Migrácie nesmú trvať neobmedzene na UI vlákne. Pri chybe aplikácia neotvorí poškodený profil na bežný zápis.

Neskoršia schéma: zobrazí „Tento profil vytvorila novšia verzia“, nechá dáta nedotknuté, ponúkne výber iného profilu alebo aktualizáciu. Žiadny pokus o automatický downgrade. Testovať aspoň predchádzajúcu vydanú schému a dlhšiu cestu z najstaršej podporovanej verzie; podporované intervaly uvádzať v release notes.

## Súbory mimo databázy

Prílohy/médiá/modely nie sú veľké BLOB polia v SQLite. Interný blob store používa UUID alebo obsahové ID, neodvodzuje cesty od ľubovoľného vstupného názvu. Záznam obsahuje hash, veľkosť, pôvodný názov a stav importu. Zmenené súbory originálu sa nesmú zameniť za rovnaký asset len podľa cesty.

DB a filesystem nemajú spoločnú transakciu. Protokol: vytvoriť staging súbor → validovať/hashovať → zaznamenať pending import → commitnúť interný súbor bez overwrite → potvrdiť odkaz v DB. Po páde recovery porovná pending stav s existenciou a hashom; žiadne samovoľné mazanie mimo interného koreňa. Garbage collector odstráni iba vlastné preukázane nereferencované objekty po ochrannom intervale a mimo aktívnej zálohy. Presné poradie a idempotencia sa overia fault injection.

ProjectLedger má výnimku: Markdown je canonical text a DB index odvodený. FileOrganizer má výnimku: zdrojové súbory sa neimportujú do blob store, len sa evidujú snapshots/journal a ich overenie. MediaWorkbench ukladá odkazy na zdroje a nové výstupy; kopírovanie vstupov je len explicitná voľba workspace.

## Konzistentná záloha

Online SQLite backup API alebo kontrolované zastavenie zápisov s korektným checkpoint postupom; nikdy jednoduché kopírovanie samotného aktívneho .db pri WAL. Backup sa vytvorí v stagingu spolu so zoznamom referencovaných príloh a manifestom, overia sa hashe a až potom vznikne finálny archív. Pri zmenách príloh počas snapshotu použiť nemenné blob objekty a referenčný zoznam z rovnakého konzistentného bodu.

Automaticky pred migráciou a manuálne na vyžiadanie. Navrhovaná lokálna retencia: posledných 5 predmigračných záloh s informáciou o priestore; používateľ môže exportovať mimo disk. Lokálna záloha na tom istom disku nie je ochrana pred stratou zariadenia. Šifrované exporty nie sú MVP; UI upozorní na citlivé údaje, nie na vymyslenú bezpečnosť archívu.

Obnova: validovať archiv bez zápisu do aktívneho profilu → vytvoriť nový isolated profile → integrity a počet/hash príloh → úspešné otvorenie → vedomé prepnutie. Aktívny profil sa pred nahradením zálohuje. Test obnovy musí otvoriť konkrétne entity a prílohy, nie iba overiť ZIP CRC.

## Implementovaný stav (Etapa 1)

- Layout dátového koreňa: `profiles/<name>/eventplanner.db`, `profiles/<name>/profile.lock`, `profiles/<name>/backups/pre-migration-*.db` (5 posledných), `active-profile.txt`, `logs/`, `staging/`. Názov profilu `^[a-z0-9][a-z0-9-]{0,63}$`.
- Profilová záloha `.epbackup` = ZIP s presne `manifest.json` (formatVersion 1, appId, exportId, createdUtc, exportScope `profile-backup`, dataSchemaVersion = posledná migrácia, appVersion, files s veľkosťou a SHA-256) a `data/eventplanner.db` (snapshot cez online backup API, journal DELETE). Archív sa pred publikovaním znovu prečíta rovnakou validáciou ako pri obnove.
- Obnova vytvorí nový profil `restored-<UTC čas>-<id>`, otvorí ho štandardnou štartovacou sekvenciou a načíta entity; aktívny profil sa nemení. Prepnutie zapíše `active-profile.txt` a vyžaduje reštart aplikácie; pôvodný profil zostáva na disku.
- Prenosný projektový export (JSON/Markdown) a prílohy sú plánované v Etape 4/5.

## Migrácie (Etapa 2)

- `Tasks` (po vydaní 1.0.0) je čisto aditívna: tabuľka Tasks s FK na projekt (CASCADE) a na predchodcu (SET NULL); test profilu verzie 1.0.0 zo zmrazenej schémy `VendorRevisionAndDecisions.sql`.
- `VendorsAndBudget` je čisto aditívna: tabuľky Vendors, Quotes, BudgetItems, Payments a ich indexy; existujúce tabuľky sa nemenia.
- Pred migráciou starého profilu `ProfileOpener` vytvorí overenú zálohu `backups/pre-migration-<UTC>-20261017073256_InitialProfile.db`. Rollback: zavrieť appku a obnoviť túto zálohu (prípadne `.epbackup`) do profilu staršej verzie; starší build novšiu schému odmietne (`NewerSchema`) bez zmeny súboru. `Down` migrácie len zahodí nové tabuľky a nepoužíva sa na používateľských dátach.
- Test: `Integration/Storage/ProfileMigrationTests.cs` nad zmrazenou schémou `EventPlanner.Tests/Fixtures/Schemas/InitialProfile.sql` a vzorovými dátami. Každá ďalšia migrácia pridá vlastnú zmrazenú schému predchádzajúcej verzie.
- `VendorRevisionAndDecisions` (Etapa 2, doplnok): pridá stĺpec `Vendors.Revision` (NOT NULL, existujúce riadky 1) a tabuľku `QuoteDecisions` s FK na Quotes (CASCADE) a indexom quoteId. Pridanie stĺpca s predvolenou hodnotou nevyžaduje rebuild tabuľky. Test: zmrazená schéma `EventPlanner.Tests/Fixtures/Schemas/VendorsAndBudget.sql` + vzorové dáta (dodávateľ, vybraná ponuka, položka, platba) → záloha `pre-migration-*-20261017082808_VendorsAndBudget.db` → migrácia so zachovaním dát. Rollback rovnako ako vyššie (predmigračná záloha).
- Záloha `.epbackup` so schémou `InitialProfile` sa obnoví do nového profilu, ten prejde predmigračnou zálohou a migráciou a dá sa používať – **overené** (`ProfileMigrationTests`; manifest je v teste zapísaný doslovne, nie cez aktuálny C# record).

## Prenos medzi Windows a Mac

Prenosný formát má manifest s formatVersion, appId, exportId, createdUtc, exportScope, dataSchemaVersion a obsahovými hashmi. Verejný projektový export je versioned JSON/Markdown + prílohy, nie nezdokumentovaná závislosť na internej SQLite schéme. Profil backup môže obsahovať DB snapshot, ale slúži na obnovu kompatibilnou verziou.

Vnútri iba relatívne cesty a štandardné UTF-8. Lokálne rooty a credentials sa nepovažujú za prenosné dáta. Import zobrazí chýbajúce zdroje a dovolí ich znovu priradiť. Neznáma verzia, chýbajúci hash, duplicitné ID alebo cross-scope väzba znamená odmietnutie pred aktiváciou. Stanoviť maximálny počet položiek/bytes; používateľ vidí voľný priestor a rozsah operácie. Cross-platform round-trip je release gate.
