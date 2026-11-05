using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Data;
using EnterpriseUI.Exporting;
using EnterpriseUI.Grids;
using EnterpriseUI.Printing;
using EnterpriseUI.Theming;
using EnterpriseUI.Theming.Themes;
using Xunit;
using static EnterpriseUI.Tests.Unit.GridTestSupport;

namespace EnterpriseUI.Tests.Unit;

public class PrintingTests
{
    private static ExportDocument Document(int rows, int columns = 4, double width = 100)
    {
        var cols = Enumerable.Range(0, columns).Select(i => new ExportColumn($"C{i}", $"Col {i}") { Width = width }).ToList();
        var data = Enumerable.Range(0, rows).Select(r => new ExportRow(ExportRowKind.Data, 0, Enumerable.Range(0, columns).Select(c => (object?)$"{r}-{c}").ToArray()));
        var document = new ExportDocument { Title = "Print test" };
        document.Sheets.Add(new ExportSheet("Sheet", cols, data));
        return document;
    }

    [Theory]
    [InlineData("Letter", false, 612, 792)]
    [InlineData("A4", false, 595.28, 841.89)]
    [InlineData("A4", true, 841.89, 595.28)]
    [InlineData("Legal", true, 1008, 612)]
    public void PaperSizesSwapForLandscape(string paper, bool landscape, double width, double height)
    {
        var settings = new PrintPageSettings { PaperSize = paper, IsLandscape = landscape };

        Assert.Equal(width, settings.PageWidth, 2);
        Assert.Equal(height, settings.PageHeight, 2);
        Assert.Equal(width - 72, settings.PrintableWidth, 2);
        Assert.Throws<ArgumentException>(() => PrintPaperSizes.Get("Poster"));
    }

    [Fact]
    public void RowsFlowAcrossPagesWithRepeatedHeadersAndSequentialPositions()
    {
        var template = PrintTemplate.Standard();
        var plan = PrintPaginator.Paginate(Document(200), template);

        Assert.True(plan.PageCount > 3);
        Assert.Equal(200, plan.Pages.Sum(p => p.Rows.Count));
        Assert.All(plan.Pages, p => Assert.True(p.ShowHeader));
        Assert.Equal(Enumerable.Range(1, plan.PageCount), plan.Pages.Select(p => p.Number));

        var page = plan.Pages[0];
        Assert.True(page.Rows[0].Y > 0);
        for (var i = 1; i < page.Rows.Count; i++)
        {
            Assert.Equal(page.Rows[i - 1].Y + page.Rows[i - 1].Height, page.Rows[i].Y, 6);
        }

        var limit = template.Settings.PrintableHeight - PrintPaginator.BandHeight(template, true) - PrintPaginator.BandHeight(template, false);
        Assert.All(plan.Pages, p => Assert.True(p.Rows.Count == 0 || p.Rows[^1].Y + p.Rows[^1].Height <= limit + 0.01));
    }

    [Fact]
    public void HeaderRowsCanBePrintedOnTheFirstPageOnly()
    {
        var template = PrintTemplate.Standard();
        template.Settings.RepeatHeaderRows = false;

        var plan = PrintPaginator.Paginate(Document(200), template);

        Assert.True(plan.Pages[0].ShowHeader);
        Assert.All(plan.Pages.Skip(1), p => Assert.False(p.ShowHeader));
        Assert.True(plan.Pages[1].Rows.Count > plan.Pages[0].Rows.Count);
        Assert.Equal(0, plan.Pages[1].Rows[0].Y);
    }

    [Fact]
    public void FitToWidthShrinksColumnsAndNoScalingSplitsThemAcrossPages()
    {
        var wide = Document(5, columns: 12, width: 160);

        var fit = PrintPaginator.Paginate(wide, PrintTemplate.Standard());
        Assert.Equal(1, fit.PageCount);
        Assert.InRange(fit.Pages[0].Scale, 0.2, 0.99);
        Assert.Equal(12, fit.Pages[0].ColumnCount);

        var template = PrintTemplate.Standard();
        template.Settings.ScaleMode = PrintScaleMode.None;
        var split = PrintPaginator.Paginate(wide, template);
        Assert.True(split.PageCount > 1);
        Assert.All(split.Pages, p => Assert.Equal(1, p.Scale));
        Assert.Equal(12, split.Pages.Select(p => (p.FirstColumn, p.ColumnCount)).Distinct().Sum(s => s.ColumnCount));
        Assert.Equal(0, split.Pages[0].FirstColumn);
        Assert.Equal(split.Pages[0].ColumnCount, split.Pages[1].FirstColumn);

        var widths = PrintPaginator.ColumnWidths(wide.Sheets[0], 1);
        Assert.All(PrintPaginator.ColumnSlices(widths, template.Settings.PrintableWidth), s => Assert.True(widths.Skip(s.Start).Take(s.Count).Sum() <= template.Settings.PrintableWidth + 0.01 || s.Count == 1));
    }

    [Fact]
    public void PageOrderControlsHowColumnBlocksAndRowBlocksInterleave()
    {
        var template = PrintTemplate.Standard();
        template.Settings.ScaleMode = PrintScaleMode.None;
        template.Settings.PrintableHeightForTest();
        var document = Document(120, columns: 10, width: 140);

        template.Settings.PageOrder = PrintPageOrder.DownThenOver;
        var down = PrintPaginator.Paginate(document, template);
        var downSlices = down.Pages.Select(p => p.FirstColumn).ToList();
        Assert.Equal(downSlices.OrderBy(x => x), downSlices);

        template.Settings.PageOrder = PrintPageOrder.OverThenDown;
        var over = PrintPaginator.Paginate(document, template);
        Assert.Equal(down.PageCount, over.PageCount);
        Assert.NotEqual(downSlices, over.Pages.Select(p => p.FirstColumn).ToList());
        Assert.Equal(0, over.Pages[0].FirstColumn);
        Assert.NotEqual(0, over.Pages[1].FirstColumn);
    }

    [Fact]
    public void FitToPagePutsTheWholeSheetOnOnePage()
    {
        var template = PrintTemplate.Standard();
        template.Settings.ScaleMode = PrintScaleMode.FitToPage;

        var plan = PrintPaginator.Paginate(Document(150), template);

        Assert.Equal(1, plan.PageCount);
        Assert.True(plan.Pages[0].Scale < 1);
        Assert.Equal(150, plan.Pages[0].Rows.Count);
    }

    [Fact]
    public void PercentScalingAndMaxRowsAreApplied()
    {
        var template = PrintTemplate.Standard();
        template.Settings.ScaleMode = PrintScaleMode.Percent;
        template.Settings.ScalePercent = 50;
        template.Settings.MaxRows = 40;

        var plan = PrintPaginator.Paginate(Document(100), template);

        Assert.All(plan.Pages, p => Assert.Equal(0.5, p.Scale));
        Assert.True(plan.Truncated);
        Assert.Equal(40, plan.TotalRows);
        Assert.Equal(40, plan.Pages.Sum(p => p.Rows.Count));
    }

    [Fact]
    public void FooterRowsFollowDataAndMultipleSheetsContinuePageNumbers()
    {
        var document = Document(3);
        document.Sheets[0].Footer = new[] { new ExportRow(ExportRowKind.Summary, 0, new object?[] { "Total", null, null, null }) };
        document.Sheets.Add(document.Sheets[0] is { } s ? new ExportSheet("Second", s.Columns, new[] { new ExportRow(ExportRowKind.Data, 0, new object?[] { "x", "y", "z", "w" }) }) : throw new InvalidOperationException());

        var plan = PrintPaginator.Paginate(document, PrintTemplate.Standard());

        Assert.Equal(2, plan.PageCount);
        Assert.Equal(ExportRowKind.Summary, plan.Pages[0].Rows[^1].Row.Kind);
        Assert.Equal(new[] { 1, 2 }, plan.Pages.Select(p => p.Number));
        Assert.Equal("Second", plan.Pages[1].Sheet.Name);
        Assert.Equal(4, plan.TotalRows);

        Assert.Equal(1, PrintPaginator.Paginate(new ExportDocument(), PrintTemplate.Standard()).PageCount);
    }

    [Fact]
    public void BandedHeadersReserveExtraHeaderRows()
    {
        var document = Document(150);
        document.Sheets[0].Columns[0].Bands = new[] { "Order" };
        document.Sheets[0].Columns[1].Bands = new[] { "Order" };

        var banded = PrintPaginator.Paginate(document, PrintTemplate.Standard());
        var plain = PrintPaginator.Paginate(Document(150), PrintTemplate.Standard());

        Assert.True(banded.Pages[0].Rows[0].Y > plain.Pages[0].Rows[0].Y);
        Assert.True(banded.Pages[0].Rows.Count < plain.Pages[0].Rows.Count);
    }

    [Fact]
    public void TemplatesResolveTokensAndMapToPdfOptions()
    {
        var template = PrintTemplate.LandscapeReport();
        template.Title = "Orders";

        Assert.Equal("Orders", template.Resolve("{title}", 1, 1));
        Assert.Equal("Page 3 of 9", template.Resolve("Page {page} of {pages}", 3, 9));
        Assert.Equal(string.Empty, template.Resolve(null, 1, 1));
        Assert.Contains(new DateTime(2024, 5, 6).ToString("d", System.Globalization.CultureInfo.CurrentCulture), template.Resolve("{date}", 1, 1, new DateTime(2024, 5, 6)));

        var pdf = template.ToPdfOptions();
        Assert.True(pdf.Landscape);
        Assert.Equal(612, pdf.PageWidth);
        Assert.True(pdf.FitToWidth);
        Assert.Equal("Page {page} of {pages}", pdf.FooterText);

        var registry = PrintTemplateRegistry.Default;
        Assert.Contains("Compact", registry.Names);
        Assert.Equal(7, registry.Find("compact")!.Settings.FontSize);
        Assert.Null(registry.Find("missing"));
        registry.Find("Compact")!.Settings.FontSize = 20;
        Assert.Equal(7, registry.Find("Compact")!.Settings.FontSize);
    }

    [Fact]
    public async Task PrintExportUsesTheTemplatePageSetupForPdf()
    {
        var (grid, _) = (OrderGrid(Orders(60)), 0);
        await grid.Loaded();
        var template = PrintTemplate.LandscapeReport();
        template.Title = "Orders";
        var document = await GridExporter.CreateDocumentAsync(grid);

        using var stream = new MemoryStream();
        var options = new ExportOptions { Pdf = template.ToPdfOptions() };
        await ExportService.Default.ExportAsync(document, "pdf", stream, options);

        var (pages, _, raw) = ExportTestSupport.InspectPdf(stream.ToArray());
        Assert.Contains("/MediaBox [0 0 792 612]", raw);
        Assert.True(pages >= 1);
    }

    [Fact]
    public async Task ServiceRequiresAHandlerAndPlansGridDocuments()
    {
        var service = PrintService.Instance;
        var grid = OrderGrid(Orders());
        await grid.Loaded();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PrintComponentAsync(grid));

        PrintTemplate? seen = null;
        void Handler(object? sender, PrintRequestedEventArgs e) { seen = e.Template; e.Handled = true; }
        service.PrintRequested += Handler;
        try
        {
            await service.PrintComponentAsync(grid);
            Assert.NotNull(seen);
        }
        finally
        {
            service.PrintRequested -= Handler;
        }

        var plan = service.CreatePlan(await GridExporter.CreateDocumentAsync(grid, new GridExportOptions { Title = "Orders" }));
        Assert.Equal(1, plan.PageCount);
        Assert.Equal("Orders", plan.Template.Title);
        Assert.Equal(6, plan.TotalRows);
    }
}

internal static class PrintSettingsTestExtensions
{
    /// <summary>Documents intent in tests: the default printable height is used as is.</summary>
    public static void PrintableHeightForTest(this PrintPageSettings settings) => Assert.True(settings.PrintableHeight > 100);
}

public class GridAccessibilityTests
{
    public static IEnumerable<object[]> AllThemes()
    {
        foreach (var scheme in new[] { ColorScheme.Light, ColorScheme.Dark })
        {
            yield return new object[] { BuiltInThemes.CreateFluent(scheme) };
            yield return new object[] { BuiltInThemes.CreateOffice(scheme) };
            yield return new object[] { BuiltInThemes.CreateMaterial(scheme) };
            yield return new object[] { BuiltInThemes.CreateBootstrap(scheme) };
            yield return new object[] { BuiltInThemes.CreateModern(scheme) };
            yield return new object[] { BuiltInThemes.CreateWindows(scheme) };
            yield return new object[] { BuiltInThemes.CreateDefault(scheme) };
        }
        yield return new object[] { BuiltInThemes.CreateHighContrast(true) };
        yield return new object[] { BuiltInThemes.CreateHighContrast(false) };
    }

    [Theory]
    [MemberData(nameof(AllThemes))]
    public void EveryBuiltInThemeMeetsWcagContrastForTheGridPalette(ITheme theme)
    {
        var failures = GridAccessibility.Audit(theme).Where(c => !c.Passes).Select(c => $"{c.Name}: {c.Ratio:0.00} < {c.Required}").ToList();

        Assert.True(failures.Count == 0, $"{theme.Name}: {string.Join("; ", failures)}");
    }

    [Fact]
    public void HighContrastThemesUseSolidSelectionAndStrongBorders()
    {
        var palette = GridPalette.FromTheme(BuiltInThemes.CreateHighContrast(true));

        Assert.True(palette.HighContrast);
        Assert.Equal(palette.Primary, palette.SelectionBack);
        Assert.Equal(palette.Text, palette.Border);
        Assert.Equal(palette.Text, palette.FocusRing);
        Assert.True(GridPalette.ContrastRatio(palette.Text, palette.Surface) >= 7);

        var light = GridPalette.FromTheme(BuiltInThemes.CreateFluent(ColorScheme.Light));
        Assert.False(light.HighContrast);
        Assert.NotEqual(light.Primary, light.SelectionBack);
    }

    [Fact]
    public void ThemeSwitchChangesPaletteWithoutAnyFeatureSpecificColors()
    {
        var fluentLight = GridPalette.FromTheme(BuiltInThemes.CreateFluent(ColorScheme.Light));
        var fluentDark = GridPalette.FromTheme(BuiltInThemes.CreateFluent(ColorScheme.Dark));

        Assert.True(GridPalette.Luminance(fluentLight.Surface) > GridPalette.Luminance(fluentDark.Surface));
        Assert.True(GridPalette.Luminance(fluentDark.Text) > GridPalette.Luminance(fluentDark.Surface));
        Assert.NotEqual(fluentLight.ErrorBack, fluentDark.ErrorBack);
        Assert.NotEqual(fluentLight.HighlightBack, fluentDark.HighlightBack);
    }

    [Theory]
    [InlineData("rgba(0, 0, 0, 0.08)", "#FFFFFF", "#EBEBEB")]
    [InlineData("rgba(255,255,255,0.5)", "#000000", "#808080")]
    [InlineData("#abc", "#FFFFFF", "#AABBCC")]
    [InlineData("#112233", "#FFFFFF", "#112233")]
    [InlineData("#00000080", "#FFFFFF", "#7F7F7F")]
    [InlineData("rgb(10, 20, 30)", "#FFFFFF", "#0A141E")]
    public void ColorTokensParseHexAndCssFunctions(string token, string background, string expected)
    {
        Assert.True(GridPalette.TryParse(token, background, out var hex));
        Assert.Equal(expected, hex, ignoreCase: true);
    }

    [Theory]
    [InlineData("")]
    [InlineData("blue")]
    [InlineData("#12345")]
    [InlineData("rgba(1,2)")]
    [InlineData("hsl(0, 0%, 0%)")]
    public void UnsupportedColorTokensAreRejected(string token)
    {
        Assert.False(GridPalette.TryParse(token, "#FFFFFF", out _));
    }

    [Fact]
    public void ContrastMathMatchesWcagReferenceValues()
    {
        Assert.Equal(21, GridPalette.ContrastRatio("#000000", "#FFFFFF"), 2);
        Assert.Equal(1, GridPalette.ContrastRatio("#777777", "#777777"), 2);
        Assert.Equal(4.48, GridPalette.ContrastRatio("#777777", "#FFFFFF"), 2);
        Assert.Equal("#000000", GridPalette.BestTextOn("#FFFF00"));
        Assert.Equal("#FFFFFF", GridPalette.BestTextOn("#000080"));

        var fixedUp = GridPalette.EnsureContrast("#999999", "#FFFFFF", 4.5);
        Assert.True(GridPalette.ContrastRatio(fixedUp, "#FFFFFF") >= 4.5);
        Assert.Equal("#101010", GridPalette.EnsureContrast("#101010", "#FFFFFF", 4.5));
        Assert.Equal("#808080", GridPalette.Blend("#000000", "#FFFFFF", 0.5), ignoreCase: true);
    }

    [Fact]
    public async Task ScreenReaderDescriptionsReflectGridState()
    {
        var grid = OrderGrid(Orders(25));
        grid.PagingMode = GridPagingMode.Client;
        grid.PageSize = 10;
        grid.EditMode = GridEditMode.InCell;
        grid.SelectionMode = GridSelectionMode.MultipleRows;
        grid.AriaLabel = "Orders";
        await grid.Loaded();
        grid.CycleSort("Quantity");
        grid.FilterBy("Region", FilterOperator.IsEqualTo, "EMEA");
        grid.GoToPage(1);

        var description = GridAccessibility.DescribeGrid(grid);
        Assert.StartsWith("Orders, 13 rows, 7 columns", description);
        Assert.Contains("sorted by Quantity ascending", description);
        Assert.Contains("1 filter applied", description);
        Assert.Contains("page 2 of 2", description);

        grid.GoToPage(0);
        grid.Selection.Click(0, 1);
        grid.TryGetRowAt(0, out var row);
        var quantity = grid.FindColumn("Quantity")!;
        var cell = GridAccessibility.DescribeCell(grid, row, quantity, 5, 7);
        Assert.Equal("Quantity, 1, row 1, column 6 of 7, selected", cell);
        Assert.StartsWith("Row 1 of 10, selected", GridAccessibility.DescribeRow(grid, row));
        Assert.Contains("sorted ascending", GridAccessibility.DescribeColumnHeader(grid, quantity));
        Assert.Contains("filtered", GridAccessibility.DescribeColumnHeader(grid, grid.FindColumn("Region")!));

        grid.Columns.First(c => c.Field == "Status").ValidationRules.Add(new RequiredRule { Message = "Status needed" });
        var order = row.Item!;
        grid.EditCell(order, "Status", "");
        var status = GridAccessibility.DescribeCell(grid, row, grid.FindColumn("Status")!, 3, 7);
        Assert.Contains("error: Status needed", status);
        Assert.Contains("1 validation error", GridAccessibility.DescribeGrid(grid));
    }

    [Fact]
    public async Task GroupAndDetailRowsAreAnnouncedWithTheirStructure()
    {
        var grid = OrderGrid(Orders());
        await grid.Loaded();
        grid.GroupBy("Region");
        grid.TryGetRowAt(0, out var header);
        grid.CollapseAllGroups();
        grid.TryGetRowAt(0, out var collapsed);

        Assert.Contains("group level 1, expanded", GridAccessibility.DescribeRow(grid, header));
        Assert.Contains("collapsed", GridAccessibility.DescribeRow(grid, collapsed));
        Assert.Contains("grouped by Region", GridAccessibility.DescribeGrid(grid));
    }

    [Fact]
    public void KeyboardMapDocumentsTheNavigationModel()
    {
        var map = GridAccessibility.KeyboardMap;

        Assert.Contains(map, k => k.Keys.Contains("Ctrl+Z"));
        Assert.Contains(map, k => k.Keys.Contains("Page Up"));
        Assert.Contains(map, k => k.Action.Contains("search", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(map.Count, map.Select(k => k.Keys).Distinct().Count());
    }
}

public class GridLayoutPersistenceTests
{
    [Fact]
    public async Task LayoutProfilesRoundTripThroughJsonIncludingQueryState()
    {
        var grid = OrderGrid(Orders());
        await grid.Loaded();
        grid.PinColumn("Id", ColumnPinPosition.Left);
        grid.ResizeColumn("Customer", 222);
        grid.SortBy("Quantity", ListSortDirection.Descending);
        grid.GroupBy("Region");
        grid.FilterBy("Customer", FilterOperator.Contains, "Customer 1");
        var profile = grid.CreateLayoutProfile("mine");

        var json = GridLayoutSerializer.ToJson(profile);
        var restored = GridLayoutSerializer.FromJson(json);

        Assert.Contains("\"Left\"", json);
        Assert.Equal("mine", restored.ProfileName);
        Assert.Equal(222, restored.Columns.First(c => c.Field == "Customer").Width);
        Assert.Equal(ColumnPinPosition.Left, restored.Columns.First(c => c.Field == "Id").PinPosition);
        Assert.Equal(ListSortDirection.Descending, restored.Sorts.Single().Direction);
        Assert.Equal("Region", restored.Groups.Single().Field);
        Assert.NotNull(restored.FiltersJson);

        var other = OrderGrid(Orders());
        await other.Loaded();
        Assert.Empty(other.ApplyLayoutProfile(restored));
        Assert.Equal("Quantity", other.CurrentRequest.Sorts.Single().Member);
        Assert.Single(other.CurrentRequest.Filters);
        Assert.Equal(222, other.FindColumn("Customer")!.Width);

        Assert.Throws<ArgumentException>(() => GridLayoutSerializer.FromJson(" "));
        Assert.Throws<System.Text.Json.JsonException>(() => GridLayoutSerializer.FromJson("null"));
    }

    [Fact]
    public async Task LayoutStoreSwitchesBetweenNamedProfilesAndPersistsToFile()
    {
        var grid = OrderGrid(Orders());
        await grid.Loaded();
        var store = new GridLayoutStore();

        store.Save(grid, "default");
        grid.SetColumnVisibility("Status", false);
        grid.SortBy("Id", ListSortDirection.Descending);
        store.Save(grid, "compact");

        store.Apply(grid, "default");
        Assert.True(grid.FindColumn("Status")!.Visible);
        Assert.Empty(grid.CurrentRequest.Sorts);

        var path = Path.Combine(Path.GetTempPath(), $"layouts-{Guid.NewGuid():N}.json");
        try
        {
            store.SaveToFile(path);
            var loaded = new GridLayoutStore();
            loaded.LoadFromFile(path);
            Assert.Equal(new[] { "compact", "default" }, loaded.Names);

            loaded.Apply(grid, "compact");
            Assert.False(grid.FindColumn("Status")!.Visible);
            Assert.Equal(ListSortDirection.Descending, grid.CurrentRequest.Sorts.Single().SortDirection);
            Assert.Throws<KeyNotFoundException>(() => loaded.Apply(grid, "nope"));
            Assert.True(loaded.Remove("compact"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ApplyingAProfileWithUnknownColumnsReportsWarningsInsteadOfFailing()
    {
        var grid = OrderGrid(Orders());
        await grid.Loaded();
        var profile = new ColumnLayoutProfile();
        profile.Sorts.Add(new ColumnSortSnapshot { Field = "Gone", Direction = ListSortDirection.Ascending });
        profile.Groups.Add(new ColumnGroupSnapshot { Field = "Missing" });
        profile.FiltersJson = EnterpriseUI.Data.FilterSerializer.Serialize(new EnterpriseUI.Data.IFilterDescriptor[]
        {
            new EnterpriseUI.Data.CustomFilterDescriptor("Id", "Unregistered operator", _ => true)
        });

        var warnings = grid.ApplyLayoutProfile(profile);

        Assert.Equal(3, warnings.Count);
        Assert.Empty(grid.CurrentRequest.Sorts);
        Assert.Equal(6, grid.TotalItemsCount);
    }
}
