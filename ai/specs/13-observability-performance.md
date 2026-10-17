# Diagnostika a merateľné správanie

Štruktúrované lokálne logy cez ILogger s operationId, feature, safe error code, fázou a trvaním; predvolený file sink vybrať malý a udržiavaný. Core závisí na abstrakcii loggeru, nie na formáte súboru. Absolútne cesty iba ak sú potrebné a redigovateľné; plný obsah údajov nikdy automaticky.

Diagnostics obrazovka: app/version/RID, OS verzia, schemaVersion, voľný priestor, pending recovery, posledné chybové kódy a relevantná verzia natívnych nástrojov. Export zostaví nový redigovaný archív s náhľadom; nezbalí celý profil ani secrets. Žiadne automatické odoslanie autorovi appky.

Rozlišovať technickú chybu, neplatné dáta, zrušenie a nepodporovaný vstup. Pri chybe poskytnúť vykonateľný ďalší krok: vybrať inú cestu, priradiť zdroj, obnoviť profil alebo otvoriť recovery. Nedávať všeobecné „Niečo sa pokazilo“ bez kódu a kontextu.

Výkonnostný report je lokálny vývojový artefakt s referenčným hardware a datasetom. Veľké zoznamy virtualizovať, textové hľadanie debounce/cancel, obrázky držať v ohraničenej cache. Nepoužívať globálny event bus alebo časovač pre každú položku bez potreby. Dlhé úlohy majú vlastný lifecycle a shutdown policy. Zrušenie musí mať jasný termín bezpečného bodu, nie predstierané okamžité zastavenie commit kroku.
