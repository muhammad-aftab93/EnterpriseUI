# EnterpriseUI Unified Controls Catalog & API Reference

This catalog documents the unified superset controls available in EnterpriseUI. Every control represents the **merger of Telerik and DevExpress capabilities** into a single, cohesive API.

---

## 1. Catalog Summary

```
Navigation & Layout:
  ├── EnterpriseRibbonBar            # Merged DevExpress RibbonControl & Telerik RadRibbonBar
  ├── EnterpriseDockManager          # Merged DevExpress EnterpriseDockManager & Telerik RadDocking
  ├── EnterpriseTreeView             # Hierarchical navigation with lazy loading
  ├── Window / Dialog      # Modal & modeless draggable, resizable windows
  └── SplitContainer       # Split view container with collapsible splitters

Data & Grids:
  ├── DataGrid<T>          # 2D virtualized high-density grid
  ├── EnterpriseTreeList<T>          # Self-referencing tree-grid
  └── EnterprisePivotGrid<T>         # Multi-dimensional cross-tab OLAP grid

Data Visualization:
  ├── EnterpriseChart                # Cartesian, polar, financial, treemap, and sankey charts
  ├── EnterpriseRadialGauge          # Speedometer, arc, and radial metric gauges
  ├── EnterpriseLinearGauge          # Vertical/horizontal bar gauges with bullet graphs
  └── EnterpriseDiagram              # Node-link diagrams with orthogonal routing

Productivity & Documents:
  ├── EnterpriseScheduler            # Calendar with RFC 5545 recurrence engine
  ├── EnterpriseSpreadsheetControl   # Multi-sheet Excel workbook model
  ├── EnterpriseRichTextEditor       # WYSIWYG document editor with formatting runs
  ├── EnterprisePdfViewer            # PDF viewing, page layout, and form filler
  └── EnterpriseAIAssistant          # Copilot chat assistant & natural language query translator

Utilities & Process:
  ├── EnterpriseGeoMap               # GIS coordinate map with layer clustering
  ├── EnterpriseWorkflowEngine       # Visual process designer & state machine
  └── ToastService         # Screen-pinned toast alerts & notification center
```

---

## 2. Detailed Control Specifications

### 2.1 `EnterpriseRibbonBar` (Navigation)

- **Namespace**: `EnterpriseUI.Navigation`
- **Telerik Equivalent**: `RadRibbonBar` | **DevExpress Equivalent**: `RibbonControl`
- **Superset Capabilities**:
  - Contextual tabs that activate based on selection.
  - Backstage View for file operations (Save, Open, Export, Print).
  - Quick Access Toolbar (QAT) pinned above or below the ribbon.
  - Auto-minimizing ribbon on double-click or toggle button.

```csharp
var ribbon = new EnterpriseRibbonBar();

var homeTab = new RibbonTab { Title = "Home" };
var clipboardGroup = new RibbonGroup { Title = "Clipboard" };

clipboardGroup.Items.Add(new RibbonItem 
{ 
    Text = "Paste", 
    Icon = "fluent:edit", 
    IsLarge = true,
    Command = new RelayCommand(() => PasteClipboard())
});

homeTab.Groups.Add(clipboardGroup);
ribbon.Tabs.Add(homeTab);
```

---

### 2.2 `EnterpriseDockManager` (Layout)

- **Namespace**: `EnterpriseUI.Layout`
- **Telerik Equivalent**: `RadDocking` | **DevExpress Equivalent**: `EnterpriseDockManager` / `DocumentManager`
- **Superset Capabilities**:
  - Visual Studio style docking guides (dock to Left, Right, Top, Bottom, or Center Document).
  - Auto-hide pinned panels along the edges.
  - Floating detachable windows with multi-monitor support.
  - Layout state save and restore (XML/JSON serialization).

```csharp
var dockManager = new EnterpriseDockManager();

var solutionExplorer = new DockPanel 
{ 
    Title = "Solution Explorer", 
    Position = DockPosition.Right, 
    Width = 300 
};

var documentEditor = new DockPanel 
{ 
    Title = "Program.cs", 
    Position = DockPosition.Document 
};

dockManager.Dock(solutionExplorer, DockPosition.Right);
dockManager.Dock(documentEditor, DockPosition.Document);
```

---

### 2.3 `EnterpriseScheduler` (Scheduling)

- **Namespace**: `EnterpriseUI.Scheduling`
- **Telerik Equivalent**: `RadScheduler` | **DevExpress Equivalent**: `SchedulerControl`
- **Superset Capabilities**:
  - 6 View Modes: Day, Week, WorkWeek, Month, Timeline, Agenda.
  - Full **RFC 5545 iCalendar recurrence engine** (`FREQ=WEEKLY;BYDAY=MO,WE,FR;COUNT=10`).
  - Resource grouping (view calendars side-by-side by Doctor, Room, or Vehicle).
  - Drag-and-drop appointment scheduling and edge-resizing.

```csharp
var scheduler = new EnterpriseScheduler
{
    ActiveView = SchedulerViewType.Week,
    DayStartTime = TimeSpan.FromHours(8),
    DayEndTime = TimeSpan.FromHours(18)
};

scheduler.Appointments.Add(new Appointment
{
    Subject = "Sprint Planning",
    Start = DateTime.Today.AddHours(9),
    End = DateTime.Today.AddHours(10).AddMinutes(30),
    RecurrenceRule = "FREQ=WEEKLY;BYDAY=MO"
});
```

---

### 2.4 `EnterpriseChart` (Data Visualization)

- **Namespace**: `EnterpriseUI.Charts`
- **Telerik Equivalent**: `RadChartView` | **DevExpress Equivalent**: `ChartControl`
- **Superset Capabilities**:
  - 17 Series Types: Bar, Column, Line, Spline, Area, SplineArea, Pie, Donut, Scatter, Bubble, Candlestick, OHLC, Radar, Polar, Funnel, Waterfall, Treemap, Sankey.
  - Composite multi-axis support (Categorical, Numeric, DateTime, Logarithmic).
  - Interactive crosshairs, tooltips, and smooth pinch/wheel zoom & pan.

```csharp
var chart = new EnterpriseChart { Title = "Quarterly Performance" };

var series = new ChartSeries 
{ 
    Name = "Gross Margin", 
    Type = SeriesType.SplineArea,
    Color = "#0078d4"
};

series.DataPoints.Add(new ChartDataPoint { Category = "Q1", Value = 125000 });
series.DataPoints.Add(new ChartDataPoint { Category = "Q2", Value = 180000 });
series.DataPoints.Add(new ChartDataPoint { Category = "Q3", Value = 240000 });

chart.Series.Add(series);
```

---

### 2.5 `EnterpriseRadialGauge` & `EnterpriseLinearGauge` (Gauges)

- **Namespace**: `EnterpriseUI.Gauges`
- **Telerik Equivalent**: `RadRadialGauge` / `RadLinearGauge` | **DevExpress Equivalent**: `GaugeControl`
- **Superset Capabilities**:
  - Radial dials, speedometers, arc meters, and linear thermometers.
  - Multi-range color bands (e.g., Green normal, Yellow caution, Red critical).
  - Configurable start/end angles, tick intervals, and pointer needles.

```csharp
var gauge = new EnterpriseRadialGauge
{
    Min = 0,
    Max = 160,
    Value = 72,
    Unit = "MPH"
};

gauge.Ranges.Add(new GaugeRange { From = 0, To = 90, Color = "#107c10" });
gauge.Ranges.Add(new GaugeRange { From = 90, To = 130, Color = "#ffb900" });
gauge.Ranges.Add(new GaugeRange { From = 130, To = 160, Color = "#d13438" });
```

---

### 2.6 `EnterpriseDiagram` (Diagramming)

- **Namespace**: `EnterpriseUI.Diagrams`
- **Telerik Equivalent**: `RadDiagram` | **DevExpress Equivalent**: `DiagramControl`
- **Superset Capabilities**:
  - Predefined shapes: Rectangle, RoundedRectangle, Circle, Diamond, Cylinder, Cloud.
  - Automatic orthogonal and curved connector routing.
  - Built-in hierarchical and tree auto-layout algorithms.

```csharp
var diagram = new EnterpriseDiagram();

var startNode = new DiagramNode { Text = "Order Placed", X = 100, Y = 100, Shape = ShapeType.Circle };
var processNode = new DiagramNode { Text = "Validate Payment", X = 100, Y = 250, Shape = ShapeType.RoundedRectangle };

diagram.Nodes.Add(startNode);
diagram.Nodes.Add(processNode);

diagram.Connectors.Add(new DiagramConnector 
{ 
    SourceNodeId = startNode.Id, 
    TargetNodeId = processNode.Id,
    Label = "On Submit"
});
```

---

### 2.7 `EnterpriseAIAssistant` (AI & Smart Assistant)

- **Namespace**: `EnterpriseUI.AI`
- **Telerik Equivalent**: `AIPrompt` | **DevExpress Equivalent**: `EnterpriseAIAssistant`
- **Superset Capabilities**:
  - Conversational copilot UI embedded seamlessly into layout windows or toolbars.
  - Built-in `TranslateNaturalLanguageToQueryAsync` that converts prompts like *"Show all high priority orders over $10,000 in EMEA"* into an executable `DataSourceRequest`.

```csharp
var aiAssistant = new EnterpriseAIAssistant();

await aiAssistant.SendPromptAsync("Summarize the top 5 customers with overdue accounts.");
```
