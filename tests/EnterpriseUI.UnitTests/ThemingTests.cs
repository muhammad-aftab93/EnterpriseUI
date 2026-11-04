using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Theming;
using EnterpriseUI.Theming.Themes;
using Xunit;

namespace EnterpriseUI.Tests.Unit;

public class ThemingTests
{
    [Fact]
    public void BuiltInThemes_CreateFluent_HasRequiredTokens()
    {
        var light = BuiltInThemes.CreateFluent(ColorScheme.Light);
        var dark = BuiltInThemes.CreateFluent(ColorScheme.Dark);

        Assert.Equal("Fluent-Light", light.Name);
        Assert.Equal("Fluent-Dark", dark.Name);
        Assert.NotEmpty(light.GetToken(DesignTokens.Primary));
        Assert.NotEmpty(dark.GetToken(DesignTokens.Primary));
        Assert.NotEqual(light.GetToken(DesignTokens.Background), dark.GetToken(DesignTokens.Background));
    }

    [Fact]
    public void Theme_ToCssVariables_OutputsValidCssBlock()
    {
        var theme = BuiltInThemes.CreateBootstrap(ColorScheme.Light);
        var css = theme.ToCssVariables("eui");

        Assert.Contains(":root[data-theme=\"bootstrap-light\"] {", css);
        Assert.Contains("--eui-primary: #0d6efd;", css);
        Assert.EndsWith("}\n", css);
    }

    [Fact]
    public void ThemeBuilder_BuildsCustomThemeWithOverrides()
    {
        var customTheme = ThemeBuilder.Create("CorporateBrand")
            .BasedOn(ThemeType.Fluent)
            .WithColorScheme(ColorScheme.Light)
            .WithPrimaryColor("#ff6600")
            .WithBorderRadius("10px")
            .Build();

        Assert.Equal("CorporateBrand", customTheme.Name);
        Assert.Equal("#ff6600", customTheme.GetToken(DesignTokens.Primary));
        Assert.Equal("10px", customTheme.GetToken(DesignTokens.BorderRadius));
        Assert.NotEmpty(customTheme.GetToken(DesignTokens.FontFamily)); // Inherited from Fluent
    }

    [Fact]
    public void ThemeManager_SwitchTheme_NotifiesSubscribers()
    {
        var manager = new ThemeManager();
        ITheme? changedTheme = null;
        manager.ThemeChanged += (_, theme) => changedTheme = theme;

        manager.ApplyTheme("Material-Dark");

        Assert.NotNull(changedTheme);
        Assert.Equal("Material-Dark", changedTheme!.Name);
        Assert.Equal(ColorScheme.Dark, changedTheme.Scheme);
    }
}

