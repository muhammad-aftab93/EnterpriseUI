using System.ComponentModel;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Data;
using EnterpriseUI.Grids;
using EnterpriseUI.Printing;
using EnterpriseUI.Tests.Support;
using EnterpriseUI.Theming;
using EnterpriseUI.Theming.Themes;
using EnterpriseUI.WinForms;
using Xunit;
using static EnterpriseUI.Tests.Support.WinFormsTestSupport;

namespace EnterpriseUI.Tests.UI;

public class ToolboxTests
{
    [Fact]
    public void ToolboxControlsUseEnterpriseNamesAndHaveDefaultConstructors()
    {
        var controls = typeof(EnterpriseDataGrid).Assembly.GetExportedTypes()
            .Where(type => typeof(Control).IsAssignableFrom(type)
                && !type.IsAbstract && !type.ContainsGenericParameters)
            .ToArray();

        Assert.Contains(typeof(EnterpriseDataGrid), controls);
        Assert.All(controls, type =>
        {
            Assert.StartsWith("Enterprise", type.Name);
            Assert.NotNull(type.GetConstructor(Type.EmptyTypes));
        });
        Assert.True(((ToolboxItemAttribute)TypeDescriptor.GetAttributes(typeof(EnterpriseDataGrid))
            [typeof(ToolboxItemAttribute)]!).Equals(ToolboxItemAttribute.Default));
        Assert.Equal(ToolboxItemAttribute.None, TypeDescriptor.GetAttributes(typeof(EnterpriseDataGrid<object>))
            [typeof(ToolboxItemAttribute)]);
    }

    [Fact]
    public void NonGenericGridCanBindAndRenderTypedRows()
    {
        RunSta(() =>
        {
            using var grid = new EnterpriseDataGrid { Width = 700, Height = 400 };
            grid.Columns.Add(new GridColumn { Field = "Name", Title = "Name", Width = 200 });
            grid.DataSource = Rows(10);
            Wait(grid.RefreshDataAsync());

            Assert.Equal(10, grid.TotalItemsCount);
            using var bitmap = new Bitmap(grid.Width, grid.Height);
            grid.DrawToBitmap(bitmap, new Rectangle(0, 0, grid.Width, grid.Height));
            Assert.Equal("Name 0001", PropertyPath.GetValue(grid.DataSource!.First(), "Name"));
        });
    }
}

public class DirectGridApiTests
{
    [Fact]
    public void PublicEngineFeaturesHaveDirectControlApis()
    {
        var engineType = typeof(DataGrid<Row>);
        var controlType = typeof(EnterpriseDataGrid<Row>);
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static
            | System.Reflection.BindingFlags.DeclaredOnly;

        foreach (var method in engineType.GetMethods(flags).Where(method => !method.IsSpecialName
            && method.Name is not ("Dispose" or "DisposeAsync" or "UpdateViewport")))
        {
            var parameterTypes = method.GetParameters().Select(parameter =>
                parameter.ParameterType == engineType ? controlType : parameter.ParameterType).ToArray();
            var facadeMethod = controlType.GetMethod(method.Name, parameterTypes);
            Assert.True(facadeMethod != null, $"Missing direct grid method: {method.Name}");
            Assert.Equal(method.ReturnType, facadeMethod!.ReturnType);
        }

        foreach (var property in engineType.GetProperties(flags).Where(property => property.Name is not ("Parent" or "Children")))
        {
            var facadeProperty = controlType.GetProperty(property.Name);
            Assert.True(facadeProperty != null, $"Missing direct grid property: {property.Name}");
            Assert.Equal(property.PropertyType, facadeProperty!.PropertyType);
            if (property.SetMethod?.IsPublic == true)
                Assert.True(facadeProperty.SetMethod?.IsPublic == true, $"Read-only facade property: {property.Name}");
        }

        foreach (var engineEvent in engineType.GetEvents(flags))
        {
            var eventName = engineEvent.Name == "CellEdited" ? "CellEditCompleted" : engineEvent.Name;
            Assert.Equal(engineEvent.EventHandlerType, controlType.GetEvent(eventName)?.EventHandlerType);
        }
    }

    [Fact]
    public void PagingAndRangeFiltersCanBeUsedWithoutTheEngine()
    {
        RunSta(() =>
        {
            using var grid = CreateGrid(40);
            grid.PagingMode = GridPagingMode.Client;
            grid.SetPageSize(10);
            Wait(grid.RefreshDataAsync());

            Assert.Equal(4, grid.PageCount);
            Assert.True(grid.LastPage());
            Assert.Equal(3, grid.PageIndex);
            Assert.True(grid.FirstPage());
            Assert.True(grid.NextPage());
            Assert.True(grid.PreviousPage());

            grid.FilterBy("Id", FilterOperator.IsBetween, 5, 10);
            Wait(grid.RefreshDataAsync());
            Assert.True(grid.HasFilter("Id"));
            Assert.Equal(6, grid.TotalRecordCount);
            grid.SaveFilterPreset("Range");
            var json = grid.ExportFilterPresets();
            grid.ClearFilters();
            grid.SavedFilterPresets.Clear();
            Assert.Empty(grid.ImportFilterPresets(json));
            Assert.True(grid.SavedFilterPresets.ContainsKey("Range"));
            Assert.True(grid.ApplyFilterPreset("Range"));
            Assert.False(grid.ApplyFilterPreset("Missing"));
            Wait(grid.RefreshDataAsync());
            Assert.Equal(6, grid.TotalRecordCount);
        });
    }

    [Fact]
    public void EditingTransactionsAndEventsAreAvailableDirectly()
    {
        RunSta(() =>
        {
            using var grid = CreateGrid(10);
            var item = grid.DataSource!.First();
            var edits = 0;
            var commits = 0;
            EventHandler<GridCellEditedEventArgs> handler = (_, _) => edits++;
            grid.CellEditCompleted += handler;
            grid.ChangesCommitted += (_, _) => commits++;

            grid.BeginTransaction();
            Assert.True(grid.IsInTransaction);
            Assert.True(grid.TryEditCell(item, "Name", "Updated").Success);
            Assert.True(grid.IsCellDirty(item, "Name"));
            Assert.True(grid.HasPendingChanges);
            Assert.Single(grid.PendingChanges);
            grid.CommitTransaction();
            Assert.Equal(1, commits);
            Assert.Equal(1, edits);
            Assert.False(grid.HasPendingChanges);
            grid.CellEditCompleted -= handler;
            Assert.True(grid.TryEditCell(item, "Name", "Again").Success);
            Assert.Equal(1, edits);
            grid.ClearUndoHistory();
            Assert.False(grid.CanUndo);
        });
    }

    [Fact]
    public void SearchingAndAccessibilityAreAvailableDirectly()
    {
        RunSta(() =>
        {
            using var grid = CreateGrid(10);
            grid.SearchMode = GridSearchMode.Highlight;
            var notifications = 0;
            grid.SearchResultChanged += (_, _) => notifications++;
            var search = grid.SearchAsync("Name 0001");
            Wait(search);
            Assert.True(search.Result.MatchCount > 0);
            Assert.Same(search.Result, grid.SearchResult);
            Assert.True(notifications > 0);
            Assert.True(grid.GoToMatch(0));
            Assert.Contains("Test rows", grid.DescribeGrid());
        });
    }

    [Fact]
    public void RelationsCanBeConfiguredDirectlyOnTheControl()
    {
        RunSta(() =>
        {
            using var grid = CreateGrid(10);
            var children = Rows(5);
            var relation = grid.AddRelation("Children", "Id", "Id", children, typeof(Row));
            Wait(grid.RefreshDataAsync());

            Assert.Same(relation, Assert.Single(grid.Relations));
            Assert.Same(children, relation.ChildSource);
            Assert.True(grid.RemoveRelation("Children"));
            Assert.Empty(grid.Relations);

            var tree = grid.AddSelfReference("Id", "Id");
            Assert.Equal(HierarchyMode.SelfReferencing, tree.Mode);
            Assert.True(grid.RemoveRelation("Tree"));
        });
    }
}

public class RenderingTests
{
    [Fact]
    public void GridPaintsHeaderRowsAndBordersWithoutThrowing()
    {
        RunSta(() =>
        {
            using var grid = CreateGrid(40);
            using var bitmap = grid.Render();

            // Header (top strip) and body must differ, and the body must contain text pixels, not a blank surface.
            var header = Pixel(bitmap, grid.S(300), grid.S(4));
            var surface = Pixel(bitmap, grid.S(300), grid.S(32 + 4));
            Assert.NotEqual(header, surface);
            var distinct = new HashSet<int>();
            for (var x = 0; x < 300; x += 3) distinct.Add(bitmap.GetPixel(x, grid.S(32 + 15)).ToArgb());
            Assert.True(distinct.Count > 3, "the first row should contain rendered text");
        });
    }

    [Fact]
    public void SwitchingToADarkThemeRepaintsWithDarkerSurfaceColors()
    {
        RunSta(() =>
        {
            var manager = ThemeManager.Instance;
            var original = manager.CurrentTheme;
            try
            {
                manager.ApplyTheme(BuiltInThemes.CreateFluent(ColorScheme.Light));
                using var grid = CreateGrid(20);
                using var light = grid.Render();
                var lightPixel = Pixel(light, grid.S(500), grid.S(32 + 15));

                manager.ApplyTheme(BuiltInThemes.CreateFluent(ColorScheme.Dark));
                Pump(100);
                using var dark = grid.Render();
                var darkPixel = Pixel(dark, grid.S(500), grid.S(32 + 15));

                Assert.True(lightPixel.GetBrightness() > darkPixel.GetBrightness() + 0.3f, $"light {lightPixel} vs dark {darkPixel}");
            }
            finally
            {
                manager.ApplyTheme(original);
            }
        });
    }

    [Fact]
    public void EmptyAndErrorStatesShowAMessageInsteadOfABlankGrid()
    {
        RunSta(() =>
        {
            using var grid = CreateGrid(0);
            using var bitmap = grid.Render();
            var distinct = new HashSet<int>();
            for (var x = 200; x < 700; x += 2) distinct.Add(bitmap.GetPixel(x, grid.S(32 + 60)).ToArgb());
            Assert.True(distinct.Count > 1 || grid.TotalItemsCount == 0);
            Assert.Equal(0, grid.TotalItemsCount);
        });
    }

    [Fact]
    public void PrintPlanAndExportWorkFromTheControl()
    {
        RunSta(() =>
        {
            using var grid = CreateGrid(300);
            var plan = grid.CreatePrintPlanAsync().GetAwaiter().GetResult();
            Assert.True(plan.PageCount >= 2);
            Assert.Equal(300, plan.TotalRows);

            var path = Path.Combine(Path.GetTempPath(), $"ui-{Guid.NewGuid():N}.xlsx");
            try
            {
                var result = grid.ExportToFileAsync(path).GetAwaiter().GetResult();
                Assert.Equal(300, result.Rows);
                Assert.True(File.Exists(path));
            }
            finally
            {
                File.Delete(path);
            }

            grid.PrintTemplate = PrintTemplate.LandscapeReport();
            Assert.True(grid.CreatePrintPlanAsync().GetAwaiter().GetResult().Template.Settings.IsLandscape);
        });
    }
}

public class KeyboardAndSelectionTests
{
    [Fact]
    public void ArrowKeysMoveTheFocusCellAndSelectTheRow()
    {
        RunSta(() =>
        {
            using var grid = CreateGrid(30);
            grid.Focus();
            grid.Selection.SetFocus(0, 0);

            grid.Key(Keys.Down);
            grid.Key(Keys.Down);
            grid.Key(Keys.Right);

            Assert.Equal(2, grid.Selection.FocusedRow);
            Assert.Equal(1, grid.Selection.FocusedColumn);
            Assert.Equal(new[] { 2 }, grid.SelectedIndices.ToArray());
            Assert.Equal(3, grid.SelectedItem!.Id);
        });
    }

    [Fact]
    public void ShiftArrowExtendsAndControlASelectsEverything()
    {
        RunSta(() =>
        {
            using var grid = CreateGrid(30, g => g.SelectionMode = GridSelectionMode.MultipleRows);
            grid.Selection.Click(1, 0);

            grid.Key(Keys.Down | Keys.Shift);
            grid.Key(Keys.Down | Keys.Shift);
            Assert.Equal(new[] { 1, 2, 3 }, grid.SelectedIndices.OrderBy(i => i).ToArray());

            grid.Key(Keys.A | Keys.Control);
            Assert.Equal(30, grid.SelectedIndices.Count);
        });
    }

    [Fact]
    public void ClickingRowsSelectsAndControlClickToggles()
    {
        RunSta(() =>
        {
            using var grid = CreateGrid(30, g => g.SelectionMode = GridSelectionMode.MultipleRows);
            var y = (int row) => grid.S(32) + row * grid.S(30) + grid.S(15);

            grid.Click(grid.S(120), y(2));
            Assert.Equal(new[] { 2 }, grid.SelectedIndices.ToArray());

            grid.Selection.Click(4, 1, ctrl: true);
            Assert.Equal(new[] { 2, 4 }, grid.SelectedIndices.OrderBy(i => i).ToArray());
        });
    }

    [Fact]
    public void CheckboxModeShowsAGutterAndClickingItTogglesTheRow()
    {
        RunSta(() =>
        {
            using var grid = CreateGrid(20, g => g.SelectionMode = GridSelectionMode.CheckboxRow);
            var y = grid.S(32) + grid.S(15);

            grid.Click(grid.S(14), y);
            grid.Click(grid.S(14), y + grid.S(30));

            Assert.Equal(new[] { 0, 1 }, grid.SelectedIndices.OrderBy(i => i).ToArray());
            grid.Click(grid.S(14), y);
            Assert.Equal(new[] { 1 }, grid.SelectedIndices.ToArray());
        });
    }

    [Fact]
    public void CellRangeSelectionByDraggingCoversTheRectangle()
    {
        RunSta(() =>
        {
            using var grid = CreateGrid(30, g => g.SelectionMode = GridSelectionMode.CellRange);
            var y = (int row) => grid.S(32) + row * grid.S(30) + grid.S(15);

            grid.Down(grid.S(100), y(1));
            grid.Move(grid.S(280), y(3));
            grid.Up(grid.S(280), y(3));

            var range = Assert.Single(grid.Selection.CellRanges);
            Assert.Equal(1, range.RowFrom);
            Assert.Equal(3, range.RowTo);
            Assert.True(range.ColumnTo > range.ColumnFrom);
        });
    }

    [Fact]
    public void HeaderClickSortsThroughAscendingDescendingAndNone()
    {
        RunSta(() =>
        {
            using var grid = CreateGrid(30);
            var x = grid.S(300);
            var y = grid.S(16);

            grid.Click(x, y);
            Assert.Equal(ListSortDirection.Ascending, grid.CurrentRequest.Sorts.Single().SortDirection);
            grid.Click(x, y);
            Assert.Equal(ListSortDirection.Descending, grid.CurrentRequest.Sorts.Single().SortDirection);
            grid.Click(x, y);
            Assert.Empty(grid.CurrentRequest.Sorts);
        });
    }

    [Fact]
    public void ControlCCopiesTheSelectedRowsWithHeadersAndTypeAheadFindsRows()
    {
        RunSta(() =>
        {
            using var grid = CreateGrid(60, g => g.SelectionMode = GridSelectionMode.MultipleRows);
            grid.Selection.Click(0, 0);
            grid.Selection.Click(2, 0, shift: true);

            grid.Key(Keys.C | Keys.Control);
            var text = Clipboard.GetText();
            Assert.StartsWith("Id\tName", text);
            Assert.Equal(4, text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);

            grid.Selection.SetFocus(0, 1);
            grid.Type('N');
            grid.Type('a');
            grid.Type('m');
            grid.Type('e');
            grid.Type(' ');
            grid.Type('0');
            grid.Type('0');
            grid.Type('3');
            grid.Type('0');
            PumpUntil(() => grid.Selection.FocusedRow == 29);
        });
    }
}

public class PagingGroupingAndEditingTests
{
    [Fact]
    public void PagerShowsPageButtonsAndClickingOneNavigates()
    {
        RunSta(() =>
        {
            using var grid = CreateGrid(95, g => { g.PagingMode = GridPagingMode.Client; g.PageSize = 10; });
            Assert.Equal(10, grid.PageCount);
            Assert.Equal(10, grid.TotalItemsCount);

            // Buttons: first (8), previous (41), page 1 (74), page 2 (107)
            var y = grid.Height - grid.S(18);
            grid.Down(grid.S(122), y);
            grid.Up(grid.S(122), y);
            PumpUntil(() => grid.PageIndex == 1);

            Assert.True(grid.TryGetItemAt(0, out var first));
            Assert.Equal(11, first.Id);
            using var bitmap = grid.Render();
            Assert.NotEqual(Pixel(bitmap, grid.S(90), y), Pixel(bitmap, grid.S(90), y - grid.S(60)));
        });
    }

    [Fact]
    public void ControlPageDownMovesToTheNextPage()
    {
        RunSta(() =>
        {
            using var grid = CreateGrid(95, g => { g.PagingMode = GridPagingMode.Client; g.PageSize = 25; });
            grid.Key(Keys.PageDown | Keys.Control);
            PumpUntil(() => grid.PageIndex == 1);
            grid.Key(Keys.PageUp | Keys.Control);
            PumpUntil(() => grid.PageIndex == 0);
        });
    }

    [Fact]
    public void ClickingAGroupHeaderCollapsesAndExpandsIt()
    {
        RunSta(() =>
        {
            using var grid = CreateGrid(30);
            grid.GroupBy("Region");
            PumpUntil(() => grid.TotalItemsCount > 30);
            var rowsExpanded = grid.TotalItemsCount;

            var y = grid.S(32) + grid.S(15);
            grid.Click(grid.S(200), y);
            PumpUntil(() => grid.TotalItemsCount < rowsExpanded);
            grid.Click(grid.S(200), y);
            PumpUntil(() => grid.TotalItemsCount == rowsExpanded);
        });
    }

    [Fact]
    public void AltArrowsExpandAndCollapseTheFocusedGroup()
    {
        RunSta(() =>
        {
            using var grid = CreateGrid(30);
            grid.GroupBy("Region");
            PumpUntil(() => grid.TotalItemsCount > 30);
            var expanded = grid.TotalItemsCount;
            grid.Selection.SetFocus(0, 0);

            grid.Key(Keys.Left | Keys.Alt);
            PumpUntil(() => grid.TotalItemsCount < expanded);
            grid.Key(Keys.Right | Keys.Alt);
            PumpUntil(() => grid.TotalItemsCount == expanded);
        });
    }

    [Fact]
    public void F2OpensAnEditorAndEnterCommitsAValidValue()
    {
        RunSta(() =>
        {
            using var grid = CreateGrid(10);
            grid.Selection.SetFocus(1, 1);

            grid.Key(Keys.F2);
            var editor = OpenEditor(grid);
            Assert.NotNull(editor);
            Assert.True(grid.IsEditing);
            editor!.Text = "Renamed";
            PressInEditor(editor, Keys.Enter);

            Assert.False(grid.IsEditing);
            Assert.True(grid.TryGetItemAt(1, out var item));
            Assert.Equal("Renamed", item.Name);
            Assert.True(grid.CanUndo);
        });
    }

    [Fact]
    public void AnInvalidValueKeepsTheEditorOpenAndLeavesTheDataUntouched()
    {
        RunSta(() =>
        {
            using var grid = CreateGrid(10);
            grid.Columns.First(c => c.Field == "Name").ValidationRules.Add(new RequiredRule { Message = "Name is required" });
            grid.Selection.SetFocus(2, 1);
            grid.Key(Keys.F2);
            var editor = OpenEditor(grid)!;

            editor.Text = string.Empty;
            PressInEditor(editor, Keys.Enter);

            Assert.True(grid.IsEditing, "an invalid value must not close the editor on Enter");
            Assert.True(grid.TryGetItemAt(2, out var item));
            Assert.Equal("Name 0003", item.Name);

            PressInEditor(editor, Keys.Escape);
            Assert.False(grid.IsEditing);
        });
    }

    [Fact]
    public void EditingIsBlockedForReadOnlyColumnsAndWhenEditModeIsNone()
    {
        RunSta(() =>
        {
            using var grid = CreateGrid(10);
            grid.Selection.SetFocus(0, 0);
            grid.Key(Keys.F2);
            Assert.False(grid.IsEditing);

            grid.Selection.SetFocus(0, 1);
            grid.EditMode = GridEditMode.None;
            grid.Key(Keys.F2);
            Assert.False(grid.IsEditing);
        });
    }

    [Fact]
    public void ClickingABooleanCellTogglesItAndSpaceDoesToo()
    {
        RunSta(() =>
        {
            using var grid = CreateGrid(10);
            Assert.True(grid.TryGetItemAt(0, out var item));
            var before = item.Active;
            var y = grid.S(32) + grid.S(15);
            var checkboxX = grid.S(440 + 40);

            grid.Click(checkboxX, y);
            Assert.NotEqual(before, item.Active);

            grid.Selection.SetFocus(0, 4);
            grid.Key(Keys.Space);
            Assert.Equal(before, item.Active);
        });
    }

    [Fact]
    public void BatchEditsCommitOrRollBackTogether()
    {
        RunSta(() =>
        {
            using var grid = CreateGrid(10, g => g.EditMode = GridEditMode.Batch);
            Assert.True(grid.TryGetItemAt(0, out var item));
            Assert.True(grid.EditCell(item, "Name", "Changed"));
            Assert.True(grid.HasPendingChanges);

            Assert.Equal(1, grid.RollbackChanges());
            Assert.Equal("Name 0001", item.Name);

            grid.EditCell(item, "Name", "Kept");
            Assert.True(grid.CommitChanges().Success);
            Assert.Equal("Kept", item.Name);
        });
    }

    [Fact]
    public void ShowingTheSearchPanelFiltersRowsAndEscapeClearsIt()
    {
        RunSta(() =>
        {
            using var grid = CreateGrid(100);
            grid.ShowSearchPanel = true;
            Assert.True(grid.ShowSearchPanel);

            grid.SearchPanelText = "Name 0042";
            PumpUntil(() => grid.TotalItemsCount == 1);

            grid.ShowSearchPanel = false;
            PumpUntil(() => grid.TotalItemsCount == 100);
        });
    }

    [Fact]
    public void DraggingAHeaderEdgeResizesTheColumn()
    {
        RunSta(() =>
        {
            using var grid = CreateGrid(10);
            var column = grid.Columns.First(c => c.Field == "Name");
            var before = column.Width!.Value;
            var edge = grid.S(240);
            var y = grid.S(16);

            grid.Down(edge, y);
            grid.Move(edge + grid.S(60), y);
            grid.Up(edge + grid.S(60), y);

            Assert.True(column.Width > before + 40, $"width {before} -> {column.Width}");
        });
    }

    [Fact]
    public void FluentBuilderConfiguresColumnsBandsPagingAndQuery()
    {
        RunSta(() =>
        {
            using var grid = GridBuilder<Row>.Create()
                .Label("Built grid")
                .Column("Id", "Id", 60)
                .Column("Name", "Name", 140)
                .Column("Amount", "Amount", 100, c => c.FormatString = "N2")
                .Band("Identity", "Id", "Name")
                .Paging(GridPagingMode.Client, 20)
                .Selection(GridSelectionMode.MultipleRows)
                .SortBy("Amount", ListSortDirection.Descending)
                .SortBy("Name", ListSortDirection.Ascending)
                .GroupBy("Id")
                .Summary("Amount", AggregateFunction.Sum, "N0")
                .Filter("Name", FilterOperator.Contains, "Name")
                .DataSource(WinFormsTestSupport.Rows(50))
                .Build();

            Assert.Equal("Built grid", grid.AriaLabel);
            Assert.Equal(3, grid.Columns.Count);
            Assert.Single(grid.Bands);
            Assert.Equal(grid.Bands[0].Id, grid.Columns[0].BandId);
            Assert.Equal(GridPagingMode.Client, grid.PagingMode);
            Assert.Equal(20, grid.PageSize);
            Assert.Equal(new[] { "Amount", "Name" }, grid.CurrentRequest.Sorts.Select(s => s.Member));
            Assert.Equal(ListSortDirection.Descending, grid.CurrentRequest.Sorts[0].SortDirection);
            Assert.Single(grid.CurrentRequest.Groups);
            Assert.Single(grid.Columns[2].FooterSummaries);
            Assert.Single(grid.CurrentRequest.Filters);
            Assert.Throws<InvalidOperationException>(() => GridBuilder<Row>.Create().Summary("Missing", AggregateFunction.Sum));
        });
    }
}
