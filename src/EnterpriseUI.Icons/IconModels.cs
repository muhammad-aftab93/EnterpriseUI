namespace EnterpriseUI.Icons;

public enum IconSourceType
{
    Svg,
    Font,
    Uri
}

public interface IIcon
{
    string Name { get; }
    string Category { get; }
    IconSourceType SourceType { get; }
    string RenderMarkup(int size = 24, string? color = null);
}

/// <summary>
/// Vector SVG icon with scalable path data.
/// </summary>
public sealed class SvgIcon : IIcon
{
    public string Name { get; }
    public string Category { get; }
    public IconSourceType SourceType => IconSourceType.Svg;
    public string ViewBox { get; }
    public string PathData { get; }

    public SvgIcon(string name, string category, string pathData, string viewBox = "0 0 24 24")
    {
        Name = name;
        Category = category;
        PathData = pathData;
        ViewBox = viewBox;
    }

    public string RenderMarkup(int size = 24, string? color = null)
    {
        var fillColor = string.IsNullOrEmpty(color) ? "currentColor" : color;
        return $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{size}\" height=\"{size}\" viewBox=\"{ViewBox}\" fill=\"{fillColor}\"><path d=\"{PathData}\"/></svg>";
    }
}

/// <summary>
/// Typography glyph-based icon font.
/// </summary>
public sealed class FontIcon : IIcon
{
    public string Name { get; }
    public string Category { get; }
    public IconSourceType SourceType => IconSourceType.Font;
    public string FontFamily { get; }
    public string Glyph { get; }

    public FontIcon(string name, string category, string fontFamily, string glyph)
    {
        Name = name;
        Category = category;
        FontFamily = fontFamily;
        Glyph = glyph;
    }

    public string RenderMarkup(int size = 24, string? color = null)
    {
        var style = $"font-family: '{FontFamily}'; font-size: {size}px;";
        if (!string.IsNullOrEmpty(color)) style += $" color: {color};";
        return $"<span class=\"eui-icon eui-icon-{Name}\" style=\"{style}\">{Glyph}</span>";
    }
}

