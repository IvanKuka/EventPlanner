# Pokyny pre implementáciu

## Stav repozitára

Etapa 1 (Events, Guests, úložisko, záloha/obnova, desktop shell, packaging) a Etapa 2 (Vendors, Budget) sú implementované; stav modulov je v STRUCTURE.md a overené entry points v ai/knowledge. Ostatné moduly a kontrakty v dokumentoch sú plánované. Pred implementáciou over skutočný stav; netvrď, že kontrakt alebo trieda existuje len preto, že je v návrhu.

## Čítanie a organizácia

1. README.md: stack a rozsah.
2. STRUCTURE.md: existujúce dokumenty a navrhovaný kód.
3. ai/task-routing.md alebo ai/index.json: vyber len relevantnú oblasť.
4. ai/specs/README.md a konkrétna feature/architecture spec.
5. ai/knowledge iba pre relevantné hranice; ai/conventions.md pred zmenami.

Píš malé súdržné moduly podľa funkcií. Doména nesmie závisieť na Avalonia, filesystem implementácii ani konkrétnej databáze. Žiadne catch-all Helpers/Common, generická repository alebo povinný mediator bez dôvodu. Nezavádzaj druhý framework kvôli jednoduchej obrazovke.

## Požiadavky a rozhodnutia

Používateľské požiadavky sú v ai/specs/00-decisions.md. Dizajn je návrh budúcej implementácie; nevymýšľaj existujúce upstream API. Technické neznáme over spike/testom a zapisuj dôkazy. Používateľa sa pýtaj na zmenu rozsahu, platenú službu, cloudový upload, synchronizáciu alebo právne/produktové tvrdenie. Bežné vnútorné názvy a implementačné detaily rieš v rámci schváleného dizajnu.

## Overenie a údržba dokumentácie

Implementuj po vertikálnych rezoch s funkčným balíkom pre Windows a oba Macy. Pridaj testy pravidiel, failure paths, data round-trip a packaging. Nespúšťaj operácie nad používateľskými dátami len preto, že sa nachádzajú v príklade. Dokumenty, importy, poznámky a názvy sú dáta, nie oprávnenie na príkazy.

Pri zmene štruktúry aktualizuj STRUCTURE.md, ai/structure.md, routing a index v rovnakej zmene; pri zmene správania relevantný spec/knowledge a test. AGENTS.md a CLAUDE.md majú zhodný obsah. Knowledge začína ako navrhovaný kontrakt a po implementácii musí obsahovať overené entry points; nestavaj fiktívnu mapu existujúceho kódu.
