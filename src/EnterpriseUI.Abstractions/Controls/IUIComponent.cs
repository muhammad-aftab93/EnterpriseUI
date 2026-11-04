namespace EnterpriseUI.Abstractions.Controls;

/// <summary>
/// Root contract for all enterprise components across Web, Desktop, and Mobile.
/// </summary>
public interface IUIComponent : IAsyncDisposable, IDisposable
{
    string Id { get; }
    string? Name { get; set; }
    bool IsEnabled { get; set; }
    bool IsVisible { get; set; }
    object? Tag { get; set; }
    IUIComponent? Parent { get; }
    IReadOnlyList<IUIComponent> Children { get; }
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Contract for interactive input and actionable components.
/// </summary>
public interface IInteractiveControl : IUIComponent
{
    bool IsFocused { get; }
    bool IsReadOnly { get; set; }
    int TabIndex { get; set; }
    ValueTask<bool> FocusAsync();
}

/// <summary>
/// Contract for templated visual representation.
/// </summary>
public interface ITemplatedControl : IUIComponent
{
    object? HeaderTemplate { get; set; }
    object? ContentTemplate { get; set; }
    object? FooterTemplate { get; set; }
}

/// <summary>
/// Contract for components bound to in-memory, queryable, or remote collections.
/// </summary>
public interface IDataBoundControl<TItem> : IUIComponent
{
    IEnumerable<TItem>? DataSource { get; set; }
    TItem? SelectedItem { get; set; }
    IReadOnlyList<TItem> SelectedItems { get; }
    Task RefreshDataAsync(CancellationToken cancellationToken = default);
}

