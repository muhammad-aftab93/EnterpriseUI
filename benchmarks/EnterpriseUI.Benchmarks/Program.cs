using System.Diagnostics;
using EnterpriseUI.Data;
using EnterpriseUI.Theming.Themes;

namespace EnterpriseUI.Benchmarks;

public class BenchmarkRecord
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

public static class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("=========================================================");
        Console.WriteLine(" EnterpriseUI High-Performance Benchmarks");
        Console.WriteLine("=========================================================\n");

        // Benchmark 1: QueryEngine on 100,000 items
        const int recordCount = 100_000;
        Console.WriteLine($"[Benchmark 1] Generating {recordCount:N0} in-memory records...");
        var list = new List<BenchmarkRecord>(recordCount);
        for (int i = 0; i < recordCount; i++)
        {
            list.Add(new BenchmarkRecord
            {
                Id = i,
                Name = $"Item {i}",
                Value = (decimal)(i % 1000)
            });
        }

        var queryable = list.AsQueryable();
        var request = new DataSourceRequest
        {
            Page = 1,
            PageSize = 50
        };
        request.Filters.Add(new FilterDescriptor("Value", FilterOperator.IsGreaterThan, 500m));
        request.Sorts.Add(new SortDescriptor("Value", System.ComponentModel.ListSortDirection.Descending));

        // Warmup
        QueryEngine.Execute(queryable, request);

        var sw = Stopwatch.StartNew();
        const int iterations = 100;
        for (int i = 0; i < iterations; i++)
        {
            QueryEngine.Execute(queryable, request);
        }
        sw.Stop();

        var avgMs = sw.Elapsed.TotalMilliseconds / iterations;
        Console.WriteLine($"    QueryEngine Execution (Filter + Sort + Page over {recordCount:N0} items):");
        Console.WriteLine($"    Average Time: {avgMs:F2} ms per query ({iterations} iterations)\n");

        // Benchmark 2: Virtualization Manager 1,000,000 items window calculation
        Console.WriteLine("[Benchmark 2] 2D Virtualization Viewport Range Calculation (1,000,000 items):");
        var virtualizer = new VirtualizationManager { ItemHeight = 36.0, ItemWidth = 120.0 };

        sw.Restart();
        const int virtIterations = 1_000_000;
        for (int i = 0; i < virtIterations; i++)
        {
            _ = virtualizer.CalculateVisibleRows(i % 10000, 800, 1_000_000);
        }
        sw.Stop();

        Console.WriteLine($"    Calculated {virtIterations:N0} viewport updates in {sw.ElapsedMilliseconds} ms ({(double)sw.ElapsedMilliseconds / virtIterations * 1000:F3} microseconds/call).\n");

        // Benchmark 3: Theme CSS Variable Compilation
        Console.WriteLine("[Benchmark 3] Theme CSS Variable Generation:");
        var fluentTheme = BuiltInThemes.CreateFluent();
        sw.Restart();
        for (int i = 0; i < 50_000; i++)
        {
            _ = fluentTheme.ToCssVariables("eui");
        }
        sw.Stop();
        Console.WriteLine($"    Compiled 50,000 full stylesheet token blocks in {sw.ElapsedMilliseconds} ms.\n");

        Console.WriteLine("Benchmarks complete.");
    }
}

