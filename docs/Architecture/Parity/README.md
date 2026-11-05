# EnterpriseUI Parity Program

Assessment: 2026-11-05. Evidence-based baseline, not a parity certification.

This directory contains the twelve requested reports plus machine-readable Evidence.json and Coverage.json. Reports are generated from current source metadata and dated vendor references. Inventories explicitly retain incompleteness and unverified findings.

- [01-TelerikThemeInventory.md](01-TelerikThemeInventory.md)
- [02-DevExpressThemeInventory.md](02-DevExpressThemeInventory.md)
- [03-ThemeGapAnalysis.md](03-ThemeGapAnalysis.md)
- [04-MissingThemes.md](04-MissingThemes.md)
- [05-ControlCoverageMatrix.md](05-ControlCoverageMatrix.md)
- [06-MissingControls.md](06-MissingControls.md)
- [07-MissingFeatures.md](07-MissingFeatures.md)
- [08-ThemeBuilderArchitecture.md](08-ThemeBuilderArchitecture.md)
- [09-RuntimeThemeEngineArchitecture.md](09-RuntimeThemeEngineArchitecture.md)
- [10-DemoCenterModernization.md](10-DemoCenterModernization.md)
- [11-DocumentationCoverage.md](11-DocumentationCoverage.md)
- [12-FeatureParityRoadmap.md](12-FeatureParityRoadmap.md)

Regenerate from the repository root:

```powershell
dotnet run --configuration Release --project samples/EnterpriseUI.WinFormsDemo -- --generate-parity-audit . docs/Architecture/Parity
```

Verify generated themes and native preview behavior:

```powershell
dotnet run --configuration Release --project samples/EnterpriseUI.WinFormsDemo -- --verify-themes artifacts/parity-verification
```
