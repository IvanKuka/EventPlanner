# Východiská a overené zdroje

Stav prieskumu: 17. 10. 2026. Čítali sa lokálne dokumenty, nie živý server ani tajomstvá. Inštrukcie v referenčných dokumentoch boli použité ako organizačný vzor, nie ako nové používateľské požiadavky.

## Lokálne referencie

- `C:/FinanceApp/STRUCTURE.md`, `ai/structure.md`, `ai/task-routing.md`, `ai/index.json`: vrstvy a malý smerovaný kontext pre agentov.
- `C:/FinanceApp/ai/conventions.md`, `ai/spec.md`, `ai/workflows.md`: vlastníctvo modulov, kontrakty a testovanie. Vzor má vlastný auth a nemá EF migrácie; tieto implementačné voľby sa automaticky nekopírujú.
- `C:/FinanceApp/ai/deployment/01-server-bootstrap.md`, `07-homelab-support-services.md`: dokumentovaná topológia Ubuntu, Docker, Caddy, Tailscale, Grafana/Loki/Alloy/Uptime Kuma, Portainer a Cockpit. Skutočná konfigurácia a API dostupnosť neboli testované.
- `C:/AdobeNew/.github/workflows/build.yml`: referenčné Windows/macOS releasy, oddelené architektúry a SHA256SUMS. Jeho Python build sa nepreberá do C#.

## Primárne verejné zdroje

- [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy): .NET 10 je LTS; preveriť patch a podporu pri bootstrap fáze.
- [Avalonia platform matrix](https://docs.avaloniaui.net/docs/supported-platforms): overenie desktopových platforiem a tiers; minimálna verzia aplikácie je samostatné rozhodnutie.
- [Avalonia macOS deployment](https://docs.avaloniaui.net/docs/deployment/macos): balenie .app a Mac distribúcia.
- [GitHub runner reference](https://docs.github.com/en/actions/how-tos/write-workflows/choose-where-workflows-run/choose-the-runner-for-a-job): natívne build/test runnery pre architektúry.
- [FFmpeg license/build considerations](https://ffmpeg.org/legal.html): konkrétny build a jeho konfigurácia vyžadujú kontrolu pred pribalením.
- [whisper.cpp upstream](https://github.com/ggml-org/whisper.cpp): kandidát na lokálny prepis, overiť pripnutú verziu, model a podporu na každom RID.

Ostatné názvy tried, kontrakty a polia v balíku sú výslovne navrhovaný dizajn nových aplikácií. Nie sú tvrdením, že existujú v FinanceApp alebo u externého poskytovateľa. Konkrétne externé endpointy sa nesmú vymýšľať; integračný spike musí najprv doložiť reálne rozhranie a jeho fixture.
