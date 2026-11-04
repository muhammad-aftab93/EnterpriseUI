using EnterpriseUI.Abstractions.Controls;
using EnterpriseUI.Core.Mvvm;

namespace EnterpriseUI.Reporting;

public enum BandType
{
    TopMargin,
    PageHeader,
    ReportHeader,
    GroupHeader,
    Detail,
    GroupFooter,
    ReportFooter,
    PageFooter,
    BottomMargin
}

public class ReportBand
{
    public BandType Type { get; set; } = BandType.Detail;
    public double Height { get; set; } = 30;
    public List<ReportControl> Controls { get; } = new();
}

public class ReportControl
{
    public string Name { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 100;
    public double Height { get; set; } = 20;
    public string Text { get; set; } = string.Empty;
    public string? DataBindingExpression { get; set; }
    public string? FormatString { get; set; }
}

/// <summary>
/// Superset Report Definition engine combining DevExpress XtraReports and Telerik Reporting.
/// </summary>
public class ReportDefinition : ObservableObject
{
    public string ReportName { get; set; } = "Untitled Report";
    public double PageWidth { get; set; } = 850; // Letter width (points)
    public double PageHeight { get; set; } = 1100; // Letter height
    public double MarginLeft { get; set; } = 50;
    public double MarginRight { get; set; } = 50;
    public double MarginTop { get; set; } = 50;
    public double MarginBottom { get; set; } = 50;

    public List<ReportBand> Bands { get; } = new();
    public object? DataSource { get; set; }
}

public class ReportViewer : ObservableObject, IUIComponent
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public ReportDefinition? Report { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public double ZoomFactor { get; set; } = 1.0;

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

