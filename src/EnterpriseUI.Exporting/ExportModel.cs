using System.Globalization;

namespace EnterpriseUI.Exporting;

public enum ExportRowKind
{
    Data,
    GroupHeader,
    GroupFooter,
    /// <summary>Title and column header of a child table.</summary>
    DetailHeader,
    DetailData,
    Summary
}

public enum ExportAlignment
{
    Left,
    Center,
    Right
}

/// <summary>Colors and emphasis for a cell or row. Colors are #RRGGBB.</summary>
public sealed class ExportCellStyle
{
    public string? BackColorHex { get; set; }
    public string? ForeColorHex { get; set; }
    public bool Bold { get; set; }
    public bool Italic { get; set; }

    /// <summary>Fills this fraction (0-1) of the cell width with <see cref="DataBarColorHex"/>.</summary>
    public double? DataBar { get; set; }
    public string? DataBarColorHex { get; set; }

    public bool IsEmpty => BackColorHex == null && ForeColorHex == null && !Bold && !Italic && DataBar == null;
}

public sealed class ExportColumn
{
    public ExportColumn(string key, string? title = null)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Title = title ?? key;
    }

    public string Key { get; }
    public string Title { get; set; }

    /// <summary>Preferred width in pixels at 96 DPI; formats scale it (Excel characters, PDF points).</summary>
    public double Width { get; set; } = 100;
    public ExportAlignment Alignment { get; set; } = ExportAlignment.Left;

    /// <summary>.NET format string such as N2, C2, d or yyyy-MM-dd; mapped to Excel number formats where possible.</summary>
    public string? Format { get; set; }
    public Type? DataType { get; set; }

    /// <summary>Header bands from the outermost band to the innermost, for multi-row (stacked) headers.</summary>
    public IReadOnlyList<string> Bands { get; set; } = Array.Empty<string>();
}

/// <summary>
/// One exported row. Group rows carry text and aggregates, detail rows carry the child table's columns on their header row.
/// </summary>
public sealed class ExportRow
{
    public ExportRow(ExportRowKind kind, int level = 0, object?[]? cells = null)
    {
        Kind = kind;
        Level = level;
        Cells = cells ?? Array.Empty<object?>();
    }

    public ExportRowKind Kind { get; }

    /// <summary>Excel outline (grouping) level; defaults to <see cref="Level"/>.</summary>
    public int? OutlineLevel { get; set; }

    /// <summary>Group depth for group and data rows in a grouped sheet; child-table depth for detail rows; tree depth in tree sheets.</summary>
    public int Level { get; set; }
    public object?[] Cells { get; set; }
    public ExportCellStyle? Style { get; set; }
    public ExportCellStyle?[]? CellStyles { get; set; }

    /// <summary>Display text of group headers and detail header titles.</summary>
    public string? Text { get; set; }
    public string? GroupField { get; set; }
    public object? GroupKey { get; set; }
    public int GroupItemCount { get; set; }

    /// <summary>Group aggregates by label.</summary>
    public IReadOnlyList<KeyValuePair<string, object?>>? Aggregates { get; set; }

    public string? RelationName { get; set; }

    /// <summary>Columns of a child table, set on <see cref="ExportRowKind.DetailHeader"/> rows.</summary>
    public IReadOnlyList<ExportColumn>? DetailColumns { get; set; }

    /// <summary>For data rows of tree sheets: whether the node has children.</summary>
    public bool HasChildren { get; set; }
}

public enum ExportConditionalFormatType
{
    DataBar,
    ColorScale
}

/// <summary>Excel-native conditional formatting over a whole column of data rows.</summary>
public sealed class ExportConditionalFormat
{
    public string ColumnKey { get; set; } = string.Empty;
    public ExportConditionalFormatType Type { get; set; }
    public double Minimum { get; set; }
    public double Maximum { get; set; } = 1;
    public string ColorHex { get; set; } = "#5088D7";

    /// <summary>High-end color of a color scale; the low end is <see cref="ColorHex"/>.</summary>
    public string? SecondColorHex { get; set; }
}

public sealed class ExportSheet
{
    public ExportSheet(string name, IReadOnlyList<ExportColumn> columns, IEnumerable<ExportRow> rows)
    {
        Name = name;
        Columns = columns ?? throw new ArgumentNullException(nameof(columns));
        Rows = rows ?? throw new ArgumentNullException(nameof(rows));
    }

    public string Name { get; set; }
    public string? Title { get; set; }
    public IReadOnlyList<ExportColumn> Columns { get; }

    /// <summary>Rows are enumerated once, in order, so very large exports stream without being buffered.</summary>
    public IEnumerable<ExportRow> Rows { get; set; }

    /// <summary>Totals shown after the last data row.</summary>
    public IReadOnlyList<ExportRow> Footer { get; set; } = Array.Empty<ExportRow>();
    public IList<ExportConditionalFormat> ConditionalFormats { get; } = new List<ExportConditionalFormat>();

    /// <summary>Total number of rows when known up front; enables progress percentages and PDF page counts.</summary>
    public long? RowCountHint { get; set; }
    public bool FreezeHeader { get; set; } = true;
    public bool AutoFilter { get; set; } = true;

    /// <summary>True when rows nest (groups, child tables, tree levels); controls indentation and outlines.</summary>
    public bool IsStructured { get; set; }
}

public sealed class ExportDocument
{
    public string Title { get; set; } = string.Empty;
    public string? Author { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public IList<ExportSheet> Sheets { get; } = new List<ExportSheet>();
}

public sealed class ExportPdfOptions
{
    /// <summary>Page size in points (1/72 inch). Letter by default.</summary>
    public double PageWidth { get; set; } = 612;
    public double PageHeight { get; set; } = 792;
    public bool Landscape { get; set; }
    public double Margin { get; set; } = 36;
    public double FontSize { get; set; } = 9;

    /// <summary>Shrinks the columns to the page width; when false, columns that do not fit are clipped.</summary>
    public bool FitToWidth { get; set; } = true;
    public bool RepeatHeader { get; set; } = true;

    /// <summary>Header line text; tokens {title}, {date}.</summary>
    public string? HeaderText { get; set; } = "{title}";

    /// <summary>Footer line text; tokens {page}, {pages}, {date}.</summary>
    public string? FooterText { get; set; } = "Page {page}";
}

public sealed class ExportOptions
{
    public bool IncludeHeaders { get; set; } = true;
    public bool IncludeGroups { get; set; } = true;
    public bool IncludeDetails { get; set; } = true;
    public bool IncludeSummaries { get; set; } = true;
    public bool ApplyFormatting { get; set; } = true;
    public CultureInfo Culture { get; set; } = CultureInfo.CurrentCulture;

    /// <summary>Excel allows 1,048,576 rows per sheet; longer sheets continue on new sheets.</summary>
    public long MaxRowsPerSheet { get; set; } = 1_048_575;

    public char CsvDelimiter { get; set; } = ',';
    public bool CsvQuoteAll { get; set; }

    /// <summary>Prefixes cells that start with = + - @ with an apostrophe so spreadsheets do not run them as formulas.</summary>
    public bool CsvSanitizeFormulas { get; set; } = true;
    public bool JsonIndented { get; set; } = true;
    public ExportPdfOptions Pdf { get; set; } = new();

    /// <summary>Progress is reported after this many rows.</summary>
    public int ProgressInterval { get; set; } = 1000;
}

public sealed record ExportProgress(long RowsProcessed, long? TotalRows, string Sheet)
{
    /// <summary>0-1 when the total is known.</summary>
    public double? Fraction => TotalRows is > 0 ? Math.Min(1, (double)RowsProcessed / TotalRows.Value) : null;
}

public sealed record ExportResult(string FormatId, long Rows, int Sheets, long Bytes, TimeSpan Duration);

public sealed class ExportRowEventArgs : EventArgs
{
    public ExportRowEventArgs(ExportSheet sheet, ExportRow row)
    {
        Sheet = sheet;
        Row = row;
    }

    public ExportSheet Sheet { get; }
    public ExportRow Row { get; }

    /// <summary>Leave the row out of the output.</summary>
    public bool Skip { get; set; }
}

public sealed class ExportCellEventArgs : EventArgs
{
    public ExportCellEventArgs(ExportSheet sheet, ExportRow row, ExportColumn? column, int columnIndex, object? value)
    {
        Sheet = sheet;
        Row = row;
        Column = column;
        ColumnIndex = columnIndex;
        Value = value;
    }

    public ExportSheet Sheet { get; }
    public ExportRow Row { get; }
    public ExportColumn? Column { get; }
    public int ColumnIndex { get; }

    /// <summary>The value to write; change it to export something else (for example masked data).</summary>
    public object? Value { get; set; }

    /// <summary>Style of this cell; assign to restyle it.</summary>
    public ExportCellStyle? Style { get; set; }
}
