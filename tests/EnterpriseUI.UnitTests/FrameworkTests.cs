using EnterpriseUI.Framework;
using Xunit;

namespace EnterpriseUI.UnitTests;

public class FrameworkElementTests
{
    private static EnterpriseElement Tree()
    {
        var root = new EnterpriseElement("Window", "main");
        var panel = root.Children.Add("Panel", "side", "dark");
        panel.Children.Add("Button", "ok", "primary");
        panel.Children.Add("Button", "cancel");
        root.Children.Add("Label", "title");
        return root;
    }

    [Fact]
    public void Children_keep_parent_links_and_reject_cycles()
    {
        var root = Tree();
        var panel = root.Find("Panel")!;
        Assert.Same(root, panel.Parent);
        Assert.Throws<InvalidOperationException>(() => panel.Children.Add(root));
        Assert.Throws<InvalidOperationException>(() => panel.Children.Add(panel));

        var button = panel.Children[0];
        root.Children.Add(button);
        Assert.Same(root, button.Parent);
        Assert.DoesNotContain(button, panel.Children);
    }

    [Theory]
    [InlineData("Button", 2)]
    [InlineData("Panel Button", 2)]
    [InlineData("Window > Button", 0)]
    [InlineData("Panel > Button.primary", 1)]
    [InlineData("#title", 1)]
    [InlineData(".dark Button", 2)]
    [InlineData("*", 5)]
    public void FindAll_matches_selectors(string selector, int expected) =>
        Assert.Equal(expected, Tree().FindAll(selector).Count);

    [Fact]
    public void State_and_attribute_selectors()
    {
        var root = Tree();
        var ok = root.Find("#ok")!;
        Assert.Empty(root.FindAll("Button:hover"));
        ok.SetState(ElementStates.Hover, true);
        Assert.Same(ok, root.Find("Button:hover"));
        ok.SetAttribute("mode", "compact");
        Assert.Same(ok, root.Find("Button[mode=compact]"));
        Assert.Same(ok, root.Find("[mode]"));
        Assert.Null(root.Find("Button[mode=wide]"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Button:unknown")]
    [InlineData("Button[")]
    [InlineData("Button@x")]
    public void Invalid_selectors_throw(string selector) => Assert.Throws<FormatException>(() => StyleSelector.Parse(selector));

    [Fact]
    public void Specificity_orders_ids_over_classes_over_types()
    {
        Assert.True(StyleSelector.Parse("#a").Specificity > StyleSelector.Parse(".a.b.c").Specificity);
        Assert.True(StyleSelector.Parse(".a").Specificity > StyleSelector.Parse("Panel Button").Specificity);
        Assert.True(StyleSelector.Parse("Button:hover").Specificity > StyleSelector.Parse("Button").Specificity);
    }
}

public class FrameworkStyleTests
{
    [Fact]
    public void Cascade_uses_specificity_then_order_then_important()
    {
        var root = new EnterpriseElement("Window");
        var button = root.Children.Add("Button", "ok", "primary");
        var sheet = StyleSheet.Parse("""
            Button { background: #111111; padding: 4 8; }
            .primary { background: #222222; }
            #ok { background: #333333; }
            Button { background: #444444 !important; }
            """);
        var resolver = new StyleResolver();
        resolver.AddSheet(sheet);
        var style = resolver.Resolve(button);
        Assert.Equal("#444444", style.Get("background"));

        Assert.True(style.TryGetThickness("padding", out var padding));
        Assert.Equal(new StyleThickness(8, 4, 8, 4), padding);
    }

    [Fact]
    public void Later_rule_wins_on_equal_specificity_and_comments_are_ignored()
    {
        var root = new EnterpriseElement("Label");
        var resolver = new StyleResolver();
        resolver.AddSheet(StyleSheet.Parse("/* first */ Label { color: red; } Label { color: blue; } /* end */"));
        Assert.Equal("blue", resolver.Resolve(root).Get("color"));
    }

    [Fact]
    public void Inherited_properties_flow_to_children_but_others_do_not()
    {
        var root = new EnterpriseElement("Window");
        var label = root.Children.Add("Label");
        var resolver = new StyleResolver();
        resolver.AddSheet(StyleSheet.Parse("Window { color: #ff0000; border-width: 3; --accent: #00ff00; }"));
        var style = resolver.Resolve(label);
        Assert.Equal("#ff0000", style.Get("color"));
        Assert.Equal("#00ff00", style.Get("--accent"));
        Assert.Null(style.Get("border-width"));
    }

    [Fact]
    public void Variables_are_substituted_with_fallbacks()
    {
        var root = new EnterpriseElement("Window");
        var button = root.Children.Add("Button");
        var resolver = new StyleResolver();
        resolver.AddSheet(StyleSheet.Parse("Window { --brand: #0a64ad; } Button { background: var(--brand); border-color: var(--missing, #cccccc); }"));
        var style = resolver.Resolve(button);
        Assert.Equal("#0a64ad", style.Get("background"));
        Assert.Equal("#cccccc", style.Get("border-color"));
    }

    [Fact]
    public void Pseudo_state_changes_are_picked_up_when_the_tree_is_watched()
    {
        var root = new EnterpriseElement("Window");
        var button = root.Children.Add("Button");
        var resolver = new StyleResolver();
        resolver.Watch(root);
        resolver.AddSheet(StyleSheet.Parse("Button { background: #111111; } Button:hover { background: #222222; }"));
        Assert.Equal("#111111", resolver.Resolve(button).Get("background"));
        button.SetState(ElementStates.Hover, true);
        Assert.Equal("#222222", resolver.Resolve(button).Get("background"));
        button.SetState(ElementStates.Hover, false);
        Assert.Equal("#111111", resolver.Resolve(button).Get("background"));
    }

    [Fact]
    public void Comma_separated_selectors_share_declarations()
    {
        var root = new EnterpriseElement("Window");
        var a = root.Children.Add("Button");
        var b = root.Children.Add("Label");
        var resolver = new StyleResolver();
        resolver.AddSheet(StyleSheet.Parse("Button, Label { margin: 2; }"));
        Assert.Equal("2", resolver.Resolve(a).Get("margin"));
        Assert.Equal("2", resolver.Resolve(b).Get("margin"));
    }

    [Theory]
    [InlineData("#0a64ad", 255, 10, 100, 173)]
    [InlineData("#fff", 255, 255, 255, 255)]
    [InlineData("#00000080", 128, 0, 0, 0)]
    [InlineData("rgb(1, 2, 3)", 255, 1, 2, 3)]
    [InlineData("rgba(1, 2, 3, 0.5)", 128, 1, 2, 3)]
    [InlineData("transparent", 0, 0, 0, 0)]
    public void Colors_parse(string text, int a, int r, int g, int b)
    {
        Assert.True(StyleColor.TryParse(text, out var color));
        Assert.Equal(new StyleColor((byte)a, (byte)r, (byte)g, (byte)b), color);
    }

    [Theory]
    [InlineData("#12")]
    [InlineData("rgb(1,2)")]
    [InlineData("rgb(300,0,0)")]
    [InlineData("notacolor")]
    [InlineData("")]
    public void Invalid_colors_are_rejected(string text) => Assert.False(StyleColor.TryParse(text, out _));

    [Theory]
    [InlineData("12", 12)]
    [InlineData("12px", 12)]
    [InlineData("12pt", 16)]
    [InlineData("2em", 32)]
    [InlineData("50%", 8)]
    public void Lengths_convert_to_pixels(string text, double pixels)
    {
        Assert.True(StyleLength.TryParse(text, out var length));
        Assert.Equal(pixels, length.ToPixels(16), 6);
    }

    [Theory]
    [InlineData("Button { color: red")]
    [InlineData("Button color: red; }")]
    [InlineData("Button { color red; }")]
    [InlineData("/* open")]
    public void Malformed_sheets_throw(string css) => Assert.Throws<FormatException>(() => StyleSheet.Parse(css));
}
