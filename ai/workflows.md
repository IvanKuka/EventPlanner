# Implementačné workflow

## Vertikálny rez

Vyber scenár z roadmap → spíš očakávané výsledky a chyby → over potrebné API/engine → vytvor len nutné projekty a moduly → implementuj pravidlá a adaptér → pripoj UI → testuj round-trip a failure → zabaľ tri RID → aktualizuj navigáciu a status.

## Zmena dát

Najprv model a invarianty, potom konfigurácia/migrácia, fixture starej DB, backup a restore test, update transfer schema ak treba. Žiadna ručná úprava používateľského profilu ako náhrada migrácie. Pri budúcej nekompatibilnej verzii import odmietnuť bez side effect.

## Nová integrácia

Pripnutá verzia a primary docs → minimálny read-only experiment → fixture reálnej odpovede → bezpečnostná/timeout politika → úzky port → deterministické testy bez živého účtu. Ak schopnosť nie je overená, označ ju unavailable, nevracaj placeholder úspech.

## Dopracovanie dokumentácie

Po implementácii zaznamenaj skutočné entry points a testy do knowledge. Špecifikáciu udržuj ako pravdu správania; nearchivuj aktívny zdroj pravidiel. V handoff uveď vykonané testy oddelene od navrhnutých testov a zostávajúce gates.
