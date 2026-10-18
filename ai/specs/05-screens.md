# Obrazovky a rozhodnutia

**Prehľad podujatia:** dátum, počet potvrdených hostí podľa relevantnej časti, plán/dohodnuté/uhradené, najbližšie úlohy a problémy. Otvorené údaje nezobrazovať ako nulové problémy len preto, že chýbajú.

**Hostia:** skupiny a jednotlivci, rýchla RSVP editácia s výnimkami, filter častí programu a samostatne chránený pohľad na stravovacie požiadavky. Search meno/kontakt; citlivý text sa nezobrazuje v default preview karte.

**Dodávatelia:** porovnávacia tabuľka ponúk so spoločnými poliami, výber nemení rezerváciu. Detail obsahuje prílohy a históriu rozhodnutia. **Rozpočet:** kategórie, tri typy súm a jednotlivé platby; vlastný stav zaplatenia jasne oddelený od splatnosti.

Implementované (Etapa 2): Dodávatelia – zoznam, porovnanie podľa kategórie (mena, zahrnuté, doprava, spolu alebo „neúplná“, osoby, vylúčené, platnosť, chýbajúce polia, príznak cudzej meny) a výber s explicitným dopadom na rozpočet a voliteľným dôvodom; detail dodávateľa a ponuky ukazuje históriu rozhodnutí (vybraná / nahradená inou / odmietnutá, cena v čase rozhodnutia, dopad na rozpočet, dôvod). Prílohy sú plánované s blob store (Etapa 5). Rozpočet – súčty projektu (plán, dohodnuté, uhradené netto, zostáva, preplatok, vrátené, plánované), cudzie meny samostatne, položky a platby s akciami Uhradiť/Zrušiť. Prehľad zobrazuje plán/dohodnuté/uhradené/zostáva a preplatok či cudziu menu ako otvorený bod.

Implementované (úlohy): záložka Úlohy vedľa Rozpočtu – pás nástrojov (Nová úloha, Zmazať úlohu s potvrdením; Rozpracovať, Hotovo, Znovu otvoriť), zoznam s počtom otvorených a po termíne, prepínač „Zobraziť hotové“, riadok úlohy s termínom, štítkom Po termíne, prioritou Dôležité, kto ju rieši a na čo čaká; detail s poliami Čo treba urobiť, Kto to rieši, Dokedy, Priorita, Nadväzuje na a Poznámky. Prehľad má kartu Najbližšie úlohy.

**Úlohy:** zoznam alebo jednoduchý Kanban, osoba/termín/priority, iba explicitné závislosti. **Harmonogram:** deň a časová zóna, radené položky a varovania kolízií. Žiadna náročná grafická Gantt komponenta pre MVP.

**Usadenie:** stoly ako zoznamy s kapacitou a neumiestnení hostia; tlačidlo Presunúť aj drag-and-drop. Grafický floorplan je neskôr. **Odovzdanie/export:** profil príjemcu, výber polí, náhľad a názov verzie. Na obrazovke musí byť zrejmé, či export obsahuje kontakty alebo požiadavky jednotlivcov.

**Dokončenie/archív:** checklist vyrovnania úloh a platieb, export finálnej zálohy, voliteľné odstránenie citlivých detailov podľa rozhodnutia používateľa. Neprisľubovať automatické zmazanie starších odovzdaných kópií.
