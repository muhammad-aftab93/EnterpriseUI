using System;
using System.Collections.Generic;
using System.Linq;

namespace EnterpriseUI.Grids;

/// <summary>A node of a tree map: its area is proportional to <see cref="TotalValue"/>.</summary>
public sealed class TreeMapItem
{
    public TreeMapItem(string name, double value = 0, double? colorValue = null, object? tag = null)
    {
        Name = name;
        Value = value;
        ColorValue = colorValue;
        Tag = tag;
    }

    public string Name { get; set; }

    /// <summary>Area weight of a leaf; ignored when the item has children.</summary>
    public double Value { get; set; }

    /// <summary>Optional second measure that drives the color (for example growth); the area uses <see cref="Value"/>.</summary>
    public double? ColorValue { get; set; }

    public object? Tag { get; set; }
    public TreeMapItem? Parent { get; internal set; }
    public List<TreeMapItem> Children { get; } = new();

    public double TotalValue => Children.Count == 0 ? Math.Max(0, Value) : Children.Sum(c => c.TotalValue);

    public TreeMapItem Add(TreeMapItem child)
    {
        child.Parent = this;
        Children.Add(child);
        return child;
    }

    public TreeMapItem Add(string name, double value, double? colorValue = null, object? tag = null) => Add(new TreeMapItem(name, value, colorValue, tag));

    public int Depth => Parent == null ? 0 : Parent.Depth + 1;

    public string Path => Parent == null ? Name : Parent.Path + " / " + Name;
}

public readonly record struct TreeMapRect(TreeMapItem Item, double X, double Y, double Width, double Height, int Depth, bool IsHeader);

/// <summary>Squarified tree map layout (Bruls, Huizing, van Wijk): rectangles keep an aspect ratio close to 1.</summary>
public static class TreeMapLayout
{
    /// <summary>
    /// Lays out <paramref name="items"/> in the given rectangle. Items with children are laid out recursively down to
    /// <paramref name="maxDepth"/> levels; a parent reserves <paramref name="headerHeight"/> for its label and <paramref name="padding"/> around its children.
    /// Items without area are skipped. Parents are returned before their children.
    /// </summary>
    public static IReadOnlyList<TreeMapRect> Compute(IEnumerable<TreeMapItem> items, double x, double y, double width, double height, int maxDepth = 1, double headerHeight = 0, double padding = 0)
    {
        var result = new List<TreeMapRect>();
        if (width <= 0 || height <= 0) return result;
        Layout(items.Where(i => i.TotalValue > 0).OrderByDescending(i => i.TotalValue).ToList(), x, y, width, height, 0, maxDepth, headerHeight, padding, result);
        return result;
    }

    private static void Layout(List<TreeMapItem> items, double x, double y, double w, double h, int depth, int maxDepth, double headerHeight, double padding, List<TreeMapRect> output)
    {
        if (items.Count == 0 || w <= 0 || h <= 0) return;

        var total = items.Sum(i => i.TotalValue);
        var scale = w * h / total;
        var areas = items.Select(i => i.TotalValue * scale).ToArray();
        var index = 0;
        var left = x;
        var top = y;
        var remainingW = w;
        var remainingH = h;

        while (index < items.Count)
        {
            var side = Math.Min(remainingW, remainingH);
            var end = index + 1;
            var worst = Worst(areas, index, end, side);
            while (end < items.Count)
            {
                var candidate = Worst(areas, index, end + 1, side);
                if (candidate > worst) break;
                worst = candidate;
                end++;
            }

            var rowArea = 0d;
            for (var i = index; i < end; i++) rowArea += areas[i];

            if (remainingW >= remainingH)
            {
                var rowWidth = remainingH <= 0 ? 0 : rowArea / remainingH;
                var cursor = top;
                for (var i = index; i < end; i++)
                {
                    var itemHeight = rowWidth <= 0 ? 0 : areas[i] / rowWidth;
                    Emit(items[i], left, cursor, rowWidth, itemHeight, depth, maxDepth, headerHeight, padding, output);
                    cursor += itemHeight;
                }
                left += rowWidth;
                remainingW -= rowWidth;
            }
            else
            {
                var rowHeight = remainingW <= 0 ? 0 : rowArea / remainingW;
                var cursor = left;
                for (var i = index; i < end; i++)
                {
                    var itemWidth = rowHeight <= 0 ? 0 : areas[i] / rowHeight;
                    Emit(items[i], cursor, top, itemWidth, rowHeight, depth, maxDepth, headerHeight, padding, output);
                    cursor += itemWidth;
                }
                top += rowHeight;
                remainingH -= rowHeight;
            }

            index = end;
        }
    }

    private static void Emit(TreeMapItem item, double x, double y, double w, double h, int depth, int maxDepth, double headerHeight, double padding, List<TreeMapRect> output)
    {
        output.Add(new TreeMapRect(item, x, y, w, h, depth, item.Children.Count > 0 && depth + 1 < maxDepth));
        if (item.Children.Count == 0 || depth + 1 >= maxDepth) return;

        var innerX = x + padding;
        var innerY = y + padding + headerHeight;
        var innerW = w - 2 * padding;
        var innerH = h - 2 * padding - headerHeight;
        if (innerW <= 1 || innerH <= 1) return;

        Layout(item.Children.Where(c => c.TotalValue > 0).OrderByDescending(c => c.TotalValue).ToList(), innerX, innerY, innerW, innerH, depth + 1, maxDepth, headerHeight, padding, output);
    }

    private static double Worst(double[] areas, int start, int end, double side)
    {
        double sum = 0, max = double.MinValue, min = double.MaxValue;
        for (var i = start; i < end; i++)
        {
            sum += areas[i];
            max = Math.Max(max, areas[i]);
            min = Math.Min(min, areas[i]);
        }

        if (sum <= 0 || min <= 0) return double.MaxValue;
        var s2 = side * side;
        return Math.Max(s2 * max / (sum * sum), sum * sum / (s2 * min));
    }
}
