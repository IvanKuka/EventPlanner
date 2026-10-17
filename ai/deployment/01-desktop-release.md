# Distribúcia EventPlanner

## Podporované balíky

Každý desktop má rovnakú funkčnú základňu pre `win-x64`, `osx-arm64`, `osx-x64`. Windows: self-contained aplikácia, per-user Inno Setup `.exe` a portable ZIP. macOS: samostatný `.app` bundle pre každú architektúru, `.dmg` a záložný ZIP. Žiadna požiadavka na Python/.NET či manuálne doinštalovanie runtime. Linux a Windows ARM nie sú súčasťou prvého release.

Cieľová kompatibilita: Windows 11 x64; Windows 10 22H2 iba ako overená kompatibilita, nie prísľub podpory OS výrobcom. macOS 14+ na oboch čipoch, pričom konečné minimum je prienik požiadaviek pripnutej Avalonie, .NET a natívnych závislostí. CI na novšom macOS nepreukazuje kompatibilitu so starším; pred vydaním vykonať samostatný manuálny test minima. Aktuálne overená Avalonia matica uvádza macOS 14/15 ako Tier 2; tento rozdiel zaznamenať do support matrix, nie skryť.

## Návrh GitHub Actions

Implementované: `.github/workflows/ci.yml` (PR/main: locked restore, format, build, test na Windows a macOS), `desktop-build.yml` (publish, smoke test, balíky pre tri RID), `release.yml` (tag `vX.Y.Z` zhodný s `<Version>` v Directory.Build.props → validácia → build troch RID → priamo zverejnený release s balíkmi, SHA256SUMS a manifestom; existujúci release sa neprepíše). Skripty: `build/publish.ps1`, `build/smoke-test.ps1`, `build/package-windows.ps1` (Inno Setup `installer/windows/EventPlanner.iss`), `build/package-macos.sh` (ad-hoc podpis, DMG/ZIP, `installer/macos/Info.plist`), `build/validate-release.sh`. Workflowy ešte neboli spustené na GitHube; release vzniká ako draft a publikuje sa manuálne.

1. Pull request: locked restore, formatter/analyzers, unit a integration testy; matrix pre filesystem/platformové hranice. Nemá release secrets ani oprávnenie zapisovať release.
2. Main a workflow_dispatch: publish a packaging na Windows, Apple Silicon a Intel runneri; natívne závislosti podľa RID; upload artifactov.
3. Tag `vMAJOR.MINOR.PATCH`: z kontrolovaného commit SHA spustí kompletnú kontrolu a zostavenie všetkých troch cieľov. Nevydávať neúplný release, keď jedna architektúra zlyhá.
4. Validácia verzie: tag, assembly metadata, installer version a manifest musia zodpovedať. Zamietnuť neplatný tag a konflikt s existujúcim immutable releasom.
5. Každý runner overí čerstvý rozbalený výsledok cez `--smoke-test`: spustenie UI runtime, inicializácia dát v izolovanom temp profile, korektné ukončenie. Media navyše otestuje pripnuté natívne nástroje. Smoke test nesmie načítať skutočný používateľský profil.
6. Balíky: jeden Setup EXE, tri ZIPy, dva DMG = šesť používateľských súborov; navyše SHA256SUMS, release manifest, zoznam licencií a SBOM. Počet a vzor názvov validovať pred publikovaním.
7. Release job je jediný s `contents: write`; používa iba artefakty aktuálneho runu a čaká na všetky buildy. Zlyhanie uploadu obnoviť kontrolovane bez prepisovania už vydaných súborov.

Konkrétne action verzie pripnúť na overené commit SHA pri implementácii. Aktuálne overené runner labely zahŕňajú `macos-26-intel`; dostupnosť a cenu overiť pri bootstrap fáze, potom pripnúť podporovaný label a sledovať deprecations. Nepreberať slepo mutable `latest` ani hlavné action verzie z cudzieho projektu.

## Installer a aktualizácie

Stabilné application ID a dátový koreň; upgrade zastaví bežiacu appku po používateľovom rozhodnutí a nikdy neprepíše profil binárkami. Windows bez administrátorských práv, Mac drag-and-drop do Applications. Súbory na read-only obraze DMG nesmú byť pracovným adresárom. Cesty, diakritika a medzery musia fungovať.

Prvá verzia: kontrola aktualizácie na vyžiadanie a odkaz na release, následne manuálny installer. Žiadny vlastný tichý self-updater bez navrhnutej autenticity a rollbacku. Na Macu nepísať používateľovi, aby plošne vypínal Gatekeeper; popísať systémový postup pre unsigned appku. Ad-hoc podpis, ak ho architektúra vyžaduje, nie je Developer ID certifikát.

## Release a obnova dát

Pred migráciou profilu vytvoriť konzistentnú zálohu; novšiu nepodporovanú schému otvoriť iba s informáciou a bezpečným ukončením. Downgrade binárky nie je automaticky downgrade dát. Rollback = predchádzajúci installer plus overená predmigračná záloha, nie spustenie starého kódu nad novou schémou.

Release checklist: všetky testy, smoke na zabalených appkách, clean-machine inštalácia bez SDK, upgrade z predošlej verzie, otvorenie projektu na oboch OS, licencia všetkých pribalených bináriek, žiadne credentials v artefaktoch, dokumentácia a známe obmedzenia. Na server sa v tejto fáze nič nenasadzuje.

## Mená artefaktov

`EventPlanner-Setup-x64.exe`, `EventPlanner-windows-x64.zip`, `EventPlanner-macos-arm64.dmg`, `EventPlanner-macos-arm64.zip`, `EventPlanner-macos-x64.dmg`, `EventPlanner-macos-x64.zip`. Release verzia je v manifestoch a release tagu; variant s verziou v názve je možný, ale musí zostať konzistentný s validátorom a dokumentáciou.
