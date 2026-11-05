using System.ComponentModel;
using System.Diagnostics;
using EnterpriseUI.Data;
using EnterpriseUI.Grids;
using EnterpriseUI.Tests.Support;
using EnterpriseUI.WinForms;
using Xunit;
using Xunit.Abstractions;
using static EnterpriseUI.Tests.Support.WinFormsTestSupport;

namespace EnterpriseUI.Tests.Performance;

/// <summary>
/// Regression guards, not marketing numbers: each threshold is several times the time measured on a development machine so that a real
/// algorithmic regression (for example an accidental O(n^2) or full materialization) fails, while normal machine variance does not.
/// The measured values are written to the test output and summarized in docs/Grid/Reports/PerformanceReport.md.
/// </summary>
[Trait("Category", "Performance")]
[Collection("Performance")]
public class EnginePerformanceTests
{
    private const int Million = 1_000_000;
    private static readonly Lazy<List<Row>> Data = new(() => WinFormsTestSupport.Rows(Million));
    private readonly ITestOutputHelper _output;

    public EnginePerformanceTests(ITestOutputHelper output) => _output = output;

    private static DataGrid<Row> Grid(List<Row> rows)
    {
        var grid = new DataGrid<Row>();
        foreach (var field in new[] { "Id", "Name", "Region", "Amount", "Active", "Date", "Priority" }) grid.Columns.Add(new GridColumn { Field = field });
        grid.DataSource = rows;
        return grid;
    }

    private long Time(string name, Action action)
    {
        var sw = Stopwatch.StartNew();
        action();
        _output.WriteLine($"{name}: {sw.ElapsedMilliseconds:N0} ms");
        return sw.ElapsedMilliseconds;
    }

    [Fact]
    public async Task BindingOneMillionRowsOnlyFetchesAWindow()
    {
        var grid = Grid(Data.Value);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var ms = Time("bind 1M rows", () => grid.RefreshDataAsync().GetAwaiter().GetResult());
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        _output.WriteLine($"allocated on the calling thread: {allocated / 1_000_000:N1} MB, rendered window: {grid.RenderedItems.Count} rows");

        Assert.Equal(Million, grid.TotalRecordCount);
        Assert.True(grid.RenderedItems.Count < 2_000, "only a window of rows may be materialized");
        Assert.True(ms < 5_000, $"binding took {ms} ms");
        await Task.CompletedTask;
    }

    [Fact]
    public void ReadingAWindowDeepInTheListIsFast()
    {
        var grid = Grid(Data.Value);
        grid.RefreshDataAsync().GetAwaiter().GetResult();

        var ms = Time("fetch window at row 750,000", () =>
        {
            grid.EnsureRowsLoaded(750_000, 60);
            SpinWait.SpinUntil(() => grid.TryGetItemAt(750_010, out _), 5_000);
        });

        Assert.True(grid.TryGetItemAt(750_010, out var item));
        Assert.Equal(750_011, item.Id);
        Assert.True(ms < 1_000, $"window fetch took {ms} ms");
    }

    [Fact]
    public void SortingFilteringAndGroupingAMillionRowsStayWithinBudget()
    {
        var grid = Grid(Data.Value);
        grid.RefreshDataAsync().GetAwaiter().GetResult();

        var sort = Time("sort 1M by Amount desc", () => { grid.SortBy("Amount", ListSortDirection.Descending); SpinWait.SpinUntil(() => !grid.IsLoading, 30_000); });
        var filter = Time("filter 1M Region = EMEA", () => { grid.ClearSort(); grid.FilterBy("Region", FilterOperator.IsEqualTo, "EMEA"); SpinWait.SpinUntil(() => !grid.IsLoading, 30_000); });
        var count = grid.TotalRecordCount;
        grid.ClearFilters();
        var group = Time("group 1M by Region", () => { grid.GroupBy("Region"); SpinWait.SpinUntil(() => !grid.IsLoading, 30_000); });

        Assert.InRange(count, Million / 3 - 10, Million / 3 + 10);
        Assert.True(sort < 15_000, $"sort {sort} ms");
        Assert.True(filter < 8_000, $"filter {filter} ms");
        Assert.True(group < 15_000, $"group {group} ms");
    }

    [Fact]
    public void RepeatedQueriesDoNotDegradeOverTheSameSource()
    {
        var grid = Grid(WinFormsTestSupport.Rows(200_000));
        grid.RefreshDataAsync().GetAwaiter().GetResult();
        var first = Time("first sort", () => { grid.SortBy("Name", ListSortDirection.Ascending); SpinWait.SpinUntil(() => !grid.IsLoading, 30_000); });
        grid.SortBy("Id", ListSortDirection.Ascending);
        SpinWait.SpinUntil(() => !grid.IsLoading, 30_000);

        var again = Time("repeat sort", () => { grid.SortBy("Name", ListSortDirection.Ascending); SpinWait.SpinUntil(() => !grid.IsLoading, 30_000); });

        Assert.True(again <= first * 3 + 50, $"warm {again} ms vs cold {first} ms");
    }

    [Fact]
    public async Task ExportingHundredsOfThousandsOfRowsRunsAtStreamingSpeed()
    {
        var grid = Grid(WinFormsTestSupport.Rows(300_000));
        await grid.RefreshDataAsync();
        var sw = Stopwatch.StartNew();

        var result = await GridExporter.ExportAsync(grid, "csv", Stream.Null);

        _output.WriteLine($"export 300k rows to csv: {sw.ElapsedMilliseconds:N0} ms ({result.Rows / Math.Max(1, sw.Elapsed.TotalSeconds):N0} rows/s)");
        Assert.Equal(300_000, result.Rows);
        Assert.True(sw.ElapsedMilliseconds < 20_000);
    }
}

[Trait("Category", "Performance")]
[Collection("Performance")]
public class RenderPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public RenderPerformanceTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void PaintingAFrameOverAHundredThousandRowGridIsFast()
    {
        RunSta(() =>
        {
            using var grid = CreateGrid(100_000, g => { g.Width = 1400; g.Height = 800; });
            using var warm = grid.Render();

            var sw = Stopwatch.StartNew();
            const int frames = 30;
            for (var i = 0; i < frames; i++)
            {
                using var bitmap = grid.Render();
            }
            var perFrame = sw.Elapsed.TotalMilliseconds / frames;
            _output.WriteLine($"paint 1400x800 over 100,000 rows: {perFrame:N1} ms per frame");

            Assert.True(perFrame < 150, $"{perFrame:N1} ms per frame");
        });
    }

    [Fact]
    public void ScrollingAcrossTheWholeDataSetKeepsFramesFast()
    {
        RunSta(() =>
        {
            using var grid = CreateGrid(100_000, g => { g.Width = 1400; g.Height = 800; });
            var sw = Stopwatch.StartNew();
            const int steps = 40;
            for (var i = 0; i < steps; i++)
            {
                grid.Selection.SetFocus(i * 2_400, 1);
                Pump(1);
                using var bitmap = grid.Render();
            }
            var perStep = sw.Elapsed.TotalMilliseconds / steps;
            _output.WriteLine($"jump-scroll + paint over 100,000 rows: {perStep:N1} ms per step");

            Assert.True(perStep < 250, $"{perStep:N1} ms per step");
        });
    }

    [Fact]
    public void AGridHoldingAMillionRowsDoesNotRetainRowObjectsPerCell()
    {
        RunSta(() =>
        {
            var rows = WinFormsTestSupport.Rows(1_000_000);
            GC.Collect();
            var baseline = GC.GetTotalMemory(true);

            using var grid = CreateGrid(0, attachToForm: false);
            grid.DataSource = rows;
            Wait(grid.RefreshDataAsync());
            GC.Collect();
            var withGrid = GC.GetTotalMemory(true);
            _output.WriteLine($"memory held by the grid for 1,000,000 bound rows: {(withGrid - baseline) / 1_000_000d:N1} MB (the rows themselves are {baseline / 1_000_000d:N0} MB)");

            Assert.True(withGrid - baseline < 40_000_000, "binding must not copy the rows");
            GC.KeepAlive(rows);
        });
    }
}
