using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Text;
using EnterpriseUI.Data;
using EnterpriseUI.Exporting;
using EnterpriseUI.Grids;
using EnterpriseUI.Printing;
using EnterpriseUI.WinForms;

namespace EnterpriseUI.Benchmarks;

public sealed class BenchmarkRecord
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
}

/// <summary>
/// Repeatable DataGrid benchmarks: memory, render, scroll, filter, sort, grouping, export and print pagination.
/// Usage: dotnet run -c Release --project benchmarks/EnterpriseUI.Benchmarks [rows] [markdown-output-path]
/// Each case runs a warm-up, then several iterations; the table shows min, median and max in milliseconds.
/// </summary>
public static class Program
{
    private sealed class TestGrid : EnterpriseDataGrid<BenchmarkRecord>
    {
        public Bitmap Render()
        {
            var bitmap = new Bitmap(Width, Height);
            DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height));
            return bitmap;
        }
    }

    private static readonly List<(string Case, double Min, double Median, double Max, string Note)> Results = new();

    [STAThread]
    public static int Main(string[] args)
    {
        var rows = args.Length > 0 && int.TryParse(args[0], out var n) ? n : 1_000_000;
        var output = args.Length > 1 ? args[1] : null;

        Console.WriteLine($"EnterpriseUI DataGrid benchmarks - {rows:N0} rows, {Environment.ProcessorCount} logical cores, {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"Build: {(IsOptimized() ? "optimized" : "DEBUG (numbers are not representative; use -c Release)")}");
        Console.WriteLine();

        var data = Generate(rows);

        MeasureMemory(data);
        var grid = NewEngine(data);
        grid.RefreshDataAsync().GetAwaiter().GetResult();

        Measure("bind (RefreshDataAsync)", 5, () => { var g = NewEngine(data); g.RefreshDataAsync().GetAwaiter().GetResult(); });
        Measure("fetch window deep in the list", 20, () => { grid.EnsureRowsLoaded(rows * 3 / 4, 60); SpinWait.SpinUntil(() => grid.TryGetItemAt(rows * 3 / 4 + 10, out _), 5000); });
        Measure("sort by Amount", 5, () => Query(grid, () => grid.SortBy("Amount", ListSortDirection.Descending)), () => Query(grid, grid.ClearSort));
        Measure("sort by Name (text)", 5, () => Query(grid, () => grid.SortBy("Name", ListSortDirection.Ascending)), () => Query(grid, grid.ClearSort));
        Measure("filter Region = EMEA", 10, () => Query(grid, () => grid.FilterBy("Region", FilterOperator.IsEqualTo, "EMEA")), () => Query(grid, grid.ClearFilters));
        Measure("filter Name contains '99'", 5, () => Query(grid, () => grid.FilterBy("Name", FilterOperator.Contains, "99")), () => Query(grid, grid.ClearFilters));
        Measure("group by Region", 5, () => Query(grid, () => grid.GroupBy("Region")), () => Query(grid, grid.ClearGroups));
        Measure("group by Region + sort by Amount", 3, () => Query(grid, () => { grid.GroupBy("Region"); grid.SortBy("Amount", ListSortDirection.Descending); }), () => Query(grid, () => { grid.ClearGroups(); grid.ClearSort(); }));
        Measure("column summaries (sum, average, count)", 5, () =>
        {
            var g = NewEngine(data);
            g.Columns.First(c => c.Field == "Amount").FooterSummaries.Add(new GridColumnSummary(AggregateFunction.Sum));
            g.Columns.First(c => c.Field == "Amount").FooterSummaries.Add(new GridColumnSummary(AggregateFunction.Average));
            g.Columns.First(c => c.Field == "Id").FooterSummaries.Add(new GridColumnSummary(AggregateFunction.Count));
            g.RefreshDataAsync().GetAwaiter().GetResult();
        });

        var exportRows = Math.Min(rows, 300_000);
        var exportGrid = NewEngine(data.Take(exportRows).ToList());
        exportGrid.RefreshDataAsync().GetAwaiter().GetResult();
        foreach (var format in new[] { "csv", "json", "xlsx", "pdf" })
        {
            var limit = format == "pdf" ? 20_000 : exportRows;
            var g = format == "pdf" ? NewEngine(data.Take(limit).ToList()) : exportGrid;
            g.RefreshDataAsync().GetAwaiter().GetResult();
            Measure($"export {limit:N0} rows to {format}", 3, () => GridExporter.ExportAsync(g, format, Stream.Null).GetAwaiter().GetResult(), note: format == "pdf" ? "capped at 20,000 rows" : string.Empty);
        }

        var printGrid = NewEngine(data.Take(Math.Min(rows, 50_000)).ToList());
        printGrid.RefreshDataAsync().GetAwaiter().GetResult();
        Measure("print pagination (50,000 rows)", 5, () =>
        {
            var document = GridExporter.CreateDocumentAsync(printGrid).GetAwaiter().GetResult();
            var template = PrintTemplate.Standard();
            template.Settings.MaxRows = int.MaxValue;
            PrintPaginator.Paginate(document, template);
        });

        MeasureRendering(data);

        Print();
        if (output != null)
        {
            File.WriteAllText(output, ToMarkdown(rows));
            Console.WriteLine($"\nWritten to {output}");
        }

        return 0;
    }

    private static bool IsOptimized()
    {
#if DEBUG
        return false;
#else
        return true;
#endif
    }

    private static List<BenchmarkRecord> Generate(int count)
    {
        var regions = new[] { "EMEA", "APAC", "AMER" };
        var list = new List<BenchmarkRecord>(count);
        for (var i = 1; i <= count; i++)
        {
            list.Add(new BenchmarkRecord { Id = i, Name = $"Item {i}", Region = regions[i % 3], Amount = 10m * (i % 97) + i % 7, Date = new DateTime(2024, 1, 1).AddDays(i % 365) });
        }
        return list;
    }

    private static DataGrid<BenchmarkRecord> NewEngine(List<BenchmarkRecord> data)
    {
        var grid = new DataGrid<BenchmarkRecord>();
        foreach (var field in new[] { "Id", "Name", "Region", "Amount", "Date" }) grid.Columns.Add(new GridColumn { Field = field, Title = field });
        grid.DataSource = data;
        return grid;
    }

    private static void Query<T>(DataGrid<T> grid, Action apply)
    {
        apply();
        SpinWait.SpinUntil(() => !grid.IsLoading, 120_000);
    }

    private static void Measure(string name, int iterations, Action action, Action? reset = null, string note = "")
    {
        action();
        reset?.Invoke();
        var samples = new List<double>();
        for (var i = 0; i < iterations; i++)
        {
            GC.Collect();
            var sw = Stopwatch.StartNew();
            action();
            samples.Add(sw.Elapsed.TotalMilliseconds);
            reset?.Invoke();
        }
        samples.Sort();
        Results.Add((name, samples[0], samples[samples.Count / 2], samples[^1], note));
        Console.WriteLine($"{name,-44} {samples[samples.Count / 2],10:N1} ms");
    }

    private static void MeasureMemory(List<BenchmarkRecord> data)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var before = GC.GetTotalMemory(true);
        var grid = NewEngine(data);
        grid.RefreshDataAsync().GetAwaiter().GetResult();
        GC.Collect();
        var after = GC.GetTotalMemory(true);
        var rowsOnly = before;
        Results.Add(("memory: rows themselves (MB)", rowsOnly / 1e6, rowsOnly / 1e6, rowsOnly / 1e6, "the bound objects, not owned by the grid"));
        Results.Add(("memory: added by binding the grid (MB)", (after - before) / 1e6, (after - before) / 1e6, (after - before) / 1e6, $"{grid.RenderedItems.Count} rows materialized"));
        Console.WriteLine($"{"memory: rows (MB)",-44} {rowsOnly / 1e6,10:N1}");
        Console.WriteLine($"{"memory: added by binding (MB)",-44} {(after - before) / 1e6,10:N1}");
        GC.KeepAlive(grid);
    }

    private static void MeasureRendering(List<BenchmarkRecord> data)
    {
        Application.SetCompatibleTextRenderingDefault(false);
        var sample = data.Take(Math.Min(data.Count, 100_000)).ToList();
        using var grid = new TestGrid { Width = 1400, Height = 800, ShowGroupPanel = false, ItemHeight = 28 };
        foreach (var field in new[] { "Id", "Name", "Region", "Amount", "Date" }) grid.Columns.Add(new GridColumn { Field = field, Title = field, Width = 160 });
        using var form = new Form { ClientSize = new Size(1400, 800), ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-4000, 0) };
        grid.Dock = DockStyle.Fill;
        form.Controls.Add(grid);
        form.Show();
        grid.DataSource = sample;
        var load = grid.RefreshDataAsync();
        while (!load.IsCompleted) { Application.DoEvents(); Thread.Sleep(2); }
        load.GetAwaiter().GetResult();
        for (var i = 0; i < 10; i++) Application.DoEvents();

        Measure("render one 1400x800 frame (100,000 rows)", 30, () => { using var bmp = grid.Render(); });

        var step = 0;
        Measure("scroll: jump 2,400 rows and render", 30, () =>
        {
            grid.Selection.SetFocus(step++ % 40 * 2_400, 1);
            Application.DoEvents();
            using var bmp = grid.Render();
        });

        grid.Selection.SetFocus(0, 0);
        Measure("scroll: 60 single-row steps and render each", 5, () =>
        {
            for (var i = 0; i < 60; i++)
            {
                grid.Selection.Move(1, 0);
                using var bmp = grid.Render();
            }
        }, () => grid.Selection.SetFocus(0, 0));
    }

    private static void Print()
    {
        Console.WriteLine();
        Console.WriteLine(ToMarkdown(0));
    }

    private static string ToMarkdown(int rows)
    {
        var sb = new StringBuilder();
        if (rows > 0) sb.AppendLine($"Benchmarks over {rows:N0} rows on {Environment.ProcessorCount} logical cores, {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}, {(IsOptimized() ? "Release" : "Debug")} build, {DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.").AppendLine();
        sb.AppendLine("| Case | Min (ms) | Median (ms) | Max (ms) | Note |");
        sb.AppendLine("|---|---:|---:|---:|---|");
        foreach (var r in Results) sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"| {r.Case} | {r.Min:N1} | {r.Median:N1} | {r.Max:N1} | {r.Note} |"));
        return sb.ToString();
    }
}
