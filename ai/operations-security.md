# Prevádzka a bezpečnosť

Tento projekt je lokálny desktop, nie serverová služba. Kanonické dokumenty: [bezpečnosť](specs/11-security-privacy.md), [dáta/backup/restore](specs/09-storage-portability.md), [diagnostika](specs/13-observability-performance.md), [release](deployment/01-desktop-release.md).

Pri incidente najprv identifikovať profil/verziu/stav, zachovať originál a použiť redigovanú diagnostiku. Nevykonávať hromadné mazanie temp alebo profilov bez overenia ich vlastníctva a rozsahu. Obnovu skúšať v oddelenom profile; neprepisovať jedinú používateľskú kópiu experimentálnou migráciou.
