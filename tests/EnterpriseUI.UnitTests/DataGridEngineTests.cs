using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnterpriseUI.Data;
using EnterpriseUI.Grids;
using Xunit;

namespace EnterpriseUI.Tests.Unit;

internal static class GridTestSupport
{
    public sealed class Order
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string Customer { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string Status { get; set; } = "Open";
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal? Discount { get; set; }
        public DateTime Date { get; set; } = new DateTime(2024, 1, 1);
    }

    public sealed class Line
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public int Qty { get; set; }
    }

    public sealed class Dept
    {
        public int Id { get; set; }
        public int? ParentId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Budget { get; set; }
    }

    public static List<Order> Orders(int count = 6)
    {
        var regions = new[] { "EMEA", "APAC", "EMEA", "AMER", "APAC", "EMEA" };
        var list = new List<Order>();
        for (var i = 1; i <= count; i++)
        {
            list.Add(new Order
            {
                Id = i,
                CustomerId = (i - 1) % 3 + 1,
                Customer = $"Customer {(i - 1) % 3 + 1}",
                Region = regions[(i - 1) % regions.Length],
                UnitPrice = 10 * i,
                Quantity = i,
                Date = new DateTime(2024, 1, 1).AddDays(i - 1)
            });
        }
        return list;
    }

    public static DataGrid<Order> OrderGrid(IEnumerable<Order>? source = null, bool columns = true)
    {
        var grid = new DataGrid<Order>();
        if (columns)
        {
            grid.Columns.Add(new GridColumn { Field = "Id", Title = "Id" });
            grid.Columns.Add(new GridColumn { Field = "Customer", Title = "Customer" });
            grid.Columns.Add(new GridColumn { Field = "Region", Title = "Region" });
            grid.Columns.Add(new GridColumn { Field = "Status", Title = "Status" });
            grid.Columns.Add(new GridColumn { Field = "UnitPrice", Title = "Unit Price", FormatString = "N2" });
            grid.Columns.Add(new GridColumn { Field = "Quantity", Title = "Quantity" });
            grid.Columns.Add(new GridColumn { Field = "Discount", Title = "Discount" });
        }

        if (source != null) grid.DataSource = source;
        return grid;
    }

    public static async Task<DataGrid<T>> Loaded<T>(this DataGrid<T> grid)
    {
        await grid.RefreshDataAsync();
        return grid;
    }

    public static List<GridRow<T>> Rows<T>(this DataGrid<T> grid)
    {
        var rows = new List<GridRow<T>>();
        for (var i = 0; i < grid.TotalItemsCount; i++)
        {
            Assert.True(grid.TryGetRowAt(i, out var row), $"row {i} should be loaded");
            rows.Add(row);
        }
        return rows;
    }

    public static List<int> Ids(this DataGrid<Order> grid) => grid.Rows().Where(r => r.Kind == GridRowKind.Data).Select(r => r.Item!.Id).ToList();

    public static async Task WaitUntil(Func<bool> condition, int timeoutMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Condition was not met in time.");
            await Task.Delay(5);
        }
    }

    public sealed class FailingSource<T> : IDataSource<T>
    {
        public Task<DataSourceResult<T>> ReadAsync(DataSourceRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("backend unavailable");
    }
}

public class DataGridBugRegressionTests
{
    [Fact]
    public async Task ClickingSortedColumnAgainSortsDescendingInTheData()
    {
        var grid = GridTestSupport.OrderGrid(GridTestSupport.Orders());
        await grid.Loaded();

        grid.CycleSort("Quantity");
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, grid.Ids());

        // Previously the second click cleared the descriptor and the data stayed ascending.
        grid.CycleSort("Quantity");
        Assert.Equal(ListSortDirection.Descending, grid.CurrentRequest.Sorts.Single().SortDirection);
        Assert.Equal(new[] { 6, 5, 4, 3, 2, 1 }, grid.Ids());

        grid.CycleSort("Quantity");
        Assert.Empty(grid.CurrentRequest.Sorts);
        Assert.Null(grid.Columns.First(c => c.Field == "Quantity").SortDirection);
    }

    [Fact]
    public async Task HeaderSortHonorsEnableSortingAndSortableFlags()
    {
        var grid = GridTestSupport.OrderGrid(GridTestSupport.Orders());
        await grid.Loaded();

        grid.EnableSorting = false;
        grid.CycleSort("Quantity");
        Assert.Empty(grid.CurrentRequest.Sorts);

        grid.EnableSorting = true;
        grid.Columns.First(c => c.Field == "Quantity").Sortable = false;
        grid.CycleSort("Quantity");
        Assert.Empty(grid.CurrentRequest.Sorts);
    }

    [Fact]
    public async Task CustomColumnComparerIsUsedForSorting()
    {
        var orders = GridTestSupport.Orders();
        var grid = GridTestSupport.OrderGrid(orders);
        grid.Columns.First(c => c.Field == "Status").CustomComparer = Comparer<object>.Create((a, b) => string.Compare((string)b, (string)a, StringComparison.Ordinal));
        orders[0].Status = "A";
        orders[1].Status = "C";
        orders[2].Status = "B";
        await grid.Loaded();

        grid.SortBy("Status", ListSortDirection.Ascending);

        Assert.Equal(new[] { 4, 5, 6, 2, 3, 1 }, grid.Ids());
    }

    [Fact]
    public async Task LayoutProfileRoundTripRestoresSortGroupFilterAndPageSize()
    {
        var grid = GridTestSupport.OrderGrid(GridTestSupport.Orders());
        grid.PagingMode = GridPagingMode.Client;
        grid.PageSize = 4;
        await grid.Loaded();
        grid.SortBy("Quantity", ListSortDirection.Descending);
        grid.GroupBy("Region");
        grid.FilterBy("Status", FilterOperator.IsEqualTo, "Open");
        var profile = grid.CreateLayoutProfile("saved");

        grid.ClearSort();
        grid.ClearGroups();
        grid.ClearFilters();
        grid.PageSize = 10;
        Assert.Empty(grid.CurrentRequest.Sorts);

        var warnings = grid.ApplyLayoutProfile(profile);

        Assert.Empty(warnings);
        Assert.Equal("Quantity", grid.CurrentRequest.Sorts.Single().Member);
        Assert.Equal(ListSortDirection.Descending, grid.CurrentRequest.Sorts.Single().SortDirection);
        Assert.Equal("Region", grid.CurrentRequest.Groups.Single().Member);
        Assert.Single(grid.CurrentRequest.Filters);
        Assert.Equal(4, grid.PageSize);
        Assert.Equal(ListSortDirection.Descending, grid.Columns.First(c => c.Field == "Quantity").SortDirection);
    }

    [Fact]
    public async Task SearchPanelAcrossNumericAndDateColumnsDoesNotThrow()
    {
        var grid = GridTestSupport.OrderGrid(GridTestSupport.Orders());
        grid.Columns.Add(new GridColumn { Field = "Date", Title = "Date" });
        await grid.Loaded();

        grid.SearchPanelText = "customer 2";
        Assert.Equal(new[] { 2, 5 }, grid.Ids());

        grid.SearchPanelText = "30";
        Assert.Equal(new[] { 3 }, grid.Ids());

        grid.SearchPanelText = "01/04/2024";
        Assert.Equal(new[] { 4 }, grid.Ids());
        grid.SearchPanelText = null;
        Assert.Equal(6, grid.TotalItemsCount);
    }

    [Fact]
    public async Task ValueListFilterUsesOneInDescriptorAndIgnoresCase()
    {
        var grid = GridTestSupport.OrderGrid(GridTestSupport.Orders());
        await grid.Loaded();

        grid.ApplyExcelFilter("Region", new object[] { "emea", "AMER" });

        var filter = Assert.IsType<FilterDescriptor>(grid.CurrentRequest.Filters.Single());
        Assert.Equal(FilterOperator.IsContainedIn, filter.Operator);
        Assert.Equal(new[] { 1, 3, 4, 6 }, grid.Ids());
        Assert.True(grid.HasFilter("Region"));

        grid.ClearExcelFilter("Region");
        Assert.False(grid.HasFilter("Region"));
        Assert.Equal(6, grid.TotalItemsCount);
    }

    [Fact]
    public async Task QuickFilterWithEmptyTextRemovesTheFilter()
    {
        var grid = GridTestSupport.OrderGrid(GridTestSupport.Orders());
        await grid.Loaded();

        grid.SetQuickFilter("Region", "apac");
        Assert.Equal(new[] { 2, 5 }, grid.Ids());
        Assert.True(grid.HasFilter("Region"));

        grid.SetQuickFilter("Region", "  ");
        Assert.Equal(6, grid.TotalItemsCount);
        Assert.False(grid.QuickFilters.ContainsKey("Region"));
    }

    [Fact]
    public async Task FilterPresetsAreCopiesAndKeepTrackingConsistent()
    {
        var grid = GridTestSupport.OrderGrid(GridTestSupport.Orders());
        await grid.Loaded();
        grid.SetQuickFilter("Region", "emea");
        grid.SaveFilterPreset("emea");

        grid.ClearFilters();
        Assert.Equal(6, grid.TotalItemsCount);

        Assert.True(grid.ApplyFilterPreset("emea"));
        Assert.Equal(new[] { 1, 3, 6 }, grid.Ids());
        Assert.True(grid.HasFilter("Region"));

        grid.SearchPanelText = "customer 1";
        grid.SaveFilterPreset("again");
        Assert.DoesNotContain(grid.SavedFilterPresets["again"], f => f is CompositeFilterDescriptor);
        Assert.False(grid.ApplyFilterPreset("missing"));

        var json = grid.ExportFilterPresets();
        var other = GridTestSupport.OrderGrid(GridTestSupport.Orders());
        Assert.Empty(other.ImportFilterPresets(json));
        Assert.Equal(2, other.SavedFilterPresets.Count);
    }

    [Fact]
    public async Task ColumnReorderRespectsReorderableAndColumnVisibilityUpdatesSearch()
    {
        var grid = GridTestSupport.OrderGrid(GridTestSupport.Orders());
        await grid.Loaded();

        grid.Columns.First(c => c.Field == "Id").Reorderable = false;
        grid.ReorderColumn("Id", 3);
        Assert.Equal("Id", grid.Columns[0].Field);

        grid.SearchPanelText = "EMEA";
        Assert.Equal(3, grid.TotalItemsCount);
        grid.SetColumnVisibility("Region", false);
        Assert.Equal(0, grid.TotalItemsCount);
    }

    [Fact]
    public async Task LoadFailuresAreReportedByEventInsteadOfThrowingOnBackgroundTasks()
    {
        var grid = GridTestSupport.OrderGrid();
        Exception? reported = null;
        grid.DataLoadFailed += (_, e) => reported = e.Exception;

        grid.CustomDataSource = new GridTestSupport.FailingSource<GridTestSupport.Order>();

        await GridTestSupport.WaitUntil(() => reported != null);
        Assert.IsType<InvalidOperationException>(reported);
        Assert.Same(reported, grid.LastError);
        await Assert.ThrowsAsync<InvalidOperationException>(() => grid.RefreshDataAsync());
    }

    [Fact]
    public async Task RowsRequestedWhileAFetchWasRunningAreLoadedWhenItCompletes()
    {
        var orders = GridTestSupport.Orders(1000);
        var gate = new TaskCompletionSource();
        var calls = 0;
        var grid = GridTestSupport.OrderGrid();
        grid.RowBuffer = 10;
        grid.CustomDataSource = new RemoteDataSource<GridTestSupport.Order>(async (request, ct) =>
        {
            if (Interlocked.Increment(ref calls) == 1) await gate.Task;
            return new DataSourceResult<GridTestSupport.Order> { Data = orders.Skip(request.EffectiveSkip).Take(request.EffectiveTake).ToList(), Total = orders.Count };
        });

        // First fetch (rows 0..) is parked; the viewport scrolls to row 500 meanwhile.
        Assert.False(grid.EnsureRowsLoaded(500, 20));
        gate.SetResult();

        await GridTestSupport.WaitUntil(() => grid.EnsureRowsLoaded(500, 20));
        Assert.True(grid.TryGetItemAt(505, out var item));
        Assert.Equal(506, item.Id);
    }

    [Fact]
    public async Task EditingASortedColumnReordersAndInvalidatesTheCache()
    {
        var orders = GridTestSupport.Orders(25_000);
        var grid = GridTestSupport.OrderGrid(orders);
        await grid.Loaded();
        grid.SortBy("Quantity", ListSortDirection.Ascending);
        await grid.RefreshDataAsync();
        Assert.True(grid.TryGetItemAt(0, out var first));
        Assert.Equal(1, first.Id);

        Assert.True(grid.EditCell(orders[24_999], "Quantity", "-5"));
        await GridTestSupport.WaitUntil(() => grid.TryGetItemAt(0, out var head) && head.Id == 25_000);
    }

    [Fact]
    public async Task PendingRowsAreReleasedWhenTheDataSourceIsReplaced()
    {
        var grid = GridTestSupport.OrderGrid(GridTestSupport.Orders());
        await grid.Loaded();
        grid.GroupBy("Region");
        Assert.True(grid.TotalItemsCount > 6);

        grid.DataSource = new List<GridTestSupport.Order>();
        await grid.RefreshDataAsync();
        Assert.Equal(0, grid.TotalItemsCount);
    }
}

public class DataGridPagingTests
{
    [Theory]
    [InlineData(GridPagingMode.Client)]
    [InlineData(GridPagingMode.Server)]
    public async Task PagedModesExposeOnlyThePageAndNavigate(GridPagingMode mode)
    {
        var grid = GridTestSupport.OrderGrid(GridTestSupport.Orders(25));
        grid.PagingMode = mode;
        grid.PageSize = 10;
        await grid.Loaded();

        Assert.Equal(3, grid.PageCount);
        Assert.Equal(10, grid.TotalItemsCount);
        Assert.Equal(25, grid.TotalRecordCount);
        Assert.Equal(Enumerable.Range(1, 10), grid.Ids());
        Assert.False(grid.HasPreviousPage);
        Assert.Contains("Page 1 of 3", grid.PagerText);

        Assert.True(grid.NextPage());
        Assert.Equal(Enumerable.Range(11, 10), grid.Ids());
        Assert.True(grid.LastPage());
        Assert.Equal(5, grid.TotalItemsCount);
        Assert.Equal(new[] { 21, 22, 23, 24, 25 }, grid.Ids());
        Assert.False(grid.HasNextPage);
        Assert.False(grid.NextPage());

        Assert.True(grid.FirstPage());
        Assert.False(grid.GoToPage(-4));
        Assert.True(grid.GoToPage(99));
        Assert.Equal(2, grid.PageIndex);
    }

    [Fact]
    public async Task ServerPagingRequestsOnlyThePageFromTheSource()
    {
        var all = GridTestSupport.Orders(95);
        var requests = new List<(int Skip, int Take)>();
        var grid = GridTestSupport.OrderGrid();
        grid.PagingMode = GridPagingMode.Server;
        grid.PageSize = 20;
        grid.RowBuffer = 0;
        grid.CustomDataSource = new RemoteDataSource<GridTestSupport.Order>((request, _) =>
        {
            requests.Add((request.EffectiveSkip, request.EffectiveTake));
            return Task.FromResult(new DataSourceResult<GridTestSupport.Order> { Data = all.Skip(request.EffectiveSkip).Take(request.EffectiveTake).ToList(), Total = all.Count });
        });
        await grid.Loaded();

        grid.GoToPage(4);
        await GridTestSupport.WaitUntil(() => grid.TotalItemsCount == 15 && grid.TryGetItemAt(0, out _));

        Assert.All(requests, r => Assert.InRange(r.Take, 1, 20));
        Assert.Contains(requests, r => r.Skip == 80);
        Assert.Equal(5, grid.PageCount);
        Assert.Equal(new[] { 81, 82 }, grid.Ids().Take(2));
    }

    [Fact]
    public async Task ChangingFilterSortOrPageSizeKeepsPagingConsistent()
    {
        var grid = GridTestSupport.OrderGrid(GridTestSupport.Orders(25));
        grid.PagingMode = GridPagingMode.Client;
        grid.PageSize = 10;
        await grid.Loaded();
        grid.LastPage();

        grid.FilterBy("Region", FilterOperator.IsEqualTo, "EMEA");
        Assert.Equal(0, grid.PageIndex);
        Assert.Equal(2, grid.PageCount);

        grid.ClearFilters();
        grid.GoToPage(2);
        grid.PageSize = 5;
        Assert.Equal(5, grid.PageCount);
        Assert.Equal(4, grid.PageIndex);

        grid.SortBy("Quantity", ListSortDirection.Descending);
        Assert.Equal(0, grid.PageIndex);
        Assert.Equal(new[] { 25, 24, 23, 22, 21 }, grid.Ids());
    }

    [Fact]
    public async Task InfinitePagingAppendsPagesUntilExhausted()
    {
        var grid = GridTestSupport.OrderGrid(GridTestSupport.Orders(25));
        grid.PagingMode = GridPagingMode.Infinite;
        grid.PageSize = 10;
        await grid.Loaded();

        Assert.Equal(10, grid.TotalItemsCount);
        Assert.True(grid.HasMoreRows);
        Assert.True(await grid.LoadMoreAsync());
        Assert.Equal(20, grid.TotalItemsCount);
        Assert.True(await grid.LoadMoreAsync());
        Assert.Equal(25, grid.TotalItemsCount);
        Assert.False(grid.HasMoreRows);
        Assert.False(await grid.LoadMoreAsync());
        Assert.Equal("25 of 25 loaded", grid.PagerText);
    }

    [Fact]
    public async Task VirtualPagingFetchesPageAlignedWindows()
    {
        var all = GridTestSupport.Orders(200);
        var skips = new List<int>();
        var grid = GridTestSupport.OrderGrid();
        grid.PagingMode = GridPagingMode.Virtual;
        grid.PageSize = 25;
        grid.RowBuffer = 5;
        grid.CustomDataSource = new RemoteDataSource<GridTestSupport.Order>((request, _) =>
        {
            skips.Add(request.EffectiveSkip);
            return Task.FromResult(new DataSourceResult<GridTestSupport.Order> { Data = all.Skip(request.EffectiveSkip).Take(request.EffectiveTake).ToList(), Total = all.Count });
        });
        await grid.Loaded();

        grid.EnsureRowsLoaded(120, 10);
        await GridTestSupport.WaitUntil(() => grid.EnsureRowsLoaded(120, 10));

        Assert.All(skips, s => Assert.Equal(0, s % 25));
        Assert.Equal(200, grid.TotalItemsCount);
        Assert.True(grid.TryGetItemAt(125, out var item));
        Assert.Equal(126, item.Id);
    }

    [Fact]
    public void PageButtonsCollapseWithEllipsis()
    {
        Assert.Equal(new[] { 0, 1, 2 }, GridPagingHelper.GetPageButtons(0, 3));
        var buttons = GridPagingHelper.GetPageButtons(10, 40);
        Assert.Equal(0, buttons[0]);
        Assert.Equal(39, buttons[^1]);
        Assert.Contains(-1, buttons);
        Assert.Contains(10, buttons);
        Assert.Equal(7, buttons.Count);
    }

    [Fact]
    public async Task GoToPageBeforeFirstLoadIsRememberedAndClampedAfterwards()
    {
        var grid = GridTestSupport.OrderGrid(GridTestSupport.Orders(25));
        grid.PagingMode = GridPagingMode.Client;
        grid.PageSize = 10;
        grid.GoToPage(1);
        await grid.Loaded();

        Assert.Equal(1, grid.PageIndex);
        Assert.Equal(11, grid.Ids().First());
    }
}

public class DataGridGroupingTests
{
    [Fact]
    public async Task GroupingProducesHeadersAndNestedLevels()
    {
        var grid = GridTestSupport.OrderGrid(GridTestSupport.Orders());
        await grid.Loaded();

        grid.GroupBy("Region");
        var rows = grid.Rows();

        Assert.Equal(3 + 6, rows.Count);
        Assert.Equal(GridRowKind.GroupHeader, rows[0].Kind);
        Assert.Equal("AMER", rows[0].Group!.Key);
        Assert.Equal(1, rows[0].Group!.ItemCount);
        Assert.Equal(4, rows[1].Item!.Id);
        Assert.Equal("APAC", rows[2].Group!.Key);
        Assert.Equal(new[] { 2, 5 }, rows.Skip(3).Take(2).Select(r => r.Item!.Id));
        Assert.All(rows.Skip(3).Take(2), r => Assert.Equal(1, r.Level));
        Assert.Equal(6, grid.TotalRecordCount);

        grid.GroupBy("Status");
        rows = grid.Rows();
        Assert.Equal(GridRowKind.GroupHeader, rows[1].Kind);
        Assert.Equal(1, rows[1].Level);
        Assert.Equal(2, rows[2].Level);
    }

    [Fact]
    public async Task GroupExpansionCollapseAndFootersWork()
    {
        var grid = GridTestSupport.OrderGrid(GridTestSupport.Orders());
        grid.ShowGroupFooters = true;
        await grid.Loaded();
        grid.GroupBy("Region");

        Assert.Equal(3 + 6 + 3, grid.TotalItemsCount);
        Assert.Equal(GridRowKind.GroupFooter, grid.Rows()[2].Kind);

        grid.CollapseAllGroups();
        Assert.Equal(3, grid.TotalItemsCount);
        Assert.All(grid.Rows(), r => Assert.False(r.IsExpanded));

        grid.ToggleGroupExpansion("EMEA");
        Assert.Equal(3 + 3 + 1, grid.TotalItemsCount);

        grid.ToggleRowExpansion(0);
        Assert.Equal(3 + 3 + 1 + 2, grid.TotalItemsCount);

        grid.ExpandAllGroups();
        Assert.Equal(12, grid.TotalItemsCount);
        Assert.True(grid.IsGroupExpanded("/Region=APAC"));
    }

    [Fact]
    public async Task GroupSummariesAreComputedPerGroupAndFormatted()
    {
        var grid = GridTestSupport.OrderGrid(GridTestSupport.Orders());
        grid.GroupSummaries.Add(new GroupSummaryDescriptor("Quantity", AggregateFunction.Sum));
        grid.GroupSummaries.Add(new GroupSummaryDescriptor("UnitPrice", AggregateFunction.Max, "max {0:N0}"));
        await grid.Loaded();

        grid.GroupBy("Region");
        var apac = grid.Rows().First(r => r.Kind == GridRowKind.GroupHeader && (string)r.Group!.Key! == "APAC");

        Assert.Equal(7L, apac.Group!.FindAggregate("Sum(Quantity)")!.Value);
        var header = grid.GetGroupHeaderText(apac);
        Assert.StartsWith("Region: APAC (2)", header);
        Assert.Contains("Quantity Sum: 7", header);
        Assert.Contains("max 50", header);
    }

    [Fact]
    public async Task SortingByGroupedColumnOrdersGroupsAndCustomKeysBucketValues()
    {
        var grid = GridTestSupport.OrderGrid(GridTestSupport.Orders());
        await grid.Loaded();
        grid.GroupBy("Region");

        grid.SortBy("Region", ListSortDirection.Descending);
        Assert.Equal("EMEA", grid.Rows()[0].Group!.Key);

        grid.ClearGroups();
        grid.GroupBy("Date", v => ((DateTime)v!).Day <= 3 ? "Early" : "Late");
        var headers = grid.Rows().Where(r => r.Kind == GridRowKind.GroupHeader).Select(r => (string)r.Group!.Key!).ToList();
        Assert.Equal(new[] { "Early", "Late" }, headers);
    }

    [Fact]
    public async Task ReorderingGroupLevelsRebuildsTheTree()
    {
        var grid = GridTestSupport.OrderGrid(GridTestSupport.Orders());
        await grid.Loaded();
        grid.GroupBy("Region");
        grid.GroupBy("Customer");

        Assert.Equal("Region", grid.Rows()[0].Group!.Member);
        Assert.True(grid.MoveGroup("Customer", 0));
        Assert.Equal("Customer", grid.Rows()[0].Group!.Member);
        Assert.False(grid.MoveGroup("Nope", 0));

        grid.RemoveGroup("Customer");
        grid.RemoveGroup("Region");
        Assert.Equal(6, grid.TotalItemsCount);
    }

    [Fact]
    public async Task GroupedRowsPageOverDisplayRows()
    {
        var grid = GridTestSupport.OrderGrid(GridTestSupport.Orders());
        grid.PagingMode = GridPagingMode.Client;
        grid.PageSize = 4;
        await grid.Loaded();
        grid.GroupBy("Region");

        Assert.Equal(3, grid.PageCount);
        Assert.Equal(4, grid.TotalItemsCount);
        grid.NextPage();
        Assert.Equal(4, grid.TotalItemsCount);
        grid.LastPage();
        Assert.Equal(1, grid.TotalItemsCount);
    }

    [Fact]
    public async Task GroupingTooManyRowsFailsWithAClearMessage()
    {
        var grid = GridTestSupport.OrderGrid(GridTestSupport.Orders(50));
        grid.MaxMaterializedRows = 10;
        Exception? failure = null;
        grid.DataLoadFailed += (_, e) => failure = e.Exception;
        await grid.Loaded();

        grid.GroupBy("Region");

        await GridTestSupport.WaitUntil(() => failure != null);
        Assert.Contains("MaxMaterializedRows", failure!.Message);
    }
}

public class DataGridHierarchyTests
{
    private static (DataGrid<GridTestSupport.Order> Grid, GridHierarchyRelation Relation) Build(out List<GridTestSupport.Order> orders)
    {
        var customers = GridTestSupport.Orders(3);
        var orderRows = GridTestSupport.Orders(6);
        var lines = orderRows.SelectMany(o => Enumerable.Range(1, o.Id % 2 + 1).Select(n => new GridTestSupport.Line { Id = o.Id * 10 + n, OrderId = o.Id, Sku = $"SKU-{o.Id}-{n}", Qty = n })).ToList();
        orders = orderRows;

        var grid = GridTestSupport.OrderGrid(customers);
        grid.EnableMasterDetail = true;
        var relation = grid.AddRelation("Orders", "Id", "CustomerId", orderRows, typeof(GridTestSupport.Order));
        relation.ChildRelations.Add(new GridHierarchyRelation { RelationName = "Lines", KeyField = "Id", ForeignField = "OrderId", ChildSource = lines, ChildItemType = typeof(GridTestSupport.Line) });
        return (grid, relation);
    }

    [Fact]
    public async Task MasterDetailExpandsChildRowsAtUnlimitedDepth()
    {
        var (grid, relation) = Build(out _);
        await grid.Loaded();
        Assert.Equal(3, grid.TotalItemsCount);
        Assert.True(grid.Rows()[0].IsExpandable);

        grid.ToggleDetailExpansion(grid.Rows()[0].Item!);
        var rows = grid.Rows();
        Assert.Equal(GridRowKind.Data, rows[0].Kind);
        Assert.True(rows[0].IsExpanded);
        Assert.Equal(GridRowKind.DetailHeader, rows[1].Kind);
        Assert.Same(relation, rows[1].Relation);
        Assert.Equal(new[] { 1 }, rows.Where(r => r.Kind == GridRowKind.DetailData).Select(r => ((GridTestSupport.Order)r.RowObject!).CustomerId).Distinct());
        var detailCount = rows.Count(r => r.Kind == GridRowKind.DetailData);
        Assert.Equal(2, detailCount);
        Assert.All(rows.Where(r => r.Kind == GridRowKind.DetailData), r => Assert.True(r.IsExpandable));

        // Expand the first order's lines: a third level.
        grid.ToggleRowExpansion(2);
        rows = grid.Rows();
        var lineHeader = rows.First(r => r.Kind == GridRowKind.DetailHeader && r.Relation!.RelationName == "Lines");
        Assert.Equal(2, lineHeader.Level);
        Assert.Contains(rows, r => r.Kind == GridRowKind.DetailData && r.RowObject is GridTestSupport.Line);

        grid.CollapseAllDetails();
        Assert.Equal(3, grid.TotalItemsCount);
    }

    [Fact]
    public async Task RelationsCanBeAddedAndRemovedAtRuntime()
    {
        var customers = GridTestSupport.Orders(3);
        var grid = GridTestSupport.OrderGrid(customers);
        grid.EnableMasterDetail = true;
        await grid.Loaded();

        // Without relations the detail is a property summary row.
        grid.ToggleDetailExpansion(customers[0]);
        Assert.Equal(GridRowKind.DetailSummary, grid.Rows()[1].Kind);
        grid.CollapseAllDetails();

        grid.AddRelation("Orders", "Id", "CustomerId", GridTestSupport.Orders(6));
        Assert.Single(grid.Relations);
        grid.ToggleDetailExpansion(customers[1]);
        Assert.Equal(GridRowKind.DetailHeader, grid.Rows()[2].Kind);

        Assert.True(grid.RemoveRelation("orders"));
        Assert.Empty(grid.Relations);
        Assert.Equal(3, grid.TotalItemsCount);
        Assert.False(grid.RemoveRelation("orders"));
    }

    [Fact]
    public async Task SelectorRelationsAndNumericKeyTypesJoinCorrectly()
    {
        var grid = GridTestSupport.OrderGrid(GridTestSupport.Orders(2));
        grid.EnableMasterDetail = true;
        var children = new[] { new { OwnerId = 1L, Name = "a" }, new { OwnerId = 2L, Name = "b" }, new { OwnerId = 1L, Name = "c" } };
        grid.AddRelation("Joined", "Id", "OwnerId", children);
        await grid.Loaded();

        grid.ToggleDetailExpansion(grid.Rows()[0].Item!);

        Assert.Equal(2, grid.Rows().Count(r => r.Kind == GridRowKind.DetailData));

        var selector = new GridHierarchyRelation { RelationName = "Self", DetailItemsSelector = parent => new[] { parent } };
        grid.Relations.Clear();
        grid.Relations.Add(selector);
        grid.NotifyRelationsChanged();
        grid.ToggleDetailExpansion(grid.Rows()[0].Item!);
        Assert.Equal(1, grid.Rows().Count(r => r.Kind == GridRowKind.DetailData));
    }

    [Fact]
    public async Task LazyRelationsLoadChildrenOnFirstExpansion()
    {
        var grid = GridTestSupport.OrderGrid(GridTestSupport.Orders(2));
        grid.EnableMasterDetail = true;
        var loads = 0;
        grid.Relations.Add(new GridHierarchyRelation
        {
            RelationName = "Lazy",
            LoadChildrenAsync = async (parent, ct) =>
            {
                Interlocked.Increment(ref loads);
                await Task.Delay(20, ct);
                return new[] { new GridTestSupport.Line { Id = 1, Sku = "X" }, new GridTestSupport.Line { Id = 2, Sku = "Y" } };
            }
        });
        await grid.Loaded();

        grid.ToggleDetailExpansion(grid.Rows()[0].Item!);
        Assert.True(grid.Rows()[1].IsLoading);

        await GridTestSupport.WaitUntil(() => grid.TotalItemsCount == 2 + 1 + 2);
        Assert.False(grid.Rows()[1].IsLoading);
        Assert.Equal(1, loads);

        // Collapsing and re-expanding uses the cache.
        grid.ToggleDetailExpansion(grid.Rows()[0].Item!);
        grid.ToggleDetailExpansion(grid.Rows()[0].Item!);
        Assert.Equal(1, loads);
        Assert.Equal(5, grid.TotalItemsCount);
    }

    [Fact]
    public async Task LazyLoadFailureCollapsesAndReportsTheError()
    {
        var grid = GridTestSupport.OrderGrid(GridTestSupport.Orders(2));
        grid.EnableMasterDetail = true;
        grid.Relations.Add(new GridHierarchyRelation { RelationName = "Broken", LoadChildrenAsync = (_, _) => throw new InvalidOperationException("nope") });
        Exception? failure = null;
        grid.DataLoadFailed += (_, e) => failure = e.Exception;
        await grid.Loaded();

        grid.ToggleDetailExpansion(grid.Rows()[0].Item!);

        await GridTestSupport.WaitUntil(() => failure != null);
        await GridTestSupport.WaitUntil(() => grid.TotalItemsCount == 2);
    }

    [Fact]
    public async Task SelfReferencingRelationBuildsATreeWithOrphansAndCycles()
    {
        var depts = new List<GridTestSupport.Dept>
        {
            new() { Id = 1, Name = "HQ" },
            new() { Id = 2, ParentId = 1, Name = "Eng" },
            new() { Id = 3, ParentId = 1, Name = "Ops" },
            new() { Id = 4, ParentId = 2, Name = "Platform" },
            new() { Id = 5, ParentId = 99, Name = "Orphan" },
            new() { Id = 6, ParentId = 7, Name = "CycleA" },
            new() { Id = 7, ParentId = 6, Name = "CycleB" }
        };
        var grid = new DataGrid<GridTestSupport.Dept>();
        grid.Columns.Add(new GridColumn { Field = "Name" });
        grid.DataSource = depts;
        grid.AddSelfReference("Id", "ParentId");
        await grid.Loaded();

        Assert.True(grid.IsTreeMode);
        var rows = grid.Rows();
        Assert.Equal(new[] { 1, 5, 6 }, rows.Select(r => r.Item!.Id));
        Assert.True(rows[0].IsExpandable);
        Assert.False(rows[1].IsExpandable);

        grid.ExpandAllTreeNodes();
        rows = grid.Rows();
        Assert.Equal(new[] { 1, 2, 4, 3, 5, 6, 7 }, rows.Select(r => r.Item!.Id));
        Assert.Equal(new[] { 0, 1, 2, 1, 0, 0, 1 }, rows.Select(r => r.Level));

        grid.CollapseAllTreeNodes();
        grid.ToggleTreeNode(depts[0]);
        Assert.Equal(new[] { 1, 2, 3, 5, 6 }, grid.Rows().Select(r => r.Item!.Id));
    }

    [Fact]
    public async Task TreeSupportsSortingAndDeepChains()
    {
        var depts = Enumerable.Range(1, 5000).Select(i => new GridTestSupport.Dept { Id = i, ParentId = i == 1 ? null : i - 1, Name = $"N{i}" }).ToList();
        var grid = new DataGrid<GridTestSupport.Dept>();
        grid.Columns.Add(new GridColumn { Field = "Name" });
        grid.DataSource = depts;
        grid.AddSelfReference("Id", "ParentId");
        grid.TreeExpandedByDefault = true;
        await grid.Loaded();

        Assert.Equal(5000, grid.TotalItemsCount);
        Assert.True(grid.TryGetRowAt(4999, out var deepest));
        Assert.Equal(4999, deepest.Level);
    }

    [Fact]
    public async Task SearchRevealsMatchesInCollapsedGroupsTreeNodesAndChildTables()
    {
        var (grid, _) = Build(out _);
        await grid.Loaded();
        grid.SearchMode = GridSearchMode.Highlight;

        var result = await grid.SearchAsync("SKU-4-1");

        Assert.True(result.MatchCount >= 1);
        // The customer (id 1) owning order 4 and the order itself were expanded to reveal the line.
        Assert.Contains(grid.Rows(), r => r.Kind == GridRowKind.DetailData && r.RowObject is GridTestSupport.Line line && line.Sku == "SKU-4-1");
        Assert.All(result.Matches, m => Assert.InRange(m.RowIndex, 0, grid.TotalItemsCount - 1));

        var depts = new[]
        {
            new GridTestSupport.Dept { Id = 1, Name = "Root" },
            new GridTestSupport.Dept { Id = 2, ParentId = 1, Name = "Branch" },
            new GridTestSupport.Dept { Id = 3, ParentId = 2, Name = "NeedleLeaf" }
        };
        var tree = new DataGrid<GridTestSupport.Dept>();
        tree.Columns.Add(new GridColumn { Field = "Name" });
        tree.DataSource = depts;
        tree.AddSelfReference("Id", "ParentId");
        await tree.Loaded();
        Assert.Equal(1, tree.TotalItemsCount);

        await tree.SearchAsync("needle");
        Assert.Equal(3, tree.TotalItemsCount);
    }
}

public class DataGridSelectionTests
{
    [Fact]
    public void SingleRowSelectionReplacesAndIgnoresModifiers()
    {
        var selection = new GridSelectionModel { Mode = GridSelectionMode.SingleRow };
        selection.SetBounds(10, 4);

        Assert.True(selection.Click(2, 1));
        Assert.True(selection.Click(5, 1, ctrl: true));
        Assert.Equal(new[] { 5 }, selection.SelectedRows);
        Assert.False(selection.Click(5, 1));
        Assert.True(selection.ToggleRow(5));
        Assert.Empty(selection.SelectedRows);
    }

    [Fact]
    public void MultipleRowSelectionSupportsCtrlShiftAndKeyboardExtend()
    {
        var selection = new GridSelectionModel { Mode = GridSelectionMode.MultipleRows };
        selection.SetBounds(20, 3);

        selection.Click(2, 0);
        selection.Click(5, 0, shift: true);
        Assert.Equal(new[] { 2, 3, 4, 5 }, selection.SelectedRows.OrderBy(r => r));

        selection.Click(9, 0, ctrl: true);
        Assert.Contains(9, selection.SelectedRows);
        selection.Click(3, 0, ctrl: true);
        Assert.DoesNotContain(3, selection.SelectedRows);

        selection.Click(10, 0);
        selection.Move(1, 0, extend: true);
        selection.Move(1, 0, extend: true);
        Assert.Equal(new[] { 10, 11, 12 }, selection.SelectedRows.OrderBy(r => r));
        selection.Move(-1, 0, extend: true);
        Assert.Equal(new[] { 10, 11 }, selection.SelectedRows.OrderBy(r => r));

        Assert.True(selection.SelectAll());
        Assert.Equal(20, selection.SelectedRows.Count);
        selection.Clear();
        Assert.False(selection.HasSelection);
    }

    [Fact]
    public void CheckboxSelectionTogglesWithoutModifiers()
    {
        var selection = new GridSelectionModel { Mode = GridSelectionMode.CheckboxRow };
        selection.SetBounds(5, 2);

        selection.ToggleRow(1);
        selection.ToggleRow(3);
        Assert.Equal(new[] { 1, 3 }, selection.SelectedRows.OrderBy(r => r));
        selection.ToggleRow(1);
        Assert.Equal(new[] { 3 }, selection.SelectedRows);
    }

    [Fact]
    public void CellRangeSelectionSupportsDragAndMultipleRanges()
    {
        var selection = new GridSelectionModel { Mode = GridSelectionMode.CellRange };
        selection.SetBounds(100, 6);

        selection.Click(2, 1);
        selection.Drag(4, 3);
        Assert.True(selection.IsCellSelected(3, 2));
        Assert.False(selection.IsCellSelected(5, 2));
        Assert.Equal(9, selection.SelectedCellCount);

        selection.Click(10, 0, ctrl: true);
        Assert.True(selection.IsCellSelected(3, 2));
        Assert.True(selection.IsCellSelected(10, 0));
        Assert.Equal(2, selection.CellRanges.Count);

        selection.Click(1, 1);
        selection.Click(3, 2, shift: true);
        Assert.Single(selection.CellRanges);
        Assert.Equal(new GridCellRange(1, 3, 1, 2), selection.CellRanges[0]);
        Assert.Equal(6, selection.EnumerateSelectedCells().Count());
        Assert.Equal(2, selection.EnumerateSelectedCells(2).Count());
    }

    [Fact]
    public void ColumnSelectionSelectsWholeColumnsAndRanges()
    {
        var selection = new GridSelectionModel { Mode = GridSelectionMode.Column };
        selection.SetBounds(50, 5);

        selection.ClickColumn(1);
        selection.ClickColumn(3, shift: true);
        Assert.Equal(new[] { 1, 2, 3 }, selection.SelectedColumns.OrderBy(c => c));
        Assert.True(selection.IsCellSelected(40, 2));
        Assert.False(selection.IsCellSelected(40, 4));

        selection.ClickColumn(4, ctrl: true);
        Assert.Contains(4, selection.SelectedColumns);
        selection.ClickColumn(2);
        Assert.Equal(new[] { 2 }, selection.SelectedColumns);
        Assert.Equal(50, selection.SelectedCellCount);
    }

    [Fact]
    public void KeyboardNavigationMovesFocusWithinBoundsAndJumps()
    {
        var selection = new GridSelectionModel { Mode = GridSelectionMode.SingleCell };
        selection.SetBounds(10, 3);

        selection.Move(1, 0);
        Assert.Equal((0, 0), (selection.FocusedRow, selection.FocusedColumn));
        selection.Move(0, 5);
        Assert.Equal(2, selection.FocusedColumn);
        selection.MoveTo(9, 0);
        selection.Move(5, 0);
        Assert.Equal(9, selection.FocusedRow);

        selection.SetBounds(4, 3);
        Assert.Equal(3, selection.FocusedRow);

        var none = new GridSelectionModel { Mode = GridSelectionMode.None };
        none.SetBounds(5, 2);
        Assert.False(none.Click(1, 1));
        Assert.Equal(1, none.FocusedRow);
        Assert.False(none.HasSelection);
    }

    [Fact]
    public async Task GridSelectionTracksItemsAndClearsWhenTheQueryChanges()
    {
        var grid = GridTestSupport.OrderGrid(GridTestSupport.Orders());
        grid.SelectionMode = GridSelectionMode.MultipleRows;
        await grid.Loaded();
        var events = 0;
        grid.SelectionChanged += (_, _) => events++;

        grid.Selection.Click(1, 0);
        grid.Selection.Click(3, 0, ctrl: true);

        Assert.Equal(new[] { 2, 4 }, grid.SelectedItems.Select(o => o.Id));
        Assert.Equal(4, grid.SelectedItem!.Id);
        Assert.Equal(2, events);

        grid.SortBy("Quantity", ListSortDirection.Descending);
        Assert.Empty(grid.SelectedItems);

        grid.SelectedItem = grid.Rows()[0].Item;
        Assert.Equal(6, grid.SelectedItem!.Id);
        grid.SelectedItem = null;
        Assert.Null(grid.SelectedItem);
    }
}
