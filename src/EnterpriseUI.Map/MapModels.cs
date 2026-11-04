using EnterpriseUI.Abstractions.Accessibility;
using EnterpriseUI.Abstractions.Controls;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Core.Mvvm;

namespace EnterpriseUI.Map;

public readonly record struct GeoLocation(double Latitude, double Longitude);

public class GeoMarker : ObservableObject
{
    public string Title { get; set; } = string.Empty;
    public GeoLocation Location { get; set; }
    public string? Description { get; set; }
    public string Color { get; set; } = "#d13438";
}

/// <summary>
/// Superset GIS Map control combining DevExpress MapControl and Telerik RadMap.
/// </summary>
public class EnterpriseGeoMap : ObservableObject, IUIComponent, IThemeableControl, IAccessibleControl
{
    private GeoLocation _center = new(37.7749, -122.4194); // San Francisco default
    private int _zoomLevel = 10;

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public GeoLocation Center { get => _center; set => SetProperty(ref _center, value); }
    public int ZoomLevel { get => _zoomLevel; set => SetProperty(ref _zoomLevel, Math.Clamp(value, 1, 20)); }
    public List<GeoMarker> Markers { get; } = new();

    public AriaRole Role { get; set; } = AriaRole.Region;
    public string? AriaLabel { get; set; } = "Geographic Map";
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

