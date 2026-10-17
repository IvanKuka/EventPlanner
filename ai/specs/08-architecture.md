# Architektúra EventPlanner

## Organizačný vzor FinanceApp

Preberá sa čitateľná solution na koreni, úzke moduly, oddelené kontrakty, modely, služby, infraštruktúra, testy a navigovateľný priečinok `ai`. Nepreberá sa finančná doména, webový frontend, prihlasovanie ani historické schémové opravy. Referenčný projekt zostáva nedotknutý.

Navrhovaná solution má štyri produkčné projekty: `.Desktop`, `.Core`, `.Infrastructure`, `.Contracts` a samostatný `.Tests`. Core obsahuje Models/<Feature>, Services/<Feature>, Validation/<Feature> a porty iba na skutočných hraniciach I/O. Contracts obsahuje stabilné príkazy, výsledky a exportné DTO. Infrastructure obsahuje Data, Files, Processes alebo Network podľa potreby. Desktop obsahuje Views/<Feature>, ViewModels/<Feature>, Components, Styles, Localization a Composition. Tests obsahuje Unit/<Feature>, Integration/<Boundary>, Ui a Packaging.

Pravidlá závislostí: Contracts nemá závislosť na UI ani persistencii; Core môže závisieť na Contracts; Infrastructure implementuje porty Core; Desktop zostavuje DI a volá aplikačné služby, nie SQL či externé procesy z viewmodelu. Tests môže testovať všetky vrstvy. Žiadny univerzálny Repository<T>, povinný mediator, mikroservisy ani zdieľaná runtime platforma pre všetkých šesť aplikácií. Rozdelenie na projekty chráni závislosti; vnútorné členenie zostáva podľa funkcií.

## Stack a životný cyklus

C# + .NET 10 LTS, aktuálne kompatibilné stabilné Avalonia UI, MVVM s CommunityToolkit.Mvvm, štandardné Microsoft.Extensions.DependencyInjection/Logging/Options. Verzie sa pri bootstrap fáze overia a presne zapíšu do global.json, Directory.Packages.props a lock súborov. Nepoužiť preview verzie. Avalonia je zvolená pre spoločné desktopové UI vo Windows aj macOS a existujúcu preferenciu C#. Platené Avalonia produkty nie sú podmienkou.

WPF by nedal požadovaný spoločný Mac frontend; web by pri lokálnych súboroch vyžadoval ďalšie mosty a server; Python/Qt je overená alternatíva z PDF nástroja, ale bez konkrétneho dôvodu nebudeme udržiavať dva hlavné aplikačné jazyky. Nevyžaduje sa NativeAOT ani agresívne trimming; začať self-contained publish a optimalizovať až podľa meraní. Používateľ neinštaluje .NET ani databázový server.

## Rozhranie aplikačných služieb

Každá dlhá operácia prijíma CancellationToken, používa stabilné operationId a posiela typovaný priebeh bez závislosti na Avalonia dispatcher. UI zobrazuje stav a umožňuje bezpečné zrušenie. Doménové rozhodnutia sú deterministické; hodiny, filesystem a externé procesy sú explicitné hranice pre testy. Async I/O nevykonávať cez blokujúce .Result/Wait. Výpočty mimo UI vlákna; renderovanie len cez UI dispatcher.

Očakávané chyby majú kód, slovenský používateľský text, bezpečný kontext a možnosť nápravy. Výnimky sa centrálne logujú a mapujú, neprehĺtajú. Chyba jednej položky dávky nesmie predstierať úspech celej dávky. Neznámy stav sa nezobrazuje ako úspech.

## Prevádzková nezávislosť

Každý repozitár má vlastnú verziu, installer, build a dáta. Žiadna závislosť na tom, že FinanceApp alebo Jarvis práve beží. Server je voliteľný integračný cieľ; len HomelabConsole potrebuje dostupný server pre živé kontroly. Kopírovanie lokálnej databázy cez sieť ani zdieľaný zápis viacerých inštancií nie je synchronizácia.
