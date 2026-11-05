using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Theming;
using EnterpriseUI.Theming.Themes;
using Xunit;

namespace EnterpriseUI.Tests.Unit;

public class ThemingTests
{
    [Fact]
    public void IncomingDefinitionsTakePrecedenceOverExternalThemeNames()
    {
        var existing = BuiltInThemes.CreateFluent();
        var document = ThemeDocument.FromTheme(existing);
        document.Tokens[DesignTokens.Primary] = "#123456";
        var result = Assert.Single(ThemeDocument.BuildAll(new[] { document }, new[] { existing }));
        Assert.Equal("#123456", result.GetToken(DesignTokens.Primary));
        Assert.NotEqual(result.GetToken(DesignTokens.Primary), existing.GetToken(DesignTokens.Primary));
    }

    [Fact]
    public void ThemeDocumentsRejectAmbiguousTokenNames()
    {
        var document = new ThemeDocument { Name = "Ambiguous", Tokens = new() { ["primary"] = "#000000", ["PRIMARY"] = "#FFFFFF" } };
        Assert.Throws<InvalidDataException>(() => document.Build());
    }

    [Fact]
    public void DesignerExportPreservesTokenJsonAndImportedBrandMetadata()
    {
        var document = ThemeDocument.FromTheme(BuiltInThemes.CreateStudio());
        document.Version = "2.4.0";
        document.Brand = "Example Brand";
        document.Author = "Example Author";
        document.License = "Apache-2.0";
        var designer = new EnterpriseUI.ThemeDesigner.ThemeDesignerEngine();
        designer.Import(document.ToJson());
        designer.UpdateToken(DesignTokens.Primary, "#123456");
        var result = designer.Export();
        using var tokens = System.Text.Json.JsonDocument.Parse(result.JsonTokens);
        Assert.Equal("#123456", tokens.RootElement.GetProperty(DesignTokens.Primary).GetString());
        var restored = ThemeDocument.FromJson(result.DocumentJson);
        Assert.Equal("2.4.0", restored.Version);
        Assert.Equal("Example Brand", restored.Brand);
        Assert.Equal("Example Author", restored.Author);
        Assert.Equal("Apache-2.0", restored.License);
        Assert.All(designer.ActiveTheme.DesignTokens, token => Assert.Contains(System.Text.Json.JsonSerializer.Serialize(token.Value), result.CSharpBuilderCode));
    }

    [Fact]
    public void DesignerInvalidUpdatesDoNotReplaceTheActiveTheme()
    {
        var designer = new EnterpriseUI.ThemeDesigner.ThemeDesignerEngine();
        var original = designer.ActiveTheme;
        Assert.Throws<InvalidDataException>(() => designer.UpdateToken("invalid;token", "#FFFFFF"));
        Assert.Same(original, designer.ActiveTheme);
    }

    [Fact]
    public void OriginalProfilesHaveDistinctDensityAndOnlyLightDarkVariants()
    {
        var manager = new ThemeManager();
        var names = manager.AvailableThemes.Select(theme => theme.Name).ToList();
        foreach (var name in new[] { "Studio", "ModernOffice", "CompactBusiness" })
        {
            Assert.Contains(manager.AvailableThemes, theme => theme.Name == name + "-Light");
            Assert.Contains(manager.AvailableThemes, theme => theme.Name == name + "-Dark");
            Assert.Equal(names.IndexOf(name + "-Light") + 1, names.IndexOf(name + "-Dark"));
            Assert.DoesNotContain(manager.AvailableThemes, theme => theme.Name == name + "-HighContrast");
            Assert.Throws<KeyNotFoundException>(() => manager.ApplyTheme(name + "-HighContrast"));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => BuiltInThemes.CreateStudio(ColorScheme.HighContrast));
        Assert.Throws<ArgumentOutOfRangeException>(() => BuiltInThemes.CreateModernOffice(ColorScheme.HighContrast));
        Assert.Throws<ArgumentOutOfRangeException>(() => BuiltInThemes.CreateCompactBusiness(ColorScheme.HighContrast));
        Assert.Contains(manager.AvailableThemes, theme => theme.Name == "HighContrast-Black");
        Assert.Contains(manager.AvailableThemes, theme => theme.Name == "HighContrast-White");
        Assert.Equal(20, manager.AvailableThemes.Count);
        Assert.NotEqual(BuiltInThemes.CreateStudio().GetToken(DesignTokens.ControlHeight), BuiltInThemes.CreateCompactBusiness().GetToken(DesignTokens.ControlHeight));
        Assert.NotEqual(BuiltInThemes.CreateStudio().GetToken(DesignTokens.Primary), BuiltInThemes.CreateModernOffice().GetToken(DesignTokens.Primary));
    }

    [Fact]
    public void ThemeDocumentRoundTripsAllTokensAndMetadata()
    {
        var original = ThemeBuilder.Create("Brand").BasedOn(ThemeType.Office).WithColorScheme(ColorScheme.Dark)
            .WithToken("brand-accent", "#123456").WithToken(DesignTokens.ControlHeight, "38px").Build();
        var document = ThemeDocument.FromTheme(original, "2.3.0");
        document.Brand = "Example";
        var restored = ThemeDocument.FromJson(document.ToJson());
        var theme = restored.Build();
        Assert.Equal("2.3.0", restored.Version);
        Assert.Equal("Example", restored.Brand);
        Assert.Equal(original.DesignTokens.Count, theme.DesignTokens.Count);
        Assert.All(original.DesignTokens, token => Assert.Equal(token.Value, theme.GetToken(token.Key)));
    }

    [Fact]
    public void ThemeDocumentsResolveInheritanceAndVariablesAndRejectCycles()
    {
        var parent = new ThemeDocument { Name = "Parent", Tokens = new() { [DesignTokens.Surface] = "#123456" } };
        var child = new ThemeDocument { Name = "Child", BaseTheme = "Parent", Variables = new() { ["accent"] = "#654321" }, Tokens = new() { [DesignTokens.Primary] = "var(--accent)" } };
        var themes = ThemeDocument.BuildAll(new[] { child, parent });
        Assert.Equal("#123456", themes.Single(theme => theme.Name == "Child").GetToken(DesignTokens.Surface));
        Assert.Equal("#654321", themes.Single(theme => theme.Name == "Child").GetToken(DesignTokens.Primary));
        parent.BaseTheme = "Child";
        Assert.Throws<InvalidDataException>(() => ThemeDocument.BuildAll(new[] { child, parent }));
        child.BaseTheme = null;
        child.Variables["accent"] = "var(--accent)";
        Assert.Throws<InvalidDataException>(() => child.Build());
    }

    [Fact]
    public void InvalidImportPreservesTheActiveTheme()
    {
        var manager = new ThemeManager();
        var original = manager.CurrentTheme;
        Assert.Throws<InvalidDataException>(() => manager.ImportTheme("{\"SchemaVersion\":99,\"Name\":\"Invalid\"}", apply: true));
        Assert.Same(original, manager.CurrentTheme);
        Assert.Throws<System.Text.Json.JsonException>(() => ThemeDocument.FromJson("{\"Name\":\"Invalid\",\"Unexpected\":true}"));
    }

    [Fact]
    public async Task FileReloadNotifiesOnlyAfterAValidImport()
    {
        var manager = new ThemeManager();
        var path = Path.GetTempFileName();
        try
        {
            var expected = ThemeBuilder.Create("Reloaded").BasedOn(ThemeType.Fluent).WithToken(DesignTokens.Primary, "#123456").Build();
            await File.WriteAllTextAsync(path, ThemeDocument.FromTheme(expected).ToJson());
            var changes = 0;
            manager.ThemeChanged += (_, _) => changes++;
            await manager.ReloadThemeAsync(path);
            Assert.Equal("Reloaded", manager.CurrentTheme.Name);
            Assert.Equal(1, changes);
            await File.WriteAllTextAsync(path, "{\"SchemaVersion\":99,\"Name\":\"Invalid\"}");
            await Assert.ThrowsAsync<InvalidDataException>(() => manager.ReloadThemeAsync(path));
            Assert.Equal("Reloaded", manager.CurrentTheme.Name);
            Assert.Equal(1, changes);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ThemeTokensAreAnImmutableSnapshot()
    {
        var tokens = new Dictionary<string, string> { [DesignTokens.Primary] = "#123456" };
        var theme = new Theme("Snapshot", ThemeType.Fluent, ColorScheme.Light, tokens);
        tokens[DesignTokens.Primary] = "#FFFFFF";
        Assert.Equal("#123456", theme.GetToken(DesignTokens.Primary));
        var exposed = Assert.IsAssignableFrom<IDictionary<string, string>>(theme.DesignTokens);
        Assert.Throws<NotSupportedException>(() => exposed[DesignTokens.Primary] = "#000000");
    }

    [Fact]
    public void BuilderCanInheritCustomThemeWithoutMutatingItsParent()
    {
        var parent = ThemeBuilder.Create("Parent").BasedOn(ThemeType.Office)
            .WithColorScheme(ColorScheme.Dark).WithToken("brand-accent", "#123456").Build();
        var child = ThemeBuilder.Create("Child").BasedOn(parent)
            .WithToken("brand-accent", "#654321").Build();
        Assert.Equal(parent.Type, child.Type);
        Assert.Equal(parent.Scheme, child.Scheme);
        Assert.Equal(parent.GetToken(DesignTokens.Surface), child.GetToken(DesignTokens.Surface));
        Assert.Equal("#123456", parent.GetToken("brand-accent"));
        Assert.Equal("#654321", child.GetToken("brand-accent"));
    }

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

