using EnterpriseUI.Abstractions.Accessibility;
using EnterpriseUI.Abstractions.Controls;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Core.Mvvm;

namespace EnterpriseUI.Media;

/// <summary>
/// Superset Image Editor combining Telerik EnterpriseImageEditor and DevExpress ImageEdit.
/// </summary>
public class EnterpriseImageEditor : ObservableObject, IUIComponent, IThemeableControl, IAccessibleControl
{
    private string? _imageSource;
    private double _rotationAngle;
    private double _zoom = 1.0;

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public string? ImageSource { get => _imageSource; set => SetProperty(ref _imageSource, value); }
    public double RotationAngle { get => _rotationAngle; set => SetProperty(ref _rotationAngle, value); }
    public double Zoom { get => _zoom; set => SetProperty(ref _zoom, value); }

    public AriaRole Role { get; set; } = AriaRole.Region;
    public string? AriaLabel { get; set; } = "Image Editor";
    public string? AriaDescription { get; set; }
    public string? AriaLabelledBy { get; set; }
    public string? AriaDescribedBy { get; set; }
    public bool AriaHidden { get; set; }
    public bool AriaExpanded { get; set; }
    public bool AriaDisabled { get; set; }
    public bool AriaRequired { get; set; }
    public bool AriaInvalid { get; set; }
    public string? ThemeOverride { get; set; }

    public void RotateClockwise() => RotationAngle = (RotationAngle + 90) % 360;
    public void RotateCounterClockwise() => RotationAngle = (RotationAngle - 90 + 360) % 360;
    public void ZoomIn() => Zoom *= 1.25;
    public void ZoomOut() => Zoom = Math.Max(0.2, Zoom / 1.25);

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void OnThemeChanged(ITheme newTheme) { }
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

