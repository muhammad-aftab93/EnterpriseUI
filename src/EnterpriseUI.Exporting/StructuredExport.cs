using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml;

namespace EnterpriseUI.Exporting;

/// <summary>A header cell of a possibly multi-row (banded) header.</summary>
public sealed record ExportHeaderCell(string Text, int Row, int Column, int ColumnSpan, int RowSpan, bool IsBand);

public static class ExportHeaderLayout
{
    /// <summary>Lays out band rows above the column titles, merging equal neighbouring bands and stretching unbanded columns over the band rows.</summary>
    public static IReadOnlyList<ExportHeaderCell> Build(IReadOnlyList<ExportColumn> columns, out int rowCount)
    {
        var depth = columns.Count == 0 ? 0 : columns.Max(c => c.Bands.Count);
        rowCount = depth + 1;
        var cells = new List<ExportHeaderCell>();

        for (var level = 0; level < depth; level++)
        {
            var i = 0;
            while (i < columns.Count)
            {
                var band = columns[i].Bands.Count > level ? columns[i].Bands[level] : null;
                if (band == null)
                {
                    i++;
                    continue;
                }

                var start = i;
                while (i < columns.Count && columns[i].Bands.Count > level && columns[i].Bands[level] == band && SamePath(columns[start], columns[i], level))
                {
                    i++;
                }
                cells.Add(new ExportHeaderCell(band, level, start, i - start, 1, true));
            }
        }

        for (var c = 0; c < columns.Count; c++)
        {
            var own = columns[c].Bands.Count;
            cells.Add(new ExportHeaderCell(columns[c].Title, own, c, 1, rowCount - own, false));
        }

        return cells;
    }

    private static bool SamePath(ExportColumn a, ExportColumn b, int level)
    {
        for (var l = 0; l <= level; l++)
        {
            if (!string.Equals(a.Bands[l], b.Bands[l], StringComparison.Ordinal)) return false;
        }
        return true;
    }
}

public sealed class CsvExportProvider : IExportProvider
{
    public string Id => "csv";
    public string DisplayName => "CSV (comma separated)";
    public string FileExtension => ".csv";
    public string ContentType => "text/csv";

    public Task WriteAsync(ExportDocument document, Stream output, ExportContext context) =>
        Task.Run(() => Write(document, output, context), context.CancellationToken);

    private static void Write(ExportDocument document, Stream output, ExportContext context)
    {
        var options = context.Options;
        // A byte order mark lets Excel open UTF-8 files with the right encoding.
        using var writer = new StreamWriter(output, new UTF8Encoding(true), 65536, leaveOpen: true) { NewLine = "\r\n" };
        var first = true;
        foreach (var sheet in document.Sheets)
        {
            if (!first)
            {
                writer.WriteLine();
                writer.WriteLine(Field($"# {sheet.Name}", options));
            }
            first = false;

            if (options.IncludeHeaders)
            {
                WriteLine(writer, sheet.Columns.Select(c => c.Title), options);
            }

            foreach (var row in sheet.Rows)
            {
                WriteRow(writer, sheet, row, options);
            }

            foreach (var row in sheet.Footer)
            {
                WriteRow(writer, sheet, row, options);
            }
        }
    }

    private static void WriteRow(StreamWriter writer, ExportSheet sheet, ExportRow row, ExportOptions options)
    {
        switch (row.Kind)
        {
            case ExportRowKind.GroupHeader:
                WriteLine(writer, new[] { row.Text ?? string.Empty }, options);
                break;

            case ExportRowKind.DetailHeader:
                WriteLine(writer, Enumerable.Repeat(string.Empty, row.Level).Concat((row.DetailColumns ?? Array.Empty<ExportColumn>()).Select(c => c.Title)), options);
                break;

            case ExportRowKind.DetailData:
                WriteLine(writer, Enumerable.Repeat(string.Empty, row.Level).Concat(row.Cells.Select(c => CellText(c, null, options))), options);
                break;

            default:
                WriteLine(writer, row.Cells.Select((c, i) => CellText(c, i < sheet.Columns.Count ? sheet.Columns[i] : null, options)), options);
                break;
        }
    }

    private static string CellText(object? value, ExportColumn? column, ExportOptions options)
    {
        var text = ExportFormatting.FormatCell(value, column, options.Culture);
        return value is string && options.CsvSanitizeFormulas && text.Length > 0 && text[0] is '=' or '+' or '-' or '@' or '\t' or '\r'
            ? "'" + text
            : text;
    }

    private static void WriteLine(StreamWriter writer, IEnumerable<string> values, ExportOptions options)
    {
        var first = true;
        foreach (var value in values)
        {
            if (!first) writer.Write(options.CsvDelimiter);
            first = false;
            writer.Write(Field(value, options));
        }
        writer.WriteLine();
    }

    private static string Field(string value, ExportOptions options)
    {
        var needsQuotes = options.CsvQuoteAll || value.Contains(options.CsvDelimiter) || value.Contains('"') || value.Contains('\n') || value.Contains('\r') ||
                          (value.Length > 0 && (value[0] == ' ' || value[^1] == ' '));
        return needsQuotes ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }
}

/// <summary>
/// Turns the flat row stream into nested records: groups contain their rows and child tables sit inside the parent record.
/// </summary>
internal abstract class StructuredWalker
{
    private enum FrameKind { Group, Record, Children }

    private readonly record struct Frame(FrameKind Kind, int Depth, IReadOnlyList<ExportColumn>? Columns);

    private readonly Stack<Frame> _frames = new();
    private int _groupCount;

    protected abstract void BeginGroup(ExportRow row);
    protected abstract void EndGroup();
    protected abstract void BeginRecord(ExportRow row, IReadOnlyList<ExportColumn> columns);
    protected abstract void EndRecord();
    protected abstract void BeginChildren(ExportRow header);
    protected abstract void EndChildren();

    protected void Walk(ExportSheet sheet)
    {
        foreach (var row in sheet.Rows)
        {
            switch (row.Kind)
            {
                case ExportRowKind.GroupHeader:
                    PopRecords();
                    PopGroupsTo(row.Level);
                    BeginGroup(row);
                    _frames.Push(new Frame(FrameKind.Group, row.Level, null));
                    _groupCount++;
                    break;

                case ExportRowKind.Data:
                    PopRecords();
                    PopGroupsTo(Math.Min(row.Level, _groupCount));
                    BeginRecord(row, sheet.Columns);
                    _frames.Push(new Frame(FrameKind.Record, 0, sheet.Columns));
                    break;

                case ExportRowKind.DetailHeader:
                    PopUntil(f => f.Kind == FrameKind.Record && f.Depth == row.Level - 1);
                    if (_frames.Count == 0) break;
                    BeginChildren(row);
                    _frames.Push(new Frame(FrameKind.Children, row.Level, row.DetailColumns ?? Array.Empty<ExportColumn>()));
                    break;

                case ExportRowKind.DetailData:
                    PopUntil(f => f.Kind == FrameKind.Children && f.Depth == row.Level);
                    if (_frames.Count == 0) break;
                    var columns = _frames.Peek().Columns ?? Array.Empty<ExportColumn>();
                    BeginRecord(row, columns);
                    _frames.Push(new Frame(FrameKind.Record, row.Level, columns));
                    break;
            }
        }

        while (_frames.Count > 0) PopFrame();
        _groupCount = 0;
    }

    private void PopRecords()
    {
        while (_frames.Count > 0 && _frames.Peek().Kind != FrameKind.Group) PopFrame();
    }

    private void PopGroupsTo(int groups)
    {
        while (_groupCount > groups && _frames.Count > 0) PopFrame();
    }

    private void PopUntil(Func<Frame, bool> match)
    {
        while (_frames.Count > 0 && !match(_frames.Peek())) PopFrame();
    }

    private void PopFrame()
    {
        var frame = _frames.Pop();
        switch (frame.Kind)
        {
            case FrameKind.Group:
                EndGroup();
                _groupCount--;
                break;
            case FrameKind.Record:
                EndRecord();
                break;
            default:
                EndChildren();
                break;
        }
    }
}

public sealed class JsonExportProvider : IExportProvider
{
    public string Id => "json";
    public string DisplayName => "JSON";
    public string FileExtension => ".json";
    public string ContentType => "application/json";

    public Task WriteAsync(ExportDocument document, Stream output, ExportContext context) =>
        Task.Run(() => Write(document, output, context), context.CancellationToken);

    private static void Write(ExportDocument document, Stream output, ExportContext context)
    {
        using var writer = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = context.Options.JsonIndented });
        writer.WriteStartObject();
        writer.WriteString("title", document.Title);
        writer.WriteString("generatedUtc", document.CreatedUtc);
        writer.WriteStartArray("sheets");
        foreach (var sheet in document.Sheets)
        {
            new JsonSheetWriter(writer, context.Options).WriteSheet(sheet);
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private sealed class JsonSheetWriter : StructuredWalker
    {
        private readonly Utf8JsonWriter _writer;
        private readonly ExportOptions _options;
        private IReadOnlyList<ExportColumn> _columns = Array.Empty<ExportColumn>();

        public JsonSheetWriter(Utf8JsonWriter writer, ExportOptions options)
        {
            _writer = writer;
            _options = options;
        }

        public void WriteSheet(ExportSheet sheet)
        {
            _columns = sheet.Columns;
            _writer.WriteStartObject();
            _writer.WriteString("name", sheet.Name);
            _writer.WriteStartArray("columns");
            foreach (var column in sheet.Columns)
            {
                _writer.WriteStartObject();
                _writer.WriteString("key", column.Key);
                _writer.WriteString("title", column.Title);
                if (column.DataType != null) _writer.WriteString("type", (Nullable.GetUnderlyingType(column.DataType) ?? column.DataType).Name);
                _writer.WriteEndObject();
            }
            _writer.WriteEndArray();

            _writer.WriteStartArray("rows");
            Walk(sheet);
            _writer.WriteEndArray();

            if (sheet.Footer.Count > 0)
            {
                _writer.WriteStartObject("summary");
                foreach (var row in sheet.Footer)
                {
                    for (var i = 0; i < row.Cells.Length && i < sheet.Columns.Count; i++)
                    {
                        if (row.Cells[i] == null) continue;
                        _writer.WritePropertyName(sheet.Columns[i].Key);
                        WriteValue(row.Cells[i]);
                    }
                }
                _writer.WriteEndObject();
            }

            _writer.WriteEndObject();
        }

        protected override void BeginGroup(ExportRow row)
        {
            _writer.WriteStartObject();
            _writer.WriteString("group", row.Text);
            _writer.WriteString("field", row.GroupField);
            _writer.WritePropertyName("key");
            WriteValue(row.GroupKey);
            _writer.WriteNumber("count", row.GroupItemCount);
            if (row.Aggregates is { Count: > 0 })
            {
                _writer.WriteStartObject("aggregates");
                foreach (var (name, value) in row.Aggregates)
                {
                    _writer.WritePropertyName(name);
                    WriteValue(value);
                }
                _writer.WriteEndObject();
            }
            _writer.WriteStartArray("items");
        }

        protected override void EndGroup()
        {
            _writer.WriteEndArray();
            _writer.WriteEndObject();
        }

        protected override void BeginRecord(ExportRow row, IReadOnlyList<ExportColumn> columns)
        {
            _writer.WriteStartObject();
            for (var i = 0; i < row.Cells.Length && i < columns.Count; i++)
            {
                _writer.WritePropertyName(columns[i].Key);
                WriteValue(row.Cells[i]);
            }
        }

        protected override void EndRecord() => _writer.WriteEndObject();

        protected override void BeginChildren(ExportRow header)
        {
            _writer.WriteStartArray(header.RelationName ?? "Details");
        }

        protected override void EndChildren() => _writer.WriteEndArray();

        private void WriteValue(object? value)
        {
            switch (value)
            {
                case null: _writer.WriteNullValue(); break;
                case string s: _writer.WriteStringValue(s); break;
                case bool b: _writer.WriteBooleanValue(b); break;
                case int i: _writer.WriteNumberValue(i); break;
                case long l: _writer.WriteNumberValue(l); break;
                case short sh: _writer.WriteNumberValue(sh); break;
                case byte by: _writer.WriteNumberValue(by); break;
                case uint ui: _writer.WriteNumberValue(ui); break;
                case ulong ul: _writer.WriteNumberValue(ul); break;
                case decimal d: _writer.WriteNumberValue(d); break;
                case double d when double.IsFinite(d): _writer.WriteNumberValue(d); break;
                case float f when float.IsFinite(f): _writer.WriteNumberValue(f); break;
                case double or float: _writer.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture)); break;
                case DateTime dt: _writer.WriteStringValue(dt); break;
                case DateTimeOffset dto: _writer.WriteStringValue(dto); break;
                case Guid g: _writer.WriteStringValue(g); break;
                default: _writer.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture)); break;
            }
        }
    }
}

public sealed class XmlExportProvider : IExportProvider
{
    public string Id => "xml";
    public string DisplayName => "XML";
    public string FileExtension => ".xml";
    public string ContentType => "application/xml";

    public Task WriteAsync(ExportDocument document, Stream output, ExportContext context) =>
        Task.Run(() => Write(document, output, context), context.CancellationToken);

    private static void Write(ExportDocument document, Stream output, ExportContext context)
    {
        var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false), CloseOutput = false, CheckCharacters = false };
        using var xml = XmlWriter.Create(output, settings);
        xml.WriteStartDocument();
        xml.WriteStartElement("Export");
        xml.WriteAttributeString("title", Clean(document.Title));
        xml.WriteAttributeString("generatedUtc", document.CreatedUtc.ToString("O", CultureInfo.InvariantCulture));
        foreach (var sheet in document.Sheets)
        {
            new XmlSheetWriter(xml).WriteSheet(sheet);
        }
        xml.WriteEndElement();
        xml.WriteEndDocument();
    }

    internal static string Clean(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c is '\t' or '\n' or '\r' || (c >= 0x20 && c <= 0xD7FF) || (c >= 0xE000 && c <= 0xFFFD)) builder.Append(c);
            else if (char.IsSurrogate(c)) builder.Append(c);
        }
        return builder.ToString();
    }

    internal static string Name(string key)
    {
        var encoded = XmlConvert.EncodeLocalName(string.IsNullOrWhiteSpace(key) ? "Field" : key.Trim());
        return char.IsDigit(encoded[0]) ? "_" + encoded : encoded;
    }

    private sealed class XmlSheetWriter : StructuredWalker
    {
        private readonly XmlWriter _xml;

        public XmlSheetWriter(XmlWriter xml) => _xml = xml;

        public void WriteSheet(ExportSheet sheet)
        {
            _xml.WriteStartElement("Sheet");
            _xml.WriteAttributeString("name", Clean(sheet.Name));
            _xml.WriteStartElement("Rows");
            Walk(sheet);
            _xml.WriteEndElement();

            if (sheet.Footer.Count > 0)
            {
                _xml.WriteStartElement("Summary");
                foreach (var row in sheet.Footer)
                {
                    for (var i = 0; i < row.Cells.Length && i < sheet.Columns.Count; i++)
                    {
                        if (row.Cells[i] == null) continue;
                        _xml.WriteElementString(Name(sheet.Columns[i].Key), Text(row.Cells[i]));
                    }
                }
                _xml.WriteEndElement();
            }

            _xml.WriteEndElement();
        }

        protected override void BeginGroup(ExportRow row)
        {
            _xml.WriteStartElement("Group");
            _xml.WriteAttributeString("field", Clean(row.GroupField));
            _xml.WriteAttributeString("key", Text(row.GroupKey));
            _xml.WriteAttributeString("count", row.GroupItemCount.ToString(CultureInfo.InvariantCulture));
            if (row.Aggregates != null)
            {
                foreach (var (name, value) in row.Aggregates)
                {
                    _xml.WriteStartElement("Aggregate");
                    _xml.WriteAttributeString("name", Clean(name));
                    _xml.WriteString(Text(value));
                    _xml.WriteEndElement();
                }
            }
            _xml.WriteStartElement("Items");
        }

        protected override void EndGroup()
        {
            _xml.WriteEndElement();
            _xml.WriteEndElement();
        }

        protected override void BeginRecord(ExportRow row, IReadOnlyList<ExportColumn> columns)
        {
            _xml.WriteStartElement("Row");
            for (var i = 0; i < row.Cells.Length && i < columns.Count; i++)
            {
                _xml.WriteStartElement(Name(columns[i].Key));
                if (row.Cells[i] != null) _xml.WriteString(Text(row.Cells[i]));
                _xml.WriteEndElement();
            }
        }

        protected override void EndRecord() => _xml.WriteEndElement();

        protected override void BeginChildren(ExportRow header) => _xml.WriteStartElement(Name(header.RelationName ?? "Details"));

        protected override void EndChildren() => _xml.WriteEndElement();

        private static string Text(object? value) => Clean(value switch
        {
            null => string.Empty,
            DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("O", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        });
    }
}
