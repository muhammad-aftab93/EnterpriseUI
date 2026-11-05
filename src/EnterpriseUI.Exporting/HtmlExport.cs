using System.Globalization;
using System.Net;
using System.Text;

namespace EnterpriseUI.Exporting;

public sealed class HtmlExportProvider : IExportProvider
{
    public string Id => "html";
    public string DisplayName => "HTML";
    public string FileExtension => ".html";
    public string ContentType => "text/html";

    public Task WriteAsync(ExportDocument document, Stream output, ExportContext context) =>
        Task.Run(() => Write(document, output, context), context.CancellationToken);

    private static void Write(ExportDocument document, Stream output, ExportContext context)
    {
        var options = context.Options;
        using var writer = new StreamWriter(output, new UTF8Encoding(false), 65536, leaveOpen: true);
        writer.WriteLine("<!DOCTYPE html>");
        writer.WriteLine("<html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        writer.WriteLine($"<title>{Encode(document.Title)}</title>");
        writer.WriteLine("<style>body{font-family:Segoe UI,Arial,sans-serif;font-size:13px;color:#1b1b1b}table{border-collapse:collapse;margin:12px 0}" +
                         "th,td{border:1px solid #c8c8c8;padding:4px 8px}th{background:#f3f3f3;text-align:left}" +
                         "tr.group td{background:#e9eef5;font-weight:600}tr.footer td,tfoot td{background:#f3f3f3;font-weight:600}" +
                         "tr.detail-header th{background:#f8f8f8;font-weight:600;font-size:12px}td.num{text-align:right}td.center{text-align:center}</style>");
        writer.WriteLine("</head><body>");
        if (!string.IsNullOrEmpty(document.Title)) writer.WriteLine($"<h1>{Encode(document.Title)}</h1>");

        foreach (var sheet in document.Sheets)
        {
            if (document.Sheets.Count > 1 || !string.IsNullOrEmpty(sheet.Title)) writer.WriteLine($"<h2>{Encode(sheet.Title ?? sheet.Name)}</h2>");
            WriteSheet(writer, sheet, options);
        }

        writer.WriteLine("</body></html>");
    }

    private static void WriteSheet(StreamWriter writer, ExportSheet sheet, ExportOptions options)
    {
        var columnCount = Math.Max(1, sheet.Columns.Count);
        writer.WriteLine("<table>");
        if (options.IncludeHeaders)
        {
            var cells = ExportHeaderLayout.Build(sheet.Columns, out var rows);
            writer.WriteLine("<thead>");
            for (var r = 0; r < rows; r++)
            {
                writer.Write("<tr>");
                foreach (var cell in cells.Where(c => c.Row == r).OrderBy(c => c.Column))
                {
                    writer.Write($"<th scope=\"{(cell.IsBand || cell.ColumnSpan > 1 ? "colgroup" : "col")}\"");
                    if (cell.ColumnSpan > 1) writer.Write($" colspan=\"{cell.ColumnSpan}\"");
                    if (cell.RowSpan > 1) writer.Write($" rowspan=\"{cell.RowSpan}\"");
                    writer.Write($">{Encode(cell.Text)}</th>");
                }
                writer.WriteLine("</tr>");
            }
            writer.WriteLine("</thead>");
        }

        writer.WriteLine("<tbody>");
        foreach (var row in sheet.Rows)
        {
            WriteRow(writer, sheet, row, columnCount, options);
        }
        writer.WriteLine("</tbody>");

        if (sheet.Footer.Count > 0)
        {
            writer.WriteLine("<tfoot>");
            foreach (var row in sheet.Footer) WriteRow(writer, sheet, row, columnCount, options);
            writer.WriteLine("</tfoot>");
        }

        writer.WriteLine("</table>");
    }

    private static void WriteRow(StreamWriter writer, ExportSheet sheet, ExportRow row, int columnCount, ExportOptions options)
    {
        var indent = row.Level * 18;
        switch (row.Kind)
        {
            case ExportRowKind.GroupHeader:
                writer.WriteLine($"<tr class=\"group\"><td colspan=\"{columnCount}\" style=\"padding-left:{8 + indent}px\">{Encode(row.Text)}</td></tr>");
                return;

            case ExportRowKind.DetailHeader:
                writer.Write("<tr class=\"detail-header\">");
                writer.Write($"<th scope=\"row\" style=\"padding-left:{8 + indent}px\">{Encode(row.RelationName ?? row.Text)}</th>");
                foreach (var column in row.DetailColumns ?? Array.Empty<ExportColumn>())
                {
                    writer.Write($"<th scope=\"col\">{Encode(column.Title)}</th>");
                }
                writer.WriteLine("</tr>");
                return;

            case ExportRowKind.DetailData:
                writer.Write("<tr class=\"detail\"><td></td>");
                for (var i = 0; i < row.Cells.Length; i++)
                {
                    writer.Write($"<td{CellAttributes(row, i, null, options)}>{Encode(ExportFormatting.FormatCell(row.Cells[i], null, options.Culture))}</td>");
                }
                writer.WriteLine("</tr>");
                return;
        }

        var cssClass = row.Kind == ExportRowKind.Data ? "data" : "footer";
        writer.Write($"<tr class=\"{cssClass}\"{RowStyle(row.Style, options)}>");
        for (var i = 0; i < row.Cells.Length; i++)
        {
            var column = i < sheet.Columns.Count ? sheet.Columns[i] : null;
            var text = Encode(ExportFormatting.FormatCell(row.Cells[i], column, options.Culture));
            var padding = i == 0 && row.Level > 0 ? $" style=\"padding-left:{8 + indent}px\"" : string.Empty;
            writer.Write($"<td{CellAttributes(row, i, column, options)}{padding}>{text}</td>");
        }
        writer.WriteLine("</tr>");
    }

    private static string CellAttributes(ExportRow row, int index, ExportColumn? column, ExportOptions options)
    {
        var classes = new List<string>(2);
        var value = row.Cells[index];
        if (column?.Alignment == ExportAlignment.Right || (column == null && ExportFormatting.IsNumeric(value))) classes.Add("num");
        else if (column?.Alignment == ExportAlignment.Center) classes.Add("center");

        var style = options.ApplyFormatting && row.CellStyles != null && index < row.CellStyles.Length ? CellCss(row.CellStyles[index]) : string.Empty;
        var result = new StringBuilder();
        if (classes.Count > 0) result.Append(" class=\"").Append(string.Join(' ', classes)).Append('"');
        if (style.Length > 0) result.Append(" style=\"").Append(style).Append('"');
        return result.ToString();
    }

    private static string RowStyle(ExportCellStyle? style, ExportOptions options)
    {
        var css = options.ApplyFormatting ? CellCss(style) : string.Empty;
        return css.Length == 0 ? string.Empty : $" style=\"{css}\"";
    }

    /// <summary>Only validated #RRGGBB colors and fixed property names reach the CSS, so cell content can never inject styles.</summary>
    private static string CellCss(ExportCellStyle? style)
    {
        if (style == null) return string.Empty;
        var css = new StringBuilder();
        if (ExportFormatting.TryParseColor(style.BackColorHex, out var r, out var g, out var b)) css.Append($"background-color:#{r:x2}{g:x2}{b:x2};");
        if (ExportFormatting.TryParseColor(style.ForeColorHex, out r, out g, out b)) css.Append($"color:#{r:x2}{g:x2}{b:x2};");
        if (style.Bold) css.Append("font-weight:600;");
        if (style.Italic) css.Append("font-style:italic;");
        if (style.DataBar is { } ratio && ExportFormatting.TryParseColor(style.DataBarColorHex ?? "#5088D7", out r, out g, out b))
        {
            var percent = (int)Math.Round(Math.Clamp(ratio, 0, 1) * 100);
            css.Append($"background-image:linear-gradient(90deg,#{r:x2}{g:x2}{b:x2}66 {percent}%,transparent {percent}%);");
        }
        return css.ToString();
    }

    private static string Encode(string? text) => WebUtility.HtmlEncode(text ?? string.Empty);
}
