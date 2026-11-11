using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using EnterpriseUI.Grids;
using EnterpriseUI.WinForms;

namespace EnterpriseUI.WinFormsDemo.Modules;

public enum DemoLogLevel { Quiet, Normal, Verbose }

/// <summary>Settings object for the property inspector demo; categories, descriptions and defaults come from attributes.</summary>
public sealed class DemoSettings
{
    [Category("General"), Description("Name shown in the window title."), DefaultValue("Executive Dashboard")]
    public string Title { get; set; } = "Executive Dashboard";

    [Category("General"), Description("Number of retries after a failed request."), DefaultValue(3)]
    public int Retries { get; set; } = 3;

    [Category("General"), Description("Turns the live refresh on or off."), DefaultValue(true)]
    public bool LiveRefresh { get; set; } = true;

    [Category("General"), Description("Date of the last review.")]
    public DateTime LastReview { get; set; } = DateTime.Today;

    [Category("Appearance"), Description("Accent color used for highlights.")]
    public Color Accent { get; set; } = Color.SteelBlue;

    [Category("Appearance"), Description("How much the application writes to the log."), DefaultValue(DemoLogLevel.Normal)]
    public DemoLogLevel LogLevel { get; set; } = DemoLogLevel.Normal;

    [Category("Layout"), Description("Initial window size; expand to edit the parts.")]
    public Size WindowSize { get; set; } = new Size(1280, 800);

    [Category("Layout"), Description("Minimum window size.")]
    public Size MinimumSize { get; set; } = new Size(800, 600);

    [Category("Diagnostics"), Description("Build identifier, read-only."), ReadOnly(true)]
    public string Build { get; set; } = "2026.11.0-rc1";
}

/// <summary>Profile with data annotations; the data entry control generates its form and validation from it.</summary>
public sealed class DemoProfile
{
    [Display(Name = "Full name", GroupName = "Identity", Order = 1, Description = "As shown on the contract.")]
    [Required, StringLength(40)]
    public string Name { get; set; } = "Alexander Hamilton";

    [Display(GroupName = "Identity", Order = 2)]
    [Required, EmailAddress]
    public string Email { get; set; } = "a.hamilton@example.com";

    [Display(GroupName = "Identity", Order = 3)]
    [DataType(DataType.Password), StringLength(30, MinimumLength = 6)]
    public string Password { get; set; } = "secret1";

    [Display(GroupName = "Employment", Order = 4)]
    public string Department { get; set; } = "Finance";

    [Display(GroupName = "Employment", Order = 5, Description = "Between 18 and 70.")]
    [Range(18, 70)]
    public int Age { get; set; } = 34;

    [Display(GroupName = "Employment", Order = 6)]
    [Range(0, 1_000_000)]
    public decimal Salary { get; set; } = 98_500m;

    [Display(GroupName = "Employment", Order = 7)]
    public DateTime Hired { get; set; } = new(2021, 3, 15);

    [Display(GroupName = "Employment", Order = 8)]
    public DayOfWeek PreferredDay { get; set; } = DayOfWeek.Tuesday;

    [Display(GroupName = "Employment", Order = 9)]
    public bool Remote { get; set; } = true;

    [DataType(DataType.MultilineText)]
    [Display(Order = 10, Description = "Free text.")]
    public string? Notes { get; set; } = "Prefers morning meetings.";
}

/// <summary>Workshop pages for the data management controls, one control per page.</summary>
public static class DataControlScenarios
{
    private static readonly string[] SelectionModes = { "One", "MultiSimple", "MultiExtended", "None" };
    private static readonly string[] Views = { "Details", "List", "Tiles" };
    private static readonly string[] ColorModes = { "Category", "Sequential", "Diverging" };

    public static IReadOnlyList<GridScenario> All { get; } = Build();

    private static List<GridScenario> Build() => new()
    {
        ListControl(), CheckedList(), ListView(), TreeView(), PropertyGrid(), DataEntry(), DataLayout(),
        CardView(), FilterView(), DataFilter(), PivotGrid(), TreeMap(), HeatMap(), VirtualGrid()
    };

    private static Control Fill(Control control)
    {
        control.Dock = DockStyle.Fill;
        return control;
    }

    private static List<EmployeeRow> Employees(int count = 60) => GridScenarioData.Employees(count);

    // ---- ListControl --------------------------------------------------------------------

    private static GridScenario ListControl() => new()
    {
        Id = "data-listcontrol",
        Title = "ListControl - Binding, Selection Modes, Filter and Sort",
        Description = "A virtualized list bound to employees with DisplayMember and ValueMember, four selection modes, filtering, sorting and type-ahead.",
        Features = new[] { "DisplayMember / ValueMember", "Selection modes", "Filter and sort", "Type-ahead", "Item formatting" },
        Documentation = "EnterpriseListControl keeps a snapshot of the data source, follows collection change notifications and only paints the visible rows.",
        Usage = "list.DataSource = employees; list.DisplayMember = \"Name\"; list.ValueMember = \"Id\"; list.SelectionChanged += ...",
        Api = "EnterpriseListControl -> DataSource, DisplayMember, ValueMember, SelectionMode, Filter, SortComparison, SelectedItems, ItemFormatting",
        Build = host =>
        {
            var list = new EnterpriseListControl { DisplayMember = "Name", ValueMember = "Id", ItemHeight = 30 };
            var all = Employees(400);
            list.DataSource = all;
            list.ItemFormatting += (_, e) =>
            {
                if (e.Item is EmployeeRow { Active: false }) e.ForeColor = Color.Gray;
            };
            void Report() => host.Metric($"{list.VisibleCount:N0} of {list.Count:N0} shown | selected: {list.SelectedItems.Count} | value: {list.SelectedValue}");
            list.SelectionChanged += (_, _) => Report();
            host.Combo("Selection:", SelectionModes, "One", v => list.SelectionMode = Enum.Parse<ListSelectionMode>(v));
            host.Combo("Department:", new[] { "All", "Engineering", "Sales", "Finance", "Operations", "Support" }, "All", v =>
            {
                list.Filter = v == "All" ? null : item => item is EmployeeRow row && row.Department == v;
                Report();
            });
            host.Button("Sort A-Z", () => list.SortComparison = (a, b) => string.Compare(((EmployeeRow)a).Name, ((EmployeeRow)b).Name, StringComparison.CurrentCultureIgnoreCase));
            host.Button("Sort by salary", () => list.SortComparison = (a, b) => ((EmployeeRow)b).Salary.CompareTo(((EmployeeRow)a).Salary));
            host.Button("Select all", list.SelectAll);
            Report();
            return list;
        }
    };

    // ---- CheckedListBox -----------------------------------------------------------------

    private static GridScenario CheckedList() => new()
    {
        Id = "data-checkedlistbox",
        Title = "CheckedListBox - Permissions and Bulk Checking",
        Description = "A list with check boxes: click the box or the row, press Space, check everything, uncheck everything or invert the checks.",
        Features = new[] { "Check boxes", "CheckOnClick", "Check all / invert", "ItemCheck event", "Keyboard support" },
        Documentation = "EnterpriseCheckedListBoxControl extends the list control; the checked state is kept per item and cleared when the data changes.",
        Usage = "list.AddRange(items); list.SetItemChecked(2, true); foreach (var item in list.CheckedItems) { ... }",
        Api = "EnterpriseCheckedListBoxControl -> CheckOnClick, GetItemChecked, SetItemChecked, CheckAll, UncheckAll, InvertChecks, CheckedItems, ItemCheck",
        Build = host =>
        {
            var list = new EnterpriseCheckedListBoxControl { ItemHeight = 32 };
            list.AddRange(new object[]
            {
                "Read customer records", "Edit customer records", "Delete customer records", "Export reports", "Schedule reports", "Approve invoices",
                "Manage users", "Manage roles", "View audit log", "Configure integrations", "Rotate API keys", "Publish dashboards"
            });
            list.SetItemChecked(0, true);
            list.SetItemChecked(3, true);
            void Report() => host.Metric($"{list.CheckedIndices.Count} of {list.Count} permissions granted");
            list.ItemCheck += (_, _) => list.BeginInvoke(new Action(Report));
            host.Toggle("Check on row click", true, v => list.CheckOnClick = v);
            host.Button("Check all", list.CheckAll);
            host.Button("Uncheck all", list.UncheckAll);
            host.Button("Invert", list.InvertChecks);
            Report();
            return list;
        }
    };

    // ---- ListView -----------------------------------------------------------------------

    private static GridScenario ListView() => new()
    {
        Id = "data-listview",
        Title = "ListView - Details, List and Tiles",
        Description = "Three views over the same items: details with sortable, resizable columns, a wrapping list and tiles. Group, check and sort without touching the data.",
        Features = new[] { "Details / List / Tiles", "Sortable resizable columns", "Groups", "Check boxes", "Multi-selection" },
        Documentation = "EnterpriseListViewControl lays out the visible items only. Columns bind to properties through Field; click a header to sort, drag its edge to resize and double-click the edge to best-fit.",
        Usage = "view.AddColumn(\"Customer\", 180, \"Customer\"); view.DataSource = records; view.View = ListViewMode.Tiles;",
        Api = "EnterpriseListViewControl -> AddColumn, DataSource, View, CheckBoxes, GroupSelector, Filter, Sort, SelectedItems",
        Build = host =>
        {
            var view = new EnterpriseListViewControl();
            view.AddColumn("Customer", 190, "Customer");
            view.AddColumn("Product", 190, "Product");
            view.AddColumn("Region", 110, "Region");
            view.AddColumn("Status", 100, "Status");
            view.AddColumn("Total", 110, "Total", ContentAlignment.MiddleRight);
            view.DataSource = GridScenarioData.Records(250);
            void Report() => host.Metric($"{view.Items.Count} items | selected: {view.SelectedItems.Count} | checked: {view.CheckedItems.Count()}");
            view.SelectionChanged += (_, _) => Report();
            view.ItemChecked += (_, _) => Report();
            host.Combo("View:", Views, "Details", v => view.View = Enum.Parse<ListViewMode>(v));
            host.Toggle("Check boxes", false, v => view.CheckBoxes = v);
            host.Toggle("Group by region", false, v => view.GroupSelector = v ? item => item.SubItems[1] : null);
            host.Button("Sort by total", () => view.Sort(4, ListViewSortOrder.Descending));
            Report();
            return view;
        }
    };

    // ---- TreeView -----------------------------------------------------------------------

    private static GridScenario TreeView() => new()
    {
        Id = "data-treeview",
        Title = "TreeView - Hierarchy, Tri-state Checks and Lazy Loading",
        Description = "An organization tree with chevron expanders, tri-state check boxes that cascade, keyboard navigation and a branch that loads its children on first expand.",
        Features = new[] { "Expand / collapse", "Tri-state cascading checks", "Lazy loading", "Keyboard navigation", "LoadHierarchy" },
        Documentation = "EnterpriseTreeViewControl flattens the expanded nodes into a virtual list. Nodes marked HasChildrenHint raise NodeExpanding so the handler can add children on demand.",
        Usage = "var root = tree.Nodes.Add(\"Company\"); root.Nodes.Add(\"Finance\"); tree.NodeExpanding += (s, e) => LoadChildren(e.Node);",
        Api = "EnterpriseTreeViewControl -> Nodes, CheckBoxes, CascadeChecks, NodeExpanding, AfterSelect, ExpandAll, CollapseAll, LoadHierarchy",
        Build = host =>
        {
            var tree = new EnterpriseTreeViewControl { CheckBoxes = true };
            var company = tree.Nodes.Add("Northwind Holdings");
            foreach (var (division, teams) in new Dictionary<string, string[]>
            {
                ["Finance"] = new[] { "Accounting", "Treasury", "Tax" },
                ["Engineering"] = new[] { "Platform", "Data", "Security", "Mobile" },
                ["Sales"] = new[] { "EMEA", "APAC", "Americas" }
            })
            {
                var node = company.Nodes.Add(division);
                foreach (var team in teams) node.Nodes.Add(team);
            }

            var archive = company.Nodes.Add("Archive (loads on demand)");
            archive.HasChildrenHint = true;
            tree.NodeExpanding += (_, e) =>
            {
                if (e.Node != archive || e.Node.Nodes.Count > 0) return;
                foreach (var year in new[] { "2023", "2024", "2025" }) e.Node.Nodes.Add($"Records {year}");
                host.Log("TreeView.NodeExpanding", "Archive children loaded");
            };
            company.Expanded = true;
            tree.AfterSelect += (_, e) => host.Metric($"Selected: {e.Node.FullPath()} | checked: {tree.CheckedNodes().Count()}");
            tree.AfterCheck += (_, _) => host.Metric($"Checked: {tree.CheckedNodes().Count()} nodes");
            host.Toggle("Check boxes", true, v => tree.CheckBoxes = v);
            host.Toggle("Cascade checks", true, v => tree.CascadeChecks = v);
            host.Button("Expand all", tree.ExpandAll);
            host.Button("Collapse all", tree.CollapseAll);
            host.Metric("Select a node and use the arrow keys; * expands everything below it.");
            return tree;
        }
    };

    // ---- PropertyGrid -------------------------------------------------------------------

    private static GridScenario PropertyGrid() => new()
    {
        Id = "data-propertygrid",
        Title = "PropertyGrid - Inspect and Edit Any Object",
        Description = "An inspector for a settings object: categories, search, nested objects, a toggle for booleans, drop-downs for enums, inline text editors with validation and Delete to reset to the default.",
        Features = new[] { "Categorized / alphabetical", "Search", "Nested properties", "Typed editors", "Description pane" },
        Documentation = "EnterprisePropertyGridControl reads browsable properties through TypeDescriptor, so Category, Description, DefaultValue, ReadOnly and type converters all apply.",
        Usage = "grid.SelectedObject = settings; grid.PropertyValueChanged += (s, e) => Save();",
        Api = "EnterprisePropertyGridControl -> SelectedObject, Categorized, SearchText, ShowDescription, SplitterRatio, PropertyValueChanged",
        Build = host =>
        {
            var settings = new DemoSettings();
            var grid = new EnterprisePropertyGridControl { SelectedObject = settings };
            grid.PropertyValueChanged += (_, entry) => host.Metric($"{entry.DisplayName} = {entry.DisplayText}");
            host.Toggle("Categorized", true, v => grid.Categorized = v);
            host.Toggle("Description", true, v => grid.ShowDescription = v);
            host.Button("Reload from object", grid.RefreshProperties);
            host.Button("Set title in code", () => { settings.Title = "Set from code"; grid.RefreshProperties(); });
            host.Metric("Click a value to edit it. Delete resets the selected property to its default.");
            return grid;
        }
    };

    // ---- DataEntry ----------------------------------------------------------------------

    private static GridScenario DataEntry() => new()
    {
        Id = "data-dataentry",
        Title = "DataEntry - A Validated Form from a Class",
        Description = "A multi-column edit form generated from DataAnnotations: required marks, range and length checks, e-mail and password fields, groups, a drop-down for the enum and Save / Reset.",
        Features = new[] { "Generated fields", "DataAnnotations validation", "Responsive columns", "Groups", "Save / Reset" },
        Documentation = "EnterpriseDataEntryControl builds an EntityFormModel from the public properties. Values stay pending until Save; invalid values are never written to the object.",
        Usage = "entry.Entity = profile; entry.Applied += (s, e) => repository.Save(profile);",
        Api = "EnterpriseDataEntryControl -> Entity, Model, Apply, ResetChanges, Validate, ColumnWidth, Applied, ValidationFailed",
        Build = host =>
        {
            var profile = new DemoProfile();
            var entry = new EnterpriseDataEntryControl { Entity = profile };
            entry.Applied += (_, _) => host.Metric($"Saved {profile.Name} ({profile.Email})");
            entry.ValidationFailed += (_, errors) => host.Metric($"{errors.Count} field(s) need attention: {string.Join("; ", errors.Values.Take(2))}");
            host.Button("Validate", () => host.Metric(entry.Validate() ? "All fields are valid" : "Some fields are invalid"));
            host.Button("Save", () => entry.Apply());
            host.Button("Reset", entry.ResetChanges);
            host.Combo("Column width:", new[] { "280", "340", "420" }, "340", v => entry.ColumnWidth = int.Parse(v));
            return entry;
        }
    };

    // ---- DataLayout ---------------------------------------------------------------------

    private static GridScenario DataLayout() => new()
    {
        Id = "data-datalayout",
        Title = "DataLayout - Responsive Grouped Layout of Any Controls",
        Description = "Arrange any controls with captions in collapsible card groups. Resize the page to change the number of columns, switch the caption position and collapse groups.",
        Features = new[] { "Groups as cards", "Responsive columns", "Column span", "Caption left / top", "Collapsible groups" },
        Documentation = "EnterpriseDataLayoutControl owns the child controls: it positions, clips and scrolls them, so a long form needs no extra panels.",
        Usage = "layout.AddItem(\"Name\", new EnterpriseInputControl(), group: \"Contact\", required: true);",
        Api = "EnterpriseDataLayoutControl -> AddItem, LabelPosition, ColumnWidth, MaxColumns, CardGroups, ToggleGroup",
        Build = host =>
        {
            var layout = new EnterpriseDataLayoutControl();
            layout.AddItem("First name", new EnterpriseInputControl { Text = "Alexander" }, "Contact", required: true);
            layout.AddItem("Last name", new EnterpriseInputControl { Text = "Hamilton" }, "Contact", required: true);
            layout.AddItem("E-mail", new EnterpriseInputControl { PlaceholderText = "name@company.com" }, "Contact", description: "Used for invoices");
            layout.AddItem("Address", new EnterpriseInputControl { Text = "1 Treasury Plaza" }, "Contact", columnSpan: 2);

            var country = new EnterpriseDropDownControl();
            country.SetItems(new[] { "United States", "Germany", "Japan", "Brazil", "United Kingdom" });
            country.SelectedIndex = 0;
            layout.AddItem("Country", country, "Contact");

            var plan = new EnterpriseDropDownControl();
            plan.SetItems(new[] { "Starter", "Business", "Enterprise" });
            plan.SelectedIndex = 1;
            layout.AddItem("Plan", plan, "Subscription");
            layout.AddItem("Seats", new EnterpriseInputControl { Text = "25" }, "Subscription");
            layout.AddItem("Billing contact", new EnterpriseInputControl { Text = "finance@example.com" }, "Subscription", columnSpan: 2);
            layout.AddItem("Auto renew", new EnterpriseCheckControl { Text = "Renew every year", Checked = true }, "Subscription");
            layout.AddItem("Notes", new EnterpriseInputControl { Multiline = true, Text = "Controls of any kind can live here." }, "Notes", columnSpan: 3, height: 90);

            void Report() => host.Metric($"{layout.ColumnCount} column(s) at the current width");
            layout.SizeChanged += (_, _) => Report();
            host.Combo("Captions:", new[] { "Top", "Left" }, "Top", v => layout.LabelPosition = Enum.Parse<DataLayoutLabelPosition>(v));
            host.Toggle("Cards", true, v => layout.CardGroups = v);
            host.Button("Toggle 'Contact'", () => layout.ToggleGroup("Contact"));
            Report();
            return layout;
        }
    };

    // ---- CardView -----------------------------------------------------------------------

    private static GridScenario CardView() => new()
    {
        Id = "data-cardview",
        Title = "CardView - Contact Cards with Groups and Filter",
        Description = "Virtualized cards for employees: title, subtitle, badge and detail lines. Group, sort, filter, check cards and move with the arrow keys.",
        Features = new[] { "Wrapping virtualized cards", "Group / sort / filter", "Badges", "Check boxes", "Keyboard navigation" },
        Documentation = "EnterpriseCardViewControl maps properties to the card parts; CardFormatting can change title, badge, accent color and image per card.",
        Usage = "cards.TitleField = \"Name\"; cards.SubtitleField = \"Department\"; cards.Fields.Add(new CardField(\"Salary\", format: \"C0\")); cards.DataSource = employees;",
        Api = "EnterpriseCardViewControl -> DataSource, TitleField, SubtitleField, BadgeField, Fields, GroupField, SortField, FilterText, CheckBoxes, CardFormatting",
        Build = host =>
        {
            var cards = new EnterpriseCardViewControl { TitleField = "Name", SubtitleField = "Department", BadgeField = "Rating", SelectionMode = ListSelectionMode.MultiExtended };
            cards.Fields.Add(new CardField("Email", "E-mail"));
            cards.Fields.Add(new CardField("Salary", "Salary", "C0"));
            cards.Fields.Add(new CardField("Hired", "Hired", "d"));
            cards.CardFormatting += (_, e) =>
            {
                if (e.Item is EmployeeRow row) e.Accent = row.Active ? null : Color.Gray;
            };
            cards.DataSource = Employees(80);
            void Report() => host.Metric($"{cards.VisibleCount} of {cards.Count} cards | selected: {cards.SelectedItems.Count} | checked: {cards.CheckedItems.Count}");
            cards.SelectionChanged += (_, _) => Report();
            cards.CardChecked += (_, _) => Report();
            host.Combo("Group:", new[] { "None", "Department" }, "None", v => cards.GroupField = v == "None" ? string.Empty : v);
            host.Combo("Sort:", new[] { "None", "Name", "Salary", "Hired" }, "None", v => cards.SortField = v == "None" ? string.Empty : v);
            host.Combo("Filter:", new[] { "(all)", "Engineering", "Sales", "Finance", "Operations", "Support" }, "(all)", v => { cards.FilterText = v == "(all)" ? string.Empty : v; Report(); });
            host.Toggle("Check boxes", false, v => cards.CheckBoxes = v);
            Report();
            return cards;
        }
    };

    // ---- FilterView ---------------------------------------------------------------------

    private static GridScenario FilterView() => new()
    {
        Id = "data-filterview",
        Title = "FilterView - Faceted Filtering with Live Counts",
        Description = "A filter panel next to a grid: tick values in the checklists, type a price range and watch the counts and the grid follow.",
        Features = new[] { "Checklist facets with counts", "Range facets", "Show more", "Clear all", "Filter descriptors" },
        Documentation = "EnterpriseFilterViewControl wraps a FacetModel: values of one facet are OR-ed, facets are AND-ed, and each count shows what selecting the value would leave.",
        Usage = "filter.AddListFacet(\"Region\"); filter.AddRangeFacet(\"UnitPrice\"); filter.DataSource = records; filter.FilterChanged += (s, e) => grid.DataSource = filter.Apply(all);",
        Api = "EnterpriseFilterViewControl -> AddListFacet, AddRangeFacet, DataSource, Apply, ToDescriptors, ClearAll, FilterChanged",
        Build = host =>
        {
            var records = GridScenarioData.Records(600);
            var grid = GridScenarios.RecordGrid(records);
            var filter = new EnterpriseFilterViewControl { Dock = DockStyle.Fill };
            filter.AddListFacet("Region");
            filter.AddListFacet("Product");
            filter.AddListFacet("Status");
            filter.AddListFacet("Priority");
            filter.AddRangeFacet("UnitPrice", "Unit price");
            filter.DataSource = records;
            filter.FilterChanged += (_, _) =>
            {
                var rows = filter.Apply(records.Cast<object>()).Cast<GridDemoRecord>().ToList();
                grid.DataSource = rows;
                host.Metric($"{rows.Count:N0} of {records.Count:N0} records match {filter.ActiveCount} active filter(s)");
            };

            var split = new TableLayoutPanel { ColumnCount = 2, RowCount = 1 };
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            grid.Dock = DockStyle.Fill;
            split.Controls.Add(filter, 0, 0);
            split.Controls.Add(grid, 1, 0);
            host.Toggle("Counts", true, v => filter.ShowCounts = v);
            host.Button("Clear all", filter.ClearAll);
            host.Metric($"{records.Count:N0} records");
            return split;
        }
    };

    // ---- DataFilter ---------------------------------------------------------------------

    private static GridScenario DataFilter() => new()
    {
        Id = "data-datafilter",
        Title = "DataFilter - Visual AND / OR / NOT Expression Builder",
        Description = "Build a nested filter with groups, per-field operators and typed value editors; the readable expression updates live and Apply filters the grid.",
        Features = new[] { "Nested AND / OR / NOT groups", "Operators per type", "Typed value editors", "Between", "Descriptor and text output" },
        Documentation = "EnterpriseDataFilterControl edits a DataFilterModel. NOT is pushed down with De Morgan's laws when the model becomes a filter descriptor.",
        Usage = "filter.ItemType = typeof(Order); filter.AddCondition(); var descriptor = filter.ToDescriptor();",
        Api = "EnterpriseDataFilterControl -> ItemType, AddCondition, AddGroup, ToDescriptor, Apply, FilterText, Validate, Load; EnterpriseDataFilter.Edit",
        Build = host =>
        {
            var records = GridScenarioData.Records(500);
            var grid = GridScenarios.RecordGrid(records);
            var filter = new EnterpriseDataFilterControl { Dock = DockStyle.Fill, ItemType = typeof(GridDemoRecord) };
            var condition = filter.AddCondition();
            condition.Field = "Region";
            condition.Value = "EMEA";
            filter.Load(filter.ToDescriptor());

            void ApplyFilter()
            {
                var errors = filter.Validate();
                if (errors.Count > 0)
                {
                    host.Metric(errors[0]);
                    return;
                }

                var rows = filter.Apply(records.Cast<object>()).Cast<GridDemoRecord>().ToList();
                grid.DataSource = rows;
                host.Metric($"{rows.Count:N0} of {records.Count:N0} records | {filter.FilterText}");
            }

            var split = new TableLayoutPanel { ColumnCount = 1, RowCount = 2 };
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            split.RowStyles.Add(new RowStyle(SizeType.Absolute, 240));
            split.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            grid.Dock = DockStyle.Fill;
            split.Controls.Add(filter, 0, 0);
            split.Controls.Add(grid, 0, 1);
            host.Button("Apply", ApplyFilter);
            host.Button("Clear", () => { filter.Clear(); ApplyFilter(); });
            host.Button("Edit in dialog...", () =>
            {
                var edited = EnterpriseDataFilter.Edit(split.FindForm(), typeof(GridDemoRecord), filter.ToDescriptor());
                filter.Load(edited);
                ApplyFilter();
            });
            host.Button("Add condition", () => filter.AddCondition());
            host.Button("Add group", () => filter.AddGroup());
            ApplyFilter();
            return split;
        }
    };

    // ---- PivotGrid ----------------------------------------------------------------------

    private static GridScenario PivotGrid() => new()
    {
        Id = "data-pivotgrid",
        Title = "PivotGrid - Cross-tab with Subtotals and Collapsible Groups",
        Description = "Revenue by region and product across order status, with subtotals, grand totals, frozen headers and groups you collapse by clicking the chevron.",
        Features = new[] { "Row and column fields", "Aggregates", "Subtotals / grand totals", "Collapsible groups", "Frozen headers" },
        Documentation = "EnterprisePivotGridControl computes the cross-tab with PivotEngine; value, subtotal and total cells are rendered by the control.",
        Usage = "pivot.RowFields.Add(\"Region\"); pivot.ColumnFields.Add(\"Status\"); pivot.Values.Add(new PivotValueField(\"Total\", AggregateFunction.Sum)); pivot.DataSource = orders;",
        Api = "EnterprisePivotGridControl -> RowFields, ColumnFields, Values, DataSource, ShowSubtotals, ShowGrandTotals, ToggleGroup, ExpandAll, CollapseAll",
        Build = host =>
        {
            var pivot = new EnterprisePivotGridControl { LabelWidth = 150, ColumnWidth = 110 };
            pivot.RowFields.Add("Region");
            pivot.RowFields.Add("Product");
            pivot.ColumnFields.Add("Status");
            pivot.Values.Add(new PivotValueField("Total", EnterpriseUI.Data.AggregateFunction.Sum, "Revenue", "C0"));
            pivot.DataSource = GridScenarioData.Records(800);
            pivot.SelectionChanged += (_, _) => host.Metric($"Selected value: {pivot.SelectedValue}");
            host.Toggle("Subtotals", true, v => pivot.ShowSubtotals = v);
            host.Toggle("Grand totals", true, v => pivot.ShowGrandTotals = v);
            host.Button("Collapse regions", pivot.CollapseAll);
            host.Button("Expand all", pivot.ExpandAll);
            host.Combo("Value:", new[] { "Sum of revenue", "Average revenue", "Count" }, "Sum of revenue", v =>
            {
                pivot.Values.Clear();
                pivot.Values.Add(v switch
                {
                    "Average revenue" => new PivotValueField("Total", EnterpriseUI.Data.AggregateFunction.Average, "Avg revenue", "C0"),
                    "Count" => new PivotValueField("Id", EnterpriseUI.Data.AggregateFunction.Count, "Orders", "N0"),
                    _ => new PivotValueField("Total", EnterpriseUI.Data.AggregateFunction.Sum, "Revenue", "C0")
                });
                pivot.Recompute();
            });
            host.Metric("Click a chevron in the row labels to collapse a group; arrow keys move between cells.");
            return pivot;
        }
    };

    // ---- TreeMap ------------------------------------------------------------------------

    private static GridScenario TreeMap() => new()
    {
        Id = "data-treemap",
        Title = "TreeMap - Area-proportional Hierarchy with Drill-down",
        Description = "Revenue by region and product as squarified tiles; color follows the category or the quantity. Double-click a group to drill in, use the breadcrumb or Backspace to go back.",
        Features = new[] { "Squarified layout", "Category / sequential / diverging colors", "Drill-down and breadcrumb", "Tooltips with share", "Keyboard navigation" },
        Documentation = "EnterpriseTreeMapControl lays tiles out with TreeMapLayout; groups reserve a header and padding, tiles smaller than a label draw no text.",
        Usage = "map.Bind(orders, nameField: \"Customer\", valueField: \"Total\", colorField: \"Quantity\", \"Region\", \"Product\");",
        Api = "EnterpriseTreeMapControl -> Bind, SetItems, ColorMode, Levels, DrillInto, DrillUp, NavigateTo, ItemClick, ItemActivate",
        Build = host =>
        {
            var map = new EnterpriseTreeMapControl { ValueFormat = "C0" };
            map.Bind(GridScenarioData.Records(120), "Customer", "Total", "Quantity", "Region", "Product");
            map.SelectionChanged += (_, _) => host.Metric(map.SelectedItem is { } item ? $"{item.Path}: {item.TotalValue:C0}" : "No selection");
            map.CurrentChanged += (_, _) => host.Metric(map.CurrentItem?.Path ?? "All regions");
            host.Combo("Color:", ColorModes, "Category", v => map.ColorMode = Enum.Parse<TreeMapColorMode>(v));
            host.Combo("Levels:", new[] { "1", "2", "3" }, "2", v => map.Levels = int.Parse(v));
            host.Button("Up", () => map.DrillUp());
            return map;
        }
    };

    // ---- HeatMap ------------------------------------------------------------------------

    private static GridScenario HeatMap() => new()
    {
        Id = "data-heatmap",
        Title = "HeatMap - Matrix of Aggregated Values",
        Description = "Revenue for every region and product pair. Pick the scale and the aggregate, click a column header to sort the rows, hover for the exact value.",
        Features = new[] { "Sequential / diverging scale", "Aggregates", "Legend", "Header sorting", "Missing cells" },
        Documentation = "EnterpriseHeatMapControl aggregates objects into a matrix with Bind or accepts one directly through SetMatrix; the cells scroll when they would get smaller than the minimum size.",
        Usage = "map.Bind(orders, rowField: \"Region\", columnField: \"Product\", valueField: \"Total\", HeatMapAggregate.Sum);",
        Api = "EnterpriseHeatMapControl -> Bind, SetMatrix, Scale, Minimum, Maximum, ShowValues, SortRowsByColumn, CellClick",
        Build = host =>
        {
            var records = GridScenarioData.Records(400);
            var map = new EnterpriseHeatMapControl { Title = "Revenue by region and product", ValueFormat = "C0" };
            void Rebind(HeatMapAggregate aggregate) => map.Bind(records, "Region", "Product", "Total", aggregate);
            Rebind(HeatMapAggregate.Sum);
            map.CellClick += (_, e) => host.Metric($"{e.RowLabel} / {e.ColumnLabel}: {e.Value:C0}");
            host.Combo("Scale:", new[] { "Sequential", "Diverging" }, "Sequential", v => map.Scale = Enum.Parse<HeatMapScale>(v));
            host.Combo("Aggregate:", Enum.GetNames<HeatMapAggregate>(), "Sum", v => Rebind(Enum.Parse<HeatMapAggregate>(v)));
            host.Toggle("Show values", true, v => map.ShowValues = v);
            host.Toggle("Legend", true, v => map.ShowLegend = v);
            return map;
        }
    };

    // ---- VirtualGrid --------------------------------------------------------------------

    private static GridScenario VirtualGrid() => new()
    {
        Id = "data-virtualgrid",
        Title = "VirtualGrid - A Million Rows on Demand",
        Description = "A grid that never holds its rows: the control asks for the visible cells only. Jump anywhere, edit a cell and see the value pushed back.",
        Features = new[] { "CellValueNeeded", "CellValuePushed", "1,000,000 rows", "Row and column headers", "Inline editing" },
        Documentation = "EnterpriseVirtualGridControl suits computed or remote data: provide values in CellValueNeeded, store edits in CellValuePushed.",
        Usage = "grid.UseSampleData = false; grid.RowCount = 5_000_000; grid.CellValueNeeded += (s, e) => e.Value = Compute(e.RowIndex, e.ColumnIndex);",
        Api = "EnterpriseVirtualGridControl -> RowCount, ColumnCount, ColumnTitles, CellValueNeeded, CellValuePushed, SelectCell, AllowEdit",
        Build = host =>
        {
            var grid = new EnterpriseVirtualGridControl { RowCount = 1_000_000, ColumnCount = 12, UseSampleData = false };
            grid.ColumnTitles.AddRange(new[] { "Transaction", "Venue", "Notional", "Volume", "Metric 5", "Metric 6" });
            var venues = new[] { "NYSE", "NASDAQ", "LSE", "TSE" };
            grid.CellValueNeeded += (_, e) => e.Value = e.ColumnIndex switch
            {
                0 => $"TX-{e.RowIndex + 100000:D7}",
                1 => venues[e.RowIndex % venues.Length],
                2 => 100m + e.RowIndex * 17 % 9000 / 10m,
                3 => e.RowIndex * 31 % 500 + 10,
                _ => (e.RowIndex * (e.ColumnIndex + 3)) % 997
            };
            grid.CellSelected += (row, column, value) => host.Metric($"Row {row + 1:N0}, column {column + 1}: {value}");
            grid.CellValuePushed += (_, e) => host.Log("VirtualGrid.CellValuePushed", $"[{e.RowIndex}, {e.ColumnIndex}] = {e.Value}");
            host.Button("First", () => grid.SelectCell(0, 0));
            host.Button("Middle", () => grid.SelectCell(500_000, 2));
            host.Button("Last", () => grid.SelectCell(grid.RowCount - 1, 0));
            host.Toggle("Editing", true, v => grid.AllowEdit = v);
            host.Metric("Press F2 or start typing to edit the selected cell.");
            return grid;
        }
    };
}
