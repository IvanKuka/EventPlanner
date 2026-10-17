# Spoločné UI pravidlá pre Plánovač podujatí

## Spoločný jazyk a ovládanie

Slovenčina je východiskový jazyk; texty cez lokalizačné zdroje, žiadne dôležité hlášky natvrdo vo viewmodeloch. Kódy doménových stavov sú anglické a stabilné. Dátumy zobrazovať podľa kultúry, časové udalosti ukladať UTC s časovou zónou podľa domény. Číselné vstupy musia podporiť slovenskú desatinnú čiarku tam, kde je relevantná.

Shell (rozhodnutie 17. 10. 2026, vizuálne zjednotené s AdobeNew): horné záložky stránok aktívneho projektu s ikonou, vpravo Projekty (zoznam, Nový projekt, Spravovať projekty), Záloha a Vzhľad; pod záložkami riadok aktívneho projektu (názov, stav, typ, dátum, miesto, Upraviť projekt) (podľa systému / svetlý / tmavý, pamätá sa pre počítač, nie profil); správa projektov a záloha nie sú záložky. Pod záložkami pás nástrojov so skupinami veľkých ikon a popisov; profil v dolnom stave, hlavná plocha, panel detailu a dolný stav. Zoznamy majú vyhľadávanie, filter, triedenie a explicitný nulový stav. Primárne tlačidlo reprezentuje ďalší krok pracovného postupu; neskladať obrazovku z desiatok rovnako výrazných tlačidiel. Detaily používať na editáciu, dialógy na krátke rozhodnutia. Interné pojmy ako adapter, schemaVersion či DTO nezobrazovať v bežnom používateľskom toku.

Všetky obrazovky definujú loading, empty, ready, saving/running, error, cancelled a recovery stav podľa relevantnosti. Chyba ostáva viditeľná pri položke; krátky toast nie je jediný nosič problému. Pri neuložených úpravách ponúknuť Uložiť/Zahodiť/Zrušiť. Zavretie okna počas zápisu najprv dokončí bezpečný bod alebo vysvetlí, prečo čaká.

## Prístupnosť a platformy

Ovládanie klávesnicou, viditeľný focus, logický tab order, pomenované controls, stav okrem farby aj textom/ikonou. Testovať 100/150/200 % škálovanie, svetlú/tmavú tému a dlhé slovenské texty. Minimálna navrhovaná plocha 1100×720, pri 900×600 sa detail prepína namiesto trvalého trojstĺpcového layoutu. Ide o návrhový cieľ overovaný na prototypoch, nie tvrdenie o existujúcom UI.

Na macOS používať systémový modifier Command a štandardné menu, na Windows Control. File pickery rešpektujú platformu a pamätajú len existujúce priečinky. Drag-and-drop vždy dopĺňa tlačidlo, nenahrádza ho. Nezavádzať vlastný systémový file picker. Obnovená pozícia okna musí zostať na dostupnej obrazovke.

## Bezpečné výsledky

Pred presunom, prepisom alebo odstránením ukázať rozsah a konkrétne ciele. Výsledok operácie obsahuje počet úspechov, preskočení, chýb a cestu k výstupu. Ponúknuť otvorenie výsledku alebo priečinka cez bezpečné platformové API; nikdy cez reťazený shell. Vrátenie operácie s externými účinkami musí uvádzať reálne obmedzenia.
