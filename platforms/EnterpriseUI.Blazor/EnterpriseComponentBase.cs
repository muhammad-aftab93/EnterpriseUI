using Microsoft.AspNetCore.Components;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Theming;

namespace EnterpriseUI.Blazor;

/// <summary>
/// Base class for all EnterpriseUI Blazor web components with theme awareness.
/// </summary>
public abstract class EnterpriseComponentBase : ComponentBase, IThemeableControl, IDisposable
{
    [Parameter]
    public string? ThemeOverride { get; set; }

    [Parameter]
    public string? Class { get; set; }

    [Parameter]
    public string? Style { get; set; }

    protected ITheme CurrentTheme => ThemeOverride != null && ThemeManager.Instance.CurrentTheme.Name != ThemeOverride
        ? ThemeManager.Instance.CurrentTheme
        : ThemeManager.Instance.CurrentTheme;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        ThemeManager.Instance.ThemeChanged += HandleThemeChanged;
    }

    private void HandleThemeChanged(object? sender, ITheme newTheme)
    {
        InvokeAsync(StateHasChanged);
    }

    public virtual void OnThemeChanged(ITheme newTheme)
    {
        StateHasChanged();
    }

    public virtual void Dispose()
    {
        ThemeManager.Instance.ThemeChanged -= HandleThemeChanged;
        GC.SuppressFinalize(this);
    }
}

