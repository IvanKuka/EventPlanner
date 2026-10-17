# Etapy a otázky do budúcna

1. EventProject + hostia + uloženie/obnova a prvý packaged desktop pre tri RID. **Implementované 17. 10. 2026** – Windows balík a smoke test overené lokálne; macOS balíky zostavuje CI (zatiaľ nespustené).
2. Ponuky, budget a payment pravidlá s testami preplatkov/refundov. **Implementované 17. 10. 2026** – Vendors a Budget, migrácie `VendorsAndBudget` a `VendorRevisionAndDecisions` (revízia dodávateľa, história rozhodnutí) s testami starých profilov a starej zálohy; win-x64 publish a smoke test overené lokálne, macOS v CI.
3. Úlohy, harmonogram a jednoduché usadenie s kapacitou.
4. Snapshot odovzdanie, privátne exportné profily, CSV a tlačový HTML.
5. Prílohy, šablóny, migrácie a stabilizácia; PDF až po overení enginu.

Rozhodnuté: osobná/rodinná offline appka bez verejnej registrácie a automatického kontaktovania ľudí. Skutočná spolupráca, verejný RSVP portál, mobilná webová verzia a automatické správy vyžadujú samostatné rozhodnutie. Aktuálne návrhy nepotrebujú tvoje konkrétne kontakty, termín svadby ani zmluvy. Konkrétne šablóny sú nastaviteľné; nič sa nepredpokladá o tvojej rodine ani účastníkoch.

Otvorené pred prvým vydaním: macOS bundle identifier (`local.eventplanner.desktop` je dočasný a po vydaní sa nesmie meniť), dostupnosť runner labelov `macos-26` / `macos-26-intel`.
