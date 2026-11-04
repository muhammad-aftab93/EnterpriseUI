using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EnterpriseUI.Data;
using EnterpriseUI.Grids;
using Xunit;

namespace EnterpriseUI.Tests.Unit;

public class GridLayoutAndWindowTests
{
    private sealed class Row
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private static GridColumn Col(string field, double? width = null, ColumnPinPosition pin = ColumnPinPosition.None)
        => new() { Field = field, Title = field, Width = width, PinPosition = pin };

    [Fact]
    public void LayoutHonorsColumnWidthsAndPinZones()
    {
        var columns = new List<GridColumn>
        {
            Col("A", 100),
            Col("B", 150, ColumnPinPosition.Right),
            Col("C", 80, ColumnPinPosition.Left),
            Col("D", 200)
        };

        var ordered = GridColumnLayout.OrderVisible(columns);
        Assert.Equal(new[] { "C", "A", "D", "B" }, ordered.Select(c => c.Field));

        var layout = GridColumnLayout.Compute(ordered, viewportWidth: 400, horizontalOffset: 0, fillViewport: false);

        Assert.Equal(0, layout.Slots[0].Left);
        Assert.Equal(80, layout.Slots[1].Left);
        Assert.Equal(180, layout.Slots[2].Left);
        Assert.Equal(250, layout.Slots[3].Left);
        Assert.Equal(530, layout.ContentWidth);
        Assert.Equal(130, layout.MaxHorizontalOffset);
    }

    [Fact]
    public void LayoutScrollsOnlyTheUnpinnedColumns()
    {
        var columns = new List<GridColumn> { Col("Pin", 60, ColumnPinPosition.Left), Col("A", 100), Col("B", 100), Col("C", 100) };
        var ordered = GridColumnLayout.OrderVisible(columns);

        var layout = GridColumnLayout.Compute(ordered, 200, 50, fillViewport: false);

        Assert.Equal(0, layout.Slots[0].Left);
        Assert.Equal(10, layout.Slots[1].Left);
        Assert.Equal(50, layout.HorizontalOffset);
        Assert.Equal(0, layout.HitTest(30));
        Assert.Equal(1, layout.HitTest(100));
    }

    [Fact]
    public void LayoutClampsOffsetAndFillsViewportWhenNarrow()
    {
        var columns = new List<GridColumn> { Col("A", 100), Col("B", 100) };
        var ordered = GridColumnLayout.OrderVisible(columns);

        var clamped = GridColumnLayout.Compute(ordered, 150, 9999, fillViewport: false);
        Assert.Equal(50, clamped.HorizontalOffset);

        var filled = GridColumnLayout.Compute(ordered, 400, 0, fillViewport: true);
        Assert.Equal(100, filled.Slots[0].Width);
        Assert.Equal(300, filled.Slots[1].Width);
        Assert.Equal(400, filled.ContentWidth);
        Assert.Equal(0, filled.MaxHorizontalOffset);

        var scaled = GridColumnLayout.Compute(ordered, 150, 0, fillViewport: false, scale: 1.5);
        Assert.Equal(150, scaled.Slots[0].Width);
        Assert.Equal(300, scaled.ContentWidth);
    }

    [Fact]
    public void ResizeHandleHitTestingRespectsTolerancePinningAndResizable()
    {
        var fixedCol = Col("Fixed", 100);
        fixedCol.Resizable = false;
        var columns = new List<GridColumn> { fixedCol, Col("B", 100) };
        var layout = GridColumnLayout.Compute(GridColumnLayout.OrderVisible(columns), 400, 0, false);

        Assert.Equal(-1, layout.HitTestResizeHandle(100, 4));
        Assert.Equal(1, layout.HitTestResizeHandle(202, 4));
        Assert.Equal(-1, layout.HitTestResizeHandle(150, 4));
    }

    [Fact]
    public void InsertionIndexUsesColumnMidpoints()
    {
        var columns = new List<GridColumn> { Col("A", 100), Col("B", 100) };
        var layout = GridColumnLayout.Compute(GridColumnLayout.OrderVisible(columns), 400, 0, false);

        Assert.Equal(0, layout.GetInsertionIndex(10));
        Assert.Equal(1, layout.GetInsertionIndex(60));
        Assert.Equal(2, layout.GetInsertionIndex(180));
        Assert.Equal(2, layout.GetInsertionIndex(390));
    }

    [Fact]
    public void BandSpansCoverContiguousColumnsAcrossNestedLevels()
    {
        var parent = new GridColumnBand { Title = "Financial" };
        var child = new GridColumnBand { Title = "Q1" };
        parent.ChildBands.Add(child);
        var identity = new GridColumnBand { Title = "Identity" };

        var id = Col("Id", 50); id.BandId = identity.Id;
        var rev = Col("Rev", 100); rev.BandId = child.Id;
        var cost = Col("Cost", 100); cost.BandId = child.Id;
        var note = Col("Note", 80);
        var columns = new List<GridColumn> { id, rev, cost, note };
        var bands = new List<GridColumnBand> { identity, parent };

        Assert.Equal(2, GridColumnLayout.BandDepth(columns, bands));

        var layout = GridColumnLayout.Compute(GridColumnLayout.OrderVisible(columns), 600, 0, false);
        var spans = GridColumnLayout.ComputeBandSpans(layout, bands);

        var top = spans.Where(s => s.Level == 0).ToList();
        Assert.Equal(2, top.Count);
        Assert.Equal("Identity", top[0].Band.Title);
        Assert.Equal(50, top[0].Width);
        Assert.Equal("Financial", top[1].Band.Title);
        Assert.Equal(200, top[1].Width);

        var nested = Assert.Single(spans, s => s.Level == 1);
        Assert.Equal("Q1", nested.Band.Title);
        Assert.Equal(50, nested.Left);
        Assert.Equal(200, nested.Width);
    }

    [Fact]
    public void ColumnWidthClampsToMinAndMax()
    {
        var column = Col("A", 10);
        column.MinWidth = 40;
        column.MaxWidth = 300;

        Assert.Equal(40, GridColumnLayout.WidthOf(column));
        Assert.Equal(300, GridColumnLayout.ClampWidth(column, 900));
    }

    [Fact]
    public async Task GridLoadsOnlyTheRequestedWindowAndFollowsScrolling()
    {
        var rows = Enumerable.Range(0, 10_000).Select(i => new Row { Id = i, Name = "R" + i }).ToList();
        var grid = new DataGrid<Row> { RowBuffer = 10 };
        grid.Columns.Add(Col("Id"));
        grid.DataSource = rows;

        Assert.False(grid.EnsureRowsLoaded(5_000, 20) && grid.WindowStart == 0);
        await grid.RefreshDataAsync();

        Assert.Equal(10_000, grid.TotalItemsCount);
        Assert.Equal(4_990, grid.WindowStart);
        Assert.Equal(40, grid.RenderedItems.Count);
        Assert.True(grid.TryGetItemAt(5_000, out var item));
        Assert.Equal(5_000, item.Id);
        Assert.False(grid.TryGetItemAt(10, out _));
        Assert.True(grid.EnsureRowsLoaded(5_005, 10));
    }

    [Fact]
    public async Task GridWindowClampsWhenFilterShrinksResult()
    {
        var rows = Enumerable.Range(0, 1_000).Select(i => new Row { Id = i, Name = i < 5 ? "keep" : "drop" }).ToList();
        var grid = new DataGrid<Row> { RowBuffer = 0 };
        grid.Columns.Add(Col("Id"));
        grid.Columns.Add(Col("Name"));
        grid.DataSource = rows;
        grid.EnsureRowsLoaded(900, 20);
        await grid.RefreshDataAsync();

        grid.FilterBy("Name", FilterOperator.IsEqualTo, "keep");
        await grid.RefreshDataAsync();

        Assert.Equal(5, grid.TotalItemsCount);
        Assert.Equal(0, grid.WindowStart);
        Assert.Equal(5, grid.RenderedItems.Count);
    }

    [Fact]
    public async Task ReadAllIgnoresTheWindowButKeepsFiltersAndSort()
    {
        var rows = Enumerable.Range(0, 500).Select(i => new Row { Id = i, Name = i % 2 == 0 ? "even" : "odd" }).ToList();
        var grid = new DataGrid<Row> { RowBuffer = 0 };
        grid.Columns.Add(Col("Id"));
        grid.Columns.Add(Col("Name"));
        grid.DataSource = rows;
        grid.FilterBy("Name", FilterOperator.IsEqualTo, "even");
        grid.SortBy("Id", System.ComponentModel.ListSortDirection.Descending);
        await grid.RefreshDataAsync();

        var all = await grid.ReadAllAsync();

        Assert.Equal(250, all.Count);
        Assert.Equal(498, all[0].Id);
        Assert.True(grid.RenderedItems.Count < all.Count);
    }

    [Fact]
    public void ResizeAndMoveColumnApplyRules()
    {
        var grid = new DataGrid<Row>();
        var locked = Col("Locked", 100);
        locked.Resizable = false;
        locked.Reorderable = false;
        grid.Columns.Add(Col("A", 100));
        grid.Columns.Add(Col("B", 100, ColumnPinPosition.Left));
        grid.Columns.Add(locked);

        Assert.True(grid.ResizeColumn("A", 5));
        Assert.Equal(grid.Columns[0].MinWidth, grid.Columns[0].Width);
        Assert.False(grid.ResizeColumn("Locked", 200));
        Assert.False(grid.ResizeColumn("Missing", 200));

        Assert.True(grid.MoveColumnBefore("A", "B"));
        Assert.Equal("A", grid.Columns[0].Field);
        Assert.Equal(ColumnPinPosition.Left, grid.Columns[0].PinPosition);
        Assert.False(grid.MoveColumnBefore("Locked", "A"));
        Assert.True(grid.MoveColumnBefore("A", null));
        Assert.Equal("A", grid.Columns[^1].Field);
    }

    [Fact]
    public async Task SearchPanelReplacesPreviousSearchInsteadOfStackingFilters()
    {
        var rows = new List<Row> { new() { Id = 1, Name = "alpha" }, new() { Id = 2, Name = "beta" } };
        var grid = new DataGrid<Row>();
        grid.Columns.Add(Col("Name"));
        grid.DataSource = rows;

        grid.SearchPanelText = "alp";
        grid.SearchPanelText = "bet";
        await grid.RefreshDataAsync();

        Assert.Single(grid.CurrentRequest.Filters);
        Assert.Equal("beta", Assert.Single(grid.RenderedItems).Name);

        grid.SearchPanelText = "";
        await grid.RefreshDataAsync();
        Assert.Empty(grid.CurrentRequest.Filters);
        Assert.Equal(2, grid.RenderedItems.Count);
    }

    [Fact]
    public async Task ExcelFilterReapplyReplacesAndClearRestoresRows()
    {
        var rows = new List<Row> { new() { Id = 1, Name = "a" }, new() { Id = 2, Name = "b" }, new() { Id = 3, Name = "c" } };
        var grid = new DataGrid<Row>();
        grid.Columns.Add(Col("Name"));
        grid.DataSource = rows;

        grid.ApplyExcelFilter("Name", new object[] { "a", "b" });
        grid.ApplyExcelFilter("Name", new object[] { "c" });
        await grid.RefreshDataAsync();

        Assert.Single(grid.CurrentRequest.Filters);
        Assert.Equal("c", Assert.Single(grid.RenderedItems).Name);

        grid.ClearExcelFilter("Name");
        await grid.RefreshDataAsync();
        Assert.Empty(grid.CurrentRequest.Filters);
        Assert.Equal(3, grid.RenderedItems.Count);
    }

    [Fact]
    public void DistinctValuesAreSortedSearchableAndBounded()
    {
        var rows = new List<Row> { new() { Name = "pear" }, new() { Name = "apple" }, new() { Name = "apple" }, new() { Name = "grape" } };
        var grid = new DataGrid<Row> { DataSource = rows };

        Assert.Equal(new object[] { "apple", "grape", "pear" }, grid.GetDistinctValues("Name"));
        Assert.Equal(new object[] { "grape" }, grid.GetDistinctValues("Name", contains: "RAP"));
        Assert.Single(grid.GetDistinctValues("Name", maxCount: 1));
        Assert.Empty(grid.GetDistinctValues("Name", maxCount: 0));
        Assert.Empty(grid.GetDistinctValues("Missing"));
    }

    [Fact]
    public async Task QuickAndDirectFiltersReplaceExcelFilterAndNullOperatorsWork()
    {
        var rows = new List<Row>
        {
            new() { Id = 1, Name = "alpha" },
            new() { Id = 2, Name = "beta" },
            new() { Id = 3, Name = null! }
        };
        var grid = new DataGrid<Row>();
        grid.Columns.Add(Col("Id"));
        grid.Columns.Add(Col("Name"));
        grid.DataSource = rows;

        grid.ApplyExcelFilter("Name", new object[] { "alpha", "beta" });
        grid.SetQuickFilter("Name", "bet");
        await grid.RefreshDataAsync();
        Assert.Single(grid.CurrentRequest.Filters);
        Assert.Equal(2, Assert.Single(grid.RenderedItems).Id);

        grid.FilterBy("Name", FilterOperator.IsNull, null);
        await grid.RefreshDataAsync();
        Assert.Single(grid.CurrentRequest.Filters);
        Assert.Equal(3, Assert.Single(grid.RenderedItems).Id);

        grid.FilterBy("Id", FilterOperator.IsNull, null);
        await grid.RefreshDataAsync();
        Assert.Empty(grid.RenderedItems);

        grid.FilterBy("Id", FilterOperator.IsNotNull, null);
        await grid.RefreshDataAsync();
        Assert.Single(grid.RenderedItems);
        Assert.Equal(3, Assert.Single(grid.RenderedItems).Id);
    }

    [Fact]
    public async Task QueryChangesRestartAtTheTopAndNotifyListeners()
    {
        var rows = Enumerable.Range(0, 5_000).Select(i => new Row { Id = i, Name = "R" + (i % 7) }).ToList();
        var grid = new DataGrid<Row> { RowBuffer = 5 };
        grid.Columns.Add(Col("Id"));
        grid.Columns.Add(Col("Name"));
        grid.DataSource = rows;
        grid.EnsureRowsLoaded(3_000, 20);
        await grid.RefreshDataAsync();
        Assert.True(grid.WindowStart > 2_000);

        var notified = 0;
        grid.QueryChanged += (_, _) => notified++;
        grid.CycleSort("Id", false);
        await grid.RefreshDataAsync();

        Assert.Equal(1, notified);
        Assert.Equal(0, grid.WindowStart);
        Assert.True(grid.TryGetItemAt(0, out var first));
        Assert.Equal(0, first.Id);
    }

    [Fact]
    public async Task ReorderingColumnsOrToggingVisibilityDoesNotResetScroll()
    {
        var rows = Enumerable.Range(0, 5_000).Select(i => new Row { Id = i, Name = "x" }).ToList();
        var grid = new DataGrid<Row> { RowBuffer = 5 };
        grid.Columns.Add(Col("Id"));
        grid.Columns.Add(Col("Name"));
        grid.DataSource = rows;
        grid.EnsureRowsLoaded(3_000, 20);
        await grid.RefreshDataAsync();

        var notified = 0;
        grid.QueryChanged += (_, _) => notified++;
        grid.MoveColumnBefore("Name", "Id");
        grid.ResizeColumn("Id", 200);
        grid.PinColumn("Id", ColumnPinPosition.Left);

        Assert.Equal(0, notified);
        Assert.True(grid.WindowStart > 2_000);
    }

    [Fact]
    public void DataSourceRequestWindowOverridesPagingAndClones()
    {
        var request = new DataSourceRequest { Page = 3, PageSize = 10 };
        Assert.Equal(20, request.EffectiveSkip);
        Assert.Equal(10, request.EffectiveTake);

        request.Skip = 7;
        request.Take = 3;
        var clone = request.Clone();
        clone.Sorts.Add(new SortDescriptor("Id", System.ComponentModel.ListSortDirection.Ascending));

        Assert.Equal(7, clone.EffectiveSkip);
        Assert.Equal(3, clone.EffectiveTake);
        Assert.Empty(request.Sorts);
    }
}
