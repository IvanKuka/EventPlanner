# Konvencie nového repozitára

- C# nullable enabled, analyzers, format kontrola, async s CancellationToken na I/O hraniciach. DTO records pre immutable príkazy/výsledky, entity iba tam, kde potrebujú lifecycle. Sériové formáty majú explicitné field names a string enum hodnoty.
- English identifikátory v kóde, slovenské produktové texty v resources. Súbory docs kebab-case. Jedna zodpovednosť súboru; rozdelenie podľa feature a následne roly.
- Views nevykonávajú SQL, spúšťanie nástrojov ani doménové výpočty. ViewModel volá use case, mapuje výsledok a drží UI stav. Dlhodobé pravidlá patria Core službe.
- EF configurations jedna na entitu. Migračná zmena obsahuje test starého profilu, indexy a backup/rollback plán. Nepoužívať produkčný SQLite cez EF InMemory v integračných testoch.
- Každý mutačný príkaz má validáciu a podľa potreby revision/idempotency token. Transakcie krátke, žiadne držanie DB transakcie počas dlhého externého procesu.
- Bindovaná kolekcia sa mení len cez UI dispatcher, pipeline dáta sa streamujú a veľké výsledky virtualizujú. Reset projektu zruší vlastné requesty a zabráni doručeniu starého výsledku do nového projektu.
- Logy neobsahujú payloady používateľských údajov. Exception zachovať pre lokálnu diagnostiku, UI dostať bezpečný error code a nápravu.
- Balíky pridávať len pre konkrétnu potrebu s overenou licenciou. Presné verzie a transitive lock spravovať v repozitári. Automatické update PR nesmú preskočiť trojplatformové kontroly.
- Build: `dotnet restore --locked-mode`, `dotnet format --verify-no-changes`, `dotnet build -c Release`, `dotnet test -c Release`. Zmena balíkov vyžaduje aktualizáciu `packages.lock.json` v rovnakej zmene.
