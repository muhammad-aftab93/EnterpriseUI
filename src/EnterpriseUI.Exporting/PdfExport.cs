using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace EnterpriseUI.Exporting;

/// <summary>
/// Writes multi-page PDF 1.4 documents with the built-in Helvetica fonts: repeated column headers, group and detail rows,
/// cell colors and data bars, page header and footer with page numbers, and scaling of the columns to the page width.
/// Pages are streamed, so memory use does not grow with the number of rows.
/// </summary>
public sealed class PdfExportProvider : IExportProvider
{
    // Helvetica glyph widths (1/1000 em) for ASCII 32..126.
    private static readonly short[] Widths =
    {
        278, 278, 355, 556, 556, 889, 667, 191, 333, 333, 389, 584, 278, 333, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 278, 278, 584, 584, 584, 556,
        1015, 667, 667, 722, 722, 667, 611, 778, 722, 278, 500, 667, 556, 833, 722, 778,
        667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 278, 278, 278, 469, 556,
        333, 556, 556, 500, 556, 556, 278, 556, 556, 222, 222, 500, 222, 833, 556, 556,
        556, 556, 333, 500, 278, 556, 500, 722, 500, 500, 500, 334, 260, 334, 584
    };

    public string Id => "pdf";
    public string DisplayName => "PDF Document";
    public string FileExtension => ".pdf";
    public string ContentType => "application/pdf";

    public Task WriteAsync(ExportDocument document, Stream output, ExportContext context) =>
        Task.Run(() => Write(document, output, context), context.CancellationToken);

    /// <summary>Text width in points for the standard Helvetica fonts.</summary>
    public static double MeasureText(string text, double fontSize, bool bold)
    {
        double total = 0;
        foreach (var c in text)
        {
            total += c is >= ' ' and <= '~' ? Widths[c - ' '] : c == '\u20AC' ? 556 : 556;
        }
        return total * fontSize / 1000 * (bold ? 1.06 : 1);
    }

    private sealed class PdfStreamWriter
    {
        private readonly Stream _output;
        public long Position { get; private set; }
        public List<long> Offsets { get; } = new() { 0 };

        public PdfStreamWriter(Stream output) => _output = output;

        public void Raw(string text)
        {
            var bytes = Encoding.Latin1.GetBytes(text);
            _output.Write(bytes, 0, bytes.Length);
            Position += bytes.Length;
        }

        public void Raw(byte[] bytes)
        {
            _output.Write(bytes, 0, bytes.Length);
            Position += bytes.Length;
        }

        public int BeginObject()
        {
            Offsets.Add(Position);
            var number = Offsets.Count - 1;
            Raw($"{number} 0 obj\n");
            return number;
        }

        public void ReserveObject() => Offsets.Add(-1);

        public void BeginReserved(int number)
        {
            Offsets[number] = Position;
            Raw($"{number} 0 obj\n");
        }
    }

    private sealed class PageBuilder
    {
        private readonly StringBuilder _content = new(8192);
        public string Content => _content.ToString();

        public void Fill(double x, double y, double w, double h, byte r, byte g, byte b)
        {
            _content.Append(Num(r / 255.0)).Append(' ').Append(Num(g / 255.0)).Append(' ').Append(Num(b / 255.0)).Append(" rg ")
                .Append(Num(x)).Append(' ').Append(Num(y)).Append(' ').Append(Num(w)).Append(' ').Append(Num(h)).Append(" re f\n");
        }

        public void Line(double x1, double y1, double x2, double y2, double gray = 0.78)
        {
            _content.Append(Num(gray)).Append(" G 0.5 w ").Append(Num(x1)).Append(' ').Append(Num(y1)).Append(" m ")
                .Append(Num(x2)).Append(' ').Append(Num(y2)).Append(" l S\n");
        }

        public void Text(double x, double y, string text, double size, bool bold, byte r = 0, byte g = 0, byte b = 0)
        {
            if (text.Length == 0) return;
            _content.Append("BT /").Append(bold ? "F2" : "F1").Append(' ').Append(Num(size)).Append(" Tf ")
                .Append(Num(r / 255.0)).Append(' ').Append(Num(g / 255.0)).Append(' ').Append(Num(b / 255.0)).Append(" rg ")
                .Append(Num(x)).Append(' ').Append(Num(y)).Append(" Td (").Append(Escape(text)).Append(") Tj ET\n");
        }

        private static string Num(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        private static string Escape(string text)
        {
            var builder = new StringBuilder(text.Length + 4);
            foreach (var c in text)
            {
                switch (c)
                {
                    case '\\': builder.Append("\\\\"); break;
                    case '(': builder.Append("\\("); break;
                    case ')': builder.Append("\\)"); break;
                    case '\u20AC': builder.Append("\\200"); break;
                    case < ' ': builder.Append(' '); break;
                    case > '\u00FF': builder.Append('?'); break;
                    case >= '\u007F' and <= '\u009F': builder.Append('?'); break;
                    default: builder.Append(c); break;
                }
            }
            return builder.ToString();
        }
    }

    private static void Write(ExportDocument document, Stream output, ExportContext context)
    {
        var options = context.Options;
        var pdf = options.Pdf;
        var width = pdf.Landscape ? Math.Max(pdf.PageWidth, pdf.PageHeight) : Math.Min(pdf.PageWidth, pdf.PageHeight);
        var height = pdf.Landscape ? Math.Min(pdf.PageWidth, pdf.PageHeight) : Math.Max(pdf.PageWidth, pdf.PageHeight);
        var margin = Math.Clamp(pdf.Margin, 10, Math.Min(width, height) / 3);
        var font = Math.Clamp(pdf.FontSize, 5, 24);
        var rowHeight = font + 6;
        var contentWidth = width - 2 * margin;

        var writer = new PdfStreamWriter(output);
        writer.Raw("%PDF-1.4\n%\u00E2\u00E3\u00CF\u00D3\n");
        writer.ReserveObject(); // 1 catalog
        writer.ReserveObject(); // 2 pages
        var f1 = writer.BeginObject();
        writer.Raw("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\nendobj\n");
        var f2 = writer.BeginObject();
        writer.Raw("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>\nendobj\n");
        var info = writer.BeginObject();
        writer.Raw($"<< /Title ({PdfString(document.Title)}) /Creator (EnterpriseUI) /Producer (EnterpriseUI.Exporting) /CreationDate (D:{document.CreatedUtc:yyyyMMddHHmmss}Z) >>\nendobj\n");

        var pages = new List<int>();
        var totalPages = KnownTotalPages(document, pdf, height, margin, rowHeight);
        var pageNumber = 0;

        foreach (var sheet in document.Sheets)
        {
            var layout = ColumnLayout(sheet, contentWidth, pdf);
            var headerBlock = HeaderHeight(sheet, options, rowHeight, pdf, document);
            PageBuilder? page = null;
            double y = 0;
            var firstPageOfSheet = true;

            void StartPage()
            {
                if (page != null) FlushPage(writer, page, pages, width, height, f1, f2, pdf, document, ref pageNumber, totalPages, margin, font);
                page = new PageBuilder();
                y = height - margin;

                var heading = firstPageOfSheet || pdf.RepeatHeader;
                if (!string.IsNullOrEmpty(pdf.HeaderText))
                {
                    var header = Tokens(pdf.HeaderText, document, pageNumber + 1, totalPages);
                    page.Text(margin, height - margin + font * 0.2, header, font + 1, true);
                    page.Line(margin, height - margin, width - margin, height - margin, 0.6);
                }
                y -= pdf.HeaderText is { Length: > 0 } ? 4 : 0;

                if (firstPageOfSheet && (document.Sheets.Count > 1 || !string.IsNullOrWhiteSpace(sheet.Title)))
                {
                    y -= rowHeight;
                    page.Text(margin, y + 3, sheet.Title ?? sheet.Name, font + 3, true);
                }

                if (heading && options.IncludeHeaders && sheet.Columns.Count > 0) DrawHeader(page, sheet, layout, margin, ref y, font, rowHeight);
                firstPageOfSheet = false;
            }

            var bottom = margin + rowHeight;
            StartPage();

            void Emit(ExportRow row)
            {
                if (y - rowHeight < bottom) StartPage();
                DrawRow(page!, sheet, row, layout, margin, ref y, font, rowHeight, options);
            }

            foreach (var row in sheet.Rows)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                Emit(row);
            }

            foreach (var row in sheet.Footer) Emit(row);

            FlushPage(writer, page!, pages, width, height, f1, f2, pdf, document, ref pageNumber, totalPages, margin, font);
            _ = headerBlock;
        }

        if (pages.Count == 0)
        {
            var empty = new PageBuilder();
            FlushPage(writer, empty, pages, width, height, f1, f2, pdf, document, ref pageNumber, totalPages, margin, font);
        }

        writer.BeginReserved(2);
        writer.Raw($"<< /Type /Pages /Count {pages.Count} /Kids [{string.Join(' ', pages.Select(p => $"{p} 0 R"))}] >>\nendobj\n");
        writer.BeginReserved(1);
        writer.Raw("<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        var xref = writer.Position;
        var count = writer.Offsets.Count;
        var table = new StringBuilder($"xref\n0 {count}\n0000000000 65535 f \n");
        for (var i = 1; i < count; i++)
        {
            table.Append(writer.Offsets[i].ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }
        writer.Raw(table.ToString());
        writer.Raw($"trailer\n<< /Size {count} /Root 1 0 R /Info {info} 0 R >>\nstartxref\n{xref}\n%%EOF\n");
    }

    private static void FlushPage(PdfStreamWriter writer, PageBuilder page, List<int> pages, double width, double height, int f1, int f2,
        ExportPdfOptions pdf, ExportDocument document, ref int pageNumber, int? totalPages, double margin, double font)
    {
        pageNumber++;
        if (!string.IsNullOrEmpty(pdf.FooterText))
        {
            var footer = Tokens(pdf.FooterText, document, pageNumber, totalPages);
            var footerWidth = MeasureText(footer, font - 1, false);
            page.Line(margin, margin - 2, width - margin, margin - 2, 0.6);
            page.Text(width - margin - footerWidth, margin - font - 2, footer, font - 1, false, 90, 90, 90);
        }

        byte[] compressed;
        using (var memory = new MemoryStream())
        {
            using (var z = new ZLibStream(memory, CompressionLevel.Optimal, leaveOpen: true))
            {
                var bytes = Encoding.Latin1.GetBytes(page.Content);
                z.Write(bytes, 0, bytes.Length);
            }
            compressed = memory.ToArray();
        }

        var contentObject = writer.BeginObject();
        writer.Raw($"<< /Length {compressed.Length} /Filter /FlateDecode >>\nstream\n");
        writer.Raw(compressed);
        writer.Raw("\nendstream\nendobj\n");

        var pageObject = writer.BeginObject();
        writer.Raw($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {width.ToString("0.##", CultureInfo.InvariantCulture)} {height.ToString("0.##", CultureInfo.InvariantCulture)}] " +
                   $"/Resources << /Font << /F1 {f1} 0 R /F2 {f2} 0 R >> >> /Contents {contentObject} 0 R >>\nendobj\n");
        pages.Add(pageObject);
    }

    private static string Tokens(string template, ExportDocument document, int page, int? pages)
    {
        var text = template.Replace("{title}", document.Title).Replace("{date}", document.CreatedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)).Replace("{page}", page.ToString(CultureInfo.InvariantCulture));
        return pages is { } total
            ? text.Replace("{pages}", total.ToString(CultureInfo.InvariantCulture))
            : text.Replace(" of {pages}", string.Empty).Replace("{pages}", string.Empty);
    }

    private static double[] ColumnLayout(ExportSheet sheet, double contentWidth, ExportPdfOptions pdf)
    {
        var widths = sheet.Columns.Select(c => Math.Max(24, c.Width * 0.75)).ToArray();
        if (widths.Length == 0) return widths;

        var total = widths.Sum();
        var scale = pdf.FitToWidth || total < contentWidth ? contentWidth / total : 1;
        if (pdf.FitToWidth && total < contentWidth) scale = Math.Min(scale, 1.4);
        return widths.Select(w => w * scale).ToArray();
    }

    private static double HeaderHeight(ExportSheet sheet, ExportOptions options, double rowHeight, ExportPdfOptions pdf, ExportDocument document)
    {
        var rows = options.IncludeHeaders && sheet.Columns.Count > 0 ? sheet.Columns.Max(c => c.Bands.Count) + 1 : 0;
        return rows * rowHeight + (string.IsNullOrEmpty(pdf.HeaderText) ? 0 : 4) + (document.Sheets.Count > 1 ? rowHeight : 0);
    }

    private static int? KnownTotalPages(ExportDocument document, ExportPdfOptions pdf, double height, double margin, double rowHeight)
    {
        if (document.Sheets.Count == 0 || document.Sheets.Any(s => s.RowCountHint == null)) return null;

        var pageHeight = (pdf.Landscape ? Math.Min(pdf.PageWidth, pdf.PageHeight) : Math.Max(pdf.PageWidth, pdf.PageHeight));
        var usable = pageHeight - 2 * Math.Clamp(pdf.Margin, 10, 60) - rowHeight - (string.IsNullOrEmpty(pdf.HeaderText) ? 0 : 4);
        var total = 0;
        foreach (var sheet in document.Sheets)
        {
            var headerRows = sheet.Columns.Count == 0 ? 0 : sheet.Columns.Max(c => c.Bands.Count) + 1;
            var titleRows = document.Sheets.Count > 1 || !string.IsNullOrWhiteSpace(sheet.Title) ? 1 : 0;
            var rows = sheet.RowCountHint!.Value + sheet.Footer.Count;
            var firstCapacity = Math.Max(1, (int)Math.Floor((usable - (headerRows + titleRows) * rowHeight) / rowHeight));
            var nextCapacity = Math.Max(1, (int)Math.Floor((usable - (pdf.RepeatHeader ? headerRows : 0) * rowHeight) / rowHeight));
            var pages = rows <= firstCapacity ? 1 : 1 + (int)Math.Ceiling((rows - firstCapacity) / (double)nextCapacity);
            total += Math.Max(1, pages);
        }
        return total;
    }

    private static void DrawHeader(PageBuilder page, ExportSheet sheet, double[] layout, double margin, ref double y, double font, double rowHeight)
    {
        var cells = ExportHeaderLayout.Build(sheet.Columns, out var rows);
        var left = new double[layout.Length + 1];
        for (var i = 0; i < layout.Length; i++) left[i + 1] = left[i] + layout[i];

        var top = y;
        foreach (var cell in cells)
        {
            var x = margin + left[cell.Column];
            var w = left[cell.Column + cell.ColumnSpan] - left[cell.Column];
            var h = rowHeight * cell.RowSpan;
            var cy = top - rowHeight * cell.Row - h;
            if (cell.IsBand) page.Fill(x, cy, w, h, 0xDC, 0xE6, 0xF2); else page.Fill(x, cy, w, h, 0xF0, 0xF0, 0xF0);
            page.Line(x, cy, x + w, cy);
            var text = Clip(cell.Text, w - 6, font, true);
            var tx = cell.IsBand ? x + Math.Max(3, (w - MeasureText(text, font, true)) / 2) : x + 3;
            page.Text(tx, cy + (h - font) / 2 + font * 0.15, text, font, true);
        }

        y = top - rows * rowHeight;
    }

    private static void DrawRow(PageBuilder page, ExportSheet sheet, ExportRow row, double[] layout, double margin, ref double y, double font, double rowHeight, ExportOptions options)
    {
        var bottom = y - rowHeight;
        var totalWidth = layout.Sum();
        var left = new double[layout.Length + 1];
        for (var i = 0; i < layout.Length; i++) left[i + 1] = left[i] + layout[i];

        double TextY() => bottom + (rowHeight - font) / 2 + font * 0.15;
        var indent = Math.Min(row.Level, 6) * 10.0;

        switch (row.Kind)
        {
            case ExportRowKind.GroupHeader:
                page.Fill(margin, bottom, totalWidth, rowHeight, 0xE9, 0xEE, 0xF5);
                page.Text(margin + 3 + indent, TextY(), Clip(row.Text ?? string.Empty, totalWidth - indent - 6, font, true), font, true);
                break;

            case ExportRowKind.DetailHeader:
            {
                page.Fill(margin + indent, bottom, totalWidth - indent, rowHeight, 0xF7, 0xF7, 0xF7);
                var titles = new List<string> { row.RelationName ?? row.Text ?? string.Empty };
                titles.AddRange((row.DetailColumns ?? Array.Empty<ExportColumn>()).Select(c => c.Title));
                var slot = Math.Min(row.Level, layout.Length - 1);
                for (var i = 0; i < titles.Count && slot + i - 1 < layout.Length; i++)
                {
                    var column = i == 0 ? Math.Max(0, slot - 1) : slot + i - 1;
                    if (column >= layout.Length) break;
                    page.Text(margin + left[column] + 3, TextY(), Clip(titles[i], layout[column] - 6, font, true), font, true, 80, 80, 80);
                }
                break;
            }

            case ExportRowKind.DetailData:
            {
                var slot = Math.Min(row.Level, layout.Length - 1);
                for (var i = 0; i < row.Cells.Length && slot + i < layout.Length; i++)
                {
                    var text = ExportFormatting.FormatCell(row.Cells[i], null, options.Culture);
                    var x = margin + left[slot + i];
                    page.Text(x + 3, TextY(), Clip(text, layout[slot + i] - 6, font, false), font, false);
                }
                break;
            }

            default:
            {
                var isSummary = row.Kind is ExportRowKind.Summary or ExportRowKind.GroupFooter;
                if (isSummary) page.Fill(margin, bottom, totalWidth, rowHeight, 0xF3, 0xF3, 0xF3);
                if (options.ApplyFormatting && row.Style != null && ExportFormatting.TryParseColor(row.Style.BackColorHex, out var rr, out var rg, out var rb))
                {
                    page.Fill(margin, bottom, totalWidth, rowHeight, rr, rg, rb);
                }

                for (var i = 0; i < row.Cells.Length && i < layout.Length; i++)
                {
                    var column = i < sheet.Columns.Count ? sheet.Columns[i] : null;
                    var style = options.ApplyFormatting && row.CellStyles != null && i < row.CellStyles.Length ? row.CellStyles[i] : null;
                    var x = margin + left[i];
                    var w = layout[i];

                    if (style != null)
                    {
                        if (ExportFormatting.TryParseColor(style.BackColorHex, out var br, out var bg, out var bb)) page.Fill(x, bottom, w, rowHeight, br, bg, bb);
                        if (style.DataBar is { } ratio && ExportFormatting.TryParseColor(style.DataBarColorHex ?? "#5088D7", out var dr, out var dg, out var db))
                        {
                            // 40% tint so the value stays readable on top of the bar.
                            page.Fill(x + 2, bottom + 2, Math.Max(0, (w - 4) * Math.Clamp(ratio, 0, 1)), rowHeight - 4, (byte)(dr + (255 - dr) * 0.6), (byte)(dg + (255 - dg) * 0.6), (byte)(db + (255 - db) * 0.6));
                        }
                    }

                    var text = ExportFormatting.FormatCell(row.Cells[i], column, options.Culture);
                    var bold = isSummary || style?.Bold == true;
                    var clipped = Clip(text, w - 6 - (i == 0 ? indent : 0), font, bold);
                    var textWidth = MeasureText(clipped, font, bold);
                    var right = column?.Alignment == ExportAlignment.Right || (column == null && ExportFormatting.IsNumeric(row.Cells[i]));
                    var center = column?.Alignment == ExportAlignment.Center;
                    var tx = right ? x + w - 3 - textWidth : center ? x + (w - textWidth) / 2 : x + 3 + (i == 0 ? indent : 0);
                    byte fr = 0, fg = 0, fb = 0;
                    if (style != null) ExportFormatting.TryParseColor(style.ForeColorHex, out fr, out fg, out fb);
                    page.Text(tx, TextY(), clipped, font, bold, fr, fg, fb);
                }
                break;
            }
        }

        page.Line(margin, bottom, margin + totalWidth, bottom, 0.88);
        y = bottom;
    }

    private static string Clip(string text, double width, double font, bool bold)
    {
        if (text.Length == 0 || width <= 0) return string.Empty;
        text = text.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
        if (MeasureText(text, font, bold) <= width) return text;

        var ellipsis = MeasureText("...", font, bold);
        var length = text.Length;
        while (length > 0 && MeasureText(text.AsSpan(0, length).ToString(), font, bold) + ellipsis > width) length--;
        return length <= 0 ? string.Empty : text[..length] + "...";
    }

    private static string PdfString(string? text)
    {
        var builder = new StringBuilder();
        foreach (var c in text ?? string.Empty)
        {
            switch (c)
            {
                case '\\': builder.Append("\\\\"); break;
                case '(': builder.Append("\\("); break;
                case ')': builder.Append("\\)"); break;
                case < ' ' or > '~': builder.Append('?'); break;
                default: builder.Append(c); break;
            }
        }
        return builder.ToString();
    }
}
