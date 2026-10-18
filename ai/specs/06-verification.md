# Akceptačné scenáre

- E-A01: projekt s 200 hosťami a skupinovými výnimkami → správne počty pre obrad/večeru a rozdielne RSVP.
- E-A02: zmena Accepted na Declined pri usadenom hosťovi → viditeľný konflikt a aktualizované počty, žiadne tiché presuny.
- E-A03: presun na plný stôl a súbeh dvoch okien → kapacita sa neprekročí.
- E-A04: plán 1000, záväzok 900, záloha 200, doplatok 700, refund 50 → oddelené a správne súčty; idempotentný payment command. **Verified** (unit `BudgetCalculatorTests`, SQLite `BudgetServiceTests` vrátane súbežného dvojitého submitu, UI headless `ShellFlowTests`).
- E-A05: ponuky v dvoch menách a chýbajúca doprava → žiadny falošný spoločný total alebo predstieraná nula. **Verified** (unit `QuoteComparisonTests`, SQLite `QuoteComparisonServiceTests`, UI headless `ShellFlowTests`).
- E-A06: harmonogram cez polnoc/DST a cyklická závislosť úloh → explicitná validácia a správne časové zobrazenie. **Čiastočne overené**: cyklická závislosť úloh (`TaskPlanningTests`, `TaskServiceTests`) a „dnes“ podľa časovej zóny projektu; harmonogram cez polnoc/DST čaká na Timeline.
- E-A07: export catering/hosť neobsahuje súkromné poznámky, nevybrané kontakty a aktívne CSV formule.
- E-A08: Windows export → Mac import ako kópia → zachované väzby/prílohy; rozdielne lokálne revision sa potichu nezlúčia.
- E-A09: 2000 hostí, 1000 úloh, 1000 platieb a 1 GB príloh → plynulé zoznamy a streaming export. **Čiastočne overené**: časť rozpočtu – prehľad 1000 platieb s presnými súčtami (`BudgetPerformanceTests`, regresná hranica 1 s; lokálne cca 40 ms studený / 15 ms teplý, Windows vývojový PC, nejde o referenčný benchmark). Zoznamy sú `ListBox` s virtualizáciou. Hostia 2000, úlohy, prílohy a export čakajú na moduly.
- E-A10: tlačové HTML s menami obsahujúcimi HTML znaky → escaped obsah, bez aktívneho skriptu a so správnym stránkovaním pri manuálnom QA.
