# Bezpečnosť EventPlanner

## Hranica dôvery

Desktop pracuje v oprávneniach prihláseného OS používateľa. Lokálna appka nemá vlastné prihlasovanie; PIN v UI sa nepovažuje za šifrovanie. Citlivé dáta chráni OS účet, oprávnenia dátového priečinka a podľa potrieb šifrovanie disku. Základná verzia nesľubuje samostatne šifrovanú databázu. Oddelené profily jednej OS relácie nie sú bezpečnostná izolácia.

Vstupné projekty, archívy, mediálne súbory, názvy a sieťové odpovede sú nedôveryhodné dáta. Žiadne inštrukcie v importe alebo texte nesmú spustiť príkaz. Procesy spúšťať s explicitným executable a ArgumentList, bez shell interpolácie. Timeout, limit stderr/stdout, cancellation a likvidácia potomkov sú povinné tam, kde proces existuje.

Import archívu: overiť manifest, verziu, veľkosť, počet súborov a hashe; odmietnuť absolútne cesty, traversal, odkazy a prekročenie cieľového koreňa. Rozbaľovať do oddeleného stagingu; commit až po validácii. Prílohy ukladať pod interným ID, pôvodný názov len ako metadáta. Otvorenie prílohy je explicitná akcia; HTML nepovažovať za bezpečný aktívny obsah.

## Tajomstvá a diagnostika

Žiadne heslá, kľúče, tokeny, obsahy osobných poznámok, kontakty hostí ani celé mediálne prepisy v logoch. Budúce tajomstvá ukladať do OS credential store cez overený adapter. Ak nie je dostupný, nepoužiť tichý plaintext fallback. Cesty a adresy môžu byť citlivé; diagnostický export má redakciu a náhľad obsahu.

Rotované lokálne logy, predvolený limit návrhu 7 dní/20 MB podľa toho, čo nastane skôr. Telemetria a odosielanie crash reportov sú vypnuté. Aktualizačné a modelové downloady sú explicitné sieťové funkcie a musia mať integritnú kontrolu. Dostupnosť novšej verzie nie je dôvodom zablokovať existujúce lokálne dáta.

## Mazanie a aktualizácie

Vymazanie databázového záznamu alebo súboru nie je garantované fyzické vymazanie SSD ani starých záloh. Retenciu a kaskády dokumentovať pre každú entitu. Odinštalovanie predvolene ponechá používateľské dáta, osobitná potvrdená voľba môže vymazať konkrétny dátový koreň.

Bez podpisových certifikátov nesľubovať tichú inštaláciu ani absenciu systémových upozornení. SHA-256 kontroluje integritu, ale sám nepotvrdzuje dôveryhodnosť vydavateľa. Distribúcia musí pomenovať unsigned stav. Podpisovanie/notarizácia je pripravená voliteľná vetva budúcich releasov, nie podmienka kompilácie.
