namespace EnterpriseUI.Grids;

public enum ColumnZone
{
    Left,
    Scrollable,
    Right
}

public sealed record ColumnSlot(GridColumn Column, double Left, double Width, ColumnZone Zone)
{
    public double Right => Left + Width;
}

public sealed record BandSpan(GridColumnBand Band, int Level, double Left, double Width, ColumnZone Zone);

/// <summary>Resolved horizontal layout of the visible columns for one viewport.</summary>
public sealed class ColumnLayoutResult
{
    public ColumnLayoutResult(IReadOnlyList<ColumnSlot> slots, double viewportWidth, double contentWidth, double horizontalOffset, double scrollableLeft, double scrollableRight)
    {
        Slots = slots;
        ViewportWidth = viewportWidth;
        ContentWidth = contentWidth;
        HorizontalOffset = horizontalOffset;
        ScrollableLeft = scrollableLeft;
        ScrollableRight = scrollableRight;
    }

    public IReadOnlyList<ColumnSlot> Slots { get; }
    public double ViewportWidth { get; }
    public double ContentWidth { get; }
    public double HorizontalOffset { get; }
    public double ScrollableLeft { get; }
    public double ScrollableRight { get; }
    public double MaxHorizontalOffset => Math.Max(0, ContentWidth - ViewportWidth);

    /// <summary>Returns the slot index under <paramref name="x"/>, preferring pinned columns, or -1.</summary>
    public int HitTest(double x)
    {
        for (int i = 0; i < Slots.Count; i++)
        {
            var slot = Slots[i];
            if (slot.Zone != ColumnZone.Scrollable && x >= slot.Left && x < slot.Right)
            {
                return i;
            }
        }

        if (x < ScrollableLeft || x >= ScrollableRight)
        {
            return -1;
        }

        for (int i = 0; i < Slots.Count; i++)
        {
            var slot = Slots[i];
            if (slot.Zone == ColumnZone.Scrollable && x >= slot.Left && x < slot.Right)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Returns the slot whose resizable right edge is within <paramref name="tolerance"/> of <paramref name="x"/>, or -1.</summary>
    public int HitTestResizeHandle(double x, double tolerance)
    {
        var best = -1;
        var bestDistance = double.MaxValue;

        for (int i = 0; i < Slots.Count; i++)
        {
            var slot = Slots[i];
            if (!slot.Column.Resizable)
            {
                continue;
            }

            var edge = slot.Right;
            var edgeVisible = slot.Zone != ColumnZone.Scrollable || (edge >= ScrollableLeft && edge <= ScrollableRight);
            var distance = Math.Abs(edge - x);
            if (edgeVisible && distance <= tolerance && distance < bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>Returns the slot index a dragged column would be inserted before; Slots.Count means append.</summary>
    public int GetInsertionIndex(double x)
    {
        var hit = HitTest(x);
        if (hit < 0)
        {
            return x < ScrollableLeft ? 0 : Slots.Count;
        }

        var slot = Slots[hit];
        return x < slot.Left + slot.Width / 2 ? hit : hit + 1;
    }
}

public static class GridColumnLayout
{
    public const double DefaultColumnWidth = 120;

    public static ColumnZone ZoneOf(GridColumn column) => column.PinPosition switch
    {
        ColumnPinPosition.Left => ColumnZone.Left,
        ColumnPinPosition.Right => ColumnZone.Right,
        _ => ColumnZone.Scrollable
    };

    /// <summary>Visible columns ordered left-pinned, scrollable, right-pinned, keeping declared order within each zone.</summary>
    public static List<GridColumn> OrderVisible(IEnumerable<GridColumn> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        return columns.Where(c => c.Visible).OrderBy(c => (int)ZoneOf(c)).ToList();
    }

    public static double ClampWidth(GridColumn column, double width)
    {
        var max = Math.Max(column.MinWidth, column.MaxWidth ?? double.MaxValue);
        return Math.Clamp(width, column.MinWidth, max);
    }

    public static double WidthOf(GridColumn column) => ClampWidth(column, column.Width ?? DefaultColumnWidth);

    public static ColumnLayoutResult Compute(IReadOnlyList<GridColumn> ordered, double viewportWidth, double horizontalOffset, bool fillViewport, double scale = 1.0)
    {
        ArgumentNullException.ThrowIfNull(ordered);
        viewportWidth = Math.Max(0, viewportWidth);

        var widths = ordered.Select(c => WidthOf(c) * scale).ToArray();
        var total = widths.Sum();
        if (fillViewport && widths.Length > 0 && total < viewportWidth)
        {
            var stretched = -1;
            for (int i = 0; i < ordered.Count; i++)
            {
                if (ZoneOf(ordered[i]) == ColumnZone.Scrollable)
                {
                    stretched = i;
                }
            }

            widths[stretched < 0 ? widths.Length - 1 : stretched] += viewportWidth - total;
            total = viewportWidth;
        }

        double leftWidth = 0, rightWidth = 0;
        for (int i = 0; i < ordered.Count; i++)
        {
            var zone = ZoneOf(ordered[i]);
            if (zone == ColumnZone.Left) leftWidth += widths[i];
            else if (zone == ColumnZone.Right) rightWidth += widths[i];
        }

        var rightStart = Math.Max(viewportWidth - rightWidth, leftWidth);
        var offset = Math.Clamp(horizontalOffset, 0, Math.Max(0, total - viewportWidth));

        var slots = new List<ColumnSlot>(ordered.Count);
        double leftCursor = 0, scrollCursor = leftWidth - offset, rightCursor = rightStart;
        for (int i = 0; i < ordered.Count; i++)
        {
            var column = ordered[i];
            var zone = ZoneOf(column);
            double left;
            switch (zone)
            {
                case ColumnZone.Left:
                    left = leftCursor;
                    leftCursor += widths[i];
                    break;
                case ColumnZone.Right:
                    left = rightCursor;
                    rightCursor += widths[i];
                    break;
                default:
                    left = scrollCursor;
                    scrollCursor += widths[i];
                    break;
            }
            slots.Add(new ColumnSlot(column, left, widths[i], zone));
        }

        return new ColumnLayoutResult(slots, viewportWidth, total, offset, leftWidth, rightStart);
    }

    /// <summary>Number of band header rows needed to show every visible column's band path.</summary>
    public static int BandDepth(IEnumerable<GridColumn> columns, IReadOnlyList<GridColumnBand> bands)
    {
        var parents = BuildParentMap(bands);
        return columns.Where(c => c.Visible).Select(c => ResolvePath(c, bands, parents).Count).DefaultIfEmpty(0).Max();
    }

    public static IReadOnlyList<BandSpan> ComputeBandSpans(ColumnLayoutResult layout, IReadOnlyList<GridColumnBand> bands)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(bands);

        var parents = BuildParentMap(bands);
        var paths = layout.Slots.Select(s => ResolvePath(s.Column, bands, parents)).ToList();
        var depth = paths.DefaultIfEmpty().Max(p => p?.Count ?? 0);
        var spans = new List<BandSpan>();

        for (int level = 0; level < depth; level++)
        {
            GridColumnBand? current = null;
            double left = 0, right = 0;
            var zone = ColumnZone.Scrollable;

            void Flush()
            {
                if (current != null)
                {
                    spans.Add(new BandSpan(current, level, left, right - left, zone));
                    current = null;
                }
            }

            for (int i = 0; i < layout.Slots.Count; i++)
            {
                var slot = layout.Slots[i];
                var band = paths[i].Count > level ? paths[i][level] : null;

                if (band != null && ReferenceEquals(band, current) && slot.Zone == zone)
                {
                    right = slot.Right;
                    continue;
                }

                Flush();
                if (band != null)
                {
                    current = band;
                    left = slot.Left;
                    right = slot.Right;
                    zone = slot.Zone;
                }
            }
            Flush();
        }

        return spans;
    }

    private static Dictionary<GridColumnBand, GridColumnBand?> BuildParentMap(IReadOnlyList<GridColumnBand> bands)
    {
        var map = new Dictionary<GridColumnBand, GridColumnBand?>(ReferenceEqualityComparer.Instance);

        void Visit(GridColumnBand band, GridColumnBand? parent)
        {
            if (!map.TryAdd(band, parent))
            {
                return;
            }

            foreach (var child in band.ChildBands)
            {
                Visit(child, band);
            }
        }

        foreach (var band in bands)
        {
            Visit(band, null);
        }

        return map;
    }

    private static List<GridColumnBand> ResolvePath(GridColumn column, IReadOnlyList<GridColumnBand> bands, Dictionary<GridColumnBand, GridColumnBand?> parents)
    {
        GridColumnBand? owner = null;
        foreach (var band in parents.Keys)
        {
            if (column.BandId != null ? band.Id == column.BandId : band.Columns.Any(c => ReferenceEquals(c, column)))
            {
                owner = band;
                break;
            }
        }

        var path = new List<GridColumnBand>();
        for (var band = owner; band != null; band = parents[band])
        {
            path.Insert(0, band);
        }
        return path;
    }
}
