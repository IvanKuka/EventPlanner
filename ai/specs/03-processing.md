# Pravidlá a workflow

## Projekt a spolupráca

Draft → Planning → Ready → Completed → Archived. Stav Ready vyžaduje manuálne potvrdenie po checklistu; otvorené úlohy sa tým nestratia. Archivovaný projekt je read-only do explicitného odomknutia. Implementované prechody (`EventStatusRules`): Draft→Planning, Planning→Draft|Ready, Ready→Planning|Completed, Completed→Ready|Archived, Archived→Completed (odomknutie). Jeden aktívny lokálny writer, žiadne otvorenie živej DB cez SMB/iCloud/Dropbox ako mechanizmus spolupráce.

Odovzdanie vytvorí exportId, projectId, revision a parentExportId, ak existuje. Import sa otvorí ako nová lokálna kópia s provenance. Ak už projekt existuje, UI ukáže vetvenie a ponúkne ponechať oddelene alebo po zálohe vedome nahradiť celý projekt; neexistuje implicitný merge. Dve rozpracované vetvy sa v MVP nezlučujú automaticky. Export kontaktného zoznamu nie je úplná záloha.

## Rozpočet a ponuky

Estimated, Committed a Paid sa zobrazujú oddelene. Výber ponuky môže po potvrdení vytvoriť/aktualizovať BudgetItem, nikdy nerezervuje dodávateľa. Zmena vybranej ponuky sa nepreleje do rozpočtu bez používateľovho rozhodnutia. Zostáva doplatiť = committed - netto uhradené; záporný výsledok je označený preplatok, nie neviditeľná nula. Duplicita platby sa kontroluje podľa operationId a lokálneho potvrdenia, nie podľa rovnakého dátumu a sumy, ktoré môžu byť legitímne.

Porovnanie ponúk ukáže menu, zahrnuté položky, dopravu, počet osôb a vylúčené služby. Prázdne pole nie je automaticky nula ani zahrnutá služba. Automatické daňové a právne výpočty nie sú v rozsahu. Zmluvné termíny zadáva organizátor podľa svojich podkladov.

Implementované (Etapa 2, `QuoteComparisonService`, `BudgetService`, `BudgetCalculator`):

- Porovnanie je per kategória dodávateľa (bez ohľadu na veľkosť písmen). Celková cena = suma + doprava len ak je doprava známa (Included, NotNeeded alebo Separate so sumou); inak je celková cena neznáma a riadok vymenuje chýbajúce polia (doprava, cena dopravy, počet osôb, vylúčené služby, platnosť). „Najnižšia“ sa označí len medzi úplnými, neodmietnutými ponukami rovnakej meny; naprieč menami sa neporovnáva ani nesčíta.
- `SelectQuote(quoteId, expectedRevision, budgetImpactPolicy, budgetItemId?, expectedBudgetItemRevision?, note?)`: LeaveBudgetUnchanged / CreateBudgetItem (položka v mene ponuky; cudzia mena je mimo súčtu projektu s varovaním) / UpdateBudgetItem (rovnaká mena, inak CurrencyMismatch; overená revision položky). Neúplná cena sa prevezme len ako známa časť s varovaním `QuoteTotalIncomplete`. Predtým vybraná ponuka tej istej kategórie sa vráti na Received. Všetko v jednej transakcii.
- Úprava vybranej ponuky nemení rozpočet; výsledok nesie varovanie `LinkedBudgetNotUpdated`, zmenu treba vedome potvrdiť opätovným výberom s UpdateBudgetItem.
- História rozhodnutí (`QuoteDecision`, append-only): `SelectQuote` zapíše `Selected` (s dopadom na rozpočet) a pre predtým vybranú ponuku kategórie `Replaced`; `DeclineQuote(quoteId, expectedRevision, note?)` zapíše `Declined`, opakované odmietnutie nič nepridá. Záznam drží čas a cenu v čase rozhodnutia (neúplná cena = null); dôvod je voliteľný, orezaný, max 2000 znakov. Úprava a zmazanie dodávateľa vyžaduje `expectedRevision`.
- `RecordPayment(itemId, operationId, kind, amountMinor, status, paidLocalDate?, dueLocalDate?, reference?)`: opakovanie s tým istým operationId vráti existujúcu platbu (`PaymentAlreadyRecorded`), iný obsah s tým istým id je Conflict; súbežný dvojitý submit chráni unikátny index. Refund je len Posted a súčet refundov nesmie prevýšiť uhradené platby položky (ani pri zrušení platby). Plánovaná platba sa uhradí cez `PostPayment`, nie novým záznamom; `CancelPayment` ju ponechá v histórii. Položku s uhradenou platbou nie je možné zmazať.
- Súčty sa počítajú per mena: plán, dohodnuté, uhradené netto, vrátené, plánované, zostáva doplatiť (súčet kladných zostatkov) a preplatok (súčet záporných zostatkov) – preplatok jednej položky nezníži dlh inej. Položky bez plánu/záväzku sa počítajú a zobrazia, nie ako nula.

## Hostia a stoly

Hosť má stabilné ID nezávislé od mena. Rovnaké meno je varovanie, nie blokácia. Skupinové RSVP (`SetGroupRsvp`) mení iba výslovne vymenovaných členov s ich revision; jedna zastaraná revision zruší celý príkaz. Plus-one je konkrétny záznam alebo nepotvrdené miesto s jasným stavom, nie neviditeľné zvýšenie počtu. Kapacitu stola kontrolovať transakčne pri presune; Declined hosť nezostane potichu započítaný do finálneho obsadenia. Po zmene RSVP sa ukáže potreba upraviť usadenie, žiadny neohlásený presun iných ľudí.

## Harmonogram a úlohy

Časy sú lokálne pre projektovú časovú zónu, aj pri práci na počítači v inej zóne. Pri DST nejednoznačnosti vyžadovať výber offsetu; interval konca musí nasledovať začiatok. Podujatie môže prejsť cez polnoc. Kolízia úloh tej istej osoby je varovanie, nie globálny zákaz paralelných aktivít. Závislosti úloh nesmú vytvoriť cyklus.

## Výstupy

Exportné profily: organizátor plný plán, dodávateľ zvolený harmonogram, catering menné alebo súhrnné počty s výslovne vybranými požiadavkami, hostia verejný program. Výber polí je allowlist, súkromné poznámky sa predvolene nezahrnú. CSV chráni pred formula injection; tlačový výstup má opakovanú hlavičku a kontrolu stránkovania. PDF renderer vybrať až po license spike; prvá verzia môže mať CSV + bezpečný statický HTML na tlač, PDF je samostatná overená akceptačná etapa.
