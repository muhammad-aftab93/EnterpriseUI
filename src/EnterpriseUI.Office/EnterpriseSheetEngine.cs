using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace EnterpriseUI.Office;

/// <summary>Headless SpreadProcessing: reads and writes .xlsx workbooks and evaluates worksheet formulas.</summary>
public static class EnterpriseSheetEngine
{
    public static byte[] Write(SpreadsheetWorkbook workbook, bool storeCalculatedValues = true)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        var sheets = workbook.Worksheets.Count == 0 ? new List<SpreadsheetWorksheet> { new() } : workbook.Worksheets;
        var names = new List<string>();
        foreach (var sheet in sheets) names.Add(XlsxPackage.SafeSheetName(sheet.Name, names));

        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            for (var i = 0; i < sheets.Count; i++)
            {
                var entry = zip.CreateEntry($"xl/worksheets/sheet{i + 1}.xml", CompressionLevel.Optimal);
                using var stream = entry.Open();
                using var w = XlsxPackage.CreateWriter(stream);
                WriteSheet(w, sheets[i], storeCalculatedValues);
            }
            XlsxPackage.WriteWorkbookParts(zip, names);
        }
        return output.ToArray();
    }

    public static SpreadsheetWorkbook Read(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        using var input = new MemoryStream(data, writable: false);
        using var package = new XlsxReaderPackage(input);
        var workbook = new SpreadsheetWorkbook();
        foreach (var name in package.SheetNames)
        {
            var sheet = new SpreadsheetWorksheet { Name = name };
            foreach (var cell in package.ReadCells(name))
            {
                var model = sheet.GetOrCreateCell(SheetReference.Format(cell.Row, cell.Column));
                model.Value = cell.Value;
                model.Formula = cell.Formula;
            }
            workbook.Worksheets.Add(sheet);
        }
        return workbook;
    }

    /// <summary>Evaluates one cell, following references and detecting circular formulas (#REF!).</summary>
    public static object? EvaluateCell(SpreadsheetWorksheet sheet, string cellReference)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        return new SheetFormulaEvaluator(sheet).EvaluateCell(cellReference);
    }

    public static object? EvaluateFormula(SpreadsheetWorksheet sheet, string formula)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(formula);
        return new SheetFormulaEvaluator(sheet).EvaluateFormula(formula);
    }

    private static void WriteSheet(XmlWriter w, SpreadsheetWorksheet sheet, bool storeCalculatedValues)
    {
        var evaluator = new SheetFormulaEvaluator(sheet);
        var cells = new List<(int Row, int Column, SpreadsheetCell Cell)>();
        foreach (var cell in sheet.Cells.Values)
        {
            if (!SheetReference.TryParse(cell.CellReference, out var row, out var column)) throw new InvalidDataException($"'{cell.CellReference}' is not a valid cell reference.");
            cells.Add((row, column, cell));
        }

        w.WriteStartDocument();
        w.WriteStartElement("worksheet", XlsxPackage.MainNs);
        w.WriteStartElement("sheetData");
        foreach (var rowGroup in cells.OrderBy(c => c.Row).GroupBy(c => c.Row))
        {
            w.WriteStartElement("row");
            w.WriteAttributeString("r", rowGroup.Key.ToString(CultureInfo.InvariantCulture));
            foreach (var (row, column, cell) in rowGroup.OrderBy(c => c.Column))
            {
                var reference = SheetReference.Format(row, column);
                if (!string.IsNullOrEmpty(cell.Formula))
                {
                    var formula = cell.Formula.TrimStart('=');
                    XlsxPackage.WriteCell(w, reference, null, formula, storeCalculatedValues ? evaluator.EvaluateCell(reference) : null);
                }
                else XlsxPackage.WriteCell(w, reference, cell.Value);
            }
            w.WriteEndElement();
        }
        w.WriteEndElement();
        w.WriteEndElement();
        w.WriteEndDocument();
    }
}

internal readonly record struct SheetCellData(int Row, int Column, object? Value, string? Formula);

/// <summary>Read side of an .xlsx package: sheet lookup, shared strings, date styles and forward-only cell enumeration.</summary>
internal sealed class XlsxReaderPackage : IDisposable
{
    private static readonly XNamespace Main = XlsxPackage.MainNs;
    private readonly ZipArchive _zip;
    private readonly List<(string Name, string Path)> _sheets = new();
    private readonly List<string> _sharedStrings = new();
    private readonly HashSet<int> _dateStyles = new();

    public XlsxReaderPackage(Stream stream)
    {
        _zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        LoadWorkbook();
        LoadSharedStrings();
        LoadStyles();
    }

    public IReadOnlyList<string> SheetNames => _sheets.Select(s => s.Name).ToList();

    public void Dispose() => _zip.Dispose();

    public IEnumerable<SheetCellData> ReadCells(string? sheetName)
    {
        var sheet = sheetName is null ? _sheets.FirstOrDefault() : _sheets.FirstOrDefault(s => s.Name.Equals(sheetName, StringComparison.OrdinalIgnoreCase));
        if (sheet.Path is null) throw new ArgumentException($"Sheet '{sheetName}' was not found.", nameof(sheetName));
        var entry = _zip.GetEntry(sheet.Path) ?? throw new InvalidDataException($"Worksheet part '{sheet.Path}' is missing.");

        using var stream = entry.Open();
        using var reader = XlsxPackage.CreateReader(stream);
        var nextRow = 0;
        reader.Read();
        while (!reader.EOF)
        {
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "row")
            {
                reader.Read();
                continue;
            }

            var rowAttr = reader.GetAttribute("r");
            var rowNumber = rowAttr is not null && int.TryParse(rowAttr, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : nextRow + 1;
            nextRow = rowNumber;
            var rowDepth = reader.Depth;
            var empty = reader.IsEmptyElement;
            reader.Read();
            if (empty) continue;

            // XNode.ReadFrom leaves the reader on the node after the cell, so only advance when no cell was consumed.
            var nextColumn = 0;
            while (!reader.EOF && reader.Depth > rowDepth)
            {
                if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "c")
                {
                    reader.Read();
                    continue;
                }

                var cell = (XElement)XNode.ReadFrom(reader);
                var column = SheetReference.TryParse((string?)cell.Attribute("r"), out _, out var parsedColumn) ? parsedColumn : nextColumn + 1;
                nextColumn = column;
                var data = ConvertCell(cell, rowNumber, column);
                if (data.Value is not null || data.Formula is not null) yield return data;
            }
        }
    }

    private SheetCellData ConvertCell(XElement cell, int row, int column)
    {
        var type = (string?)cell.Attribute("t");
        var formula = cell.Element(Main + "f")?.Value;
        var raw = cell.Element(Main + "v")?.Value;
        object? value = null;

        switch (type)
        {
            case "s":
                value = int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var index) && index < _sharedStrings.Count ? _sharedStrings[index] : null;
                break;
            case "inlineStr":
                value = string.Concat(cell.Element(Main + "is")?.Descendants(Main + "t").Select(t => t.Value) ?? Enumerable.Empty<string>());
                break;
            case "str":
                value = raw ?? string.Empty;
                break;
            case "b":
                value = raw == "1";
                break;
            case "e":
                value = new SheetError(raw ?? "#VALUE!");
                break;
            case "d":
                value = DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var iso) ? iso : raw;
                break;
            default:
                if (raw is not null && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                {
                    var style = int.TryParse((string?)cell.Attribute("s"), NumberStyles.None, CultureInfo.InvariantCulture, out var s) ? s : 0;
                    value = _dateStyles.Contains(style) && number is >= -657435 and < 2958466 ? DateTime.FromOADate(number) : number;
                }
                break;
        }
        return new SheetCellData(row, column, value, formula);
    }

    private XDocument? LoadPart(string path)
    {
        var entry = _zip.GetEntry(path);
        if (entry is null) return null;
        if (entry.Length > XlsxPackage.MaxPartBytes) throw new InvalidDataException($"The part '{path}' is too large.");
        using var stream = entry.Open();
        using var reader = XlsxPackage.CreateReader(stream);
        return XDocument.Load(reader);
    }

    private void LoadWorkbook()
    {
        var workbook = LoadPart("xl/workbook.xml") ?? throw new InvalidDataException("The package has no xl/workbook.xml part.");
        var rels = LoadPart("xl/_rels/workbook.xml.rels");
        var targets = rels?.Root?.Elements().ToDictionary(e => (string?)e.Attribute("Id") ?? string.Empty, e => (string?)e.Attribute("Target") ?? string.Empty) ?? new Dictionary<string, string>();
        XNamespace relNs = XlsxPackage.RelNs;

        foreach (var sheet in workbook.Root?.Element(Main + "sheets")?.Elements(Main + "sheet") ?? Enumerable.Empty<XElement>())
        {
            var id = (string?)sheet.Attribute(relNs + "id") ?? string.Empty;
            if (!targets.TryGetValue(id, out var target)) continue;
            var path = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
            _sheets.Add(((string?)sheet.Attribute("name") ?? "Sheet", path));
        }
    }

    private void LoadSharedStrings()
    {
        var part = LoadPart("xl/sharedStrings.xml");
        if (part?.Root is null) return;
        foreach (var item in part.Root.Elements(Main + "si"))
            _sharedStrings.Add(string.Concat(item.Descendants(Main + "t").Where(t => t.Parent?.Name != Main + "rPh").Select(t => t.Value)));
    }

    private void LoadStyles()
    {
        var part = LoadPart("xl/styles.xml");
        if (part?.Root is null) return;
        var custom = new Dictionary<int, bool>();
        foreach (var format in part.Root.Element(Main + "numFmts")?.Elements(Main + "numFmt") ?? Enumerable.Empty<XElement>())
            if (int.TryParse((string?)format.Attribute("numFmtId"), NumberStyles.None, CultureInfo.InvariantCulture, out var id))
                custom[id] = IsDateFormat((string?)format.Attribute("formatCode") ?? string.Empty);

        var index = 0;
        foreach (var xf in part.Root.Element(Main + "cellXfs")?.Elements(Main + "xf") ?? Enumerable.Empty<XElement>())
        {
            if (int.TryParse((string?)xf.Attribute("numFmtId"), NumberStyles.None, CultureInfo.InvariantCulture, out var fmt)
                && (custom.TryGetValue(fmt, out var isDate) ? isDate : fmt is >= 14 and <= 22 or >= 45 and <= 47))
                _dateStyles.Add(index);
            index++;
        }
    }

    private static bool IsDateFormat(string code)
    {
        var stripped = Regex.Replace(code, "\"[^\"]*\"|\\[[^\\]]*\\]|\\\\.", string.Empty);
        return Regex.IsMatch(stripped, "[ydhsYDHS]");
    }
}
