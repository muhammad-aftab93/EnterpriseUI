using EnterpriseUI.Abstractions.Controls;
using EnterpriseUI.Core.Mvvm;

namespace EnterpriseUI.Printing;

public class PrintPageSettings
{
    public bool IsLandscape { get; set; }
    public double MarginLeft { get; set; } = 25;
    public double MarginRight { get; set; } = 25;
    public double MarginTop { get; set; } = 25;
    public double MarginBottom { get; set; } = 25;
    public string PaperSize { get; set; } = "Letter";
}

/// <summary>
/// Enterprise printing orchestration service and preview pipeline.
/// </summary>
public sealed class PrintService : ObservableObject
{
    private static readonly Lazy<PrintService> _instance = new(() => new PrintService());
    public static PrintService Instance => _instance.Value;

    public PrintPageSettings CurrentSettings { get; set; } = new();

    public Task PrintComponentAsync(IUIComponent component, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(component);
        // Dispatch to platform native printing pipeline
        return Task.CompletedTask;
    }
}

