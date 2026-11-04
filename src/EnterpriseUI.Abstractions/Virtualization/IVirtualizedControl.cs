namespace EnterpriseUI.Abstractions.Virtualization;

public readonly record struct IndexRange(int StartIndex, int EndIndex)
{
    public int Count => Math.Max(0, EndIndex - StartIndex + 1);
}

public interface IVirtualizedControl
{
    double ViewportWidth { get; }
    double ViewportHeight { get; }
    double ItemHeight { get; set; }
    double ItemWidth { get; set; }
    int TotalItemsCount { get; }
    IndexRange VisibleRowRange { get; }
    IndexRange VisibleColumnRange { get; }
    void UpdateViewport(double horizontalOffset, double verticalOffset, double viewportWidth, double viewportHeight);
}

