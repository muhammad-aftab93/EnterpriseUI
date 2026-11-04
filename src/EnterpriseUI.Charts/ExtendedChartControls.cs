using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnterpriseUI.Abstractions.Accessibility;
using EnterpriseUI.Abstractions.Controls;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Core.Mvvm;

namespace EnterpriseUI.Charts;

/// <summary>
/// Clean-room Hierarchical TreeMap Control (Telerik RadTreeMap equivalent).
/// Computes normalized proportional bounding boxes for weighted hierarchical nodes.
/// </summary>
public class TreeMap : ObservableObject, IUIComponent, IThemeableControl, IAccessibleControl
{
    public sealed record TreeMapNode(string Id, string Label, string Category, double Value, double ColorValue);
    public sealed record TreeMapLayoutBox(TreeMapNode Node, double X, double Y, double Width, double Height);

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public List<TreeMapNode> Nodes { get; } = new();

    public IReadOnlyList<TreeMapLayoutBox> ComputeNormalizedLayout()
    {
        var result = new List<TreeMapLayoutBox>(Nodes.Count);
        if (Nodes.Count == 0) return result;

        double x = 0, y = 0, w = 1.0, h = 1.0;
        bool vertical = true;
        var ordered = Nodes.OrderByDescending(n => n.Value).ToList();

        for (int i = 0; i < ordered.Count; i++)
        {
            var node = ordered[i];
            double rem = ordered.Skip(i).Sum(n => Math.Max(0.0001, n.Value));
            double ratio = Math.Max(0.0001, node.Value) / rem;

            if (i == ordered.Count - 1)
            {
                result.Add(new TreeMapLayoutBox(node, x, y, w, h));
            }
            else if (vertical)
            {
                double sliceW = w * ratio;
                result.Add(new TreeMapLayoutBox(node, x, y, sliceW, h));
                x += sliceW;
                w -= sliceW;
            }
            else
            {
                double sliceH = h * ratio;
                result.Add(new TreeMapLayoutBox(node, x, y, w, sliceH));
                y += sliceH;
                h -= sliceH;
            }
            vertical = !vertical;
        }
        return result;
    }

    public AriaRole Role { get; set; } = AriaRole.Region;
    public string? AriaLabel { get; set; } = "TreeMap Visualization";
    public string? AriaDescription { get; set; }
    public string? AriaLabelledBy { get; set; }
    public string? AriaDescribedBy { get; set; }
    public bool AriaHidden { get; set; }
    public bool AriaExpanded { get; set; }
    public bool AriaDisabled { get; set; }
    public bool AriaRequired { get; set; }
    public bool AriaInvalid { get; set; }
    public string? ThemeOverride { get; set; }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void OnThemeChanged(ITheme newTheme) { }
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Clean-room 2D Matrix HeatMap Control (Telerik RadHeatMap equivalent).
/// </summary>
public class HeatMap : ObservableObject, IUIComponent, IThemeableControl, IAccessibleControl
{
    public sealed record HeatMapCell(string RowCategory, string ColumnCategory, double Value);

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public List<HeatMapCell> Cells { get; } = new();
    public string LowColorHex { get; set; } = "#E8F4FD";
    public string HighColorHex { get; set; } = "#005A9E";
    public bool ShowCellLabels { get; set; } = true;

    public double NormalizeCellValue(double value)
    {
        if (Cells.Count == 0) return 0;
        double min = Cells.Min(c => c.Value);
        double max = Cells.Max(c => c.Value);
        return Math.Abs(max - min) < 1e-9 ? 0.5 : Math.Clamp((value - min) / (max - min), 0.0, 1.0);
    }

    public AriaRole Role { get; set; } = AriaRole.Region;
    public string? AriaLabel { get; set; } = "HeatMap Matrix";
    public string? AriaDescription { get; set; }
    public string? AriaLabelledBy { get; set; }
    public string? AriaDescribedBy { get; set; }
    public bool AriaHidden { get; set; }
    public bool AriaExpanded { get; set; }
    public bool AriaDisabled { get; set; }
    public bool AriaRequired { get; set; }
    public bool AriaInvalid { get; set; }
    public string? ThemeOverride { get; set; }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void OnThemeChanged(ITheme newTheme) { }
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Clean-room Inline Micro-Chart Sparkline (Telerik RadSparkline equivalent).
/// Supports Line, Column, Area, and WinLoss sparkline modes with high/low/first/last markers.
/// </summary>
public class Sparkline : ObservableObject, IUIComponent, IThemeableControl, IAccessibleControl
{
    public enum SparklineKind { Line, Column, Area, WinLoss }

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public SparklineKind Kind { get; set; } = SparklineKind.Line;
    public List<double> Values { get; } = new();
    public bool ShowHighLowIndicators { get; set; } = true;
    public bool ShowFirstLastIndicators { get; set; } = true;

    public AriaRole Role { get; set; } = AriaRole.Region;
    public string? AriaLabel { get; set; } = "Trend Sparkline";
    public string? AriaDescription { get; set; }
    public string? AriaLabelledBy { get; set; }
    public string? AriaDescribedBy { get; set; }
    public bool AriaHidden { get; set; }
    public bool AriaExpanded { get; set; }
    public bool AriaDisabled { get; set; }
    public bool AriaRequired { get; set; }
    public bool AriaInvalid { get; set; }
    public string? ThemeOverride { get; set; }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void OnThemeChanged(ITheme newTheme) { }
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
