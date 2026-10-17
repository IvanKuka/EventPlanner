# Invarianty EventPlanner

- Produktové požiadavky: [rozhodnutia](specs/00-decisions.md); rozsah [produkt](specs/01-product.md).
- Bez serverovej DB, žiadny povinný cloud ani zdieľaná živá SQLite.
- Lokálne dáta sa nemenia bez validácie; každý import má verziu a staging.
- UI nie je zdroj pravdy pre doménové pravidlá; výsledok musí prežiť reštart.
- Citlivé dáta sa neposielajú ani nelogujú automaticky.
- Tri desktop RID a installer smoke sú súčasťou dokončenia, nie dodatočný bonus.
- Dlhá operácia má cancellation, operationId a recovery podľa [workflow](specs/03-processing.md).
- Návrh API v docs nie je existujúca upstream implementácia; overuj integrácie.
- Pri zmene správania sa aktualizuje spec, pri zmene štruktúry routing/index/knowledge.
