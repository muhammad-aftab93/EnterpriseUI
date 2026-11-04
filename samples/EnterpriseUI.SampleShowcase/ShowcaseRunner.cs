using System.ComponentModel;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Charts;
using EnterpriseUI.Data;
using EnterpriseUI.Exporting;
using EnterpriseUI.Grids;
using EnterpriseUI.Theming;
using EnterpriseUI.Theming.Themes;

namespace EnterpriseUI.SampleShowcase;

public class OrderItem
{
    public int OrderId { get; set; }
    public string Customer { get; set; } = string.Empty;
    public string Product { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime OrderDate { get; set; }
    public string Status { get; set; } = "Completed";
}

public static class ShowcaseRunner
{
    public static async Task RunShowcaseAsync()
    {
        Console.WriteLine("=========================================================");
        Console.WriteLine(" EnterpriseUI Commercial Component Ecosystem Showcase");
        Console.WriteLine(" Superset of Telerik UI + DevExpress Architecture");
        Console.WriteLine("=========================================================\n");

        // 1. Theme Engine Showcase
        Console.WriteLine("[1] Theming Engine:");
        var fluentTheme = BuiltInThemes.CreateFluent(ColorScheme.Dark);
        ThemeManager.Instance.ApplyTheme(fluentTheme);
        Console.WriteLine($"    Active Theme: {ThemeManager.Instance.CurrentTheme.Name}");
        Console.WriteLine($"    Primary Accent: {ThemeManager.Instance.CurrentTheme.GetToken(DesignTokens.Primary)}");
        Console.WriteLine($"    Surface Color: {ThemeManager.Instance.CurrentTheme.GetToken(DesignTokens.Surface)}\n");

        // 2. DataGrid Showcase
        Console.WriteLine("[2] Virtualized DataGrid with Expression Tree Query Engine:");
        var grid = new DataGrid<OrderItem>();
        grid.Columns.Add(new GridColumn { Field = "OrderId", Title = "Order #" });
        grid.Columns.Add(new GridColumn { Field = "Customer", Title = "Customer Name" });
        grid.Columns.Add(new GridColumn { Field = "Product", Title = "Product" });
        grid.Columns.Add(new GridColumn { Field = "Amount", Title = "Total Amount", FormatString = "C2" });

        var testOrders = new List<OrderItem>
        {
            new() { OrderId = 101, Customer = "Acme Corp", Product = "Cloud Server", Amount = 1200.00m, OrderDate = DateTime.Today.AddDays(-2) },
            new() { OrderId = 102, Customer = "Contoso Ltd", Product = "Enterprise License", Amount = 4500.00m, OrderDate = DateTime.Today.AddDays(-1) },
            new() { OrderId = 103, Customer = "Fabrikam Inc", Product = "Consulting Pack", Amount = 800.00m, OrderDate = DateTime.Today }
        };

        grid.DataSource = testOrders;
        await grid.RefreshDataAsync();

        Console.WriteLine($"    Rendered Items Count: {grid.RenderedItems.Count}");
        foreach (var item in grid.RenderedItems)
        {
            Console.WriteLine($"    - #{item.OrderId}: {item.Customer} -> {item.Product} ({item.Amount:C})");
        }

        // 3. Export Engine Showcase
        Console.WriteLine("\n[3] Streaming Multi-Format Exporter:");
        using (var mem = new MemoryStream())
        {
            await ExportEngine.ExportAsync(testOrders, ExportFormat.Csv, mem);
            mem.Position = 0;
            using var reader = new StreamReader(mem);
            var csvSample = await reader.ReadToEndAsync();
            Console.WriteLine("    Generated CSV Output:\n" + csvSample);
        }

        // 4. EnterpriseChart Showcase
        Console.WriteLine("[4] Multi-Series Business EnterpriseChart:");
        var chart = new EnterpriseChart { Title = "Q4 Revenue" };
        var series = new ChartSeries { Name = "Revenue", Type = SeriesType.Column };
        series.DataPoints.Add(new ChartDataPoint { Category = "October", Value = 45000 });
        series.DataPoints.Add(new ChartDataPoint { Category = "November", Value = 62000 });
        series.DataPoints.Add(new ChartDataPoint { Category = "December", Value = 89000 });
        chart.Series.Add(series);
        Console.WriteLine($"    EnterpriseChart '{chart.Title}' configured with {chart.Series.Count} series and {series.DataPoints.Count} points.");

        Console.WriteLine("\nEnterpriseUI component platform execution completed successfully.");
    }
}

