using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using EnterpriseUI.Data;
using EnterpriseUI.Grids;
using EnterpriseUI.WinForms;

namespace EnterpriseUI.WinFormsDemo.Modules;

/// <summary>The DataGrid workshop: one page per feature area, each driven by the live grid and generating code from its state.</summary>
public static partial class GridScenarios
{
    private const string Related = "EnterpriseDataGrid<T>, DataGrid<T>, GridBuilder<T>, EnterpriseTreeList, EnterprisePivotGrid";

    public static IReadOnlyList<GridScenario> All { get; } = BuildAll();

    private static List<GridScenario> BuildAll()
    {
        var list = new List<GridScenario>();
        list.AddRange(CoreScenarios());
        list.AddRange(DataScenarios());
        return list;
    }

    public static EnterpriseDataGrid<GridDemoRecord> RecordGrid(IEnumerable<GridDemoRecord> data, string label = "Sales records")
    {
        var grid = new EnterpriseDataGrid<GridDemoRecord> { Dock = DockStyle.Fill, AriaLabel = label, ItemHeight = 28 };
        grid.Columns.Add(new GridColumn { Field = "Id", Title = "Record ID", Width = 90, ReadOnly = true });
        grid.Columns.Add(new GridColumn { Field = "Customer", Title = "Enterprise Customer", Width = 180 });
        grid.Columns.Add(new GridColumn { Field = "Product", Title = "Product Line", Width = 170 });
        grid.Columns.Add(new GridColumn { Field = "Region", Title = "Region", Width = 110 });
        grid.Columns.Add(new GridColumn { Field = "UnitPrice", Title = "Unit Price", FormatString = "C2", Width = 100, Alignment = ColumnTextAlignment.Right });
        grid.Columns.Add(new GridColumn { Field = "Quantity", Title = "Qty", Width = 70, Alignment = ColumnTextAlignment.Right });
        grid.Columns.Add(new GridColumn { Field = "Total", Title = "Gross Amount", FormatString = "C2", Width = 110, Alignment = ColumnTextAlignment.Right, ReadOnly = true });
        grid.Columns.Add(new GridColumn { Field = "Priority", Title = "Priority", Width = 90, ComboItems = new object[] { "Low", "Normal", "High", "Critical" } });
        grid.Columns.Add(new GridColumn { Field = "Status", Title = "Status", Width = 100 });
        grid.Columns.Add(new GridColumn { Field = "Timestamp", Title = "Date", FormatString = "d", Width = 100 });
        grid.DataSource = data;
        return grid;
    }

    private static void ReportCounts(ScenarioHost host, EnterpriseDataGrid<GridDemoRecord> grid)
    {
        void Update() => host.Metric($"{grid.TotalRecordCount:N0} records | {grid.Engine.PagerText} | selected: {grid.Selection.SelectedRows.Count}");
        grid.DataWindowChanged += (_, _) => Update();
        grid.SelectionChanged += (_, _) => Update();
        grid.PageChanged += (_, _) => Update();
    }

    private static IEnumerable<GridScenario> CoreScenarios()
    {
        yield return new GridScenario
        {
            Id = "grid-basics",
            Title = "Grid Basics - Columns, Rows and Windowed Rendering",
            Description = "A bound grid with typed columns, alternating rows, a status bar and keyboard navigation. Toggle the chrome and row density; the code tabs follow.",
            Features = new[] { "Data binding", "Typed columns", "Row density", "Keyboard navigation", "Status bar" },
            Documentation = "EnterpriseDataGrid<T> draws with GDI+ from DataGrid<T>, which fetches only the rows near the viewport. Columns map to public properties of T.",
            Usage = "Add GridColumn entries (Field, Title, Width, FormatString), assign DataSource, then optionally call RefreshDataAsync after external changes.",
            BestPractices = "Keep row height fixed, format values with FormatString and give the grid an AriaLabel.",
            Api = "EnterpriseDataGrid<T> -> Columns, DataSource, ItemHeight, EnableAlternateRows, ShowSummaryFooter, AriaLabel",
            Build = host =>
            {
                var grid = host.Attach(RecordGrid(GridScenarioData.Records(200)));
                host.Toggle("Alternate rows", true, v => grid.EnableAlternateRows = v);
                host.Toggle("Filter row", false, v => grid.ShowFilterRow = v);
                host.Toggle("Group panel", true, v => grid.ShowGroupPanel = v);
                host.Toggle("Status bar", true, v => grid.ShowSummaryFooter = v);
                host.Combo("Row height:", new[] { "Compact (24)", "Standard (28)", "Comfortable (36)" }, "Standard (28)", v => grid.ItemHeight = v.Contains("24") ? 24 : v.Contains("36") ? 36 : 28);
                host.Button("Best fit columns", grid.BestFitAllColumns);
                host.Button("Select first row", () => grid.SelectedRowIndex = 0);
                ReportCounts(host, grid);
                return grid;
            }
        };

        yield return new GridScenario
        {
            Id = "grid-paging",
            Title = "Paging - Client, Server, Virtual and Infinite Modes",
            Description = "Switch between client paging, server-style paging, virtual scrolling and infinite load-more over 5,000 rows. The pager appears for paged modes.",
            Features = new[] { "Client paging", "Server paging", "Virtual scroll", "Infinite scroll", "Page size", "Pager UI" },
            Documentation = "PagingMode decides how rows are requested. Client reads the filtered, sorted result once and pages in memory; Server sends skip/take to the data source; Infinite appends pages while scrolling.",
            Usage = "Set PagingMode and PageSize; use GoToPage, NextPage and PageChanged. Selection and detail expansion reset when the page changes.",
            Architecture = "Paging sits between the query result and the row window; sorting, filtering and grouping run over the full result before a page is cut.",
            Performance = "Client paging holds one query result; use Server or Virtual paging for sources too large to materialize.",
            Accessibility = "Pager buttons are drawn, so page changes are announced through the live region (Page 2 of 40). Ctrl+PageUp/PageDown change page from the keyboard.",
            BestPractices = "Choose Server paging for remote or very large sources and keep PageSizeOptions small and meaningful.",
            Api = "DataGrid<T> -> PagingMode, PageSize, PageIndex, PageCount, GoToPage, NextPage, PreviousPage, LoadMoreAsync, PageChanged",
            Build = host =>
            {
                var grid = RecordGrid(GridScenarioData.Records(5000), "Paged records");
                grid.PagingMode = GridPagingMode.Client;
                grid.PageSize = 25;
                host.Attach(grid);
                host.Combo("Mode:", new[] { "Client", "Server", "Virtual", "Infinite", "None" }, "Client", v => grid.PagingMode = Enum.Parse<GridPagingMode>(v));
                host.Combo("Page size:", new[] { "10", "25", "50", "100" }, "25", v => grid.PageSize = int.Parse(v));
                host.Separator();
                host.Button("|<", () => grid.Engine.FirstPage());
                host.Button("<", () => grid.Engine.PreviousPage());
                host.Button(">", () => grid.Engine.NextPage());
                host.Button(">|", () => grid.Engine.LastPage());
                host.Button("Go to page 5", () => grid.GoToPage(4));
                host.Toggle("Show pager", true, v => grid.ShowPager = v);
                ReportCounts(host, grid);
                return grid;
            }
        };

        yield return new GridScenario
        {
            Id = "grid-sorting",
            Title = "Sorting - Multi-Column, Custom Order and Tri-State Headers",
            Description = "Click a header to cycle ascending, descending and none; Ctrl+click adds a sort level. Priority sorts by business rank instead of alphabet.",
            Features = new[] { "Single sort", "Multi-column sort", "Custom comparer", "Sort indicators", "Programmatic sort" },
            Documentation = "Sorts are SortDescriptor entries in the grid's request. Columns with CustomComparer sort in memory with that comparer.",
            Usage = "Call SortBy(field, direction) to replace the sort, CycleSort(field, true) to add a level, ClearSort() to reset.",
            Accessibility = "Sorted headers are announced as 'sorted ascending' and the change is announced to screen readers.",
            Api = "EnterpriseDataGrid<T> -> SortBy, CycleSort, ClearSort, EnableSorting, EnableMultiColumnSorting; GridColumn -> Sortable, CustomComparer",
            Build = host =>
            {
                var grid = RecordGrid(GridScenarioData.Records(2000), "Sortable records");
                var rank = new Dictionary<string, int> { ["Critical"] = 0, ["High"] = 1, ["Normal"] = 2, ["Low"] = 3 };
                grid.Columns.First(c => c.Field == "Priority").CustomComparer = Comparer<object>.Create((a, b) =>
                    rank.GetValueOrDefault(a?.ToString() ?? string.Empty, 99).CompareTo(rank.GetValueOrDefault(b?.ToString() ?? string.Empty, 99)));
                host.Attach(grid);

                void Describe() => host.Metric("Sort: " + (grid.CurrentRequest.Sorts.Count == 0 ? "none" : string.Join(", ", grid.CurrentRequest.Sorts.Select(s => $"{s.Member} {(s.SortDirection == ListSortDirection.Ascending ? "asc" : "desc")}"))));
                grid.QueryChanged += (_, _) => Describe();

                host.Button("Region asc + Total desc", () =>
                {
                    grid.SortBy("Region", ListSortDirection.Ascending);
                    grid.CycleSort("Total", true);
                    grid.CycleSort("Total", true);
                });
                host.Button("Priority (business order)", () => grid.SortBy("Priority", ListSortDirection.Ascending));
                host.Button("Date newest first", () => grid.SortBy("Timestamp", ListSortDirection.Descending));
                host.Button("Clear sort", grid.ClearSort);
                host.Toggle("Multi-column", true, v => grid.EnableMultiColumnSorting = v);
                host.Toggle("Sorting enabled", true, v => grid.EnableSorting = v);
                Describe();
                return grid;
            }
        };

        yield return new GridScenario
        {
            Id = "grid-filtering",
            Title = "Filtering - Filter Row, Value Lists, Operators and Presets",
            Description = "Type in the filter row, open the funnel in a header for a value list, or apply operator filters from the toolbar. Presets save and restore a whole filter set.",
            Features = new[] { "Filter row", "Excel-style value list", "18 operators", "Between", "Presets", "Search panel" },
            Documentation = "Filters are descriptors evaluated by the query engine. The funnel shows distinct values with search; the filter row applies Contains.",
            Usage = "FilterBy(field, operator, value), SetQuickFilter(field, text), ApplyExcelFilter(field, values), SaveFilterPreset(name).",
            Performance = "Filters compile to expression trees for in-memory sources and translate to OData-style requests for remote sources.",
            Accessibility = "Filtered headers are announced as 'filtered'; the status bar and live region report the matching row count.",
            Api = "DataGrid<T> -> FilterBy, AddFilter, ClearFilters, ApplyExcelFilter, SetQuickFilter, SaveFilterPreset, ApplyFilterPreset, ExportFilterPresets",
            Build = host =>
            {
                var grid = RecordGrid(GridScenarioData.Records(3000), "Filterable records");
                grid.ShowFilterRow = true;
                host.Attach(grid);
                host.Button("Region = EMEA", () => grid.FilterBy("Region", FilterOperator.IsEqualTo, "EMEA"));
                host.Button("Gross > 20,000", () => grid.FilterBy("Total", FilterOperator.IsGreaterThan, 20000m));
                host.Button("Customer contains 'con'", () => grid.FilterBy("Customer", FilterOperator.Contains, "con"));
                host.Button("Qty between 5 and 10", () => grid.Engine.FilterBy("Quantity", FilterOperator.IsBetween, 5, 10));
                host.Button("Clear filters", () => { grid.ClearFilters(); grid.QuickFilters.Clear(); });
                host.Separator();
                host.Button("Save preset", () => grid.SaveFilterPreset("workshop"));
                host.Button("Apply preset", () => { if (!grid.Engine.ApplyFilterPreset("workshop")) host.Metric("Save a preset first."); });
                host.Button("Copy presets JSON", () => { Clipboard.SetText(grid.Engine.ExportFilterPresets()); host.Metric("Filter presets copied to the clipboard."); });
                host.Toggle("Search panel", false, v => grid.ShowSearchPanel = v);
                grid.DataWindowChanged += (_, _) => host.Metric($"{grid.TotalRecordCount:N0} of 3,000 rows match");
                return grid;
            }
        };

        yield return new GridScenario
        {
            Id = "grid-grouping",
            Title = "Grouping - Multi-Level Groups, Summaries and Footers",
            Description = "Group by one or more columns from the toolbar, the group panel or a header menu. Group headers show counts and aggregates; footers repeat the totals.",
            Features = new[] { "Multi-level groups", "Group summaries", "Group footers", "Expand/collapse", "Group panel drag" },
            Documentation = "Groups are computed over the whole filtered result, so group counts and aggregates are exact even with paging or virtualization.",
            Usage = "GroupBy(field), RemoveGroup(field), ExpandAllGroups(), GroupSummaries.Add(new GroupSummaryDescriptor(field, AggregateFunction.Sum, \"{0:C0}\")).",
            Performance = "Grouping flattens the group tree into a row list once per query; collapsed groups cost no row rendering.",
            Accessibility = "Group rows expose level, expanded state and item count; Alt+Right and Alt+Left expand and collapse the focused group.",
            Api = "DataGrid<T> -> GroupBy, RemoveGroup, MoveGroup, ExpandAllGroups, CollapseAllGroups, ShowGroupFooters, GroupSummaries, GroupsExpandedByDefault",
            Build = host =>
            {
                var grid = RecordGrid(GridScenarioData.Records(1500), "Grouped records");
                grid.GroupSummaries.Add(new GroupSummaryDescriptor("Total", AggregateFunction.Sum, "Sum {0:C0}"));
                grid.GroupSummaries.Add(new GroupSummaryDescriptor("Quantity", AggregateFunction.Average, "Avg qty {0:0.0}"));
                grid.GroupBy("Region");
                host.Attach(grid);
                host.Button("Group by Region", () => grid.GroupBy("Region"));
                host.Button("+ Priority", () => grid.GroupBy("Priority"));
                host.Button("+ Status", () => grid.GroupBy("Status"));
                host.Button("Remove Priority", () => grid.RemoveGroup("Priority"));
                host.Button("Clear groups", grid.ClearGroups);
                host.Separator();
                host.Button("Expand all", grid.ExpandAllGroups);
                host.Button("Collapse all", grid.CollapseAllGroups);
                host.Toggle("Group footers", false, v => grid.ShowGroupFooters = v);
                host.Toggle("Expanded by default", true, v => grid.GroupsExpandedByDefault = v);
                grid.QueryChanged += (_, _) => host.Metric("Groups: " + (grid.CurrentRequest.Groups.Count == 0 ? "none" : string.Join(" > ", grid.CurrentRequest.Groups.Select(g => g.Member))));
                return grid;
            }
        };

        yield return new GridScenario
        {
            Id = "grid-hierarchy",
            Title = "Hierarchy - Three-Level Customers, Orders and Order Lines",
            Description = "Expand a customer to see its orders, and an order to see its lines. Relations nest without a depth limit; children can load lazily on first expansion.",
            Features = new[] { "Nested relations", "Lazy loading", "Expand/collapse all", "Child columns", "Search reveals matches" },
            Documentation = "A GridHierarchyRelation joins child rows on KeyField/ForeignField. ChildRelations adds further levels. LoadChildrenAsync defers loading until the row is expanded.",
            Usage = "grid.Engine.AddRelation(name, keyField, foreignField, childRows, typeof(Child)); relation.ChildRelations.Add(...); grid.EnableMasterDetail = true.",
            Architecture = "Expanded relations contribute DetailHeader and DetailData rows to the materialized row list, so selection, search and export all see them.",
            Accessibility = "Detail rows announce their level and expanded state; Alt+Right and Alt+Left expand and collapse the focused row.",
            Api = "DataGrid<T> -> AddRelation, AddSelfReference, ToggleRowExpansion, ExpandAllDetails, CollapseAllDetails; GridHierarchyRelation -> ChildRelations, LoadChildrenAsync",
            Build = host =>
            {
                var (customers, orders, lines) = GridScenarioData.Sales();
                var grid = new EnterpriseDataGrid<CustomerRow> { Dock = DockStyle.Fill, AriaLabel = "Customers with orders and lines", EnableMasterDetail = true, ShowGroupPanel = false };
                grid.Columns.Add(new GridColumn { Field = "Id", Title = "ID", Width = 70 });
                grid.Columns.Add(new GridColumn { Field = "Name", Title = "Customer", Width = 240 });
                grid.Columns.Add(new GridColumn { Field = "Country", Title = "Country", Width = 120 });
                grid.Columns.Add(new GridColumn { Field = "Segment", Title = "Segment", Width = 120 });
                var orderRelation = grid.Engine.AddRelation("Orders", "Id", "CustomerId", orders, typeof(OrderRow));
                orderRelation.ChildRelations.Add(new GridHierarchyRelation { RelationName = "Lines", KeyField = "OrderId", ForeignField = "OrderId", ChildItemType = typeof(OrderLineRow), ChildSource = lines });
                grid.DataSource = customers;
                host.Attach(grid);
                host.Button("Expand all", grid.ExpandAllDetails);
                host.Button("Collapse all", grid.CollapseAllDetails);
                host.Toggle("Search panel", false, v => grid.ShowSearchPanel = v);
                host.Metric($"{customers.Count} customers, {orders.Count} orders, {lines.Count} order lines");
                return grid;
            }
        };

        yield return new GridScenario
        {
            Id = "grid-columns",
            Title = "Column Management - Pin, Hide, Reorder, Resize, Bands and Layouts",
            Description = "Drag headers to reorder or onto the group panel, drag the edge to resize, double-click the edge for best fit, right-click for pin, hide and the column chooser. Save and restore layouts as JSON.",
            Features = new[] { "Pinning", "Column chooser", "Reorder", "Resize", "Best fit", "Stacked bands", "Layout persistence", "Column types" },
            Documentation = "Column state (visibility, width, order, pinning, sorts, groups, filters, page size) round-trips through ColumnLayoutProfile and GridLayoutSerializer.",
            Usage = "var json = GridLayoutSerializer.ToJson(grid.CreateLayoutProfile()); grid.ApplyLayoutProfile(GridLayoutSerializer.FromJson(json)). Unknown columns produce warnings instead of exceptions.",
            Accessibility = "Column order changes are announced; the focused cell stays visible when pinned columns are used.",
            Api = "DataGrid<T> -> PinColumn, SetColumnVisibility, MoveColumnBefore, ResizeColumn, BestFitColumn, CreateLayoutProfile, ApplyLayoutProfile",
            Build = host =>
            {
                var grid = RecordGrid(GridScenarioData.Records(300), "Column management");
                var order = new GridColumnBand { Title = "Order" };
                var commercial = new GridColumnBand { Title = "Commercial" };
                var pricing = new GridColumnBand { Title = "Pricing" };
                var workflow = new GridColumnBand { Title = "Workflow" };
                commercial.ChildBands.Add(pricing);
                grid.Bands.AddRange(new[] { order, commercial, workflow });
                foreach (var column in grid.Columns)
                {
                    column.BandId = column.Field switch
                    {
                        "Id" or "Customer" or "Product" or "Region" => order.Id,
                        "UnitPrice" or "Quantity" or "Total" => pricing.Id,
                        _ => workflow.Id
                    };
                }
                grid.Columns.First(c => c.Field == "Total").ColumnType = GridColumnType.Progress;
                host.Attach(grid);

                string? saved = null;
                host.Button("Pin Customer", () => grid.PinColumn("Customer", ColumnPinPosition.Left));
                host.Button("Pin Status right", () => grid.PinColumn("Status", ColumnPinPosition.Right));
                host.Button("Unpin all", () => { foreach (var c in grid.Columns) grid.PinColumn(c.Field, ColumnPinPosition.None); });
                host.Button("Toggle Priority", () => { var c = grid.Columns.First(x => x.Field == "Priority"); grid.SetColumnVisibility("Priority", !c.Visible); });
                host.Button("Best fit", grid.BestFitAllColumns);
                host.Toggle("Bands", true, v => grid.ShowBandedHeaders = v);
                host.Separator();
                host.Button("Save layout", () => { saved = GridLayoutSerializer.ToJson(grid.CreateLayoutProfile("workshop")); host.Metric($"Layout saved ({saved.Length:N0} characters of JSON)."); });
                host.Button("Restore layout", () =>
                {
                    if (saved == null) { host.Metric("Save a layout first."); return; }
                    var warnings = grid.Engine.ApplyLayoutProfile(GridLayoutSerializer.FromJson(saved));
                    host.Metric(warnings.Count == 0 ? "Layout restored." : $"Layout restored with {warnings.Count} warning(s).");
                });
                return grid;
            }
        };

        yield return new GridScenario
        {
            Id = "grid-rows",
            Title = "Rows - Add, Delete, Reorder and Drag Between Grids",
            Description = "Add and delete rows with undo, drag selected rows to reorder, or drag them into the second grid (Ctrl copies). Both grids share the same item type.",
            Features = new[] { "Add row", "Delete rows", "Undo/redo", "Drag reorder", "Cross-grid drag and drop", "Row styling" },
            Documentation = "Structural edits work on in-memory list sources and are undoable. MoveRows reorders as one undo step; TransferRows moves or copies rows to another grid.",
            Usage = "grid.AllowRowDrag = true; grid.AllowRowDrop = true; grid.AddRow(item); grid.RemoveRows(grid.SelectedItems); grid.Engine.MoveRows(items, before).",
            Accessibility = "Moves are announced ('Moved 3 rows'); every drag action also has a keyboard path through Delete, Ctrl+Z and the context menu.",
            Api = "DataGrid<T> -> AddRow, RemoveRows, MoveRows, ReorderRow, TransferRows; EnterpriseDataGrid<T> -> AllowRowDrag, AllowRowDrop, AllowRowDelete, RowStyleNeeded",
            Build = host =>
            {
                var left = RecordGrid(GridScenarioData.Records(40), "Source rows");
                left.SelectionMode = GridSelectionMode.MultipleRows;
                left.AllowRowDrag = true;
                left.AllowRowDrop = true;
                left.AllowRowDelete = true;
                left.RowStyleNeeded += (_, e) => { if (e.Item.Priority == "Critical") { e.BackColorHex = "#FDE7E9"; e.Bold = true; } };
                var right = RecordGrid(new List<GridDemoRecord>(), "Target rows");
                right.SelectionMode = GridSelectionMode.MultipleRows;
                right.AllowRowDrag = true;
                right.AllowRowDrop = true;
                host.Attach(left);

                var next = 90_000;
                host.Button("Add row", () => left.AddRow(new GridDemoRecord { Id = ++next, Customer = "New customer", Product = "New product", Region = "EMEA", UnitPrice = 500, Quantity = 1 }, 0));
                host.Button("Delete selected", () => host.Metric($"Deleted {left.RemoveRows(left.SelectedItems.ToList())} row(s)"));
                host.Button("Undo", () => left.UndoLastEdit());
                host.Button("Redo", () => left.RedoLastEdit());
                host.Button("Move selected to top", () => left.Engine.MoveRows(left.SelectedItems.ToList(), left.DataSource?.FirstOrDefault()));
                host.Metric("Drag selected rows between the grids; hold Ctrl to copy.");

                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
                layout.Controls.Add(left, 0, 0);
                layout.Controls.Add(right, 1, 0);
                return layout;
            }
        };

        yield return new GridScenario
        {
            Id = "grid-editing",
            Title = "Editing - In-Cell, Popup, Batch and Validation",
            Description = "Edit with F2, Enter or double-click. Required, range and regex rules reject bad values with a message; switch to popup or batch mode and commit or roll back.",
            Features = new[] { "In-cell editing", "Popup/form editing", "Batch commit/rollback", "Validation rules", "Undo/redo", "Typed editors" },
            Documentation = "Editors follow the column type: checkbox, combo box, date picker or text. Validation rules sit on the column; invalid values are rejected and the cell shows an error marker with its message.",
            Usage = "grid.EditMode = GridEditMode.Batch; column.ValidationRules.Add(new RangeRule(30000, 200000) { Message = \"...\" }); grid.CommitChanges() / RollbackChanges().",
            Accessibility = "Errors are announced assertively, shown as a marker plus text (not color alone) and exposed in the cell's accessible name.",
            Api = "DataGrid<T> -> EditMode, TryEditCell, BeginRowEdit, CommitChanges, RollbackChanges, UndoLastEdit, RowValidators, ValidationSummary",
            Build = host =>
            {
                var grid = new EnterpriseDataGrid<EmployeeRow> { Dock = DockStyle.Fill, AriaLabel = "Employees", ItemHeight = 28, ShowGroupPanel = false };
                grid.Columns.Add(new GridColumn { Field = "Id", Title = "ID", Width = 60, ReadOnly = true });
                var name = new GridColumn { Field = "Name", Title = "Name", Width = 170 };
                name.ValidationRules.Add(new RequiredRule { Message = "Name is required" });
                name.ValidationRules.Add(new LengthRule(2, 40) { Message = "Name must be 2 to 40 characters" });
                grid.Columns.Add(name);
                var email = new GridColumn { Field = "Email", Title = "Email", Width = 230 };
                email.ValidationRules.Add(new RegexRule(@"^[^@\s]+@[^@\s]+\.[^@\s]+$") { Message = "Enter a valid email address" });
                grid.Columns.Add(email);
                grid.Columns.Add(new GridColumn { Field = "Department", Title = "Department", Width = 120, ComboItems = new object[] { "Engineering", "Sales", "Finance", "Operations", "Support" } });
                var salary = new GridColumn { Field = "Salary", Title = "Salary", Width = 100, FormatString = "C0", Alignment = ColumnTextAlignment.Right };
                salary.ValidationRules.Add(new RangeRule(30000m, 200000m) { Message = "Salary must be between 30,000 and 200,000" });
                salary.FooterSummaries.Add(new GridColumnSummary(AggregateFunction.Average, "C0"));
                grid.Columns.Add(salary);
                grid.Columns.Add(new GridColumn { Field = "Active", Title = "Active", Width = 70 });
                grid.Columns.Add(new GridColumn { Field = "Hired", Title = "Hired", Width = 100, FormatString = "d" });
                var rating = new GridColumn { Field = "Rating", Title = "Rating", Width = 70, Alignment = ColumnTextAlignment.Right };
                rating.ValidationRules.Add(new RangeRule(1, 5) { Message = "Rating is 1 to 5" });
                grid.Columns.Add(rating);
                grid.DataSource = GridScenarioData.Employees(60);
                host.Attach(grid);

                void Report() => host.Metric($"Edit mode: {grid.EditMode} | pending: {grid.Engine.PendingChanges.Count} | errors: {grid.Engine.ValidationSummary.Count} | undo: {(grid.CanUndo ? "yes" : "no")}");
                grid.CellEdited += (_, d) => { host.Log("CellEdited", d); Report(); };
                grid.ValidationFailed += (_, e) => host.Metric(e.Errors.FirstOrDefault()?.Message ?? "Validation failed");

                host.Combo("Edit mode:", new[] { "InCell", "InlineRow", "PopupModal", "Batch", "Form", "None" }, "InCell", v => { grid.EditMode = Enum.Parse<GridEditMode>(v); Report(); });
                host.Button("Commit", () => { var r = grid.CommitChanges(); host.Metric(r.Success ? $"Committed {r.Changes.Count} change(s)" : $"Commit blocked by {r.Errors.Count} error(s)"); });
                host.Button("Roll back", () => host.Metric($"Rolled back {grid.RollbackChanges()} change(s)"));
                host.Button("Undo", () => grid.UndoLastEdit());
                host.Button("Redo", () => grid.RedoLastEdit());
                host.Toggle("Reject invalid", true, v => grid.RejectInvalidEdits = v);
                Report();
                return grid;
            }
        };

        yield return new GridScenario
        {
            Id = "grid-selection",
            Title = "Selection - Rows, Checkboxes, Cells, Ranges and Columns",
            Description = "Pick a selection mode, then click, Shift+click, Ctrl+click or drag. Ctrl+A selects all, Ctrl+C copies with headers, arrow keys move the focus cell.",
            Features = new[] { "Single/multiple rows", "Checkbox column", "Cell range", "Column selection", "Select all", "Clipboard copy" },
            Documentation = "GridSelectionModel tracks rows, cell ranges and columns by display position and implements the click, drag and keyboard gestures in one testable class.",
            Usage = "grid.SelectionMode = GridSelectionMode.CellRange; grid.Selection.SelectAll(); grid.SelectedItems; grid.CopySelectionToClipboard().",
            Accessibility = "The focus cell has a 2 px ring in every theme, selection never relies on color alone in high contrast, and selection changes are exposed through UI Automation.",
            Api = "GridSelectionModel -> Click, Drag, Move, SelectAll, Clear, SelectedRows, CellRanges, SelectedColumns; DataGrid<T> -> SelectionMode, SelectedItems",
            Build = host =>
            {
                var grid = RecordGrid(GridScenarioData.Records(500), "Selectable records");
                grid.SelectionMode = GridSelectionMode.MultipleRows;
                host.Attach(grid);
                void Report() => host.Metric($"Mode: {grid.SelectionMode} | rows: {grid.Selection.SelectedRows.Count} | cells: {(grid.Selection.HasSelection ? grid.Selection.SelectedCellCount : 0):N0} | focus: row {grid.Selection.FocusedRow}, column {grid.Selection.FocusedColumn}");
                grid.SelectionChanged += (_, _) => Report();
                host.Combo("Mode:", new[] { "SingleRow", "MultipleRows", "CheckboxRow", "CellRange", "SingleCell", "Column", "None" }, "MultipleRows", v => { grid.SelectionMode = Enum.Parse<GridSelectionMode>(v); Report(); });
                host.Button("Select all", () => grid.Selection.SelectAll());
                host.Button("Clear", () => grid.Selection.Clear());
                host.Button("Copy", grid.CopySelectionToClipboard);
                host.Combo("Copy mode:", new[] { "CellsWithHeaders", "Cells", "None" }, "CellsWithHeaders", v => grid.ClipboardCopyMode = Enum.Parse<GridClipboardCopyMode>(v));
                Report();
                return grid;
            }
        };
    }
}
