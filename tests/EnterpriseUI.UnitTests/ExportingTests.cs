using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using EnterpriseUI.Data;
using EnterpriseUI.Exporting;
using EnterpriseUI.Grids;
using Xunit;
using static EnterpriseUI.Tests.Unit.GridTestSupport;

namespace EnterpriseUI.Tests.Unit;

internal static class ExportTestSupport
{
    public static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public static ExportOptions InvariantOptions() => new() { Culture = CultureInfo.InvariantCulture };

    public static ExportDocument SimpleDocument(IEnumerable<ExportRow>? rows = null, int count = 3)
    {
        var columns = new List<ExportColumn>
        {
            new("Id", "Id") { DataType = typeof(int), Alignment = ExportAlignment.Right },
            new("Name", "Name"),
            new("Amount", "Amount") { DataType = typeof(decimal), Format = "N2", Alignment = ExportAlignment.Right },
            new("When", "When") { DataType = typeof(DateTime) }
        };
        rows ??= Enumerable.Range(1, count).Select(i => new ExportRow(ExportRowKind.Data, 0, new object?[] { i, $"Item {i}", i * 10.5m, new DateTime(2024, 1, 1).AddDays(i - 1) }));
        var sheet = new ExportSheet("Data", columns, rows) { Title = "Report" };
        var document = new ExportDocument { Title = "Test report" };
        document.Sheets.Add(sheet);
        return document;
    }

    public static async Task<byte[]> Export(ExportDocument document, string format, ExportOptions? options = null, ExportService? service = null)
    {
        using var stream = new MemoryStream();
        await (service ?? new ExportService()).ExportAsync(document, format, stream, options ?? InvariantOptions());
        return stream.ToArray();
    }

    public static ZipArchive Open(byte[] bytes) => new(new MemoryStream(bytes), ZipArchiveMode.Read);

    public static XDocument ReadPart(ZipArchive zip, string name)
    {
        var entry = zip.GetEntry(name) ?? throw new InvalidOperationException($"Missing part {name}");
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    public static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');

    /// <summary>Validates the PDF cross-reference table byte offsets and returns the page count and decoded content streams.</summary>
    public static (int Pages, List<string> Contents, string Raw) InspectPdf(byte[] bytes)
    {
        var raw = Encoding.Latin1.GetString(bytes);
        Assert.StartsWith("%PDF-1.4", raw);
        Assert.EndsWith("%%EOF\n", raw);

        var startXref = Convert.ToInt32(Regex.Match(raw, @"startxref\n(\d+)\n%%EOF").Groups[1].Value);
        Assert.Equal("xref", raw.Substring(startXref, 4));

        var header = Regex.Match(raw[startXref..], @"xref\n0 (\d+)\n");
        var count = int.Parse(header.Groups[1].Value);
        var entries = Regex.Matches(raw[(startXref + header.Length)..], @"(\d{10}) (\d{5}) ([nf]) \n").Cast<Match>().Take(count).ToList();
        Assert.Equal(count, entries.Count);
        Assert.Equal("f", entries[0].Groups[3].Value);
        for (var i = 1; i < count; i++)
        {
            var offset = int.Parse(entries[i].Groups[1].Value);
            Assert.StartsWith($"{i} 0 obj", raw[offset..]);
        }

        var pages = int.Parse(Regex.Match(raw, @"/Type /Pages /Count (\d+)").Groups[1].Value);
        var contents = new List<string>();
        foreach (Match match in Regex.Matches(raw, @"/Length (\d+) /Filter /FlateDecode >>\nstream\n"))
        {
            var start = match.Index + match.Length;
            var length = int.Parse(match.Groups[1].Value);
            using var inflater = new ZLibStream(new MemoryStream(bytes, start, length), CompressionMode.Decompress);
            using var reader = new StreamReader(inflater, Encoding.Latin1);
            contents.Add(reader.ReadToEnd());
        }

        Assert.Equal(pages, contents.Count);
        return (pages, contents, raw);
    }
}

public class ExportProviderTests
{
    [Fact]
    public async Task CsvQuotesFieldsNeutralizesFormulasAndWritesUtf8Bom()
    {
        var document = ExportTestSupport.SimpleDocument(new[]
        {
            new ExportRow(ExportRowKind.Data, 0, new object?[] { 1, "plain", 1.5m, new DateTime(2024, 1, 2) }),
            new ExportRow(ExportRowKind.Data, 0, new object?[] { 2, "has,comma \"and\" quote", 2m, null }),
            new ExportRow(ExportRowKind.Data, 0, new object?[] { 3, "=HYPERLINK(\"http://evil\")", -4m, null }),
            new ExportRow(ExportRowKind.Data, 0, new object?[] { 4, "line\nbreak", 0m, null }),
            new ExportRow(ExportRowKind.Data, 0, new object?[] { -5, "-5 units", 0m, null })
        });

        var bytes = await ExportTestSupport.Export(document, "csv");

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3));
        var lines = ExportTestSupport.Text(bytes).Split("\r\n");
        Assert.Equal("Id,Name,Amount,When", lines[0]);
        Assert.Equal("1,plain,1.50,2024-01-02", lines[1]);
        Assert.Contains("\"has,comma \"\"and\"\" quote\"", lines[2]);
        Assert.Contains("'=HYPERLINK", lines[3]);
        Assert.Contains("\"line\nbreak\"", ExportTestSupport.Text(bytes));
        Assert.StartsWith("-5,'-5 units", ExportTestSupport.Text(bytes).Split("\r\n").Last(l => l.StartsWith("-5")));

        var raw = await ExportTestSupport.Export(document, "csv", new ExportOptions { Culture = CultureInfo.InvariantCulture, CsvSanitizeFormulas = false, CsvDelimiter = ';', CsvQuoteAll = true });
        Assert.Contains("\"=HYPERLINK(\"\"http://evil\"\")\"", ExportTestSupport.Text(raw));
        Assert.Contains("\"Id\";\"Name\"", ExportTestSupport.Text(raw));
    }

    [Fact]
    public async Task CsvWritesGroupDetailAndFooterRowsInTableOrder()
    {
        var detailColumns = new[] { new ExportColumn("Sku", "SKU"), new ExportColumn("Qty", "Qty") };
        var rows = new[]
        {
            new ExportRow(ExportRowKind.GroupHeader, 0) { Text = "Region: EMEA (1)" },
            new ExportRow(ExportRowKind.Data, 1, new object?[] { 1, "A", 1m, null }),
            new ExportRow(ExportRowKind.DetailHeader, 1) { RelationName = "Lines", DetailColumns = detailColumns },
            new ExportRow(ExportRowKind.DetailData, 1, new object?[] { "S-1", 4 })
        };
        var document = ExportTestSupport.SimpleDocument(rows);
        document.Sheets[0].Footer = new[] { new ExportRow(ExportRowKind.Summary, 0, new object?[] { "Total", null, 99m, null }) };

        var lines = ExportTestSupport.Text(await ExportTestSupport.Export(document, "csv")).TrimEnd().Split("\r\n");

        Assert.Equal("Region: EMEA (1)", lines[1]);
        Assert.StartsWith("1,A", lines[2]);
        Assert.Equal(",SKU,Qty", lines[3]);
        Assert.Equal(",S-1,4", lines[4]);
        Assert.Equal("Total,,99.00,", lines[5]);
    }

    [Fact]
    public async Task JsonNestsGroupsAndChildTablesAndKeepsTypes()
    {
        var detailColumns = new[] { new ExportColumn("Sku", "SKU") };
        var rows = new[]
        {
            new ExportRow(ExportRowKind.GroupHeader, 0) { Text = "Region: EMEA", GroupField = "Region", GroupKey = "EMEA", GroupItemCount = 2, Aggregates = new[] { new KeyValuePair<string, object?>("Sum(Amount)", 30m) } },
            new ExportRow(ExportRowKind.Data, 1, new object?[] { 1, "A", 10m, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) }),
            new ExportRow(ExportRowKind.DetailHeader, 1) { RelationName = "Lines", DetailColumns = detailColumns },
            new ExportRow(ExportRowKind.DetailData, 1, new object?[] { "S-1" }),
            new ExportRow(ExportRowKind.DetailData, 1, new object?[] { "S-2" }),
            new ExportRow(ExportRowKind.Data, 1, new object?[] { 2, "B", 20m, null }),
            new ExportRow(ExportRowKind.GroupHeader, 0) { Text = "Region: APAC", GroupField = "Region", GroupKey = "APAC", GroupItemCount = 1 },
            new ExportRow(ExportRowKind.Data, 1, new object?[] { 3, "C", 5m, null })
        };
        var document = ExportTestSupport.SimpleDocument(rows);
        document.Sheets[0].Footer = new[] { new ExportRow(ExportRowKind.Summary, 0, new object?[] { null, null, 35m, null }) };

        var bytes = await ExportTestSupport.Export(document, "json");

        using var json = JsonDocument.Parse(bytes);
        var sheet = json.RootElement.GetProperty("sheets")[0];
        Assert.Equal("Data", sheet.GetProperty("name").GetString());
        var groups = sheet.GetProperty("rows");
        Assert.Equal(2, groups.GetArrayLength());
        Assert.Equal("EMEA", groups[0].GetProperty("key").GetString());
        Assert.Equal(30m, groups[0].GetProperty("aggregates").GetProperty("Sum(Amount)").GetDecimal());
        var items = groups[0].GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        Assert.Equal(10m, items[0].GetProperty("Amount").GetDecimal());
        Assert.Equal(1, items[0].GetProperty("Id").GetInt32());
        Assert.Equal(2, items[0].GetProperty("Lines").GetArrayLength());
        Assert.Equal("S-2", items[0].GetProperty("Lines")[1].GetProperty("Sku").GetString());
        Assert.False(items[1].TryGetProperty("Lines", out _));
        Assert.Equal(JsonValueKind.Null, items[1].GetProperty("When").ValueKind);
        Assert.Equal(1, groups[1].GetProperty("items").GetArrayLength());
        Assert.Equal(35m, sheet.GetProperty("summary").GetProperty("Amount").GetDecimal());
    }

    [Fact]
    public async Task XmlIsWellFormedEncodesNamesAndStripsInvalidCharacters()
    {
        var columns = new List<ExportColumn> { new("Unit Price"), new("2nd") };
        var rows = new[] { new ExportRow(ExportRowKind.Data, 0, new object?[] { "a\u0001b <&>", 5 }) };
        var document = new ExportDocument { Title = "T\u0002" };
        document.Sheets.Add(new ExportSheet("S", columns, rows));

        var xml = XDocument.Parse(ExportTestSupport.Text(await ExportTestSupport.Export(document, "xml")));

        var row = xml.Descendants("Row").Single();
        Assert.Equal("ab <&>", row.Elements().First().Value);
        Assert.Contains(row.Elements(), e => e.Value == "5");
        Assert.DoesNotContain('\u0002', (string)xml.Root!.Attribute("title")!);
    }

    [Fact]
    public async Task HtmlEncodesContentAndOnlyEmitsValidatedColors()
    {
        var evil = new ExportCellStyle { BackColorHex = "red;background:url(javascript:alert(1))", ForeColorHex = "#112233", Bold = true };
        var rows = new[]
        {
            new ExportRow(ExportRowKind.Data, 0, new object?[] { 1, "<script>alert('x')</script>", 1m, null }) { CellStyles = new ExportCellStyle?[] { evil, null, new ExportCellStyle { DataBar = 0.5, DataBarColorHex = "#ff0000" }, null } }
        };
        var document = ExportTestSupport.SimpleDocument(rows);

        var html = ExportTestSupport.Text(await ExportTestSupport.Export(document, "html"));

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("javascript", html);
        Assert.Contains("color:#112233;", html);
        Assert.Contains("linear-gradient(90deg,#ff000066 50%", html);
        Assert.Contains("<th scope=\"col\">Name</th>", html);
        Assert.StartsWith("<!DOCTYPE html>", html);
    }

    [Fact]
    public async Task HtmlRendersStackedHeadersGroupsAndFooters()
    {
        var columns = new List<ExportColumn>
        {
            new("A", "A") { Bands = new[] { "Order" } },
            new("B", "B") { Bands = new[] { "Order" } },
            new("C", "C")
        };
        var document = new ExportDocument { Title = "Bands" };
        var sheet = new ExportSheet("S", columns, new[]
        {
            new ExportRow(ExportRowKind.GroupHeader, 0) { Text = "G" },
            new ExportRow(ExportRowKind.Data, 1, new object?[] { 1, 2, 3 })
        })
        { Footer = new[] { new ExportRow(ExportRowKind.Summary, 0, new object?[] { "Total", null, 9 }) } };
        document.Sheets.Add(sheet);

        var html = ExportTestSupport.Text(await ExportTestSupport.Export(document, "html"));

        Assert.Contains("colspan=\"2\"", html);
        Assert.Contains("rowspan=\"2\"", html);
        Assert.Contains("class=\"group\"", html);
        Assert.Contains("<tfoot>", html);
    }

    [Fact]
    public async Task XlsxProducesAValidPackageWithTypedCells()
    {
        var rows = new[]
        {
            new ExportRow(ExportRowKind.Data, 0, new object?[] { 7, "=SUM(1,2)", 1234.5m, new DateTime(2024, 3, 4) }),
            new ExportRow(ExportRowKind.Data, 0, new object?[] { true, "ctrl\u0001char <&>", double.NaN, null })
        };
        var document = ExportTestSupport.SimpleDocument(rows);

        var bytes = await ExportTestSupport.Export(document, "xlsx");

        using var zip = ExportTestSupport.Open(bytes);
        foreach (var part in new[] { "[Content_Types].xml", "_rels/.rels", "xl/workbook.xml", "xl/_rels/workbook.xml.rels", "xl/styles.xml", "xl/worksheets/sheet1.xml", "docProps/core.xml" })
        {
            Assert.NotNull(zip.GetEntry(part));
            ExportTestSupport.ReadPart(zip, part);
        }

        var sheet = ExportTestSupport.ReadPart(zip, "xl/worksheets/sheet1.xml");
        var ns = ExportTestSupport.Main;
        var cells = sheet.Descendants(ns + "c").ToDictionary(c => (string)c.Attribute("r")!);
        Assert.Equal("Report", (string)cells["A1"].Descendants(ns + "t").Single());
        Assert.Equal("Id", cells["A2"].Value);
        Assert.Equal("7", cells["A3"].Element(ns + "v")!.Value);
        Assert.Null(cells["A3"].Attribute("t"));
        Assert.Equal("inlineStr", (string)cells["B3"].Attribute("t")!);
        Assert.Equal("=SUM(1,2)", cells["B3"].Descendants(ns + "t").Single().Value);
        Assert.Equal("1234.5", cells["C3"].Element(ns + "v")!.Value);
        Assert.Equal(new DateTime(2024, 3, 4).ToOADate().ToString("R", CultureInfo.InvariantCulture), cells["D3"].Element(ns + "v")!.Value);
        Assert.Equal("b", (string)cells["A4"].Attribute("t")!);
        Assert.Equal("1", cells["A4"].Element(ns + "v")!.Value);
        Assert.Equal("ctrlchar <&>", cells["B4"].Descendants(ns + "t").Single().Value);
        Assert.Equal("inlineStr", (string)cells["C4"].Attribute("t")!);
        Assert.Null(cells["D4"].Element(ns + "v"));

        var styles = ExportTestSupport.ReadPart(zip, "xl/styles.xml");
        Assert.Contains(styles.Descendants(ns + "numFmt"), n => (string)n.Attribute("formatCode")! == "#,##0.00");
        Assert.Contains(styles.Descendants(ns + "numFmt"), n => (string)n.Attribute("formatCode")! == "yyyy-mm-dd");
        var xfCount = styles.Descendants(ns + "cellXfs").Single().Elements().Count();
        Assert.All(sheet.Descendants(ns + "c").Select(c => (int)c.Attribute("s")!), s => Assert.InRange(s, 0, xfCount - 1));

        var pane = sheet.Descendants(ns + "pane").Single();
        Assert.Equal("2", (string)pane.Attribute("ySplit")!);
        Assert.Equal("A2:D4", (string)sheet.Descendants(ns + "autoFilter").Single().Attribute("ref")!.Value.Replace("A2:D4", "A2:D4"));
        Assert.Equal("A1:D1", (string)sheet.Descendants(ns + "mergeCell").Single().Attribute("ref")!);
    }

    [Fact]
    public async Task XlsxMergesGroupRowsOutlinesNestedRowsAndAddsFooter()
    {
        var detailColumns = new[] { new ExportColumn("Sku", "SKU"), new ExportColumn("Qty", "Qty") };
        var rows = new[]
        {
            new ExportRow(ExportRowKind.GroupHeader, 0) { Text = "Region: EMEA (1)" },
            new ExportRow(ExportRowKind.Data, 1, new object?[] { 1, "A", 10m, null }),
            new ExportRow(ExportRowKind.DetailHeader, 1) { RelationName = "Lines", DetailColumns = detailColumns, OutlineLevel = 2 },
            new ExportRow(ExportRowKind.DetailData, 1, new object?[] { "S-1", 4 }) { OutlineLevel = 2 }
        };
        var document = ExportTestSupport.SimpleDocument(rows);
        document.Sheets[0].IsStructured = true;
        document.Sheets[0].Footer = new[] { new ExportRow(ExportRowKind.Summary, 0, new object?[] { "Total", null, 10m, null }) };

        using var zip = ExportTestSupport.Open(await ExportTestSupport.Export(document, "xlsx"));
        var sheet = ExportTestSupport.ReadPart(zip, "xl/worksheets/sheet1.xml");
        var ns = ExportTestSupport.Main;

        var xmlRows = sheet.Descendants(ns + "row").ToDictionary(r => (int)r.Attribute("r")!);
        Assert.Null(xmlRows[3].Attribute("outlineLevel"));
        Assert.Equal("1", (string?)xmlRows[4].Attribute("outlineLevel"));
        Assert.Equal("2", (string?)xmlRows[5].Attribute("outlineLevel"));
        Assert.Equal("2", (string?)xmlRows[6].Attribute("outlineLevel"));
        Assert.Contains(sheet.Descendants(ns + "mergeCell"), m => (string)m.Attribute("ref")! == "A3:D3");
        Assert.Equal("Lines", xmlRows[5].Elements(ns + "c").First().Descendants(ns + "t").Single().Value);
        Assert.Equal("S-1", xmlRows[6].Elements(ns + "c").First().Descendants(ns + "t").Single().Value);
        Assert.Equal("B6", (string)xmlRows[6].Elements(ns + "c").First().Attribute("r")!);
        Assert.Equal("Total", xmlRows[7].Elements(ns + "c").First().Descendants(ns + "t").Single().Value);
        Assert.Empty(sheet.Descendants(ns + "autoFilter"));
        Assert.Equal("0", (string)sheet.Descendants(ns + "outlinePr").Single().Attribute("summaryBelow")!);
    }

    [Fact]
    public async Task XlsxWritesBandedHeadersWithMergesNativeDataBarsAndContinuationSheets()
    {
        var columns = new List<ExportColumn>
        {
            new("A", "A") { Bands = new[] { "Order" } },
            new("B", "B") { Bands = new[] { "Order" }, DataType = typeof(int) },
            new("C", "C")
        };
        var rows = Enumerable.Range(1, 10).Select(i => new ExportRow(ExportRowKind.Data, 0, new object?[] { i, i * 10, "x" }));
        var sheet = new ExportSheet("Sales: 2024/Q1 [draft]*", columns, rows);
        sheet.ConditionalFormats.Add(new ExportConditionalFormat { ColumnKey = "B", Type = ExportConditionalFormatType.DataBar, Minimum = 0, Maximum = 100, ColorHex = "#5088D7" });
        sheet.ConditionalFormats.Add(new ExportConditionalFormat { ColumnKey = "B", Type = ExportConditionalFormatType.ColorScale, Minimum = 0, Maximum = 100, ColorHex = "#FFFFFF", SecondColorHex = "#00FF00" });
        var document = new ExportDocument { Title = "x" };
        document.Sheets.Add(sheet);
        document.Sheets.Add(new ExportSheet("Sales: 2024/Q1 [draft]*", columns, rows.Take(1)));

        var options = ExportTestSupport.InvariantOptions();
        options.MaxRowsPerSheet = 4;
        using var zip = ExportTestSupport.Open(await ExportTestSupport.Export(document, "xlsx", options));

        var workbook = ExportTestSupport.ReadPart(zip, "xl/workbook.xml");
        var ns = ExportTestSupport.Main;
        var names = workbook.Descendants(ns + "sheet").Select(s => (string)s.Attribute("name")!).ToList();
        Assert.Equal(4, names.Count);
        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(names, n => { Assert.InRange(n.Length, 1, 31); Assert.DoesNotContain(n, c => ":\\/?*[]".Contains(c)); });

        var first = ExportTestSupport.ReadPart(zip, "xl/worksheets/sheet1.xml");
        var merges = first.Descendants(ns + "mergeCell").Select(m => (string)m.Attribute("ref")!).ToList();
        Assert.Contains("A1:B1", merges);
        Assert.Contains("C1:C2", merges);
        Assert.Equal(4, first.Descendants(ns + "row").Count(r => (int)r.Attribute("r")! > 2));

        var cf = first.Descendants(ns + "conditionalFormatting").ToList();
        Assert.Equal(2, cf.Count);
        Assert.Equal("B3:B6", (string)cf[0].Attribute("sqref")!);
        Assert.NotNull(cf[0].Descendants(ns + "dataBar").SingleOrDefault());
        Assert.Equal("FF00FF00", (string)cf[1].Descendants(ns + "color").Last().Attribute("rgb")!);

        var cont = ExportTestSupport.ReadPart(zip, "xl/worksheets/sheet3.xml");
        Assert.Equal(2, cont.Descendants(ns + "row").Count(r => (int)r.Attribute("r")! > 2));
    }

    [Fact]
    public async Task SpreadsheetMlWritesTypedCellsAndSafeSheetNames()
    {
        var document = ExportTestSupport.SimpleDocument(new[] { new ExportRow(ExportRowKind.Data, 0, new object?[] { 1, "a&b", 2.5m, new DateTime(2024, 5, 6) }) });
        document.Sheets[0].Name = "Bad:Name/Here";

        var xml = XDocument.Parse(ExportTestSupport.Text(await ExportTestSupport.Export(document, "excelxml")));

        XNamespace ss = "urn:schemas-microsoft-com:office:spreadsheet";
        Assert.Equal("BadNameHere", (string)xml.Descendants().First(e => e.Name.LocalName == "Worksheet").Attribute(ss + "Name")!);
        var types = xml.Descendants().Where(e => e.Name.LocalName == "Data").Select(e => (string)e.Attribute(ss + "Type")!).ToList();
        Assert.Contains("Number", types);
        Assert.Contains("DateTime", types);
    }

    [Fact]
    public async Task PdfIsAValidMultiPageDocumentWithRepeatedHeadersAndPageNumbers()
    {
        var rows = Enumerable.Range(1, 120).Select(i => new ExportRow(ExportRowKind.Data, 0, new object?[] { i, $"Item (#{i}) \\ back", i * 1.5m, new DateTime(2024, 1, 1) }));
        var document = ExportTestSupport.SimpleDocument(rows);
        document.Sheets[0].RowCountHint = 120;

        var options = ExportTestSupport.InvariantOptions();
        options.Pdf.FooterText = "Page {page} of {pages}";

        var (pages, contents, _) = ExportTestSupport.InspectPdf(await ExportTestSupport.Export(document, "pdf", options));

        Assert.InRange(pages, 3, 4);
        Assert.All(contents, c => Assert.Contains("(Name) Tj", c));
        Assert.Contains($"(Page 1 of {pages}) Tj", contents[0]);
        Assert.Contains($"(Page {pages} of {pages}) Tj", contents[^1]);
        Assert.Contains("(Item \\(#1\\) \\\\ back) Tj", contents[0]);
        Assert.Contains("(Test report) Tj", contents[0]);
    }

    [Fact]
    public async Task PdfHonorsOrientationClipsLongTextAndDrawsGroupsAndColors()
    {
        var rows = new[]
        {
            new ExportRow(ExportRowKind.GroupHeader, 0) { Text = "Region: EMEA (2)" },
            new ExportRow(ExportRowKind.Data, 1, new object?[] { 1, new string('W', 200), 5m, null })
            {
                CellStyles = new ExportCellStyle?[] { null, null, new ExportCellStyle { BackColorHex = "#FF0000", DataBar = 0.5 }, null }
            }
        };
        var document = ExportTestSupport.SimpleDocument(rows);
        var options = ExportTestSupport.InvariantOptions();
        options.Pdf.Landscape = true;
        options.Pdf.PageWidth = 595;
        options.Pdf.PageHeight = 842;

        var (_, contents, raw) = ExportTestSupport.InspectPdf(await ExportTestSupport.Export(document, "pdf", options));

        Assert.Contains("/MediaBox [0 0 842 595]", raw);
        Assert.Contains("(Region: EMEA \\(2\\)) Tj", contents[0]);
        Assert.Contains("1 0 0 rg", contents[0]);
        Assert.Matches(@"\(W+\.\.\.\) Tj", contents[0]);
        Assert.DoesNotContain(new string('W', 100), contents[0]);
    }

    [Fact]
    public void PdfMeasuresTextWithHelveticaMetrics()
    {
        Assert.Equal(0, PdfExportProvider.MeasureText(string.Empty, 10, false));
        Assert.Equal(5.56, PdfExportProvider.MeasureText("0", 10, false), 2);
        Assert.True(PdfExportProvider.MeasureText("Hello", 10, true) > PdfExportProvider.MeasureText("Hello", 10, false));
        Assert.True(PdfExportProvider.MeasureText("WWW", 10, false) > PdfExportProvider.MeasureText("iii", 10, false));
    }

    [Fact]
    public async Task EmptyDocumentsStillProduceValidFiles()
    {
        var empty = new ExportDocument { Title = "Empty" };
        empty.Sheets.Add(new ExportSheet("S", new List<ExportColumn> { new("A") }, Array.Empty<ExportRow>()));

        ExportTestSupport.InspectPdf(await ExportTestSupport.Export(empty, "pdf"));
        using var zip = ExportTestSupport.Open(await ExportTestSupport.Export(empty, "xlsx"));
        Assert.NotNull(zip.GetEntry("xl/worksheets/sheet1.xml"));
        Assert.Contains("A", ExportTestSupport.Text(await ExportTestSupport.Export(empty, "csv")));

        var none = new ExportDocument();
        ExportTestSupport.InspectPdf(await ExportTestSupport.Export(none, "pdf"));
        using var emptyZip = ExportTestSupport.Open(await ExportTestSupport.Export(none, "xlsx"));
        Assert.NotNull(emptyZip.GetEntry("xl/workbook.xml"));
    }

    [Fact]
    public void ExcelNumberFormatsMapFromDotNetFormats()
    {
        var invariant = CultureInfo.InvariantCulture;
        Assert.Equal("#,##0.00", ExportFormatting.ToExcelNumberFormat("N2", typeof(decimal), invariant));
        Assert.Equal("0.0", ExportFormatting.ToExcelNumberFormat("F1", typeof(double), invariant));
        Assert.Equal("0.00%", ExportFormatting.ToExcelNumberFormat("P", typeof(double), invariant));
        Assert.Equal("000", ExportFormatting.ToExcelNumberFormat("D3", typeof(int), invariant));
        Assert.Equal("\"\u00A4\"#,##0.00", ExportFormatting.ToExcelNumberFormat("C2", typeof(decimal), invariant));
        Assert.Equal("yyyy-mm-dd", ExportFormatting.ToExcelNumberFormat(null, typeof(DateTime?), invariant));
        Assert.Equal("yyyy-mm-dd hh:mm", ExportFormatting.ToExcelNumberFormat("g", typeof(DateTime), invariant));
        Assert.Equal("yyyy-mm-dd", ExportFormatting.ToExcelNumberFormat("yyyy-MM-dd", typeof(DateTime), invariant));
        Assert.Null(ExportFormatting.ToExcelNumberFormat(null, typeof(int), invariant));
    }

    [Fact]
    public void ColorParsingAcceptsOnlySixDigitHex()
    {
        Assert.True(ExportFormatting.TryParseColor("#FFa500", out var r, out var g, out var b));
        Assert.Equal((255, 165, 0), (r, g, b));
        Assert.True(ExportFormatting.TryParseColor("00ff00", out _, out _, out _));
        Assert.False(ExportFormatting.TryParseColor("#fff", out _, out _, out _));
        Assert.False(ExportFormatting.TryParseColor("red", out _, out _, out _));
        Assert.False(ExportFormatting.TryParseColor(null, out _, out _, out _));
        Assert.False(ExportFormatting.TryParseColor("#GG0000", out _, out _, out _));
    }
}

public class ExportServiceTests
{
    [Fact]
    public async Task ServiceReportsProgressResultAndRaisesEvents()
    {
        var service = new ExportService();
        var started = new List<string>();
        ExportResult? completed = null;
        service.Started += (_, id) => started.Add(id);
        service.Completed += (_, result) => completed = result;
        var reports = new List<ExportProgress>();
        var document = ExportTestSupport.SimpleDocument(count: 250);
        document.Sheets[0].RowCountHint = 250;
        var options = ExportTestSupport.InvariantOptions();
        options.ProgressInterval = 100;

        using var stream = new MemoryStream();
        var result = await service.ExportAsync(document, "csv", stream, options, new SyncProgress<ExportProgress>(reports.Add));

        Assert.Equal(250, result.Rows);
        Assert.Equal(1, result.Sheets);
        Assert.Equal(stream.Length, result.Bytes);
        Assert.Equal("csv", Assert.Single(started));
        Assert.Same(result, completed);
        Assert.Equal(new long[] { 100, 200, 250 }, reports.Select(r => r.RowsProcessed));
        Assert.Equal(0.4, reports[0].Fraction);
        Assert.Equal(1d, reports[^1].Fraction);
    }

    [Fact]
    public async Task RowAndCellEventsCanSkipRowsAndMaskValues()
    {
        var service = new ExportService();
        service.RowExporting += (_, e) => { if (e.Row.Cells[0] is 2) e.Skip = true; };
        service.CellExporting += (_, e) =>
        {
            if (e.Column?.Key == "Name") e.Value = "***";
            if (e.Column?.Key == "Amount") e.Style = new ExportCellStyle { Bold = true };
        };

        var csv = ExportTestSupport.Text(await ExportTestSupport.Export(ExportTestSupport.SimpleDocument(), "csv", service: service));

        var lines = csv.TrimEnd().Split("\r\n");
        Assert.Equal(3, lines.Length);
        Assert.StartsWith("1,***", lines[1]);
        Assert.StartsWith("3,***", lines[2]);
    }

    [Fact]
    public async Task OptionsExcludeGroupsDetailsAndSummaries()
    {
        var rows = new[]
        {
            new ExportRow(ExportRowKind.GroupHeader, 0) { Text = "G" },
            new ExportRow(ExportRowKind.Data, 1, new object?[] { 1, "A", 1m, null }),
            new ExportRow(ExportRowKind.DetailHeader, 1) { DetailColumns = new[] { new ExportColumn("x") } },
            new ExportRow(ExportRowKind.DetailData, 1, new object?[] { "child" })
        };
        var document = ExportTestSupport.SimpleDocument(rows);
        document.Sheets[0].Footer = new[] { new ExportRow(ExportRowKind.Summary, 0, new object?[] { "Total", null, 1m, null }) };
        var options = ExportTestSupport.InvariantOptions();
        options.IncludeGroups = false;
        options.IncludeDetails = false;
        options.IncludeSummaries = false;
        options.IncludeHeaders = false;

        var csv = ExportTestSupport.Text(await ExportTestSupport.Export(document, "csv", options)).TrimEnd();

        Assert.Equal("1,A,1.00,", csv);
    }

    [Fact]
    public async Task CancellationStopsTheExportAndLeavesNoFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"export-{Guid.NewGuid():N}.csv");
        try
        {
            var generated = 0;
            IEnumerable<ExportRow> Rows()
            {
                for (var i = 0; i < 1_000_000; i++)
                {
                    generated++;
                    yield return new ExportRow(ExportRowKind.Data, 0, new object?[] { i, "x", 1m, null });
                }
            }

            using var cts = new CancellationTokenSource();
            var service = new ExportService();
            service.RowExporting += (_, e) => { if ((int)e.Row.Cells[0]! == 500) cts.Cancel(); };

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                service.ExportToFileAsync(ExportTestSupport.SimpleDocument(Rows()), "csv", path, ExportTestSupport.InvariantOptions(), null, cts.Token));

            Assert.False(File.Exists(path));
            Assert.Empty(Directory.GetFiles(Path.GetTempPath(), Path.GetFileName(path) + ".*.tmp"));
            Assert.InRange(generated, 501, 502);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task ExportToFileReplacesTheTargetOnlyOnSuccess()
    {
        var path = Path.Combine(Path.GetTempPath(), $"export-{Guid.NewGuid():N}.csv");
        try
        {
            await File.WriteAllTextAsync(path, "previous");
            var service = new ExportService();

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExportToFileAsync(
                ExportTestSupport.SimpleDocument(Boom()), "csv", path, ExportTestSupport.InvariantOptions()));
            Assert.Equal("previous", await File.ReadAllTextAsync(path));

            await service.ExportToFileAsync(ExportTestSupport.SimpleDocument(), "csv", path, ExportTestSupport.InvariantOptions());
            Assert.Contains("Item 1", await File.ReadAllTextAsync(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }

        static IEnumerable<ExportRow> Boom()
        {
            yield return new ExportRow(ExportRowKind.Data, 0, new object?[] { 1, "a", 1m, null });
            throw new InvalidOperationException("data source failed");
        }
    }

    [Fact]
    public async Task CustomProvidersCanBeRegisteredAndUnknownFormatsAreRejected()
    {
        var service = new ExportService();
        service.Register(new TsvProvider());

        using var stream = new MemoryStream();
        await service.ExportAsync(ExportTestSupport.SimpleDocument(count: 2), "tsv", stream, ExportTestSupport.InvariantOptions());

        Assert.Equal("Id\tName\tAmount\tWhen\n1\tItem 1\t10.50\t2024-01-01\n2\tItem 2\t21.00\t2024-01-02\n".Replace("\n", Environment.NewLine), Encoding.UTF8.GetString(stream.ToArray()));
        Assert.Contains(service.Providers, p => p.Id == "tsv");
        Assert.Contains(service.Providers, p => p.Id == "xlsx");
        await Assert.ThrowsAsync<ArgumentException>(() => service.ExportAsync(ExportTestSupport.SimpleDocument(), "nope", new MemoryStream()));
    }

    [Fact]
    public async Task LargeExportsStreamWithoutBufferingRows()
    {
        const int total = 200_000;
        var alive = 0;
        var peak = 0;
        IEnumerable<ExportRow> Rows()
        {
            for (var i = 0; i < total; i++)
            {
                var current = Interlocked.Increment(ref alive);
                peak = Math.Max(peak, current);
                yield return new ExportRow(ExportRowKind.Data, 0, new object?[] { i, "name " + i, i * 0.5m, null });
                Interlocked.Decrement(ref alive);
            }
        }

        var document = ExportTestSupport.SimpleDocument(Rows());
        using var stream = new MemoryStream();
        var result = await new ExportService().ExportAsync(document, "xlsx", stream, ExportTestSupport.InvariantOptions());

        Assert.Equal(total, result.Rows);
        Assert.Equal(1, peak);
        using var zip = ExportTestSupport.Open(stream.ToArray());
        var sheet = ExportTestSupport.ReadPart(zip, "xl/worksheets/sheet1.xml");
        Assert.Equal(total + 2, sheet.Descendants(ExportTestSupport.Main + "row").Count());
    }

    [Fact]
    public async Task EngineWrapperExportsObjectListsInEveryFormat()
    {
        var data = Enumerable.Range(1, 5).Select(i => new GridTestSupport.Order { Id = i, Customer = $"C{i}", UnitPrice = i * 2 }).ToList();

        foreach (var format in Enum.GetValues<ExportFormat>())
        {
            using var stream = new MemoryStream();
            await ExportEngine.ExportAsync(data, format, stream);
            Assert.True(stream.Length > 50, format.ToString());
        }

        using var pdf = new MemoryStream();
        await ExportEngine.ExportAsync(data, ExportFormat.Pdf, pdf);
        ExportTestSupport.InspectPdf(pdf.ToArray());

        using var json = new MemoryStream();
        await ExportEngine.ExportAsync(data, ExportFormat.Json, json);
        Assert.Equal(5, JsonDocument.Parse(json.ToArray()).RootElement.GetArrayLength());
        Assert.Equal(".xlsx", ExportEngine.FileExtension(ExportFormat.Xlsx));
        Assert.Equal("excelxml", ExportEngine.FormatId(ExportFormat.ExcelXml));
    }

    private sealed class TsvProvider : IExportProvider
    {
        public string Id => "tsv";
        public string DisplayName => "Tab separated";
        public string FileExtension => ".tsv";
        public string ContentType => "text/tab-separated-values";

        public Task WriteAsync(ExportDocument document, Stream output, ExportContext context)
        {
            using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);
            foreach (var sheet in document.Sheets)
            {
                writer.WriteLine(string.Join('\t', sheet.Columns.Select(c => c.Title)));
                foreach (var row in sheet.Rows)
                {
                    writer.WriteLine(string.Join('\t', row.Cells.Select((c, i) => ExportFormatting.FormatCell(c, sheet.Columns[i], context.Options.Culture))));
                }
            }
            return Task.CompletedTask;
        }
    }

    private sealed class SyncProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;
        public SyncProgress(Action<T> handler) => _handler = handler;
        public void Report(T value) => _handler(value);
    }
}

public class GridExportTests
{
    private sealed class Line
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public int Qty { get; set; }
    }

    private static async Task<(DataGrid<Order> Grid, List<Order> Orders)> SampleGrid()
    {
        var orders = Orders();
        var grid = OrderGrid(orders);
        await grid.Loaded();
        return (grid, orders);
    }

    [Fact]
    public async Task GridExportCoversVisibleColumnsInDisplayOrderWithTypedValues()
    {
        var (grid, _) = await SampleGrid();
        grid.PinColumn("Quantity", ColumnPinPosition.Left);
        grid.SetColumnVisibility("Status", false);
        grid.Columns.Add(new GridCommandColumn { Field = "Actions" });
        grid.Columns.Add(new GridColumn { Field = "Amount", Title = "Amount", Expression = "[UnitPrice] * [Quantity]", FormatString = "N2" });
        grid.SortBy("Id", ListSortDirection.Descending);

        var document = await GridExporter.CreateDocumentAsync(grid);
        var sheet = document.Sheets.Single();
        var rows = sheet.Rows.ToList();

        Assert.Equal(new[] { "Quantity", "Id", "Customer", "Region", "UnitPrice", "Discount", "Amount" }, sheet.Columns.Select(c => c.Key));
        Assert.Equal(6, rows.Count);
        Assert.Equal(6, rows[0].Cells[1]);
        Assert.Equal(360m, rows[0].Cells[6]);
        Assert.Equal(ExportAlignment.Right, sheet.Columns[0].Alignment);
        Assert.Equal(6, sheet.RowCountHint);
    }

    [Fact]
    public async Task GroupedExportCarriesHeadersAggregatesAndFooters()
    {
        var (grid, _) = await SampleGrid();
        grid.GroupSummaries.Add(new GroupSummaryDescriptor("Quantity", AggregateFunction.Sum));
        grid.Columns.First(c => c.Field == "Quantity").FooterSummaries.Add(new GridColumnSummary(AggregateFunction.Sum));
        grid.Columns.First(c => c.Field == "UnitPrice").FooterSummaries.Add(new GridColumnSummary(AggregateFunction.Average));
        grid.Columns.First(c => c.Field == "UnitPrice").FooterSummaries.Add(new GridColumnSummary(AggregateFunction.Max));
        grid.ShowGroupFooters = true;
        grid.GroupBy("Region");
        await grid.RefreshDataAsync();

        var sheet = (await GridExporter.CreateDocumentAsync(grid)).Sheets.Single();
        var rows = sheet.Rows.ToList();

        Assert.True(sheet.IsStructured);
        Assert.False(sheet.AutoFilter);
        Assert.Equal(new[] { ExportRowKind.GroupHeader, ExportRowKind.Data, ExportRowKind.GroupFooter }, rows.Take(3).Select(r => r.Kind));
        Assert.StartsWith("Region: AMER (1)", rows[0].Text);
        Assert.Equal("Region", rows[0].GroupField);
        Assert.Equal(1, rows[1].Level);
        Assert.Equal("Total", rows[2].Cells[0]);
        Assert.Equal(4L, rows[2].Cells[5]);
        Assert.Equal(2, sheet.Footer.Count);
        Assert.Equal(21L, sheet.Footer[0].Cells[5]);
        Assert.Equal(60m, sheet.Footer[1].Cells[4]);

        var noGroups = (await GridExporter.CreateDocumentAsync(grid, new GridExportOptions { IncludeGroups = false })).Sheets.Single();
        Assert.Equal(6, noGroups.Rows.Count(r => r.Kind == ExportRowKind.Data));
        Assert.DoesNotContain(noGroups.Rows, r => r.Kind == ExportRowKind.GroupHeader);
    }

    [Fact]
    public async Task ConditionalFormattingBecomesCellStylesAndNativeRules()
    {
        var (grid, _) = await SampleGrid();
        grid.FormatRules.Add(new GridFormatRule { ColumnField = "Quantity", RuleType = FormatRuleType.HighlightCell, Operator = FormatConditionOperator.GreaterThan, Value1 = 4, BackgroundColorHex = "#FFEEAA", ForegroundColorHex = "#552200" });
        grid.FormatRules.Add(new GridFormatRule { ColumnField = "UnitPrice", RuleType = FormatRuleType.DataBar, Value1 = 0m, Value2 = 100m, BackgroundColorHex = "#5088D7" });
        grid.FormatRules.Add(new GridFormatRule { ColumnField = "Id", RuleType = FormatRuleType.ColorScale, Value1 = 1, Value2 = 6, BackgroundColorHex = "#000000", ForegroundColorHex = "#FFFFFF" });
        grid.FormatRules.Add(new GridFormatRule { ColumnField = "Region", RuleType = FormatRuleType.HighlightCell, Operator = FormatConditionOperator.Equal, Value1 = "APAC", ApplyToRow = true, BackgroundColorHex = "#CCCCCC" });

        var sheet = (await GridExporter.CreateDocumentAsync(grid)).Sheets.Single();
        var rows = sheet.Rows.ToList();
        var quantity = sheet.Columns.Select(c => c.Key).ToList().IndexOf("Quantity");
        var price = sheet.Columns.Select(c => c.Key).ToList().IndexOf("UnitPrice");
        var id = sheet.Columns.Select(c => c.Key).ToList().IndexOf("Id");

        Assert.Null(rows[0].CellStyles![quantity]?.BackColorHex);
        Assert.Equal("#FFEEAA", rows[4].CellStyles![quantity]!.BackColorHex);
        Assert.Equal("#552200", rows[4].CellStyles![quantity]!.ForeColorHex);
        Assert.Equal(0.5, rows[4].CellStyles![price]!.DataBar);
        Assert.Equal("#000000", rows[0].CellStyles![id]!.BackColorHex);
        Assert.Equal("#FFFFFF", rows[5].CellStyles![id]!.BackColorHex);
        Assert.Equal("#CCCCCC", rows[1].Style!.BackColorHex);
        Assert.Null(rows[0].Style);

        Assert.Equal(2, sheet.ConditionalFormats.Count);
        Assert.Contains(sheet.ConditionalFormats, f => f.Type == ExportConditionalFormatType.DataBar && f.ColumnKey == "UnitPrice" && f.Maximum == 100);

        var plain = (await GridExporter.CreateDocumentAsync(grid, new GridExportOptions { ApplyConditionalFormatting = false })).Sheets.Single();
        Assert.Empty(plain.ConditionalFormats);
        Assert.All(plain.Rows, r => Assert.Null(r.CellStyles));
    }

    [Fact]
    public async Task HierarchyExportsNestedChildTablesOrOneSheetPerRelation()
    {
        var customers = Orders(3);
        var orders = Orders(6);
        var lines = orders.SelectMany(o => Enumerable.Range(1, 2).Select(n => new Line { Id = o.Id * 10 + n, OrderId = o.Id, Sku = $"SKU-{o.Id}-{n}", Qty = n })).ToList();
        var grid = OrderGrid(customers);
        grid.EnableMasterDetail = true;
        var relation = grid.AddRelation("Orders", "Id", "CustomerId", orders);
        relation.ChildRelations.Add(new GridHierarchyRelation { RelationName = "Lines", KeyField = "Id", ForeignField = "OrderId", ChildSource = lines });
        await grid.Loaded();

        var nested = (await GridExporter.CreateDocumentAsync(grid)).Sheets.Single();
        var rows = nested.Rows.ToList();

        Assert.True(nested.IsStructured);
        Assert.Equal(3, rows.Count(r => r.Kind == ExportRowKind.Data));
        Assert.Equal(3, rows.Count(r => r.Kind == ExportRowKind.DetailHeader && r.RelationName == "Orders"));
        Assert.Equal(6, rows.Count(r => r.Kind == ExportRowKind.DetailHeader && r.RelationName == "Lines"));
        Assert.Equal(12, rows.Count(r => r.Kind == ExportRowKind.DetailData && r.Level == 2));
        Assert.Equal(6, rows.Count(r => r.Kind == ExportRowKind.DetailData && r.Level == 1));
        var firstHeader = rows.First(r => r.Kind == ExportRowKind.DetailHeader);
        Assert.Contains(firstHeader.DetailColumns!, c => c.Key == "Customer");
        Assert.Equal(1, firstHeader.Level);

        using var zip = ExportTestSupport.Open(await Export(nested, grid));
        Assert.NotNull(zip.GetEntry("xl/worksheets/sheet1.xml"));

        var sheets = await GridExporter.CreateDocumentAsync(grid, new GridExportOptions { HierarchyAsSheets = true });
        Assert.Equal(new[] { "Data", "Orders", "Orders - Lines" }, sheets.Sheets.Select(s => s.Name));
        Assert.Equal("Parent", sheets.Sheets[1].Columns[0].Key);
        Assert.Equal(6, sheets.Sheets[1].Rows.Count());
        Assert.Equal(12, sheets.Sheets[2].Rows.Count());
        Assert.Equal(1, sheets.Sheets[1].Rows.First().Cells[0]);

        var flat = (await GridExporter.CreateDocumentAsync(grid, new GridExportOptions { IncludeHierarchy = false })).Sheets.Single();
        Assert.Equal(3, flat.Rows.Count());
        Assert.False(flat.IsStructured);
    }

    private static async Task<byte[]> Export(ExportSheet sheet, DataGrid<Order> grid)
    {
        var document = new ExportDocument { Title = "h" };
        document.Sheets.Add(sheet);
        return await ExportTestSupport.Export(document, "xlsx");
    }

    [Fact]
    public async Task LazyRelationsAreLoadedBeforeExporting()
    {
        var grid = OrderGrid(Orders(2));
        grid.EnableMasterDetail = true;
        grid.Relations.Add(new GridHierarchyRelation
        {
            RelationName = "Lazy",
            LoadChildrenAsync = async (parent, ct) =>
            {
                await Task.Delay(1, ct);
                return new[] { new Line { Id = 1, Sku = "X" } };
            }
        });
        await grid.Loaded();

        var rows = (await GridExporter.CreateDocumentAsync(grid)).Sheets.Single().Rows.ToList();

        Assert.Equal(2, rows.Count(r => r.Kind == ExportRowKind.DetailData));
    }

    [Fact]
    public async Task TreeExportListsRowsDepthFirstWithLevels()
    {
        var depts = new List<Dept>
        {
            new() { Id = 1, Name = "HQ" }, new() { Id = 2, ParentId = 1, Name = "Eng" }, new() { Id = 3, ParentId = 2, Name = "Platform" },
            new() { Id = 4, ParentId = 1, Name = "Ops" }, new() { Id = 5, ParentId = 6, Name = "A" }, new() { Id = 6, ParentId = 5, Name = "B" }
        };
        var grid = new DataGrid<Dept>();
        grid.Columns.Add(new GridColumn { Field = "Name" });
        grid.DataSource = depts;
        grid.AddSelfReference("Id", "ParentId");
        await grid.Loaded();

        var rows = (await GridExporter.CreateDocumentAsync(grid)).Sheets.Single().Rows.ToList();

        Assert.Equal(new[] { "HQ", "Eng", "Platform", "Ops", "A", "B" }, rows.Select(r => (string)r.Cells[0]!));
        Assert.Equal(new[] { 0, 1, 2, 1, 0, 1 }, rows.Select(r => r.Level));
        Assert.True(rows[0].HasChildren);
        Assert.False(rows[2].HasChildren);
    }

    [Fact]
    public async Task RunningTotalsBandsAndPagingOptionsAreRespected()
    {
        var (grid, _) = await SampleGrid();
        grid.Columns.Add(new GridColumn { Field = "Running", Title = "Running", RunningTotalOf = "Quantity" });
        var band = new GridColumnBand { Title = "Commercial" };
        grid.Bands.Add(band);
        grid.Columns.First(c => c.Field == "UnitPrice").BandId = band.Id;
        grid.Columns.First(c => c.Field == "Quantity").BandId = band.Id;
        grid.PagingMode = GridPagingMode.Client;
        grid.PageSize = 4;
        await grid.RefreshDataAsync();
        grid.NextPage();

        var all = (await GridExporter.CreateDocumentAsync(grid)).Sheets.Single();
        Assert.Equal(6, all.Rows.Count());
        Assert.Equal(new decimal[] { 1, 3, 6, 10, 15, 21 }, all.Rows.Select(r => (decimal)r.Cells[^1]!));
        Assert.Equal(new[] { "Commercial" }, all.Columns.First(c => c.Key == "UnitPrice").Bands);
        Assert.Empty(all.Columns.First(c => c.Key == "Id").Bands);

        var page = (await GridExporter.CreateDocumentAsync(grid, new GridExportOptions { CurrentPageOnly = true, IncludeBands = false })).Sheets.Single();
        Assert.Equal(new[] { 5, 6 }, page.Rows.Select(r => (int)r.Cells[0]!));
        Assert.Empty(page.Columns.First(c => c.Key == "UnitPrice").Bands);

        var limited = (await GridExporter.CreateDocumentAsync(grid, new GridExportOptions { MaxRows = 2 })).Sheets.Single();
        Assert.Equal(2, limited.Rows.Count());
    }

    [Fact]
    public async Task ExportToFileInfersTheFormatAndHonorsFormatOptions()
    {
        var (grid, _) = await SampleGrid();
        var dir = Path.Combine(Path.GetTempPath(), "gridexport-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var xlsx = Path.Combine(dir, "grid.xlsx");
            var result = await GridExporter.ExportToFileAsync(grid, xlsx, new GridExportOptions { Title = "Orders" });
            Assert.Equal("xlsx", result.FormatId);
            using (var zip = ZipFile.OpenRead(xlsx))
            {
                Assert.NotNull(zip.GetEntry("xl/worksheets/sheet1.xml"));
            }

            await GridExporter.ExportToFileAsync(grid, Path.Combine(dir, "grid.pdf"));
            ExportTestSupport.InspectPdf(await File.ReadAllBytesAsync(Path.Combine(dir, "grid.pdf")));
            await GridExporter.ExportToFileAsync(grid, Path.Combine(dir, "grid.csv"));
            Assert.Equal(7, (await File.ReadAllLinesAsync(Path.Combine(dir, "grid.csv"))).Length);
            await GridExporter.ExportToFileAsync(grid, Path.Combine(dir, "grid.json"));
            using (var json = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(dir, "grid.json"))))
            {
                Assert.Equal(6, json.RootElement.GetProperty("sheets")[0].GetProperty("rows").GetArrayLength());
            }

            Assert.Throws<ArgumentException>(() => GridExporter.FormatIdForPath("grid.docx"));
            await Assert.ThrowsAsync<ArgumentException>(() => GridExporter.ExportToFileAsync(grid, Path.Combine(dir, "grid.docx")));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task MillionRowGridExportsToCsvWithoutMaterializingTheDocument()
    {
        var orders = Orders(250_000);
        var grid = OrderGrid(orders);
        await grid.Loaded();
        grid.SortBy("Quantity", ListSortDirection.Descending);
        await grid.RefreshDataAsync();

        using var stream = new MemoryStream();
        var result = await GridExporter.ExportAsync(grid, "csv", stream, new GridExportOptions(), ExportTestSupport.InvariantOptions());

        Assert.Equal(250_000, result.Rows);
        var firstLines = ExportTestSupport.Text(stream.ToArray()).Split("\r\n").Take(3).ToList();
        Assert.StartsWith("250000,", firstLines[1]);
    }
}
