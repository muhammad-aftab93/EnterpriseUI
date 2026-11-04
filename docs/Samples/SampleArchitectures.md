# EnterpriseUI Reference Application Architectures

This document details 10 reference enterprise architectures built using the EnterpriseUI platform.

---

## 1. CRM Customer Relationship Portal
- **Key Components**: `DataGrid<Customer>`, `DockManager`, `RibbonBar`, `Timeline`, `ToastService`.
- **Architecture**: Master-detail customer list with docked timeline activities, conversation history, and quick access ribbon actions.

```csharp
var crmGrid = new DataGrid<Customer>();
crmGrid.Columns.Add(new GridColumn { Field = "Name", Title = "Company" });
crmGrid.Columns.Add(new GridColumn { Field = "Tier", Title = "Account Tier" });
crmGrid.Columns.Add(new GridColumn { Field = "LifetimeValue", Title = "LTV", FormatString = "C0" });
crmGrid.EnableMasterDetail = true; // Displays child contacts and opportunity pipeline
```

---

## 2. ERP Supply Chain & Inventory Portal
- **Key Components**: `TreeList<InventoryNode>`, `DataGrid<Shipment>`, `SpreadsheetControl`, `ExportEngine`.
- **Architecture**: Multi-level hierarchical bill of materials (BOM), real-time warehouse inventory tree, and integrated spreadsheet for bulk stock reconciliation.

---

## 3. Financial High-Frequency Trading Platform
- **Key Components**: `DataGrid<OrderBookItem>` (In-Cell editing, 60 FPS repaint), `Chart` (Candlestick / OHLC / Volume bars), `LinearGauge` (Market depth).
- **Architecture**: Zero-allocation high-throughput order book with sub-millisecond Expression Tree sorting and real-time streaming updates.

---

## 4. Healthcare Patient & EMR Portal
- **Key Components**: `Scheduler` (Doctor/Room resource view, RFC 5545 recurrence), `PdfViewer` (Lab results), `SecurityManager` (HIPAA field-level privacy).
- **Architecture**: Color-coded appointment booking, embedded PDF lab reports, and automated permission gating for patient records.

---

## 5. Manufacturing IoT Telemetry Dashboard
- **Key Components**: `RadialGauge` (RPM / Pressure), `Chart` (Vibration waveforms), `Dashboard` (KPI metric cards), `AlertBanner`.
- **Architecture**: Live telemetry streaming with visual threshold indicators (Green/Yellow/Red range bands) and alert notifications.

---

## 6. HR Human Resources & Payroll System
- **Key Components**: `DataGrid<Employee>`, `PivotGrid<PayrollRecord>`, `Reporting.ReportDefinition`.
- **Architecture**: Employee directory with inline role edits, multi-dimensional OLAP payroll cost analysis across departments, and banded pay stub generation.

---

## 7. Project Management & Gantt Suite
- **Key Components**: `Diagram` (PERT chart / dependency routing), `TreeList<TaskItem>`, `WorkflowEngine`.
- **Architecture**: Interactive task breakdown structure with auto-layout hierarchical diagramming and step state tracking.

---

## 8. Executive BI Analytics Portal
- **Key Components**: `Dashboard`, `Chart` (Treemap / Sankey / Waterfall), `PivotGrid`.
- **Architecture**: Drill-down executive summary cards, dynamic cross-filtering between charts and pivot grids.

---

## 9. AI Copilot Enterprise Assistant
- **Key Components**: `AIAssistant`, `DataGrid`, `QueryEngine`.
- **Architecture**: Conversational sidecar that translates natural language prompts (*"Show me top 10 delayed shipments in Europe"*) into compiled `DataSourceRequest` filters applied instantly to the data grid.

---

## 10. Document & Financial Reporting Portal
- **Key Components**: `ReportViewer`, `SpreadsheetControl`, `PdfViewer`, `ExportEngine`.
- **Architecture**: Centralized document repository with multi-format streaming export (Excel XML, PDF, CSV, JSON).
