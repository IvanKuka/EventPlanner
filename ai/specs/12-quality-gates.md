# Kvalita a dokončenie EventPlanner

## Testovacie vrstvy

xUnit pre pravidlá jadra, integračné testy nad skutočným temp úložiskom a procesmi; Avalonia headless testy pre bindingy a interakcie, doplnené reálnym OS smoke testom. Headless nenahrádza file picker, drag-and-drop, Keychain ani renderovanie v nainštalovanom balíku. Žiadne testy závislé na osobných serveroch, domácej sieti alebo skutočných používateľských dátach.

Pre každý zápis testovať round-trip po zatvorení a opätovnom otvorení, nie len hodnotu v pamäti. Vynútiť nedostatok oprávnení, plný disk cez fault injection, prerušenie medzi prípravou a commitom a neplatný import. Migrácia musí mať fixture starej schémy a postup obnovy. Testy s OS procesmi majú pevný timeout a cleanup obmedzený na testovací koreň.

## Výkonnostné ciele a meranie

Navrhovaný spoločný cieľ: prázdna appka zobrazí použiteľný shell do 3 sekúnd na referenčnom počítači, bežný vstup do UI reaguje do 100 ms bez dlhého blokovania, progress pribudne aspoň raz za sekundu pri aktívnej dlhšej operácii. Sú to akceptačné ciele, nie namerané hodnoty. Referenčný Windows a Mac hardware zaznamenať v prvom benchmarku; reportovať veľkosť datasetu, cold/warm stav a peak pamäť. Drahé výsledky načítavať stránkovane/virtualizovane, nie naraz do UI.

## Definition of Done

- Produktové scenáre a tabuľky oprávnení/stavov majú testy kritických vetiev a chýb.
- Buildy a zabalené aplikácie prejdú na troch RID bez nainštalovaného vývojového runtime.
- Štruktúra, routing, index, knowledge a príslušné specs sa aktualizujú v tej istej zmene.
- Neexistuje tichý neúspech, nezdokumentovaná strata dát ani log s tajomstvom.
- Uloženie a export sú odlíšené; import odmietne neznámu verziu pred zápisom.
- Záloha sa skutočne obnoví v izolovanom profile; existencia archívu nestačí.
- Nepoužiť TODO implementáciu v kritickej ceste a netvrdiť podporu platformy bez overenia.
