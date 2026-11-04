using EnterpriseUI.Abstractions.Controls;

namespace EnterpriseUI.VisualDesigner;

public sealed record ToolboxItem(string Name, string Category, Type ComponentType, string Description);

/// <summary>
/// Design-time toolbox registration and visual component reflection service.
/// </summary>
public static class ToolboxService
{
    private static readonly List<ToolboxItem> _items = new();

    public static IReadOnlyList<ToolboxItem> Items => _items;

    public static void Register(string name, string category, Type componentType, string description = "")
    {
        _items.Add(new ToolboxItem(name, category, componentType, description));
    }
}

