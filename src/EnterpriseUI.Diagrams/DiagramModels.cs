using EnterpriseUI.Abstractions.Accessibility;
using EnterpriseUI.Abstractions.Controls;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Core.Mvvm;

namespace EnterpriseUI.Diagrams;

public enum ShapeType
{
    Rectangle,
    RoundedRectangle,
    Circle,
    Diamond,
    Cylinder,
    Parallelogram,
    Cloud
}

public class DiagramNode : ObservableObject
{
    private double _x;
    private double _y;
    private string _text = string.Empty;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Text { get => _text; set => SetProperty(ref _text, value); }
    public double X { get => _x; set => SetProperty(ref _x, value); }
    public double Y { get => _y; set => SetProperty(ref _y, value); }
    public double Width { get; set; } = 120;
    public double Height { get; set; } = 60;
    public ShapeType Shape { get; set; } = ShapeType.RoundedRectangle;
    public string FillColor { get; set; } = "#ffffff";
    public string StrokeColor { get; set; } = "#0078d4";
}

public class DiagramConnector : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string SourceNodeId { get; set; } = string.Empty;
    public string TargetNodeId { get; set; } = string.Empty;
    public string? Label { get; set; }
    public bool IsOrthogonal { get; set; } = true;
    public string StrokeColor { get; set; } = "#605e5c";
}

/// <summary>
/// Superset Node-Link Diagramming component combining DevExpress DiagramControl and Telerik RadDiagram.
/// </summary>
public class Diagram : ObservableObject, IUIComponent, IThemeableControl, IAccessibleControl
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public List<DiagramNode> Nodes { get; } = new();
    public List<DiagramConnector> Connectors { get; } = new();
    public double ZoomLevel { get; set; } = 1.0;
    public bool SnapToGrid { get; set; } = true;

    public AriaRole Role { get; set; } = AriaRole.Region;
    public string? AriaLabel { get; set; } = "Interactive Diagram";
    public string? AriaDescription { get; set; }
    public string? AriaLabelledBy { get; set; }
    public string? AriaDescribedBy { get; set; }
    public bool AriaHidden { get; set; }
    public bool AriaExpanded { get; set; }
    public bool AriaDisabled { get; set; }
    public bool AriaRequired { get; set; }
    public bool AriaInvalid { get; set; }
    public string? ThemeOverride { get; set; }

    public void AutoLayoutHierarchical()
    {
        // Simple automatic vertical tree arrangement
        double currentY = 50;
        foreach (var node in Nodes)
        {
            node.X = 200;
            node.Y = currentY;
            currentY += node.Height + 50;
        }
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void OnThemeChanged(ITheme newTheme) { }
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

