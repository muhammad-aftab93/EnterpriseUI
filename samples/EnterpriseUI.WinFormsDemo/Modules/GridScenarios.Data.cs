using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Data;
using EnterpriseUI.Exporting;
using EnterpriseUI.Grids;
using EnterpriseUI.Printing;
using EnterpriseUI.Theming;
using EnterpriseUI.Theming.Themes;
using EnterpriseUI.WinForms;

namespace EnterpriseUI.WinFormsDemo.Modules;

public static partial class GridScenarios
{
    private static async Task WaitForLoadAsync<TItem>(EnterpriseDataGrid<TItem> grid)
    {
        await Task.Delay(1);
        while (grid.IsLoading) await Task.Delay(2);
    }

    private static async Task<long> TimeAsync<TItem>(EnterpriseDataGrid<TItem> grid, Action apply)
    {
        var sw = Stopwatch.StartNew();
        apply();
        await WaitForLoadAsync(grid);
        return sw.ElapsedMilliseconds;
    }

    private static IEnumerable<GridScenario> DataScenarios()
    {
        yield return new GridScenario
        {
            Id = "grid-exporting",
            Title = "Exporting - Excel, CSV, PDF, HTML, JSON and XML",
            Description = "Export every row matching the current query in a streaming pass with progress and cancel. Groups, summaries and conditional formatting carry into the file.",
            Features = new[] { "xlsx", "csv", "pdf", "html", "json", "xml", "Group outline", "Summaries", "Conditional formatting", "Progress/cancel" },
            Documentation = "GridExporter builds an export document from the grid (columns, bands, groups, hierarchy, summaries) and ExportService streams it through a provider. Files are written atomically: nothing appears when the export fails or is cancelled.",
            Usage = "await grid.ExportToFileAsync(\"report.xlsx\", new GridExportOptions { IncludeGroups = true }); or grid.ExportWithDialogAsync(\"pdf\").",
            Architecture = "Providers implement IExportProvider and receive rows one at a time, so memory stays flat for large exports; the service pumps progress and cancellation.",
            Performance = "CSV and JSON stream with constant memory; xlsx writes worksheet XML directly without an intermediate workbook object.",
            Accessibility = "Progress and completion are announced; exported PDFs carry the document title and real text (not images).",
            Troubleshooting = "A CSV opened in Excel can run formulas; CsvSanitizeFormulas (on by default) prefixes dangerous cells so they stay text.",
            Api = "GridExporter -> ExportAsync, ExportToFileAsync, CreateDocumentAsync; GridExportOptions; ExportOptions; ExportService.Register(IExportProvider)",
            Build = host =>
            {
                var grid = RecordGrid(GridScenarioData.Records(10_000), "Exportable records");
                grid.Columns.First(c => c.Field == "Total").FooterSummaries.Add(new GridColumnSummary(AggregateFunction.Sum, "C0"));
                grid.Columns.First(c => c.Field == "Quantity").FooterSummaries.Add(new GridColumnSummary(AggregateFunction.Average, "0.0"));
                grid.FormatRules.Add(new GridFormatRule { Name = "Gross bar", ColumnField = "Total", RuleType = FormatRuleType.DataBar, Value1 = 0m, Value2 = 60000m, BackgroundColorHex = "#5088d7" });
                grid.GroupBy("Region");
                host.Attach(grid);

                var groups = true;
                var summaries = true;
                var formatting = true;
                var pageOnly = false;
                host.Toggle("Groups", true, v => groups = v);
                host.Toggle("Summaries", true, v => summaries = v);
                host.Toggle("Formatting", true, v => formatting = v);
                host.Toggle("Current page only", false, v => pageOnly = v);
                host.Separator();

                async Task Export(string extension, string filter)
                {
                    using var dialog = new SaveFileDialog { Filter = filter, FileName = "grid-export" + extension, DefaultExt = extension.TrimStart('.') };
                    if (dialog.ShowDialog() != DialogResult.OK) return;
                    var options = new GridExportOptions { Title = "Sales export", IncludeGroups = groups, IncludeSummaries = summaries, ApplyConditionalFormatting = formatting, CurrentPageOnly = pageOnly };
                    var progress = new Progress<ExportProgress>(p => host.Metric($"Exporting... {p.RowsProcessed:N0} rows"));
                    var result = await grid.ExportToFileAsync(dialog.FileName, options, null, progress);
                    host.Metric($"Exported {result.Rows:N0} rows to {Path.GetFileName(dialog.FileName)} ({result.Bytes / 1024d:N0} KB in {result.Duration.TotalMilliseconds:N0} ms)");
                    host.Log("DataExported", $"{result.FormatId}: {result.Rows:N0} rows, {result.Bytes:N0} bytes");
                }

                host.AsyncButton("Excel (.xlsx)", () => Export(".xlsx", "Excel Workbook (*.xlsx)|*.xlsx"));
                host.AsyncButton("CSV", () => Export(".csv", "CSV (*.csv)|*.csv"));
                host.AsyncButton("PDF", () => Export(".pdf", "PDF (*.pdf)|*.pdf"));
                host.AsyncButton("HTML", () => Export(".html", "HTML (*.html)|*.html"));
                host.AsyncButton("JSON", () => Export(".json", "JSON (*.json)|*.json"));
                host.AsyncButton("XML", () => Export(".xml", "XML (*.xml)|*.xml"));
                host.AsyncButton("PDF with progress dialog", () => grid.ExportWithDialogAsync("pdf"));
                return grid;
            }
        };

        yield return new GridScenario
        {
            Id = "grid-printing",
            Title = "Printing - Page Setup, Scaling, Templates and Preview",
            Description = "Preview and print with repeated header rows, fit-to-width or percent scaling, header and footer tokens, and multi-page column splitting.",
            Features = new[] { "Print preview", "Page setup", "Fit to width/page", "Repeat headers", "Header/footer tokens", "Templates", "Page order" },
            Documentation = "PrintPaginator turns the export document into pages of plain geometry (points); the WinForms control renders those pages to PrintDocument. The same plan can drive PDF output.",
            Usage = "grid.PrintTemplate = PrintTemplate.LandscapeReport(); await grid.ShowPrintPreviewDialogAsync(); var plan = await grid.CreatePrintPlanAsync(); plan.PageCount.",
            Architecture = "Pagination is pure and testable: given a document and a template it returns PrintPage objects with column slices, scale and placed rows.",
            Api = "PrintTemplate, PrintPageSettings, PrintPaginator.Paginate, PrintPlan; EnterpriseDataGrid<T> -> PrintTemplate, ShowPrintPreviewDialogAsync, PrintAsync, ShowPageSetupDialog, CreatePrintPlanAsync",
            Build = host =>
            {
                var grid = RecordGrid(GridScenarioData.Records(400), "Printable records");
                grid.PrintTemplate.Title = "Sales Report";
                grid.PrintTemplate.HeaderLeft = "{title}";
                grid.PrintTemplate.HeaderRight = "{date}";
                grid.PrintTemplate.FooterCenter = "Page {page} of {pages}";
                host.Attach(grid);

                async Task Pages()
                {
                    var plan = await grid.CreatePrintPlanAsync();
                    host.Metric($"{plan.PageCount} page(s) | {plan.TotalRows:N0} rows | scale {plan.Pages[0].Scale:P0} | {plan.Template.Settings.PaperSize} {(plan.Template.Settings.IsLandscape ? "landscape" : "portrait")}");
                }

                host.Combo("Template:", new[] { "Standard", "LandscapeReport", "Compact" }, "Standard", v =>
                {
                    var template = PrintTemplateRegistry.Default.Find(v)?.Clone() ?? PrintTemplate.Standard();
                    template.Title = "Sales Report";
                    template.HeaderLeft = "{title}";
                    template.HeaderRight = "{date}";
                    template.FooterCenter = "Page {page} of {pages}";
                    grid.PrintTemplate = template;
                });
                host.Combo("Scale:", Enum.GetNames<PrintScaleMode>(), nameof(PrintScaleMode.FitToWidth), v => grid.PrintTemplate.Settings.ScaleMode = Enum.Parse<PrintScaleMode>(v));
                host.Toggle("Landscape", false, v => grid.PrintTemplate.Settings.IsLandscape = v);
                host.Toggle("Repeat header", true, v => grid.PrintTemplate.Settings.RepeatHeaderRows = v);
                host.Toggle("Grid lines", true, v => grid.PrintTemplate.Settings.ShowGridLines = v);
                host.Separator();
                host.AsyncButton("Count pages", Pages);
                host.AsyncButton("Print preview", () => grid.ShowPrintPreviewDialogAsync());
                host.Button("Page setup...", () => grid.ShowPageSetupDialog());
                host.AsyncButton("Print...", () => grid.PrintAsync());
                return grid;
            }
        };

        yield return new GridScenario
        {
            Id = "grid-performance",
            Title = "Performance - One Million Rows, Timed Operations",
            Description = "Bind, sort, filter and group one million in-memory rows and see the elapsed time of each operation measured on this machine.",
            Features = new[] { "1M rows", "Windowed rows", "Timed sort/filter/group", "Materialized groups" },
            Documentation = "Only the rows near the viewport are fetched; sort, filter and group run in the query engine over the source. The timings below are measured live, not quoted.",
            Performance = "Results depend on hardware and data shape. Run the BenchmarkDotNet-style harness in benchmarks/EnterpriseUI.Benchmarks for repeatable numbers (memory, render, scroll, filter and grouping).",
            Troubleshooting = "If grouping a million rows feels slow, group by a low-cardinality column and collapse groups by default.",
            Api = "DataGrid<T> -> EnableVirtualization, RowBuffer, EnsureRowsLoaded, MaxMaterializedRows, RefreshDataAsync",
            Build = host =>
            {
                var grid = RecordGrid(GridScenarioData.Records(1000), "Performance records");
                host.Attach(grid);

                host.AsyncButton("Generate and bind 1,000,000 rows", async () =>
                {
                    host.Metric("Generating rows...");
                    var data = await Task.Run(() => GridScenarioData.Records(1_000_000));
                    var ms = await TimeAsync(grid, () => grid.DataSource = data);
                    host.Metric($"Bound 1,000,000 rows in {ms:N0} ms (rendering only fetches the visible window).");
                    host.Log("Bind", $"1M rows in {ms} ms");
                });
                host.AsyncButton("Sort by Total desc", async () => { var ms = await TimeAsync(grid, () => grid.SortBy("Total", ListSortDirection.Descending)); host.Metric($"Sort over {grid.TotalRecordCount:N0} rows: {ms:N0} ms"); });
                host.AsyncButton("Filter Region = EMEA", async () => { var ms = await TimeAsync(grid, () => grid.FilterBy("Region", FilterOperator.IsEqualTo, "EMEA")); host.Metric($"Filter -> {grid.TotalRecordCount:N0} rows: {ms:N0} ms"); });
                host.AsyncButton("Group by Region", async () => { var ms = await TimeAsync(grid, () => grid.GroupBy("Region")); host.Metric($"Group over {grid.TotalRecordCount:N0} rows: {ms:N0} ms"); });
                host.AsyncButton("Reset query", async () => { var ms = await TimeAsync(grid, () => { grid.ClearGroups(); grid.ClearSort(); grid.ClearFilters(); }); host.Metric($"Reset: {ms:N0} ms"); });
                host.Toggle("Virtualization", true, v => grid.EnableVirtualization = v);
                return grid;
            }
        };

        yield return new GridScenario
        {
            Id = "grid-accessibility",
            Title = "Accessibility - Keyboard, Screen Reader and Contrast Audit",
            Description = "Navigate with the keyboard only, run the WCAG contrast audit for the active theme, and read what a screen reader hears for the grid, a row and a cell.",
            Features = new[] { "UI Automation tree", "Keyboard map", "Contrast audit", "Live-region announcements", "High contrast", "Focus ring" },
            Documentation = "The grid exposes a Table > Row > Cell UI Automation tree with names built from the live state (sorted, filtered, selected, error). Colors are derived from theme tokens and corrected to meet WCAG 2.2 AA for text and non-text indicators.",
            Usage = "grid.AriaLabel = \"Sales records\"; grid.DescribeGrid(); GridAccessibility.Audit(theme).",
            Accessibility = "Windows high contrast uses system colors; selection, focus, errors and search matches stay distinguishable without color through shape, outline and text.",
            Api = "GridAccessibility -> DescribeGrid, DescribeRow, DescribeCell, DescribeColumnHeader, Audit, KeyboardMap; GridPalette.FromTheme",
            Build = host =>
            {
                var grid = RecordGrid(GridScenarioData.Records(300), "Sales records");
                grid.SelectionMode = GridSelectionMode.CellRange;
                grid.Columns.First(c => c.Field == "Quantity").ValidationRules.Add(new RangeRule(1, 100) { Message = "Quantity must be 1 to 100" });
                host.Attach(grid);

                host.Button("Contrast audit", () =>
                {
                    var checks = GridAccessibility.Audit(ThemeManager.Instance.CurrentTheme);
                    var failed = checks.Where(c => !c.Passes).ToList();
                    host.Metric($"{ThemeManager.Instance.CurrentTheme.Name}: {checks.Count - failed.Count}/{checks.Count} contrast checks pass" + (failed.Count > 0 ? $" (failing: {string.Join(", ", failed.Select(f => f.Name))})" : string.Empty));
                    foreach (var check in checks) host.Log("ContrastCheck", $"{check.Name}: {check.Ratio:0.0}:1 (needs {check.Required:0.0}) {(check.Passes ? "pass" : "FAIL")}");
                });
                host.Button("Describe grid", () => host.Metric(grid.DescribeGrid()));
                host.Button("Describe focus cell", () =>
                {
                    var f = grid.Selection;
                    var columns = GridColumnLayout.OrderVisible(grid.Columns);
                    if (f.FocusedRow >= 0 && grid.TryGetRowAt(f.FocusedRow, out var row) && f.FocusedColumn >= 0)
                        host.Metric(grid.DescribeCell(row, columns[f.FocusedColumn], f.FocusedColumn, columns.Count));
                    else host.Metric("Focus a cell first (click a cell or press an arrow key).");
                });
                host.Button("Keyboard map", () => MessageBox.Show(string.Join(Environment.NewLine, GridAccessibility.KeyboardMap.Select(k => $"{k.Keys,-22} {k.Action}")), "DataGrid keyboard map"));
                host.Button("High contrast theme", () => ThemeManager.Instance.ApplyTheme(BuiltInThemes.CreateHighContrast(true)));
                host.Button("Enter invalid quantity", () => { if (grid.TryGetItemAt(0, out var item)) host.Metric(grid.TryEditCell(item, "Quantity", 500).Success ? "Accepted" : "Rejected: Quantity must be 1 to 100 (announced to screen readers)"); });
                return grid;
            }
        };

        yield return new GridScenario
        {
            Id = "grid-theming",
            Title = "Theming - Fluent, Office, Material, Dark and High Contrast",
            Description = "Switch themes live. Every grid element (rows, selection, focus, search matches, errors, pager, summaries) takes its color from the theme tokens.",
            Features = new[] { "Theme switch", "Dark schemes", "High contrast", "Token-derived selection/focus/error colors", "WCAG-corrected palette" },
            Documentation = "GridPalette.FromTheme resolves #hex and rgba() tokens, composites translucent colors over the surface and raises any color that would fall below its contrast requirement.",
            Usage = "ThemeManager.Instance.ApplyTheme(\"Fluent-Dark\"); var palette = GridPalette.FromTheme(ThemeManager.Instance.CurrentTheme);",
            Accessibility = "High contrast themes use the text color for borders and a solid primary for selection so state never depends on subtle tints.",
            Api = "ThemeManager -> ApplyTheme, AvailableThemes; GridPalette -> FromTheme, ContrastRatio, EnsureContrast; GridAccessibility.Audit",
            Build = host =>
            {
                var grid = RecordGrid(GridScenarioData.Records(400), "Themed records");
                grid.SelectionMode = GridSelectionMode.CellRange;
                grid.ShowFilterRow = true;
                grid.PagingMode = GridPagingMode.Client;
                grid.PageSize = 25;
                grid.Columns.First(c => c.Field == "Total").FooterSummaries.Add(new GridColumnSummary(AggregateFunction.Sum, "C0"));
                grid.FormatRules.Add(new GridFormatRule { Name = "Critical", ColumnField = "Priority", RuleType = FormatRuleType.HighlightCell, Operator = FormatConditionOperator.Equal, Value1 = "Critical", BackgroundColorHex = "#fff0c2", ForegroundColorHex = "#713f12" });
                grid.SearchPanelText = "con";
                host.Attach(grid);

                void Describe(ITheme theme)
                {
                    var palette = GridPalette.FromTheme(theme);
                    host.Metric($"{theme.Name}: text/surface {GridPalette.ContrastRatio(palette.Text, palette.Surface):0.0}:1 | selection {GridPalette.ContrastRatio(palette.SelectionText, palette.SelectionBack):0.0}:1 | error indicator {GridPalette.ContrastRatio(palette.Danger, palette.Surface):0.0}:1 | high contrast: {palette.HighContrast}");
                }

                var names = ThemeManager.Instance.AvailableThemes.Select(t => t.Name).OrderBy(n => n).ToList();
                host.Combo("Theme:", names, ThemeManager.Instance.CurrentTheme.Name, v => { ThemeManager.Instance.ApplyTheme(v); Describe(ThemeManager.Instance.CurrentTheme); });
                host.Button("Fluent light", () => ThemeManager.Instance.ApplyTheme(BuiltInThemes.CreateFluent(ColorScheme.Light)));
                host.Button("Fluent dark", () => ThemeManager.Instance.ApplyTheme(BuiltInThemes.CreateFluent(ColorScheme.Dark)));
                host.Button("High contrast", () => ThemeManager.Instance.ApplyTheme(BuiltInThemes.CreateHighContrast(true)));
                Describe(ThemeManager.Instance.CurrentTheme);
                return grid;
            }
        };

        yield return new GridScenario
        {
            Id = "grid-realtime",
            Title = "Real-Time Data - Live Ticks with Property Change Tracking",
            Description = "Quotes implement INotifyPropertyChanged and tick many times a second. The grid repaints changed rows, keeps selection and re-sorts when a sorted column changes.",
            Features = new[] { "INotifyPropertyChanged tracking", "Live re-sort", "Conditional colors", "Update rate control" },
            Documentation = "The control watches items of the visible window. Changes that cannot affect the query only repaint; changes to sorted, grouped or filtered values trigger a debounced re-query (AutoRefreshDelay).",
            Usage = "Make the row type raise PropertyChanged and bind it; set ObserveItemChanges = false to pause, and call Engine.NotifyDataChanged() after bulk external changes.",
            Performance = "Observers are limited to the rendered window (at most 2,000 items), so streaming into a million-row source does not subscribe a million handlers.",
            Api = "EnterpriseDataGrid<T> -> ObserveItemChanges; DataGrid<T> -> AutoRefreshDelay, AutoRefreshOnCollectionChange, NotifyDataChanged",
            Build = host =>
            {
                var quotes = GridScenarioData.Quotes(500);
                var grid = new EnterpriseDataGrid<LiveQuote> { Dock = DockStyle.Fill, AriaLabel = "Live quotes", ItemHeight = 26, ShowGroupPanel = false };
                grid.Columns.Add(new GridColumn { Field = "Symbol", Title = "Symbol", Width = 90 });
                grid.Columns.Add(new GridColumn { Field = "Name", Title = "Name", Width = 190 });
                grid.Columns.Add(new GridColumn { Field = "Price", Title = "Price", FormatString = "N2", Width = 100, Alignment = ColumnTextAlignment.Right });
                grid.Columns.Add(new GridColumn { Field = "ChangePercent", Title = "Change %", FormatString = "P2", Width = 100, Alignment = ColumnTextAlignment.Right });
                grid.Columns.Add(new GridColumn { Field = "Volume", Title = "Volume", FormatString = "N0", Width = 110, Alignment = ColumnTextAlignment.Right });
                grid.Columns.Add(new GridColumn { Field = "Updated", Title = "Updated", FormatString = "HH:mm:ss.fff", Width = 120 });
                grid.FormatRules.Add(new GridFormatRule { Name = "Up", ColumnField = "ChangePercent", RuleType = FormatRuleType.HighlightCell, Operator = FormatConditionOperator.GreaterThan, Value1 = 0m, BackgroundColorHex = "#DFF6DD", ForegroundColorHex = "#0B6A0B" });
                grid.FormatRules.Add(new GridFormatRule { Name = "Down", ColumnField = "ChangePercent", RuleType = FormatRuleType.HighlightCell, Operator = FormatConditionOperator.LessThan, Value1 = 0m, BackgroundColorHex = "#FDE7E9", ForegroundColorHex = "#A4262C" });
                grid.DataSource = quotes;
                host.Attach(grid);

                var rng = new Random(5);
                var updates = 0L;
                var perTick = 20;
                var timer = new System.Windows.Forms.Timer { Interval = 100 };
                var started = Stopwatch.StartNew();
                timer.Tick += (_, _) =>
                {
                    for (var i = 0; i < perTick; i++)
                    {
                        var quote = quotes[rng.Next(quotes.Count)];
                        var move = (decimal)(rng.NextDouble() - 0.5) * 2m;
                        var newPrice = Math.Max(1m, Math.Round(quote.Price + move, 2));
                        quote.ChangePercent = (newPrice - quote.Price) / quote.Price;
                        quote.Price = newPrice;
                        quote.Volume += rng.Next(1, 500);
                        quote.Updated = DateTime.Now;
                        updates++;
                    }
                    host.Metric($"{updates:N0} updates | {updates / Math.Max(1, started.Elapsed.TotalSeconds):N0} updates/s | {grid.AutoRefreshDelay.TotalMilliseconds:N0} ms re-query debounce");
                };
                grid.Disposed += (_, _) => timer.Dispose();

                host.Toggle("Stream ticks", false, v => { if (v) { started.Restart(); updates = 0; timer.Start(); } else timer.Stop(); });
                host.Combo("Rate:", new[] { "50/s", "200/s", "1,000/s" }, "200/s", v => perTick = v.StartsWith("50") ? 5 : v.StartsWith("1") ? 100 : 20);
                host.Button("Sort by change %", () => grid.SortBy("ChangePercent", ListSortDirection.Descending));
                host.Button("Clear sort", grid.ClearSort);
                host.Toggle("Track changes", true, v => grid.ObserveItemChanges = v);
                return grid;
            }
        };

        yield return new GridScenario
        {
            Id = "grid-large-data",
            Title = "Large Data - Remote Source, Server Paging and Failure Recovery",
            Description = "Reads a 500,000-row store through an asynchronous data source with simulated latency. Sorting, filtering and paging run in the data source; failures show an error state you can retry.",
            Features = new[] { "IDataSource<T>", "Server paging", "Latency handling", "Cancellation of stale requests", "Error state and retry", "Infinite scroll" },
            Documentation = "A custom IDataSource<T> receives a DataSourceRequest (page, sorts, filters, groups) and returns a DataSourceResult. The grid cancels superseded requests and shows Loading... while waiting.",
            Usage = "grid.CustomDataSource = new RemoteDataSource<T>(async (request, ct) => await api.QueryAsync(request, ct)); grid.PagingMode = GridPagingMode.Server;",
            Architecture = "Remote requests are described by DataSourceRequest and can be translated to OData with ODataQueryBuilder.",
            Troubleshooting = "If rows never appear, check LastError; a source that throws puts the grid in an error state until RefreshDataAsync succeeds.",
            Api = "IDataSource<T>, RemoteDataSource<T>, DataSourceRequest, DataSourceResult<T>, ODataQueryBuilder; DataGrid<T> -> CustomDataSource, LastError, DataLoadFailed",
            Build = host =>
            {
                var store = new List<GridDemoRecord>();
                var requests = 0;
                var failing = false;
                var latency = 120;
                var grid = RecordGrid(Array.Empty<GridDemoRecord>(), "Remote records");
                grid.PagingMode = GridPagingMode.Server;
                grid.PageSize = 50;
                host.Attach(grid);

                grid.CustomDataSource = new RemoteDataSource<GridDemoRecord>(async (request, ct) =>
                {
                    Interlocked.Increment(ref requests);
                    await Task.Delay(latency, ct);
                    if (failing) throw new IOException("The remote service is unavailable (simulated).");
                    return QueryEngine.Execute(store.AsQueryable(), request);
                });

                grid.DataLoadFailed += (_, e) => host.Metric($"Load failed: {e.Exception.Message} - press Retry.");
                grid.DataWindowChanged += (_, _) => host.Metric($"{grid.TotalRecordCount:N0} rows in store | {Volatile.Read(ref requests)} requests | {grid.PagerText}");

                host.AsyncButton("Load 500,000 rows", async () =>
                {
                    host.Metric("Generating the remote store...");
                    var data = await Task.Run(() => GridScenarioData.Records(500_000));
                    store.Clear();
                    store.AddRange(data);
                    await grid.RefreshDataAsync();
                });
                host.Combo("Mode:", new[] { "Server", "Virtual", "Infinite" }, "Server", v => grid.PagingMode = Enum.Parse<GridPagingMode>(v));
                host.Combo("Latency:", new[] { "0 ms", "120 ms", "800 ms" }, "120 ms", v => latency = int.Parse(v.Split(' ')[0]));
                host.Toggle("Simulate failure", false, v => failing = v);
                host.AsyncButton("Retry", () => grid.RefreshDataAsync());
                host.Button("Sort by Total desc", () => grid.SortBy("Total", ListSortDirection.Descending));
                host.Button("Filter Region = APAC", () => grid.FilterBy("Region", FilterOperator.IsEqualTo, "APAC"));
                host.Button("Reset", () => { grid.ClearSort(); grid.ClearFilters(); });
                return grid;
            }
        };

        yield return new GridScenario
        {
            Id = "grid-master-detail",
            Title = "Master Detail - Orders per Customer, Lazy Loading and Property Summary",
            Description = "Expand a customer to see its orders in a nested table. Turn on lazy loading to fetch children on first expansion, or remove the relation to fall back to a property summary.",
            Features = new[] { "Master/detail", "Lazy child loading", "Property summary fallback", "Expand/collapse all", "Search in details" },
            Documentation = "With EnableMasterDetail and a relation, each parent row can expand to a child table. Without a relation the grid shows the parent's properties in a summary row.",
            Usage = "var relation = grid.AddRelation(\"Orders\", \"Id\", \"CustomerId\", orders, typeof(OrderRow)); relation.LoadChildrenAsync = async (parent, ct) => await api.OrdersAsync(parent, ct);",
            Accessibility = "Expanded state is exposed on the row; Alt+Right expands and Alt+Left collapses; the child table's header row is announced with its title.",
            Api = "DataGrid<T> -> EnableMasterDetail, AddRelation, RemoveRelation, ToggleDetailExpansion, ExpandAllDetails; GridHierarchyRelation -> LoadChildrenAsync, DetailItemsSelector",
            Build = host =>
            {
                var (customers, orders, _) = GridScenarioData.Sales(60);
                var grid = new EnterpriseDataGrid<CustomerRow> { Dock = DockStyle.Fill, AriaLabel = "Customers", EnableMasterDetail = true, ShowGroupPanel = false };
                grid.Columns.Add(new GridColumn { Field = "Id", Title = "ID", Width = 70 });
                grid.Columns.Add(new GridColumn { Field = "Name", Title = "Customer", Width = 240 });
                grid.Columns.Add(new GridColumn { Field = "Country", Title = "Country", Width = 120 });
                grid.Columns.Add(new GridColumn { Field = "Segment", Title = "Segment", Width = 120 });
                var relation = grid.AddRelation("Orders", "Id", "CustomerId", orders, typeof(OrderRow));
                relation.ChildColumns.Add(new GridColumn { Field = "OrderId", Title = "Order", Width = 90 });
                relation.ChildColumns.Add(new GridColumn { Field = "OrderDate", Title = "Date", FormatString = "d", Width = 100 });
                relation.ChildColumns.Add(new GridColumn { Field = "Amount", Title = "Amount", FormatString = "C2", Width = 110, Alignment = ColumnTextAlignment.Right });
                relation.ChildColumns.Add(new GridColumn { Field = "Status", Title = "Status", Width = 100 });
                grid.DataSource = customers;
                host.Attach(grid);

                host.Button("Expand all", grid.ExpandAllDetails);
                host.Button("Collapse all", grid.CollapseAllDetails);
                host.Toggle("Lazy load orders", false, v =>
                {
                    relation.LoadChildrenAsync = v
                        ? async (parent, ct) => { await Task.Delay(300, ct); return orders.Where(o => o.CustomerId == ((CustomerRow)parent).Id).ToList(); }
                        : null;
                    grid.NotifyRelationsChanged();
                });
                host.Button("Remove relation (property summary)", () => { grid.RemoveRelation("Orders"); grid.NotifyRelationsChanged(); });
                host.Toggle("Search panel", false, v => grid.ShowSearchPanel = v);
                host.Metric($"{customers.Count} customers, {orders.Count} orders");
                return grid;
            }
        };

        yield return new GridScenario
        {
            Id = "grid-treegrid",
            Title = "TreeGrid - Self-Referencing Organization with Budget Roll-Ups",
            Description = "Rows form a tree through ParentId. Expand and collapse nodes with the chevron or Alt+Right/Left; search expands the path to each match; summaries stay exact.",
            Features = new[] { "Self-reference tree", "Expand to reveal search matches", "Cycle-safe", "Column summaries", "Sorting within levels" },
            Documentation = "AddSelfReference(keyField, parentField) turns the grid's own rows into a tree. Sorting orders siblings, filtering keeps the ancestors of matches, and cyclic parent chains are detected instead of looping.",
            Usage = "grid.AddSelfReference(\"Id\", \"ParentId\"); grid.TreeExpandedByDefault = true; grid.ExpandAllTreeNodes().",
            Accessibility = "Tree rows announce level and expanded state; the chevron is also reachable with Alt+Right and Alt+Left.",
            Api = "DataGrid<T> -> AddSelfReference, TreeExpandedByDefault, ToggleTreeNode, ExpandAllTreeNodes, CollapseAllTreeNodes, IsTreeMode",
            Build = host =>
            {
                var grid = new EnterpriseDataGrid<OrgNode> { Dock = DockStyle.Fill, AriaLabel = "Organization", ShowGroupPanel = false };
                grid.Columns.Add(new GridColumn { Field = "Name", Title = "Unit", Width = 260 });
                grid.Columns.Add(new GridColumn { Field = "Role", Title = "Role", Width = 140 });
                grid.Columns.Add(new GridColumn { Field = "Budget", Title = "Budget", FormatString = "C0", Width = 120, Alignment = ColumnTextAlignment.Right, FooterSummaries = { new GridColumnSummary(AggregateFunction.Sum, "C0") } });
                grid.Columns.Add(new GridColumn { Field = "Headcount", Title = "Headcount", Width = 100, Alignment = ColumnTextAlignment.Right });
                grid.AddSelfReference("Id", "ParentId");
                grid.DataSource = GridScenarioData.Organization();
                host.Attach(grid);

                host.Button("Expand all", grid.ExpandAllTreeNodes);
                host.Button("Collapse all", grid.CollapseAllTreeNodes);
                host.Toggle("Expanded by default", false, v => { grid.TreeExpandedByDefault = v; grid.NotifyRelationsChanged(); });
                host.Button("Sort by budget", () => grid.SortBy("Budget", ListSortDirection.Descending));
                host.Button("Clear sort", grid.ClearSort);
                host.Toggle("Search panel", false, v => grid.ShowSearchPanel = v);
                grid.DataWindowChanged += (_, _) => host.Metric($"{grid.TotalItemsCount} visible tree rows");
                return grid;
            }
        };

        yield return new GridScenario
        {
            Id = "grid-enterprise",
            Title = "Enterprise Scenario - Sales Order Workbench",
            Description = "Everything together: stacked bands, pinned key column, paging, grouping with totals, conditional formatting, batch editing with validation, search, saved layouts, export and print.",
            Features = new[] { "Bands", "Pinning", "Paging", "Group totals", "Conditional formatting", "Batch edit + validation", "Search panel", "Layouts", "Export", "Print" },
            Documentation = "A realistic order-entry workbench built only from public grid features. The Generated Code tabs reproduce this exact configuration, including every toolbar change.",
            Usage = "Start from the Exportable Example tab: it contains the model, the fluent configuration and sample rows as a runnable form.",
            BestPractices = "Validate on the column, edit in batch mode for review-and-save workflows, persist layouts per user, and export from the grid so the file matches what the user sees.",
            Api = "GridBuilder<T>, EnterpriseDataGrid<T>, GridLayoutStore, GridExporter, PrintTemplate",
            Build = host =>
            {
                var grid = RecordGrid(GridScenarioData.Records(20_000), "Sales order workbench");
                var order = new GridColumnBand { Title = "Order" };
                var money = new GridColumnBand { Title = "Pricing" };
                var flow = new GridColumnBand { Title = "Workflow" };
                grid.Bands.AddRange(new[] { order, money, flow });
                foreach (var column in grid.Columns)
                {
                    column.BandId = column.Field switch
                    {
                        "Id" or "Customer" or "Product" or "Region" => order.Id,
                        "UnitPrice" or "Quantity" or "Total" => money.Id,
                        _ => flow.Id
                    };
                }
                grid.PinColumn("Id", ColumnPinPosition.Left);
                grid.Columns.First(c => c.Field == "Quantity").ValidationRules.Add(new RangeRule(1, 999) { Message = "Quantity must be 1 to 999" });
                grid.Columns.First(c => c.Field == "UnitPrice").ValidationRules.Add(new RangeRule(1m, 100000m) { Message = "Unit price must be positive" });
                grid.Columns.First(c => c.Field == "Total").FooterSummaries.Add(new GridColumnSummary(AggregateFunction.Sum, "C0"));
                grid.Columns.First(c => c.Field == "Quantity").FooterSummaries.Add(new GridColumnSummary(AggregateFunction.Average, "0.0"));
                grid.FormatRules.Add(new GridFormatRule { Name = "Gross bar", ColumnField = "Total", RuleType = FormatRuleType.DataBar, Value1 = 0m, Value2 = 60000m, BackgroundColorHex = "#5088d7" });
                grid.FormatRules.Add(new GridFormatRule { Name = "Critical", ColumnField = "Priority", RuleType = FormatRuleType.HighlightCell, Operator = FormatConditionOperator.Equal, Value1 = "Critical", BackgroundColorHex = "#fff0c2", ForegroundColorHex = "#713f12" });
                grid.GroupSummaries.Add(new GroupSummaryDescriptor("Total", AggregateFunction.Sum, "Sum {0:C0}"));
                grid.PagingMode = GridPagingMode.Client;
                grid.PageSize = 50;
                grid.EditMode = GridEditMode.Batch;
                grid.SelectionMode = GridSelectionMode.CheckboxRow;
                grid.ShowFilterRow = true;
                host.Attach(grid);

                string? layout = null;
                void Report() => host.Metric($"{grid.TotalRecordCount:N0} orders | {grid.PagerText} | pending: {grid.PendingChanges.Count} | errors: {grid.ValidationSummary.Count} | selected: {grid.Selection.SelectedRows.Count}");
                grid.DataWindowChanged += (_, _) => Report();
                grid.SelectionChanged += (_, _) => Report();
                grid.CellEdited += (_, d) => { host.Log("CellEdited", d); Report(); };

                host.Button("Group by Region", () => grid.GroupBy("Region"));
                host.Button("Clear groups", grid.ClearGroups);
                host.Button("Critical only", () => grid.FilterBy("Priority", FilterOperator.IsEqualTo, "Critical"));
                host.Button("Clear filters", () => { grid.ClearFilters(); grid.QuickFilters.Clear(); });
                host.Toggle("Search panel", false, v => grid.ShowSearchPanel = v);
                host.Separator();
                host.Button("Save changes", () => { var r = grid.CommitChanges(); host.Metric(r.Success ? $"Saved {r.Changes.Count} change(s)" : $"{r.Errors.Count} validation error(s) block the save"); });
                host.Button("Discard", () => host.Metric($"Discarded {grid.RollbackChanges()} change(s)"));
                host.Separator();
                host.Button("Save layout", () => { layout = GridLayoutSerializer.ToJson(grid.CreateLayoutProfile("workbench")); host.Metric("Layout saved."); });
                host.Button("Restore layout", () => { if (layout != null) grid.ApplyLayoutProfile(GridLayoutSerializer.FromJson(layout)); else host.Metric("Save a layout first."); });
                host.AsyncButton("Export Excel", async () =>
                {
                    var path = Path.Combine(Path.GetTempPath(), $"orders-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx");
                    var result = await grid.ExportToFileAsync(path);
                    host.Metric($"Exported {result.Rows:N0} rows to {path}");
                });
                host.AsyncButton("Print preview", () => grid.ShowPrintPreviewDialogAsync());
                Report();
                return grid;
            }
        };
    }
}
