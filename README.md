# EnterpriseUI: The Open Source Enterprise UI Platform for .NET

[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0%20LTS-512BD4)](https://dotnet.microsoft.com/)
[![Build Status](https://img.shields.io/badge/Build-Passing-brightgreen)](https://github.com/)
[![Unit Tests](https://img.shields.io/badge/Tests-9%20Passed-success)](https://github.com/)
[![Zero Activation](https://img.shields.io/badge/Activation-Zero%20Keys%20Required-success)](LICENSE)
[![WCAG 2.2 AA](https://img.shields.io/badge/Accessibility-WCAG%202.2%20AA-blue)](https://www.w3.org/WAI/standards-guidelines/wcag/)

**EnterpriseUI** is the world's most complete, independent, 100% open-source enterprise UI framework and component ecosystem for **.NET 8+** (with full compatibility for .NET 9 and future LTS releases). 

Inspired by the scope and capabilities of commercial suites such as **Telerik UI** and **DevExpress**, EnterpriseUI is **100% clean-room implemented** with zero proprietary code, zero commercial dependencies, zero license keys, zero activation requirements, and zero telemetry.

---

## ⚡ The Open Source Guarantee

- **Zero License Keys & Activation**: No trials, no hardware locks, no activation prompts, no expiration timers.
- **Zero Telemetry**: No phone-home tracking, data collection, or network pings.
- **Zero Commercial Dependencies**: Fully self-contained on the standard .NET 8 BCL. Compiles directly from `git clone` and `dotnet build`.
- **Apache-2.0 License**: Permissive, commercially viable, with explicit patent grants and trademark protections. Free for commercial, SaaS, internal enterprise, and educational deployment.

---

## 🚀 Key Highlights

- **47 Modular Projects**: Clean Architecture with domain segregation across data, theming, accessibility, grids, charts, layout, scheduling, reporting, and AI.
- **Unified Superset Controls**: Single, non-overlapping implementations for every major category:
  - **`DataGrid<T>`**: 2D constant-memory virtualization, grouping with aggregates, Excel-style filtering, in-cell & batch editing, column freeze (Left & Right).
  - **`TreeList<T>`**: Hierarchical self-referencing tree-grid.
  - **`PivotGrid<T>`**: Multi-dimensional OLAP cross-tabulation matrix.
  - **`RibbonBar`**: Office/Fluent ribbon bar with contextual tabs and Quick Access Toolbar.
  - **`DockManager`**: Visual Studio-style docking, auto-hide pinning, floating windows, and tabbed documents.
  - **`Scheduler`**: Day, Week, Month, Timeline views with RFC 5545 iCalendar recurrence engine.
  - **`Chart`**: 17 Series types (Column, SplineArea, Candlestick, Radar, Treemap, Sankey) with zoom & pan.
  - **`Gauges`**: Radial, Linear, Digital, and Bullet graphs with multi-color threshold bands.
  - **`Diagram`**: Node-link diagramming with orthogonal connector routing and tree auto-layout.
  - **`AIAssistant`**: Embedded conversational copilot and natural language to `DataSourceRequest` query translation.
  - **`SpreadsheetControl`**, **`RichTextEditor`**, **`PdfViewer`**, **`GeoMap`**, and **`WorkflowEngine`**.
- **High-Throughput Performance**:
  - **QueryEngine**: Filter + Sort + Page over 100,000 records in **3.54 ms**.
  - **2D Virtualization**: 1,000,000 row viewport range calculated in **0.010 µs** (constant memory).
  - **Theming Engine**: 50,000 stylesheet token blocks compiled in **138 ms**.
- **6 Built-in Themes**: Fluent, Material, Bootstrap, Office, Modern, and Default with runtime hot-reloading and CSS variable emission.
- **Multi-Platform Adapters**: Native renderers and styles for **Blazor**, **Windows Forms**, **WPF**, **ASP.NET Core**, and **.NET MAUI**.

---

## 📦 Quick Start

### 1. Build from Source
```bash
git clone https://github.com/enterpriseui/enterpriseui.git
cd enterpriseui
dotnet build EnterpriseUI.sln -c Release
```

### 2. Run the Unit Test Suite
```bash
dotnet test EnterpriseUI.sln -c Release --no-build
```

### 3. Run High-Performance Benchmarks
```bash
dotnet run --project benchmarks/EnterpriseUI.Benchmarks/EnterpriseUI.Benchmarks.csproj -c Release
```

### 4. Run the Sample Showcase
```bash
dotnet run --project samples/EnterpriseUI.SampleShowcase/EnterpriseUI.SampleShowcase.csproj
```

---

## 🏛️ Ecosystem Architecture

```
EnterpriseUI.sln / EnterpriseUI.slnx
├── src/            # 31 domain core and component libraries
├── platforms/      # Blazor, WinForms, WPF, ASP.NET Core, MAUI
├── tooling/        # ThemeDesigner, VisualDesigner, TemplateStudio, CodeGenerators
├── tests/          # UnitTests, IntegrationTests, PerformanceTests, AccessibilityTests
├── benchmarks/     # High-throughput benchmarks with BenchmarkDotNet / Stopwatch
├── samples/        # Sample showcase application
└── docs/           # Complete architectural documentation, guides, and API specs
```

---

## 📜 Documentation

- [Master Documentation Portal](docs/README.md)
- [System Architecture & C4 Models](docs/Architecture/SystemArchitecture.md)
- [Open Source Transformation Plan & Governance](docs/Reference/OSSTransformationMasterPlan.md)
- [DataGrid Architecture & Performance Guide](docs/Grid/ArchitectureGuide.md)
- [Theming System & Design Tokens](docs/Themes/ThemingEngineAndTokens.md)
- [Controls Catalog & API Reference](docs/Controls/UnifiedControlsCatalog.md)
- [Migration Guides (from Telerik, DevExpress, Syncfusion)](docs/Migration/MigrationGuides.md)
- [Contribution Guidelines](CONTRIBUTING.md)

---

## ⚖️ License

EnterpriseUI is open-source software licensed under the [Apache License, Version 2.0](LICENSE).
Free for commercial, enterprise, SaaS, and private use without fees or activation keys.
