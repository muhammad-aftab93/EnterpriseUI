using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace EnterpriseUI.Office;

/// <summary>A formula result that is an error such as #DIV/0!.</summary>
public sealed record SheetError(string Code)
{
    public static readonly SheetError DivideByZero = new("#DIV/0!");
    public static readonly SheetError Value = new("#VALUE!");
    public static readonly SheetError Name = new("#NAME?");
    public static readonly SheetError Reference = new("#REF!");
    public static readonly SheetError Number = new("#NUM!");
    public override string ToString() => Code;
}

/// <summary>A formula written by the streaming writer; the expression has no leading '='.</summary>
public sealed record SheetFormula(string Expression);

/// <summary>A1-style reference helpers. Rows and columns are 1-based.</summary>
public static class SheetReference
{
    public const int MaxRows = 1_048_576;
    public const int MaxColumns = 16_384;

    public static string ColumnName(int column)
    {
        if (column < 1 || column > MaxColumns) throw new ArgumentOutOfRangeException(nameof(column));
        var sb = new StringBuilder();
        for (var n = column; n > 0; n = (n - 1) / 26) sb.Insert(0, (char)('A' + (n - 1) % 26));
        return sb.ToString();
    }

    public static int ColumnIndex(string name)
    {
        var value = 0;
        foreach (var c in name.ToUpperInvariant())
        {
            if (c is < 'A' or > 'Z') throw new FormatException($"'{name}' is not a column name.");
            value = value * 26 + (c - 'A' + 1);
            if (value > MaxColumns) throw new FormatException($"'{name}' is outside the supported column range.");
        }
        return value > 0 ? value : throw new FormatException("A column name is required.");
    }

    public static bool TryParse(string? reference, out int row, out int column)
    {
        row = column = 0;
        if (string.IsNullOrWhiteSpace(reference)) return false;
        var match = Regex.Match(reference.Trim(), @"^\$?([A-Za-z]{1,3})\$?(\d{1,7})$");
        if (!match.Success) return false;
        try { column = ColumnIndex(match.Groups[1].Value); } catch (FormatException) { return false; }
        row = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        return row is >= 1 and <= MaxRows;
    }

    public static string Format(int row, int column) => ColumnName(column) + row.ToString(CultureInfo.InvariantCulture);
}

internal static class XlsxPackage
{
    public const string MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    public const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    public const string PackageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";
    public const long MaxPartBytes = 128L * 1024 * 1024;
    public const int DateStyle = 1;

    public static XmlWriter CreateWriter(Stream stream) =>
        XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), CloseOutput = false });

    public static XmlReader CreateReader(Stream stream) =>
        XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreWhitespace = true });

    public static string SafeSheetName(string? name, ICollection<string> used)
    {
        var cleaned = new string((name ?? string.Empty).Select(c => "[]:*?/\\".Contains(c) ? '_' : c).ToArray()).Trim('\'', ' ');
        if (cleaned.Length == 0) cleaned = "Sheet" + (used.Count + 1);
        if (cleaned.Length > 31) cleaned = cleaned[..31];
        var candidate = cleaned;
        for (var i = 2; used.Contains(candidate, StringComparer.OrdinalIgnoreCase); i++)
        {
            var suffix = "_" + i;
            candidate = cleaned[..Math.Min(cleaned.Length, 31 - suffix.Length)] + suffix;
        }
        return candidate;
    }

    public static string CleanXml(string text)
    {
        var sb = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])) sb.Append(c).Append(text[++i]);
            else if (!char.IsSurrogate(c) && XmlConvert.IsXmlChar(c)) sb.Append(c);
        }
        return sb.ToString();
    }

    public static void WriteCell(XmlWriter w, string reference, object? value, string? formula = null, object? cached = null)
    {
        if (formula is not null)
        {
            w.WriteStartElement("c");
            w.WriteAttributeString("r", reference);
            var result = cached;
            switch (result)
            {
                case string: w.WriteAttributeString("t", "str"); break;
                case bool: w.WriteAttributeString("t", "b"); break;
                case SheetError: w.WriteAttributeString("t", "e"); break;
            }
            w.WriteElementString("f", CleanXml(formula));
            if (result is not null) w.WriteElementString("v", Scalar(result));
            w.WriteEndElement();
            return;
        }

        switch (value)
        {
            case null:
                return;
            case SheetFormula f:
                WriteCell(w, reference, null, f.Expression);
                return;
            case SheetError error:
                w.WriteStartElement("c"); w.WriteAttributeString("r", reference); w.WriteAttributeString("t", "e");
                w.WriteElementString("v", error.Code); w.WriteEndElement();
                return;
            case string s:
                WriteString(w, reference, s);
                return;
            case char ch:
                WriteString(w, reference, ch.ToString());
                return;
            case bool b:
                w.WriteStartElement("c"); w.WriteAttributeString("r", reference); w.WriteAttributeString("t", "b");
                w.WriteElementString("v", b ? "1" : "0"); w.WriteEndElement();
                return;
            case DateTime or DateTimeOffset or DateOnly:
                WriteNumber(w, reference, ToOADate(value), DateStyle);
                return;
            case TimeSpan span:
                WriteNumber(w, reference, span.TotalDays.ToString("R", CultureInfo.InvariantCulture), null);
                return;
            case sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal:
                var text = Scalar(value);
                if (text is "NaN" or "Infinity" or "-Infinity") { WriteCell(w, reference, SheetError.Number); return; }
                WriteNumber(w, reference, text, null);
                return;
            default:
                WriteString(w, reference, Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
                return;
        }
    }

    private static string ToOADate(object value) => value switch
    {
        DateTime d => d.ToOADate().ToString("R", CultureInfo.InvariantCulture),
        DateTimeOffset o => o.DateTime.ToOADate().ToString("R", CultureInfo.InvariantCulture),
        DateOnly d => d.ToDateTime(TimeOnly.MinValue).ToOADate().ToString("R", CultureInfo.InvariantCulture),
        _ => "0"
    };

    private static void WriteNumber(XmlWriter w, string reference, string number, int? style)
    {
        w.WriteStartElement("c");
        w.WriteAttributeString("r", reference);
        if (style is { } s) w.WriteAttributeString("s", s.ToString(CultureInfo.InvariantCulture));
        w.WriteElementString("v", number);
        w.WriteEndElement();
    }

    private static void WriteString(XmlWriter w, string reference, string text)
    {
        w.WriteStartElement("c");
        w.WriteAttributeString("r", reference);
        w.WriteAttributeString("t", "inlineStr");
        w.WriteStartElement("is");
        w.WriteStartElement("t");
        w.WriteAttributeString("xml", "space", null, "preserve");
        w.WriteString(CleanXml(text));
        w.WriteEndElement();
        w.WriteEndElement();
        w.WriteEndElement();
    }

    private static string Scalar(object value) => value switch
    {
        bool b => b ? "1" : "0",
        string s => CleanXml(s),
        SheetError e => e.Code,
        double d => d.ToString("R", CultureInfo.InvariantCulture),
        float f => f.ToString("R", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
    };

    public static void WriteWorkbookParts(System.IO.Compression.ZipArchive zip, IReadOnlyList<string> sheetNames)
    {
        Part(zip, "[Content_Types].xml", w =>
        {
            w.WriteStartElement("Types", "http://schemas.openxmlformats.org/package/2006/content-types");
            Default(w, "rels", "application/vnd.openxmlformats-package.relationships+xml");
            Default(w, "xml", "application/xml");
            Override(w, "/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
            Override(w, "/xl/styles.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml");
            for (var i = 0; i < sheetNames.Count; i++) Override(w, $"/xl/worksheets/sheet{i + 1}.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
            w.WriteEndElement();
        });

        Part(zip, "_rels/.rels", w =>
        {
            w.WriteStartElement("Relationships", PackageRelNs);
            Relationship(w, "rId1", RelNs + "/officeDocument", "xl/workbook.xml");
            w.WriteEndElement();
        });

        Part(zip, "xl/_rels/workbook.xml.rels", w =>
        {
            w.WriteStartElement("Relationships", PackageRelNs);
            for (var i = 0; i < sheetNames.Count; i++) Relationship(w, $"rId{i + 1}", RelNs + "/worksheet", $"worksheets/sheet{i + 1}.xml");
            Relationship(w, $"rId{sheetNames.Count + 1}", RelNs + "/styles", "styles.xml");
            w.WriteEndElement();
        });

        Part(zip, "xl/workbook.xml", w =>
        {
            w.WriteStartElement("workbook", MainNs);
            w.WriteAttributeString("xmlns", "r", null, RelNs);
            w.WriteStartElement("sheets");
            for (var i = 0; i < sheetNames.Count; i++)
            {
                w.WriteStartElement("sheet");
                w.WriteAttributeString("name", CleanXml(sheetNames[i]));
                w.WriteAttributeString("sheetId", (i + 1).ToString(CultureInfo.InvariantCulture));
                w.WriteAttributeString("r", "id", RelNs, $"rId{i + 1}");
                w.WriteEndElement();
            }
            w.WriteEndElement();
            w.WriteEndElement();
        });

        Part(zip, "xl/styles.xml", w =>
        {
            w.WriteStartElement("styleSheet", MainNs);
            w.WriteStartElement("fonts"); w.WriteAttributeString("count", "1");
            w.WriteStartElement("font"); w.WriteStartElement("sz"); w.WriteAttributeString("val", "11"); w.WriteEndElement(); w.WriteEndElement();
            w.WriteEndElement();
            w.WriteStartElement("fills"); w.WriteAttributeString("count", "2");
            w.WriteStartElement("fill"); w.WriteStartElement("patternFill"); w.WriteAttributeString("patternType", "none"); w.WriteEndElement(); w.WriteEndElement();
            w.WriteStartElement("fill"); w.WriteStartElement("patternFill"); w.WriteAttributeString("patternType", "gray125"); w.WriteEndElement(); w.WriteEndElement();
            w.WriteEndElement();
            w.WriteStartElement("borders"); w.WriteAttributeString("count", "1");
            w.WriteStartElement("border"); foreach (var side in new[] { "left", "right", "top", "bottom", "diagonal" }) w.WriteElementString(side, null); w.WriteEndElement();
            w.WriteEndElement();
            w.WriteStartElement("cellXfs"); w.WriteAttributeString("count", "2");
            w.WriteStartElement("xf"); w.WriteAttributeString("numFmtId", "0"); w.WriteAttributeString("fontId", "0"); w.WriteAttributeString("fillId", "0"); w.WriteAttributeString("borderId", "0"); w.WriteEndElement();
            w.WriteStartElement("xf"); w.WriteAttributeString("numFmtId", "14"); w.WriteAttributeString("fontId", "0"); w.WriteAttributeString("fillId", "0"); w.WriteAttributeString("borderId", "0"); w.WriteAttributeString("applyNumberFormat", "1"); w.WriteEndElement();
            w.WriteEndElement();
            w.WriteEndElement();
        });
    }

    private static void Part(System.IO.Compression.ZipArchive zip, string path, Action<XmlWriter> body)
    {
        var entry = zip.CreateEntry(path, System.IO.Compression.CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var w = CreateWriter(stream);
        w.WriteStartDocument();
        body(w);
        w.WriteEndDocument();
    }

    private static void Default(XmlWriter w, string extension, string type)
    {
        w.WriteStartElement("Default"); w.WriteAttributeString("Extension", extension); w.WriteAttributeString("ContentType", type); w.WriteEndElement();
    }

    private static void Override(XmlWriter w, string part, string type)
    {
        w.WriteStartElement("Override"); w.WriteAttributeString("PartName", part); w.WriteAttributeString("ContentType", type); w.WriteEndElement();
    }

    private static void Relationship(XmlWriter w, string id, string type, string target)
    {
        w.WriteStartElement("Relationship"); w.WriteAttributeString("Id", id); w.WriteAttributeString("Type", type); w.WriteAttributeString("Target", target); w.WriteEndElement();
    }
}
