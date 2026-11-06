using EnterpriseUI.Abstractions.Controls;
using EnterpriseUI.Core.Mvvm;
using EnterpriseUI.Exporting;

namespace EnterpriseUI.Printing;

public enum PrintScaleMode
{
    /// <summary>Print at 100%; columns that do not fit continue on additional pages.</summary>
    None,
    /// <summary>Shrink so every column fits the page width.</summary>
    FitToWidth,
    /// <summary>Shrink so the whole sheet fits on one page.</summary>
    FitToPage,
    /// <summary>Fixed percentage, see <see cref="PrintPageSettings.ScalePercent"/>.</summary>
    Percent
}

public enum PrintPageOrder
{
    /// <summary>All rows of the left columns first, then the next column block.</summary>
    DownThenOver,
    /// <summary>Each row block across all column blocks, then the next row block.</summary>
    OverThenDown
}

/// <summary>Paper sizes in points (1/72 inch), portrait.</summary>
public static class PrintPaperSizes
{
    private static readonly Dictionary<string, (double Width, double Height)> Sizes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Letter"] = (612, 792),
        ["Legal"] = (612, 1008),
        ["Tabloid"] = (792, 1224),
        ["A3"] = (841.89, 1190.55),
        ["A4"] = (595.28, 841.89),
        ["A5"] = (419.53, 595.28)
    };

    public static IReadOnlyCollection<string> Names => Sizes.Keys;

    public static (double Width, double Height) Get(string name) =>
        Sizes.TryGetValue(name, out var size) ? size : throw new ArgumentException($"Unknown paper size '{name}'.", nameof(name));
}

public class PrintPageSettings
{
    public bool IsLandscape { get; set; }

    /// <summary>Margins in points.</summary>
    public double MarginLeft { get; set; } = 36;
    public double MarginRight { get; set; } = 36;
    public double MarginTop { get; set; } = 36;
    public double MarginBottom { get; set; } = 36;
    public string PaperSize { get; set; } = "Letter";
    public PrintScaleMode ScaleMode { get; set; } = PrintScaleMode.FitToWidth;
    public double ScalePercent { get; set; } = 100;
    public double FontSize { get; set; } = 9;
    public bool RepeatHeaderRows { get; set; } = true;
    public bool ShowGridLines { get; set; } = true;
    public PrintPageOrder PageOrder { get; set; } = PrintPageOrder.DownThenOver;

    /// <summary>Rows beyond this are not printed (the plan reports truncation); use PDF export for larger reports.</summary>
    public int MaxRows { get; set; } = 100_000;

    public double PageWidth => IsLandscape ? PrintPaperSizes.Get(PaperSize).Height : PrintPaperSizes.Get(PaperSize).Width;
    public double PageHeight => IsLandscape ? PrintPaperSizes.Get(PaperSize).Width : PrintPaperSizes.Get(PaperSize).Height;
    public double PrintableWidth => Math.Max(10, PageWidth - MarginLeft - MarginRight);
    public double PrintableHeight => Math.Max(10, PageHeight - MarginTop - MarginBottom);

    public PrintPageSettings Clone() => (PrintPageSettings)MemberwiseClone();
}

/// <summary>Colors and fonts of a printed grid. Colors are #RRGGBB; printing always uses paper colors, independent of the screen theme.</summary>
public sealed class PrintStyle
{
    public string Name { get; set; } = "Standard";
    public string FontFamily { get; set; } = "Segoe UI";
    public string HeaderBackHex { get; set; } = "#F0F0F0";
    public string HeaderForeHex { get; set; } = "#000000";
    public string BandBackHex { get; set; } = "#DCE6F2";
    public string GroupBackHex { get; set; } = "#E9EEF5";
    public string SummaryBackHex { get; set; } = "#F3F3F3";
    public string GridLineHex { get; set; } = "#C8C8C8";
    public string? AlternateRowHex { get; set; }
    public string TextHex { get; set; } = "#000000";

    public static PrintStyle Standard { get; } = new();
    public static PrintStyle Zebra { get; } = new() { Name = "Zebra", AlternateRowHex = "#F7F9FC", HeaderBackHex = "#2F4B6E", HeaderForeHex = "#FFFFFF" };
    public static PrintStyle Minimal { get; } = new() { Name = "Minimal", HeaderBackHex = "#FFFFFF", GroupBackHex = "#FFFFFF", SummaryBackHex = "#FFFFFF", GridLineHex = "#999999", BandBackHex = "#FFFFFF" };
}

/// <summary>Page setup, style and header/footer text bundled under a name. Header and footer text may use {title}, {date}, {page}, {pages}.</summary>
public sealed class PrintTemplate
{
    public string Name { get; set; } = "Standard";
    public string? Title { get; set; }
    public PrintPageSettings Settings { get; set; } = new();
    public PrintStyle Style { get; set; } = PrintStyle.Standard;
    public string? HeaderLeft { get; set; } = "{title}";
    public string? HeaderCenter { get; set; }
    public string? HeaderRight { get; set; } = "{date}";
    public string? FooterLeft { get; set; }
    public string? FooterCenter { get; set; } = "Page {page} of {pages}";
    public string? FooterRight { get; set; }

    public bool HasHeader => !string.IsNullOrEmpty(HeaderLeft) || !string.IsNullOrEmpty(HeaderCenter) || !string.IsNullOrEmpty(HeaderRight);
    public bool HasFooter => !string.IsNullOrEmpty(FooterLeft) || !string.IsNullOrEmpty(FooterCenter) || !string.IsNullOrEmpty(FooterRight);

    public string Resolve(string? text, int page, int pages, DateTime? date = null)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text
            .Replace("{title}", Title ?? string.Empty)
            .Replace("{date}", (date ?? DateTime.Now).ToString("d", System.Globalization.CultureInfo.CurrentCulture))
            .Replace("{page}", page.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Replace("{pages}", pages.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public PrintTemplate Clone() => new()
    {
        Name = Name, Title = Title, Settings = Settings.Clone(), Style = Style,
        HeaderLeft = HeaderLeft, HeaderCenter = HeaderCenter, HeaderRight = HeaderRight,
        FooterLeft = FooterLeft, FooterCenter = FooterCenter, FooterRight = FooterRight
    };

    /// <summary>Maps the setup to the PDF exporter, for "print to PDF" with the same page layout.</summary>
    public ExportPdfOptions ToPdfOptions()
    {
        var size = PrintPaperSizes.Get(Settings.PaperSize);
        return new ExportPdfOptions
        {
            PageWidth = size.Width,
            PageHeight = size.Height,
            Landscape = Settings.IsLandscape,
            Margin = Math.Min(Math.Min(Settings.MarginLeft, Settings.MarginRight), Math.Min(Settings.MarginTop, Settings.MarginBottom)),
            FontSize = Settings.FontSize,
            FitToWidth = Settings.ScaleMode is PrintScaleMode.FitToWidth or PrintScaleMode.FitToPage,
            RepeatHeader = Settings.RepeatHeaderRows,
            HeaderText = HasHeader ? (HeaderCenter ?? HeaderLeft) : null,
            FooterText = HasFooter ? (FooterCenter ?? FooterRight ?? FooterLeft)?.Replace("{pages}", "{pages}") : null
        };
    }

    public static PrintTemplate Standard() => new() { Name = "Standard" };

    public static PrintTemplate LandscapeReport() => new()
    {
        Name = "Landscape report",
        Settings = new PrintPageSettings { IsLandscape = true, ScaleMode = PrintScaleMode.FitToWidth },
        Style = PrintStyle.Zebra
    };

    public static PrintTemplate Compact() => new()
    {
        Name = "Compact",
        Settings = new PrintPageSettings { FontSize = 7, MarginLeft = 24, MarginRight = 24, MarginTop = 24, MarginBottom = 24, PaperSize = "A4" },
        Style = PrintStyle.Minimal,
        HeaderLeft = null,
        HeaderRight = null
    };
}

/// <summary>Named print templates that applications and users can add to.</summary>
public sealed class PrintTemplateRegistry
{
    private readonly Dictionary<string, PrintTemplate> _templates = new(StringComparer.OrdinalIgnoreCase);

    public static PrintTemplateRegistry Default { get; } = CreateDefault();

    public IReadOnlyCollection<string> Names => _templates.Keys;

    public void Register(PrintTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        _templates[template.Name] = template;
    }

    public PrintTemplate? Find(string name) => _templates.TryGetValue(name, out var template) ? template.Clone() : null;

    private static PrintTemplateRegistry CreateDefault()
    {
        var registry = new PrintTemplateRegistry();
        registry.Register(PrintTemplate.Standard());
        registry.Register(PrintTemplate.LandscapeReport());
        registry.Register(PrintTemplate.Compact());
        return registry;
    }
}

/// <summary>A row positioned on a page, with its y offset from the top of the table area in points.</summary>
public sealed record PlacedRow(ExportRow Row, double Y, double Height);

public sealed class PrintPage
{
    public PrintPage(int number, ExportSheet sheet, int firstColumn, int columnCount, double scale, bool showHeader, IReadOnlyList<PlacedRow> rows)
    {
        Number = number;
        Sheet = sheet;
        FirstColumn = firstColumn;
        ColumnCount = columnCount;
        Scale = scale;
        ShowHeader = showHeader;
        Rows = rows;
    }

    /// <summary>1-based page number across the whole job.</summary>
    public int Number { get; }
    public ExportSheet Sheet { get; }

    /// <summary>First and count of the columns on this page (columns can span several pages when scaling is off).</summary>
    public int FirstColumn { get; }
    public int ColumnCount { get; }
    public double Scale { get; }
    public bool ShowHeader { get; }
    public IReadOnlyList<PlacedRow> Rows { get; }
}

public sealed class PrintPlan
{
    public PrintPlan(IReadOnlyList<PrintPage> pages, PrintTemplate template, ExportDocument document, bool truncated, long totalRows)
    {
        Pages = pages;
        Template = template;
        Document = document;
        Truncated = truncated;
        TotalRows = totalRows;
    }

    public IReadOnlyList<PrintPage> Pages { get; }
    public PrintTemplate Template { get; }
    public ExportDocument Document { get; }

    /// <summary>True when rows beyond <see cref="PrintPageSettings.MaxRows"/> were left out.</summary>
    public bool Truncated { get; }
    public long TotalRows { get; }
    public int PageCount => Pages.Count;
}

/// <summary>
/// Splits an export document into printable pages: scaling, repeated header rows, multi-page columns and page order.
/// The result is plain geometry in points so any platform can render it (GDI+ print preview, PDF, image).
/// </summary>
public static class PrintPaginator
{
    private const double MinScale = 0.2;
    private const double PixelsToPoints = 0.75;

    public static double BandHeight(PrintTemplate template, bool header, double scale = 1) =>
        (header ? template.HasHeader : template.HasFooter) ? template.Settings.FontSize * scale * 2.2 : 0;

    public static double RowHeight(PrintPageSettings settings, double scale) => settings.FontSize * 1.7 * scale;

    public static IReadOnlyList<double> ColumnWidths(ExportSheet sheet, double scale) =>
        sheet.Columns.Select(c => Math.Max(24, c.Width * PixelsToPoints) * scale).ToList();

    public static PrintPlan Paginate(ExportDocument document, PrintTemplate? template = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        template ??= PrintTemplate.Standard();
        template.Title ??= document.Title;
        var settings = template.Settings;

        var pages = new List<PrintPage>();
        long totalRows = 0;
        var truncated = false;

        foreach (var sheet in document.Sheets)
        {
            var rows = new List<ExportRow>();
            foreach (var row in sheet.Rows)
            {
                if (rows.Count >= settings.MaxRows)
                {
                    truncated = true;
                    break;
                }
                rows.Add(row);
            }
            totalRows += rows.Count;
            rows.AddRange(sheet.Footer);

            PaginateSheet(sheet, rows, template, pages);
        }

        if (pages.Count == 0)
        {
            var empty = new ExportSheet("Empty", Array.Empty<ExportColumn>(), Array.Empty<ExportRow>());
            pages.Add(new PrintPage(1, empty, 0, 0, 1, false, Array.Empty<PlacedRow>()));
        }

        return new PrintPlan(pages, template, document, truncated, totalRows);
    }

    private static void PaginateSheet(ExportSheet sheet, List<ExportRow> rows, PrintTemplate template, List<PrintPage> pages)
    {
        var settings = template.Settings;
        var headerLevels = sheet.Columns.Count == 0 ? 0 : sheet.Columns.Max(c => c.Bands.Count) + 1;
        var contentHeight = settings.PrintableHeight - BandHeight(template, true) - BandHeight(template, false);

        var baseWidths = ColumnWidths(sheet, 1);
        var totalWidth = baseWidths.Sum();
        var scale = settings.ScaleMode switch
        {
            PrintScaleMode.Percent => Math.Clamp(settings.ScalePercent / 100, MinScale, 4),
            PrintScaleMode.FitToWidth or PrintScaleMode.FitToPage => totalWidth <= 0 ? 1 : Math.Clamp(settings.PrintableWidth / totalWidth, MinScale, 1),
            _ => 1
        };

        if (settings.ScaleMode == PrintScaleMode.FitToPage)
        {
            var height = (rows.Count + headerLevels) * RowHeight(settings, 1);
            if (height * scale > contentHeight) scale = Math.Clamp(Math.Min(scale, contentHeight / height), MinScale, 1);
        }

        var rowHeight = RowHeight(settings, scale);
        var widths = baseWidths.Select(w => w * scale).ToList();
        var slices = ColumnSlices(widths, settings.PrintableWidth);

        var firstCapacity = Math.Max(1, (int)Math.Floor(contentHeight / rowHeight) - headerLevels);
        var nextCapacity = Math.Max(1, (int)Math.Floor(contentHeight / rowHeight) - (settings.RepeatHeaderRows ? headerLevels : 0));

        var blocks = new List<(int Start, int Count)>();
        if (rows.Count == 0) blocks.Add((0, 0));
        for (var start = 0; start < rows.Count;)
        {
            var capacity = blocks.Count == 0 ? firstCapacity : nextCapacity;
            var count = Math.Min(capacity, rows.Count - start);
            blocks.Add((start, count));
            start += count;
        }

        IEnumerable<(int Slice, int Block)> Order()
        {
            if (settings.PageOrder == PrintPageOrder.DownThenOver)
            {
                for (var s = 0; s < slices.Count; s++)
                    for (var b = 0; b < blocks.Count; b++)
                        yield return (s, b);
            }
            else
            {
                for (var b = 0; b < blocks.Count; b++)
                    for (var s = 0; s < slices.Count; s++)
                        yield return (s, b);
            }
        }

        foreach (var (sliceIndex, blockIndex) in Order())
        {
            var slice = slices[sliceIndex];
            var block = blocks[blockIndex];
            var showHeader = settings.RepeatHeaderRows || blockIndex == 0;
            var y = showHeader ? headerLevels * rowHeight : 0;
            var placed = new List<PlacedRow>(block.Count);
            for (var i = 0; i < block.Count; i++)
            {
                placed.Add(new PlacedRow(rows[block.Start + i], y, rowHeight));
                y += rowHeight;
            }

            pages.Add(new PrintPage(pages.Count + 1, sheet, slice.Start, slice.Count, scale, showHeader && headerLevels > 0, placed));
        }
    }

    /// <summary>Greedy column blocks that each fit the printable width; a single over-wide column gets its own block.</summary>
    public static IReadOnlyList<(int Start, int Count)> ColumnSlices(IReadOnlyList<double> widths, double printableWidth)
    {
        var slices = new List<(int, int)>();
        if (widths.Count == 0)
        {
            slices.Add((0, 0));
            return slices;
        }

        var start = 0;
        double used = 0;
        for (var i = 0; i < widths.Count; i++)
        {
            if (i > start && used + widths[i] > printableWidth + 0.01)
            {
                slices.Add((start, i - start));
                start = i;
                used = 0;
            }
            used += widths[i];
        }
        slices.Add((start, widths.Count - start));
        return slices;
    }
}

public sealed class PrintRequestedEventArgs : EventArgs
{
    public PrintRequestedEventArgs(IUIComponent component, PrintTemplate template)
    {
        Component = component;
        Template = template;
    }

    public IUIComponent Component { get; }
    public PrintTemplate Template { get; }

    /// <summary>Set by the platform handler that took over the job.</summary>
    public bool Handled { get; set; }
}

/// <summary>Print orchestration. Platform layers subscribe to <see cref="PrintRequested"/> to render a component; settings and templates are shared.</summary>
public sealed class PrintService : ObservableObject
{
    private static readonly Lazy<PrintService> _instance = new(() => new PrintService());
    public static PrintService Instance => _instance.Value;

    public PrintPageSettings CurrentSettings { get; set; } = new();
    public PrintTemplateRegistry Templates { get; } = PrintTemplateRegistry.Default;

    public event EventHandler<PrintRequestedEventArgs>? PrintRequested;

    /// <summary>Asks the platform layer to print the component. Throws when no handler took the job, instead of silently doing nothing.</summary>
    public Task PrintComponentAsync(IUIComponent component, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(component);
        cancellationToken.ThrowIfCancellationRequested();

        var template = PrintTemplate.Standard();
        template.Settings = CurrentSettings.Clone();
        var args = new PrintRequestedEventArgs(component, template);
        PrintRequested?.Invoke(this, args);
        return args.Handled ? Task.CompletedTask : Task.FromException(new InvalidOperationException("No print handler is registered for this component."));
    }

    /// <summary>Builds the page plan for a document.</summary>
    public PrintPlan CreatePlan(ExportDocument document, PrintTemplate? template = null)
    {
        var effectiveTemplate = template ?? PrintTemplate.Standard();
        if (template == null) effectiveTemplate.Settings = CurrentSettings.Clone();
        return PrintPaginator.Paginate(document, effectiveTemplate);
    }
}
