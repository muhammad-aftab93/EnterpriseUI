using System.ComponentModel;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using EnterpriseUI.Data;
using EnterpriseUI.Exporting;
using EnterpriseUI.Grids;
using EnterpriseUI.Printing;
using Xunit;

namespace EnterpriseUI.Tests.Integration;

public sealed class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public sealed class SalesOrder
{
    public int OrderId { get; set; }
    public int CustomerId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class OrderLine
{
    public int LineId { get; set; }
    public int OrderId { get; set; }
    public string Sku { get; set; } = string.Empty;
}

public sealed class Unit
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Budget { get; set; }
}

internal static class Fixtures
{
    public static List<Customer> Customers(int count) =>
        Enumerable.Range(1, count).Select(i => new Customer { Id = i, Name = $"Customer {i:000}", Country = i % 3 == 0 ? "DE" : i % 3 == 1 ? "US" : "JP" }).ToList();

    public static List<SalesOrder> Orders(int customers, int perCustomer) =>
        Enumerable.Range(1, customers).SelectMany(c => Enumerable.Range(1, perCustomer).Select(o => new SalesOrder
        {
            OrderId = c * 100 + o, CustomerId = c, Amount = 10m * o + c, Status = o % 2 == 0 ? "Open" : "Paid"
        })).ToList();

    public static List<OrderLine> Lines(IEnumerable<SalesOrder> orders) =>
        orders.SelectMany(o => Enumerable.Range(1, 2).Select(l => new OrderLine { LineId = o.OrderId * 10 + l, OrderId = o.OrderId, Sku = $"SKU-{o.OrderId}-{l}" })).ToList();

    public static DataGrid<Customer> CustomerGrid(IEnumerable<Customer> data)
    {
        var grid = new DataGrid<Customer>();
        grid.Columns.Add(new GridColumn { Field = "Id", Title = "Id" });
        grid.Columns.Add(new GridColumn { Field = "Name", Title = "Customer" });
        grid.Columns.Add(new GridColumn { Field = "Country", Title = "Country" });
        grid.DataSource = data;
        return grid;
    }

    public static string TempFile(string extension) => Path.Combine(Path.GetTempPath(), $"enterpriseui-{Guid.NewGuid():N}{extension}");
}

public class HierarchyIntegrationTests
{
    [Fact]
    public async Task ThreeLevelHierarchyReachesTheXlsxFileWithEveryLevel()
    {
        var customers = Fixtures.Customers(4);
        var orders = Fixtures.Orders(4, 2);
        var lines = Fixtures.Lines(orders);
        var grid = Fixtures.CustomerGrid(customers);
        grid.EnableMasterDetail = true;
        var relation = grid.AddRelation("Orders", "Id", "CustomerId", orders, typeof(SalesOrder));
        relation.ChildRelations.Add(new GridHierarchyRelation { RelationName = "Lines", KeyField = "OrderId", ForeignField = "OrderId", ChildItemType = typeof(OrderLine), ChildSource = lines });
        await grid.RefreshDataAsync();

        using var stream = new MemoryStream();
        var result = await GridExporter.ExportAsync(grid, "xlsx", stream);

        Assert.Equal("xlsx", result.FormatId);
        using var zip = new ZipArchive(new MemoryStream(stream.ToArray()), ZipArchiveMode.Read);
        var content = string.Concat(zip.Entries.Where(e => e.FullName.EndsWith(".xml", StringComparison.Ordinal)).Select(e =>
        {
            using var reader = new StreamReader(e.Open(), Encoding.UTF8);
            return reader.ReadToEnd();
        }));

        Assert.Contains("Customer 001", content);
        Assert.Contains("SKU-101-1", content);
        Assert.Contains("Amount", content);
    }

    [Fact]
    public async Task ExpandedDetailsAreSearchableAndRevealedThroughTheGrid()
    {
        var customers = Fixtures.Customers(6);
        var orders = Fixtures.Orders(6, 3);
        var grid = Fixtures.CustomerGrid(customers);
        grid.EnableMasterDetail = true;
        grid.SearchMode = GridSearchMode.Highlight;
        grid.AddRelation("Orders", "Id", "CustomerId", orders, typeof(SalesOrder));
        await grid.RefreshDataAsync();

        var result = await grid.SearchAsync("Paid");

        Assert.True(result.MatchCount > 0);
        Assert.True(grid.TotalItemsCount > customers.Count);
    }

    [Fact]
    public async Task TreeExportCarriesOutlineLevelsMatchingTheHierarchy()
    {
        var units = new List<Unit>
        {
            new() { Id = 1, Name = "HQ", Budget = 100 },
            new() { Id = 2, ParentId = 1, Name = "Eng", Budget = 40 },
            new() { Id = 3, ParentId = 2, Name = "Team", Budget = 10 },
            new() { Id = 4, ParentId = 1, Name = "Sales", Budget = 30 }
        };
        var grid = new DataGrid<Unit>();
        grid.Columns.Add(new GridColumn { Field = "Name", Title = "Unit" });
        grid.Columns.Add(new GridColumn { Field = "Budget", Title = "Budget" });
        grid.AddSelfReference("Id", "ParentId");
        grid.TreeExpandedByDefault = true;
        grid.DataSource = units;
        await grid.RefreshDataAsync();

        var document = await GridExporter.CreateDocumentAsync(grid);
        var rows = document.Sheets.Single().Rows.Where(r => r.Kind == ExportRowKind.Data).ToList();

        Assert.Equal(4, rows.Count);
        var team = rows.Single(r => Equals(r.Cells[0], "Team"));
        Assert.Equal(2, team.Level);
    }
}

public class PagingIntegrationTests
{
    [Fact]
    public async Task ServerPagingAsksTheSourceForOnePageAndTheTotal()
    {
        var store = Fixtures.Customers(1000);
        var requests = new List<(int Skip, int Take)>();
        var grid = new DataGrid<Customer>();
        grid.Columns.Add(new GridColumn { Field = "Id" });
        grid.Columns.Add(new GridColumn { Field = "Name" });
        grid.PagingMode = GridPagingMode.Server;
        grid.PageSize = 25;
        grid.CustomDataSource = new RemoteDataSource<Customer>((request, ct) =>
        {
            lock (requests) requests.Add((request.EffectiveSkip, request.EffectiveTake));
            return Task.FromResult(QueryEngine.Execute(store.AsQueryable(), request));
        });

        await grid.RefreshDataAsync();
        grid.GoToPage(3);
        await WaitUntil(() => grid.TryGetItemAt(0, out var first) && first.Id == 76);

        Assert.Equal(40, grid.PageCount);
        Assert.Equal(1000, grid.TotalRecordCount);
        Assert.Equal(25, grid.TotalItemsCount);
        lock (requests) Assert.All(requests, r => Assert.True(r.Take <= 25 + 1, $"requested {r.Take} rows"));
    }

    [Fact]
    public async Task ASlowSupersededRequestNeverOverwritesTheLatestQuery()
    {
        var store = Fixtures.Customers(200);
        var first = 1;
        var grid = new DataGrid<Customer>();
        grid.Columns.Add(new GridColumn { Field = "Id" });
        grid.Columns.Add(new GridColumn { Field = "Country" });
        grid.CustomDataSource = new RemoteDataSource<Customer>(async (request, ct) =>
        {
            if (Interlocked.Exchange(ref first, 0) == 1) await Task.Delay(150, ct);
            return QueryEngine.Execute(store.AsQueryable(), request);
        });

        var slow = grid.RefreshDataAsync();
        grid.FilterBy("Country", FilterOperator.IsEqualTo, "DE");
        await WaitUntil(() => !grid.IsLoading);
        try { await slow; } catch (OperationCanceledException) { }

        Assert.True(grid.TotalRecordCount > 0 && grid.TotalRecordCount < 200);
        Assert.True(grid.TryGetItemAt(0, out var item));
        Assert.Equal("DE", item.Country);
    }

    [Fact]
    public async Task AFailingSourceSurfacesTheErrorAndRecoversOnRetry()
    {
        var failing = true;
        var store = Fixtures.Customers(30);
        var grid = new DataGrid<Customer>();
        grid.Columns.Add(new GridColumn { Field = "Id" });
        grid.CustomDataSource = new RemoteDataSource<Customer>((request, ct) =>
            failing ? throw new IOException("offline") : Task.FromResult(QueryEngine.Execute(store.AsQueryable(), request)));
        Exception? reported = null;
        grid.DataLoadFailed += (_, e) => reported = e.Exception;

        // An explicit refresh reports the failure to the caller; window loads triggered by the view report it through the event.
        await Assert.ThrowsAsync<IOException>(() => grid.RefreshDataAsync());
        grid.EnsureRowsLoaded(0, 10);
        await WaitUntil(() => reported != null || grid.LastError != null);
        Assert.NotNull(grid.LastError);

        failing = false;
        await grid.RefreshDataAsync();
        await WaitUntil(() => grid.TotalRecordCount == 30);
        Assert.Null(grid.LastError);
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Condition was not met in time.");
            await Task.Delay(5);
        }
    }
}

public class ExportPrintIntegrationTests
{
    [Fact]
    public async Task FilteredSortedGridExportsToCsvInDisplayOrder()
    {
        var grid = Fixtures.CustomerGrid(Fixtures.Customers(60));
        grid.FilterBy("Country", FilterOperator.IsEqualTo, "DE");
        grid.SortBy("Name", ListSortDirection.Descending);
        await grid.RefreshDataAsync();

        using var stream = new MemoryStream();
        await GridExporter.ExportAsync(grid, "csv", stream);
        var lines = Encoding.UTF8.GetString(stream.ToArray()).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToList();

        Assert.Equal(21, lines.Count);
        Assert.Contains("Customer 060", lines[1]);
        Assert.Contains("Customer 003", lines[^1]);
    }

    [Fact]
    public async Task EveryProviderProducesAWellFormedFileForTheSameGrid()
    {
        var grid = Fixtures.CustomerGrid(Fixtures.Customers(40));
        grid.GroupBy("Country");
        grid.Columns[0].FooterSummaries.Add(new GridColumnSummary(AggregateFunction.Count));
        await grid.RefreshDataAsync();

        foreach (var format in new[] { "csv", "json", "xml", "html", "xlsx", "excelxml", "pdf" })
        {
            using var stream = new MemoryStream();
            var result = await GridExporter.ExportAsync(grid, format, stream);
            Assert.True(stream.Length > 100, $"{format} output is empty");
            Assert.True(result.Rows >= 40, $"{format} wrote {result.Rows} rows");
            var bytes = stream.ToArray();

            switch (format)
            {
                case "json": using (JsonDocument.Parse(bytes)) { } break;
                case "xml":
                case "excelxml": System.Xml.Linq.XDocument.Parse(Encoding.UTF8.GetString(bytes)); break;
                case "xlsx": using (new ZipArchive(new MemoryStream(bytes))) { } break;
                case "pdf":
                    Assert.StartsWith("%PDF-1.4", Encoding.ASCII.GetString(bytes, 0, 8));
                    Assert.Contains("%%EOF", Encoding.ASCII.GetString(bytes, bytes.Length - 16, 16));
                    break;
            }
        }
    }

    [Fact]
    public async Task CancelledExportLeavesNoFileBehind()
    {
        var grid = Fixtures.CustomerGrid(Fixtures.Customers(50_000));
        await grid.RefreshDataAsync();
        var path = Fixtures.TempFile(".csv");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => GridExporter.ExportToFileAsync(grid, path, cancellationToken: cts.Token));

        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task GroupedGridPrintsWithHeadersAndNeverSplitsARowAcrossPages()
    {
        var grid = Fixtures.CustomerGrid(Fixtures.Customers(300));
        grid.GroupBy("Country");
        await grid.RefreshDataAsync();
        var document = await GridExporter.CreateDocumentAsync(grid, new GridExportOptions { Title = "Customers" });

        var template = PrintTemplate.Standard();
        var plan = PrintPaginator.Paginate(document, template);

        Assert.True(plan.PageCount > 2);
        Assert.All(plan.Pages, page => Assert.True(page.ShowHeader));
        Assert.Contains(plan.Pages.SelectMany(p => p.Rows), r => r.Row.Kind == ExportRowKind.GroupHeader);
        var contentHeight = template.Settings.PrintableHeight - PrintPaginator.BandHeight(template, true) - PrintPaginator.BandHeight(template, false);
        Assert.All(plan.Pages, page => Assert.All(page.Rows, r => Assert.True(r.Y + r.Height <= contentHeight + 0.01)));
        Assert.Equal(plan.TotalRows, plan.Pages.SelectMany(p => p.Rows).Count(r => r.Row.Kind != ExportRowKind.Summary));
    }

    [Fact]
    public async Task LargeCsvExportStreamsWithBoundedMemory()
    {
        var grid = Fixtures.CustomerGrid(Fixtures.Customers(300_000));
        await grid.RefreshDataAsync();
        GC.Collect();
        var before = GC.GetTotalMemory(true);
        long peak = before;
        var progress = new Progress<ExportProgress>(_ => peak = Math.Max(peak, GC.GetTotalMemory(false)));

        var result = await GridExporter.ExportAsync(grid, "csv", Stream.Null, progress: progress);

        Assert.Equal(300_000, result.Rows);
        Assert.True(peak - before < 120_000_000, $"memory grew by {(peak - before) / 1_000_000} MB while streaming");
    }
}

public class LayoutIntegrationTests
{
    [Fact]
    public async Task SavedLayoutRestoresSortGroupFilterAndColumnsOnAFreshGrid()
    {
        var data = Fixtures.Customers(90);
        var grid = Fixtures.CustomerGrid(data);
        grid.GroupBy("Country");
        grid.SortBy("Name", ListSortDirection.Descending);
        grid.FilterBy("Name", FilterOperator.Contains, "0");
        grid.ResizeColumn("Name", 222);
        grid.SetColumnVisibility("Id", false);
        await grid.RefreshDataAsync();
        var store = new GridLayoutStore();
        store.Save(grid, "mine");
        var path = Fixtures.TempFile(".json");

        try
        {
            store.SaveToFile(path);
            var loaded = new GridLayoutStore();
            loaded.LoadFromFile(path);
            var fresh = Fixtures.CustomerGrid(data);
            var warnings = loaded.Apply(fresh, "mine");
            await fresh.RefreshDataAsync();

            Assert.Empty(warnings);
            Assert.Equal(222, fresh.FindColumn("Name")!.Width);
            Assert.False(fresh.FindColumn("Id")!.Visible);
            Assert.Equal("Country", fresh.CurrentRequest.Groups.Single().Member);
            Assert.Equal(grid.TotalRecordCount, fresh.TotalRecordCount);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
