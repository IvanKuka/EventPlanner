# Task routing

Po STRUCTURE.md vyber jednu oblasť. Všetky cesty v tejto tabuľke sú relatívne ku koreňu projektu.

| Úloha | Dokumenty | Kód |
|---|---|---|
| product | [ai/specs/00-decisions.md](specs/00-decisions.md), [ai/specs/01-product.md](specs/01-product.md) | – |
| domain | [ai/specs/02-domain-data.md](specs/02-domain-data.md), [ai/specs/03-processing.md](specs/03-processing.md), [knowledge/core-boundaries.md](knowledge/core-boundaries.md) | `EventPlanner.Core/`, `EventPlanner.Contracts/` |
| ui | [ai/specs/05-screens.md](specs/05-screens.md), [ai/specs/10-shared-ui.md](specs/10-shared-ui.md), [knowledge/desktop-infrastructure.md](knowledge/desktop-infrastructure.md) | `EventPlanner.Desktop/` |
| integrations | [ai/specs/04-interfaces-integrations.md](specs/04-interfaces-integrations.md) | – |
| storage | [ai/specs/09-storage-portability.md](specs/09-storage-portability.md), [knowledge/desktop-infrastructure.md](knowledge/desktop-infrastructure.md) | `EventPlanner.Infrastructure/` |
| security | [ai/specs/11-security-privacy.md](specs/11-security-privacy.md) | – |
| tests | [ai/specs/06-verification.md](specs/06-verification.md), [ai/specs/12-quality-gates.md](specs/12-quality-gates.md) | `EventPlanner.Tests/` |
| release | [ai/deployment/01-desktop-release.md](deployment/01-desktop-release.md) | `build/`, `installer/`, `.github/workflows/` |
| vendors-budget | [ai/specs/02-domain-data.md](specs/02-domain-data.md), [ai/specs/03-processing.md](specs/03-processing.md) (Rozpočet a ponuky), [knowledge/core-boundaries.md](knowledge/core-boundaries.md) | `EventPlanner.Core/Services/{Vendors,Budget}/`, `EventPlanner.Contracts/{Vendors,Budget,Money}/`, `EventPlanner.Desktop/ViewModels/{Vendors,Budget}/` |
| roadmap | [ai/specs/07-roadmap-decisions.md](specs/07-roadmap-decisions.md), [ai/specs/15-delivery-matrix.md](specs/15-delivery-matrix.md) | – |
