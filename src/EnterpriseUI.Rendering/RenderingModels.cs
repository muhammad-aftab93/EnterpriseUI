using EnterpriseUI.Abstractions.Controls;
using EnterpriseUI.Abstractions.Theming;

namespace EnterpriseUI.Rendering;

public enum RenderNodeType
{
    Element,
    Text,
    Container,
    Custom
}

public class RenderNode
{
    public string TagName { get; set; } = "div";
    public RenderNodeType Type { get; set; } = RenderNodeType.Element;
    public string? TextContent { get; set; }
    public Dictionary<string, string> Attributes { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Styles { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<RenderNode> Children { get; } = new();
}

/// <summary>
/// Platform-agnostic Virtual DOM and Render Tree engine.
/// </summary>
public static class RenderTreeBuilder
{
    public static RenderNode CreateElement(string tag, string? className = null)
    {
        var node = new RenderNode { TagName = tag, Type = RenderNodeType.Element };
        if (!string.IsNullOrEmpty(className))
        {
            node.Attributes["class"] = className;
        }
        return node;
    }

    public static RenderNode CreateText(string text)
    {
        return new RenderNode { Type = RenderNodeType.Text, TextContent = text };
    }
}

