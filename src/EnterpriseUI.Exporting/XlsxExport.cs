using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace EnterpriseUI.Exporting;

/// <summary>
/// Writes real Office Open XML workbooks (.xlsx): typed cells, number formats, colors, merged group rows, outline levels for
/// groups and child tables, frozen headers, auto filters, native data bars and color scales, and automatic continuation sheets.
/// </summary>
public sealed class XlsxExportProvider : IExportProvider
{
    private const int MaxOutlineLevel = 7;
    private const int MaxCellText = 32767;

    public string Id => "xlsx";
    public string DisplayName => "Excel Workbook (.xlsx)";
    public string FileExtension => ".xlsx";
    public string ContentType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public Task WriteAsync(ExportDocument document, Stream output, ExportContext context) =>
        Task.Run(() => Write(document, output, context), context.CancellationToken);

    private sealed record StyleKey(string? NumberFormat, bool Bold, bool Italic, string? FontColor, string? FillColor, int Align, int Indent, bool TopBorder, double FontSize, bool Wrap);

    private sealed class StyleRegistry
    {
        private readonly Dictionary<StyleKey, int> _styles = new();
        private readonly List<StyleKey> _ordered = new();

        public StyleRegistry() => Get(new StyleKey(null, false, false, null, null, 0, 0, false, 11, false));

        public int Get(StyleKey key)
        {
            if (_styles.TryGetValue(key, out var index)) return index;
            index = _ordered.Count;
            _styles[key] = index;
            _ordered.Add(key);
            return index;
        }

        public string ToXml()
        {
            var formats = _ordered.Select(s => s.NumberFormat).Where(f => f != null).Distinct().ToList();
            var fonts = _ordered.Select(s => (s.Bold, s.Italic, s.FontColor, s.FontSize)).Distinct().ToList();
            var fills = _ordered.Select(s => s.FillColor).Where(f => f != null).Distinct().ToList();

            var xml = new StringBuilder();
            xml.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            xml.Append("<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");

            if (formats.Count > 0)
            {
                xml.Append($"<numFmts count=\"{formats.Count}\">");
                for (var i = 0; i < formats.Count; i++)
                {
                    xml.Append($"<numFmt numFmtId=\"{164 + i}\" formatCode=\"{Escape(formats[i]!)}\"/>");
                }
                xml.Append("</numFmts>");
            }

            xml.Append($"<fonts count=\"{fonts.Count}\">");
            foreach (var (bold, italic, color, size) in fonts)
            {
                xml.Append("<font>");
                if (bold) xml.Append("<b/>");
                if (italic) xml.Append("<i/>");
                xml.Append($"<sz val=\"{size.ToString(CultureInfo.InvariantCulture)}\"/>");
                xml.Append(color != null ? $"<color rgb=\"{Argb(color)}\"/>" : "<color theme=\"1\"/>");
                xml.Append("<name val=\"Calibri\"/><family val=\"2\"/></font>");
            }
            xml.Append("</fonts>");

            xml.Append($"<fills count=\"{fills.Count + 2}\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill>");
            foreach (var fill in fills)
            {
                xml.Append($"<fill><patternFill patternType=\"solid\"><fgColor rgb=\"{Argb(fill!)}\"/><bgColor indexed=\"64\"/></patternFill></fill>");
            }
            xml.Append("</fills>");

            xml.Append("<borders count=\"2\"><border><left/><right/><top/><bottom/><diagonal/></border>" +
                       "<border><left/><right/><top style=\"thin\"><color auto=\"1\"/></top><bottom/><diagonal/></border></borders>");
            xml.Append("<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>");

            xml.Append($"<cellXfs count=\"{_ordered.Count}\">");
            foreach (var s in _ordered)
            {
                var numFmt = s.NumberFormat == null ? 0 : 164 + formats.IndexOf(s.NumberFormat);
                var font = fonts.IndexOf((s.Bold, s.Italic, s.FontColor, s.FontSize));
                var fill = s.FillColor == null ? 0 : 2 + fills.IndexOf(s.FillColor);
                xml.Append($"<xf numFmtId=\"{numFmt}\" fontId=\"{font}\" fillId=\"{fill}\" borderId=\"{(s.TopBorder ? 1 : 0)}\" xfId=\"0\"");
                if (numFmt != 0) xml.Append(" applyNumberFormat=\"1\"");
                if (font != 0) xml.Append(" applyFont=\"1\"");
                if (fill != 0) xml.Append(" applyFill=\"1\"");
                if (s.TopBorder) xml.Append(" applyBorder=\"1\"");
                if (s.Align != 0 || s.Indent > 0 || s.Wrap)
                {
                    var horizontal = s.Align switch { 1 => "left", 2 => "center", 3 => "right", _ => "general" };
                    xml.Append($" applyAlignment=\"1\"><alignment horizontal=\"{horizontal}\"{(s.Indent > 0 ? $" indent=\"{s.Indent}\"" : string.Empty)}{(s.Wrap ? " wrapText=\"1\"" : string.Empty)}/></xf>");
                }
                else
                {
                    xml.Append("/>");
                }
            }
            xml.Append("</cellXfs>");
            xml.Append("<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>");
            xml.Append("</styleSheet>");
            return xml.ToString();
        }
    }

    private static void Write(ExportDocument document, Stream output, ExportContext context)
    {
        var options = context.Options;
        var styles = new StyleRegistry();
        var sheetNames = new List<string>();

        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8);
        foreach (var sheet in document.Sheets)
        {
            WriteSheet(zip, sheet, options, styles, sheetNames, context.CancellationToken);
        }

        if (sheetNames.Count == 0)
        {
            WriteSheet(zip, new ExportSheet("Sheet1", Array.Empty<ExportColumn>(), Array.Empty<ExportRow>()), options, styles, sheetNames, context.CancellationToken);
        }

        WriteEntry(zip, "[Content_Types].xml", ContentTypes(sheetNames.Count));
        WriteEntry(zip, "_rels/.rels",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
            "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/></Relationships>");
        WriteEntry(zip, "docProps/core.xml",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" " +
            "xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
            $"<dc:title>{Escape(document.Title)}</dc:title><dc:creator>{Escape(document.Author ?? "EnterpriseUI")}</dc:creator>" +
            $"<dcterms:created xsi:type=\"dcterms:W3CDTF\">{document.CreatedUtc.ToUniversalTime():yyyy-MM-ddTHH:mm:ssZ}</dcterms:created></cp:coreProperties>");

        var workbook = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
                                         "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>");
        var rels = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
        for (var i = 0; i < sheetNames.Count; i++)
        {
            workbook.Append($"<sheet name=\"{Escape(sheetNames[i])}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>");
            rels.Append($"<Relationship Id=\"rId{i + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i + 1}.xml\"/>");
        }
        workbook.Append("</sheets></workbook>");
        rels.Append($"<Relationship Id=\"rId{sheetNames.Count + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>");

        WriteEntry(zip, "xl/workbook.xml", workbook.ToString());
        WriteEntry(zip, "xl/_rels/workbook.xml.rels", rels.ToString());
        WriteEntry(zip, "xl/styles.xml", styles.ToXml());
    }

    private static string ContentTypes(int sheetCount)
    {
        var xml = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                                    "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                                    "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                                    "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
                                    "<Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/>");
        for (var i = 1; i <= sheetCount; i++)
        {
            xml.Append($"<Override PartName=\"/xl/worksheets/sheet{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
        }
        xml.Append("</Types>");
        return xml.ToString();
    }

    private static void WriteEntry(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        var bytes = new UTF8Encoding(false).GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }

    private sealed class SheetWriter
    {
        private readonly ZipArchive _zip;
        private readonly ExportSheet _sheet;
        private readonly ExportOptions _options;
        private readonly StyleRegistry _styles;
        private readonly int _columnCount;
        private readonly int _headerRowCount;
        private readonly int _titleRows;

        private StreamWriter _writer = null!;
        private int _row;
        private int _firstDataRow;
        private int _lastDataRow;
        private long _dataRows;
        private readonly List<string> _merges = new();
        private readonly List<(int Start, int End)> _dataSpans = new();

        public SheetWriter(ZipArchive zip, ExportSheet sheet, ExportOptions options, StyleRegistry styles, int headerRows, int titleRows)
        {
            _zip = zip;
            _sheet = sheet;
            _options = options;
            _styles = styles;
            _columnCount = Math.Max(1, sheet.Columns.Count);
            _headerRowCount = headerRows;
            _titleRows = titleRows;
        }

        public long DataRows => _dataRows;

        public void Begin(int sheetIndex, bool first)
        {
            var entry = _zip.CreateEntry($"xl/worksheets/sheet{sheetIndex}.xml", CompressionLevel.Optimal);
            _writer = new StreamWriter(entry.Open(), new UTF8Encoding(false), 65536);
            _row = 0;
            _firstDataRow = 0;
            _lastDataRow = 0;
            _dataRows = 0;
            _merges.Clear();
            _dataSpans.Clear();

            _writer.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            _writer.Write("<sheetPr><outlinePr summaryBelow=\"0\"/></sheetPr>");

            var frozen = _headerRowCount + _titleRows;
            _writer.Write($"<sheetViews><sheetView workbookViewId=\"0\"{(first ? " tabSelected=\"1\"" : string.Empty)}>");
            if (_sheet.FreezeHeader && frozen > 0)
            {
                _writer.Write($"<pane ySplit=\"{frozen}\" topLeftCell=\"A{frozen + 1}\" activePane=\"bottomLeft\" state=\"frozen\"/><selection pane=\"bottomLeft\"/>");
            }
            _writer.Write("</sheetView></sheetViews><sheetFormatPr defaultRowHeight=\"15\" outlineLevelRow=\"" + (_sheet.IsStructured ? MaxOutlineLevel : 0) + "\"/>");

            if (_sheet.Columns.Count > 0)
            {
                _writer.Write("<cols>");
                for (var i = 0; i < _sheet.Columns.Count; i++)
                {
                    var width = Math.Clamp(Math.Round(_sheet.Columns[i].Width / 7.0, 2), 6, 80);
                    _writer.Write($"<col min=\"{i + 1}\" max=\"{i + 1}\" width=\"{width.ToString(CultureInfo.InvariantCulture)}\" customWidth=\"1\"/>");
                }
                _writer.Write("</cols>");
            }

            _writer.Write("<sheetData>");
            WriteTitle();
            WriteHeader();
        }

        private void WriteTitle()
        {
            if (_titleRows == 0) return;
            _row++;
            var style = _styles.Get(new StyleKey(null, true, false, null, null, 0, 0, false, 14, false));
            _writer.Write($"<row r=\"{_row}\" ht=\"22\" customHeight=\"1\">");
            WriteString(1, style, _sheet.Title ?? _sheet.Name);
            _writer.Write("</row>");
            if (_columnCount > 1) _merges.Add($"A{_row}:{ColumnName(_columnCount - 1)}{_row}");
        }

        private void WriteHeader()
        {
            if (!_options.IncludeHeaders || _sheet.Columns.Count == 0) return;

            var cells = ExportHeaderLayout.Build(_sheet.Columns, out var rows);
            var bandStyle = _styles.Get(new StyleKey(null, true, false, null, "#DCE6F2", 2, 0, false, 11, false));
            var titleStyle = _styles.Get(new StyleKey(null, true, false, null, "#F2F2F2", 0, 0, false, 11, false));
            var startRow = _row;
            for (var r = 0; r < rows; r++)
            {
                _row++;
                _writer.Write($"<row r=\"{_row}\">");
                // Cells covered by a row span must still be written (empty) so that styles apply across the merge.
                for (var c = 0; c < _columnCount; c++)
                {
                    var cell = cells.FirstOrDefault(h => h.Row == r && h.Column == c);
                    var covered = cells.FirstOrDefault(h => h.Row < r && h.Row + h.RowSpan > r && h.Column == c);
                    if (cell != null)
                    {
                        WriteString(c + 1, cell.IsBand ? bandStyle : titleStyle, cell.Text);
                        for (var extra = 1; extra < cell.ColumnSpan; extra++) WriteEmpty(c + 1 + extra, cell.IsBand ? bandStyle : titleStyle);
                    }
                    else if (covered != null)
                    {
                        WriteEmpty(c + 1, titleStyle);
                    }
                }
                _writer.Write("</row>");
            }

            foreach (var cell in cells.Where(h => h.ColumnSpan > 1 || h.RowSpan > 1))
            {
                var from = $"{ColumnName(cell.Column)}{startRow + cell.Row + 1}";
                var to = $"{ColumnName(cell.Column + cell.ColumnSpan - 1)}{startRow + cell.Row + cell.RowSpan}";
                _merges.Add($"{from}:{to}");
            }
        }

        public void WriteRow(ExportRow row)
        {
            _row++;
            var outline = _sheet.IsStructured ? Math.Clamp(row.OutlineLevel ?? row.Level, 0, MaxOutlineLevel) : 0;
            var rowAttributes = outline > 0 ? $" outlineLevel=\"{outline}\"" : string.Empty;

            switch (row.Kind)
            {
                case ExportRowKind.GroupHeader:
                {
                    _writer.Write($"<row r=\"{_row}\"{rowAttributes}>");
                    var style = _styles.Get(new StyleKey(null, true, false, null, "#E9EEF5", 1, 0, false, 11, false));
                    WriteString(1, style, row.Text ?? string.Empty);
                    for (var c = 2; c <= _columnCount; c++) WriteEmpty(c, style);
                    _writer.Write("</row>");
                    if (_columnCount > 1) _merges.Add($"A{_row}:{ColumnName(_columnCount - 1)}{_row}");
                    break;
                }

                case ExportRowKind.DetailHeader:
                {
                    _writer.Write($"<row r=\"{_row}\"{rowAttributes}>");
                    var nameStyle = _styles.Get(new StyleKey(null, false, true, "#595959", null, 3, 0, false, 11, false));
                    var titleStyle = _styles.Get(new StyleKey(null, true, false, null, "#F7F7F7", 0, 0, false, 11, false));
                    if (row.Level >= 1) WriteString(row.Level, nameStyle, row.RelationName ?? row.Text ?? string.Empty);
                    var detailColumns = row.DetailColumns ?? Array.Empty<ExportColumn>();
                    for (var i = 0; i < detailColumns.Count; i++) WriteString(row.Level + 1 + i, titleStyle, detailColumns[i].Title);
                    _writer.Write("</row>");
                    break;
                }

                case ExportRowKind.DetailData:
                {
                    _writer.Write($"<row r=\"{_row}\"{rowAttributes}>");
                    for (var i = 0; i < row.Cells.Length; i++)
                    {
                        WriteValue(row.Level + 1 + i, row.Cells[i], null, StyleFor(row, i, null, false, 0));
                    }
                    _writer.Write("</row>");
                    break;
                }

                default:
                {
                    var isData = row.Kind == ExportRowKind.Data;
                    _writer.Write($"<row r=\"{_row}\"{rowAttributes}>");
                    for (var i = 0; i < row.Cells.Length && i < Math.Max(_columnCount, row.Cells.Length); i++)
                    {
                        var column = i < _sheet.Columns.Count ? _sheet.Columns[i] : null;
                        var indent = i == 0 && _sheet.IsStructured && row.Level > 0 && isData ? Math.Min(row.Level, 7) : 0;
                        WriteValue(i + 1, row.Cells[i], column, StyleFor(row, i, column, !isData, indent));
                    }
                    _writer.Write("</row>");
                    if (isData)
                    {
                        _dataRows++;
                        if (_firstDataRow == 0) _firstDataRow = _row;
                        _lastDataRow = _row;
                        if (_dataSpans.Count > 0 && _dataSpans[^1].End == _row - 1) _dataSpans[^1] = (_dataSpans[^1].Start, _row);
                        else _dataSpans.Add((_row, _row));
                    }
                    break;
                }
            }
        }

        private int StyleFor(ExportRow row, int index, ExportColumn? column, bool summary, int indent)
        {
            var value = row.Cells[index];
            var cellStyle = _options.ApplyFormatting && row.CellStyles != null && index < row.CellStyles.Length ? row.CellStyles[index] : null;
            var rowStyle = _options.ApplyFormatting ? row.Style : null;
            var back = Normalize(cellStyle?.BackColorHex ?? rowStyle?.BackColorHex);
            var fore = Normalize(cellStyle?.ForeColorHex ?? rowStyle?.ForeColorHex);
            var align = column?.Alignment switch { ExportAlignment.Right => 3, ExportAlignment.Center => 2, ExportAlignment.Left => 1, _ => 0 };
            if (align == 0 && ExportFormatting.IsNumeric(value)) align = 0;

            var numberFormat = ExportFormatting.ToExcelNumberFormat(column?.Format, column?.DataType ?? value?.GetType(), _options.Culture);
            if (numberFormat == null && ExportFormatting.IsDate(value)) numberFormat = "yyyy-mm-dd";
            if (numberFormat == null && value is TimeSpan) numberFormat = "[h]:mm:ss";

            return _styles.Get(new StyleKey(
                numberFormat,
                summary || cellStyle?.Bold == true || rowStyle?.Bold == true,
                cellStyle?.Italic == true || rowStyle?.Italic == true,
                fore,
                back,
                align,
                indent,
                row.Kind is ExportRowKind.Summary or ExportRowKind.GroupFooter,
                11,
                false));
        }

        private static string? Normalize(string? hex) =>
            ExportFormatting.TryParseColor(hex, out var r, out var g, out var b) ? $"#{r:X2}{g:X2}{b:X2}" : null;

        public void WriteFooter(IReadOnlyList<ExportRow> footer)
        {
            foreach (var row in footer) WriteRow(row);
        }

        public void End()
        {
            _writer.Write("</sheetData>");

            if (_sheet.AutoFilter && !_sheet.IsStructured && _options.IncludeHeaders && _sheet.Columns.Count > 0 && _lastDataRow >= _firstDataRow && _firstDataRow > 0)
            {
                var headerRow = _titleRows + _headerRowCount;
                _writer.Write($"<autoFilter ref=\"A{headerRow}:{ColumnName(_columnCount - 1)}{_lastDataRow}\"/>");
            }

            if (_merges.Count > 0)
            {
                _writer.Write($"<mergeCells count=\"{_merges.Count}\">");
                foreach (var merge in _merges) _writer.Write($"<mergeCell ref=\"{merge}\"/>");
                _writer.Write("</mergeCells>");
            }

            if (_options.ApplyFormatting)
            {
                var priority = 1;
                foreach (var format in _sheet.ConditionalFormats)
                {
                    var column = _sheet.Columns.ToList().FindIndex(c => c.Key == format.ColumnKey);
                    if (column < 0 || _dataSpans.Count == 0) continue;

                    var range = string.Join(' ', _dataSpans.Select(s => $"{ColumnName(column)}{s.Start}:{ColumnName(column)}{s.End}"));
                    var min = format.Minimum.ToString("R", CultureInfo.InvariantCulture);
                    var max = format.Maximum.ToString("R", CultureInfo.InvariantCulture);
                    _writer.Write($"<conditionalFormatting sqref=\"{range}\">");
                    if (format.Type == ExportConditionalFormatType.DataBar)
                    {
                        _writer.Write($"<cfRule type=\"dataBar\" priority=\"{priority++}\"><dataBar><cfvo type=\"num\" val=\"{min}\"/><cfvo type=\"num\" val=\"{max}\"/><color rgb=\"{Argb(format.ColorHex)}\"/></dataBar></cfRule>");
                    }
                    else
                    {
                        _writer.Write($"<cfRule type=\"colorScale\" priority=\"{priority++}\"><colorScale><cfvo type=\"num\" val=\"{min}\"/><cfvo type=\"num\" val=\"{max}\"/><color rgb=\"{Argb(format.ColorHex)}\"/><color rgb=\"{Argb(format.SecondColorHex ?? format.ColorHex)}\"/></colorScale></cfRule>");
                    }
                    _writer.Write("</conditionalFormatting>");
                }
            }

            _writer.Write("<pageMargins left=\"0.7\" right=\"0.7\" top=\"0.75\" bottom=\"0.75\" header=\"0.3\" footer=\"0.3\"/></worksheet>");
            _writer.Dispose();
        }

        private void WriteEmpty(int column, int style) => _writer.Write($"<c r=\"{ColumnName(column - 1)}{_row}\" s=\"{style}\"/>");

        private void WriteString(int column, int style, string text)
        {
            var reference = $"{ColumnName(column - 1)}{_row}";
            _writer.Write($"<c r=\"{reference}\" s=\"{style}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{Escape(Truncate(text))}</t></is></c>");
        }

        private void WriteValue(int column, object? value, ExportColumn? exportColumn, int style)
        {
            var reference = $"{ColumnName(column - 1)}{_row}";
            switch (value)
            {
                case null:
                    if (style != 0) _writer.Write($"<c r=\"{reference}\" s=\"{style}\"/>");
                    return;
                case bool flag:
                    _writer.Write($"<c r=\"{reference}\" s=\"{style}\" t=\"b\"><v>{(flag ? 1 : 0)}</v></c>");
                    return;
                case sbyte or byte or short or ushort or int or uint or long or ulong:
                    _writer.Write($"<c r=\"{reference}\" s=\"{style}\"><v>{Convert.ToString(value, CultureInfo.InvariantCulture)}</v></c>");
                    return;
                case decimal d:
                    _writer.Write($"<c r=\"{reference}\" s=\"{style}\"><v>{d.ToString(CultureInfo.InvariantCulture)}</v></c>");
                    return;
                case double d when double.IsFinite(d):
                    _writer.Write($"<c r=\"{reference}\" s=\"{style}\"><v>{d.ToString("R", CultureInfo.InvariantCulture)}</v></c>");
                    return;
                case float f when float.IsFinite(f):
                    _writer.Write($"<c r=\"{reference}\" s=\"{style}\"><v>{f.ToString("R", CultureInfo.InvariantCulture)}</v></c>");
                    return;
                case DateTime dt when TryOaDate(dt, out var serial):
                    _writer.Write($"<c r=\"{reference}\" s=\"{style}\"><v>{serial.ToString("R", CultureInfo.InvariantCulture)}</v></c>");
                    return;
                case DateTimeOffset dto when TryOaDate(dto.DateTime, out var serial):
                    _writer.Write($"<c r=\"{reference}\" s=\"{style}\"><v>{serial.ToString("R", CultureInfo.InvariantCulture)}</v></c>");
                    return;
                case DateOnly date when TryOaDate(date.ToDateTime(TimeOnly.MinValue), out var serial):
                    _writer.Write($"<c r=\"{reference}\" s=\"{style}\"><v>{serial.ToString("R", CultureInfo.InvariantCulture)}</v></c>");
                    return;
                case TimeSpan span:
                    _writer.Write($"<c r=\"{reference}\" s=\"{style}\"><v>{span.TotalDays.ToString("R", CultureInfo.InvariantCulture)}</v></c>");
                    return;
                default:
                    var text = value is IFormattable formattable ? formattable.ToString(null, _options.Culture) : Convert.ToString(value, _options.Culture) ?? string.Empty;
                    WriteString(column, style, text);
                    return;
            }
        }

        private static bool TryOaDate(DateTime value, out double serial)
        {
            serial = 0;
            if (value.Year < 1900) return false;
            serial = value.ToOADate();
            return true;
        }

        private static string Truncate(string text) => text.Length <= MaxCellText ? text : text[..MaxCellText];
    }

    private static void WriteSheet(ZipArchive zip, ExportSheet sheet, ExportOptions options, StyleRegistry styles, List<string> names, CancellationToken ct)
    {
        var headerRows = options.IncludeHeaders && sheet.Columns.Count > 0 ? ExportHeaderLayout.Build(sheet.Columns, out var count).Count > 0 ? count : 1 : 0;
        var titleRows = string.IsNullOrWhiteSpace(sheet.Title) ? 0 : 1;
        var limit = Math.Max(1, options.MaxRowsPerSheet);

        var part = 0;
        var writer = (SheetWriter?)null;

        void Open()
        {
            part++;
            names.Add(UniqueName(part == 1 ? sheet.Name : $"{sheet.Name} ({part})", names));
            writer = new SheetWriter(zip, sheet, options, styles, headerRows, titleRows);
            writer.Begin(names.Count, names.Count == 1);
        }

        Open();
        foreach (var row in sheet.Rows)
        {
            ct.ThrowIfCancellationRequested();
            if (row.Kind == ExportRowKind.Data && writer!.DataRows >= limit)
            {
                writer.End();
                Open();
            }
            writer!.WriteRow(row);
        }

        writer!.WriteFooter(sheet.Footer);
        writer.End();
    }

    private static string UniqueName(string requested, List<string> existing)
    {
        var invalid = new[] { ':', '\\', '/', '?', '*', '[', ']' };
        var cleaned = new string(requested.Where(c => !invalid.Contains(c) && c >= ' ').ToArray()).Trim('\'', ' ');
        if (cleaned.Length == 0) cleaned = "Sheet";
        if (cleaned.Length > 31) cleaned = cleaned[..31];

        var name = cleaned;
        for (var n = 2; existing.Any(e => string.Equals(e, name, StringComparison.OrdinalIgnoreCase)); n++)
        {
            var suffix = $" ({n})";
            name = (cleaned.Length + suffix.Length > 31 ? cleaned[..(31 - suffix.Length)] : cleaned) + suffix;
        }
        return name;
    }

    internal static string ColumnName(int index)
    {
        var name = new StringBuilder();
        for (var n = index + 1; n > 0; n = (n - 1) / 26)
        {
            name.Insert(0, (char)('A' + (n - 1) % 26));
        }
        return name.ToString();
    }

    private static string Argb(string hex) =>
        ExportFormatting.TryParseColor(hex, out var r, out var g, out var b) ? $"FF{r:X2}{g:X2}{b:X2}" : "FF000000";

    internal static string Escape(string text)
    {
        var builder = new StringBuilder(text.Length + 8);
        foreach (var c in text)
        {
            switch (c)
            {
                case '&': builder.Append("&amp;"); break;
                case '<': builder.Append("&lt;"); break;
                case '>': builder.Append("&gt;"); break;
                case '"': builder.Append("&quot;"); break;
                case '\t' or '\n' or '\r': builder.Append(c); break;
                default:
                    // Characters that are not valid in XML 1.0 would corrupt the workbook, so they are dropped.
                    if (c >= 0x20 && c != 0xFFFE && c != 0xFFFF && (c < 0xD800 || c > 0xDFFF || char.IsSurrogate(c))) builder.Append(c);
                    break;
            }
        }
        return builder.ToString();
    }
}

/// <summary>Excel 2003 XML Spreadsheet (SpreadsheetML), kept for tools that cannot read .xlsx. Cells are typed and sheet names are sanitized.</summary>
public sealed class SpreadsheetMlExportProvider : IExportProvider
{
    public string Id => "excelxml";
    public string DisplayName => "Excel 2003 XML Spreadsheet";
    public string FileExtension => ".xml";
    public string ContentType => "application/vnd.ms-excel";

    public Task WriteAsync(ExportDocument document, Stream output, ExportContext context) =>
        Task.Run(() => Write(document, output, context), context.CancellationToken);

    private static void Write(ExportDocument document, Stream output, ExportContext context)
    {
        var options = context.Options;
        using var writer = new StreamWriter(output, new UTF8Encoding(false), 65536, leaveOpen: true);
        writer.WriteLine("<?xml version=\"1.0\"?>");
        writer.WriteLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\">");
        writer.WriteLine("<Styles><Style ss:ID=\"h\"><Font ss:Bold=\"1\"/></Style><Style ss:ID=\"d\"><NumberFormat ss:Format=\"yyyy\\-mm\\-dd\"/></Style></Styles>");

        var names = new List<string>();
        foreach (var sheet in document.Sheets)
        {
            var name = names.Count + 1 is var n && string.IsNullOrWhiteSpace(sheet.Name) ? $"Sheet{n}" : new string(sheet.Name.Where(c => c is not (':' or '\\' or '/' or '?' or '*' or '[' or ']')).ToArray());
            if (name.Length > 31) name = name[..31];
            var unique = name;
            for (var i = 2; names.Contains(unique, StringComparer.OrdinalIgnoreCase); i++) unique = $"{name[..Math.Min(name.Length, 28)]} {i}";
            names.Add(unique);

            writer.WriteLine($"<Worksheet ss:Name=\"{XlsxExportProvider.Escape(unique)}\"><Table>");
            if (options.IncludeHeaders)
            {
                writer.Write("<Row>");
                foreach (var column in sheet.Columns) writer.Write($"<Cell ss:StyleID=\"h\"><Data ss:Type=\"String\">{XlsxExportProvider.Escape(column.Title)}</Data></Cell>");
                writer.WriteLine("</Row>");
            }

            foreach (var row in sheet.Rows.Concat(sheet.Footer))
            {
                writer.Write("<Row>");
                switch (row.Kind)
                {
                    case ExportRowKind.GroupHeader:
                        writer.Write($"<Cell ss:StyleID=\"h\"><Data ss:Type=\"String\">{XlsxExportProvider.Escape(row.Text ?? string.Empty)}</Data></Cell>");
                        break;
                    case ExportRowKind.DetailHeader:
                        foreach (var _ in Enumerable.Range(0, row.Level)) writer.Write("<Cell/>");
                        foreach (var column in row.DetailColumns ?? Array.Empty<ExportColumn>()) writer.Write($"<Cell ss:StyleID=\"h\"><Data ss:Type=\"String\">{XlsxExportProvider.Escape(column.Title)}</Data></Cell>");
                        break;
                    default:
                        if (row.Kind == ExportRowKind.DetailData) foreach (var _ in Enumerable.Range(0, row.Level)) writer.Write("<Cell/>");
                        foreach (var value in row.Cells) WriteCell(writer, value, options);
                        break;
                }
                writer.WriteLine("</Row>");
            }

            writer.WriteLine("</Table></Worksheet>");
        }

        writer.WriteLine("</Workbook>");
    }

    private static void WriteCell(StreamWriter writer, object? value, ExportOptions options)
    {
        switch (value)
        {
            case null:
                writer.Write("<Cell/>");
                break;
            case bool b:
                writer.Write($"<Cell><Data ss:Type=\"Boolean\">{(b ? 1 : 0)}</Data></Cell>");
                break;
            case DateTime dt:
                writer.Write($"<Cell ss:StyleID=\"d\"><Data ss:Type=\"DateTime\">{dt:yyyy-MM-ddTHH:mm:ss.fff}</Data></Cell>");
                break;
            case DateTimeOffset dto:
                writer.Write($"<Cell ss:StyleID=\"d\"><Data ss:Type=\"DateTime\">{dto.DateTime:yyyy-MM-ddTHH:mm:ss.fff}</Data></Cell>");
                break;
            default:
                if (ExportFormatting.IsNumeric(value) && (value is not (double or float) || double.IsFinite(Convert.ToDouble(value, CultureInfo.InvariantCulture))))
                {
                    writer.Write($"<Cell><Data ss:Type=\"Number\">{Convert.ToString(value, CultureInfo.InvariantCulture)}</Data></Cell>");
                }
                else
                {
                    writer.Write($"<Cell><Data ss:Type=\"String\">{XlsxExportProvider.Escape(Convert.ToString(value, options.Culture) ?? string.Empty)}</Data></Cell>");
                }
                break;
        }
    }
}
