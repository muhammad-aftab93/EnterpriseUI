using System.Globalization;
using EnterpriseUI.Documents;
using EnterpriseUI.Editors;
using EnterpriseUI.Office;
using EnterpriseUI.Pdf;
using Xunit;

namespace EnterpriseUI.Tests.Unit;

public class DocumentEngineTests
{
    [Fact]
    public void PdfRoundTripKeepsPagesAndText()
    {
        var source = EnterprisePdfEngine.CreateFromLines("Report (draft)", new[] { "Line one", "Parens (a) and \\ slash", string.Empty, "Last" });
        var bytes = EnterprisePdfEngine.Write(source);
        var read = EnterprisePdfEngine.Read(bytes);

        Assert.Equal("Report (draft)", read.Title);
        Assert.Single(read.Pages);
        Assert.Equal(source.Pages[0].TextElements, read.Pages[0].TextElements);
        Assert.Equal(612, read.Pages[0].Width);
    }

    [Fact]
    public void PdfPaginatesWrapsMergesAndSplits()
    {
        var lines = Enumerable.Range(1, 120).Select(i => "Row " + i + " " + new string('x', 150));
        var doc = EnterprisePdfEngine.CreateFromLines("Long", lines, maxCharsPerLine: 80);
        Assert.True(doc.Pages.Count > 3);
        Assert.All(doc.Pages.SelectMany(p => p.TextElements), l => Assert.True(l.Length <= 80));

        var parts = EnterprisePdfEngine.Split(doc, 2);
        Assert.Equal((doc.Pages.Count + 1) / 2, parts.Count);
        var merged = EnterprisePdfEngine.Merge(parts);
        Assert.Equal(doc.Pages.Count, merged.Pages.Count);
        Assert.Equal(EnterprisePdfEngine.ExtractText(doc), EnterprisePdfEngine.ExtractText(EnterprisePdfEngine.Read(EnterprisePdfEngine.Write(merged))));
        Assert.Throws<ArgumentOutOfRangeException>(() => EnterprisePdfEngine.ExtractPages(doc, new[] { 999 }));
    }

    [Fact]
    public void PdfReadRejectsNonPdfData()
    {
        Assert.Throws<InvalidDataException>(() => EnterprisePdfEngine.Read(new byte[] { 1, 2, 3 }));
    }

    [Fact]
    public void WordRoundTripKeepsStructureAndFormatting()
    {
        var doc = new WordDocument { Title = "Letter", Author = "Office" };
        doc.AddParagraph("Heading", WordParagraphStyle.Heading1);
        var mixed = doc.AddParagraph("Plain ");
        mixed.Runs.Add(new WordRun("bold", bold: true));
        mixed.Runs.Add(new WordRun(" and <italic> & more", italic: true));
        doc.AddParagraph("Item", WordParagraphStyle.ListBullet);
        doc.AddTable(new[] { new[] { "Name", "Grade" }, new[] { "Ana", "A" } });

        var read = EnterpriseWordEngine.Read(EnterpriseWordEngine.Write(doc));

        Assert.Equal("Letter", read.Title);
        Assert.Equal("Office", read.Author);
        Assert.Equal(4, read.Blocks.Count);
        Assert.Equal(WordParagraphStyle.Heading1, ((WordParagraph)read.Blocks[0]).Style);
        var paragraph = (WordParagraph)read.Blocks[1];
        Assert.Equal("Plain bold and <italic> & more", paragraph.Text);
        Assert.True(paragraph.Runs[1].Bold);
        Assert.True(paragraph.Runs[2].Italic);
        var table = (WordTable)read.Blocks[3];
        Assert.True(table.HasHeaderRow);
        Assert.Equal("Ana", table.Rows[1][0]);
    }

    [Fact]
    public void WordReadRejectsPackagesWithoutDocument()
    {
        Assert.ThrowsAny<Exception>(() => EnterpriseWordEngine.Read(new byte[] { 1, 2, 3 }));
    }

    [Fact]
    public void SheetRoundTripKeepsValuesFormulasAndDates()
    {
        var book = new SpreadsheetWorkbook();
        var sheet = new SpreadsheetWorksheet { Name = "Grades" };
        sheet.GetOrCreateCell("A1").Value = "Math";
        sheet.GetOrCreateCell("B1").Value = 90;
        sheet.GetOrCreateCell("B2").Value = 80.5m;
        sheet.GetOrCreateCell("B3").Formula = "=AVERAGE(B1:B2)";
        sheet.GetOrCreateCell("C1").Value = new DateTime(2026, 3, 14);
        sheet.GetOrCreateCell("D1").Value = true;
        sheet.GetOrCreateCell("E1").Value = "=not a formula <&>";
        book.Worksheets.Add(sheet);

        var read = EnterpriseSheetEngine.Read(EnterpriseSheetEngine.Write(book));
        var back = Assert.Single(read.Worksheets);

        Assert.Equal("Grades", back.Name);
        Assert.Equal("Math", back.Cells["A1"].Value);
        Assert.Equal(90d, back.Cells["B1"].Value);
        Assert.Equal("AVERAGE(B1:B2)", back.Cells["B3"].Formula);
        Assert.Equal(85.25, back.Cells["B3"].Value);
        Assert.Equal(new DateTime(2026, 3, 14), back.Cells["C1"].Value);
        Assert.Equal(true, back.Cells["D1"].Value);
        Assert.Null(back.Cells["E1"].Formula);
        Assert.Equal("=not a formula <&>", back.Cells["E1"].Value);
    }

    [Theory]
    [InlineData("=1+2*3", 7d)]
    [InlineData("=(1+2)*3", 9d)]
    [InlineData("=2^3^2", 64d)]
    [InlineData("=-A1+10", 5d)]
    [InlineData("=SUM(A1:A3)", 30d)]
    [InlineData("=MAX(A1,A2,100)", 100d)]
    [InlineData("=IF(A1>=5,\"big\",\"small\")", "big")]
    [InlineData("=\"a\"&\"b\"&A1", "ab5")]
    [InlineData("=ROUND(2.5,0)", 3d)]
    [InlineData("=50%", 0.5)]
    public void FormulasEvaluate(string formula, object expected)
    {
        var sheet = new SpreadsheetWorksheet();
        sheet.GetOrCreateCell("A1").Value = 5;
        sheet.GetOrCreateCell("A2").Value = 10;
        sheet.GetOrCreateCell("A3").Value = 15;
        Assert.Equal(expected, EnterpriseSheetEngine.EvaluateFormula(sheet, formula));
    }

    [Fact]
    public void FormulaErrorsAreReportedNotThrown()
    {
        var sheet = new SpreadsheetWorksheet();
        sheet.GetOrCreateCell("A1").Formula = "=B1";
        sheet.GetOrCreateCell("B1").Formula = "=A1";
        sheet.GetOrCreateCell("C1").Value = "text";

        Assert.Equal(SheetError.DivideByZero, EnterpriseSheetEngine.EvaluateFormula(sheet, "=1/0"));
        Assert.Equal(SheetError.Reference, EnterpriseSheetEngine.EvaluateCell(sheet, "A1"));
        Assert.Equal(SheetError.Value, EnterpriseSheetEngine.EvaluateFormula(sheet, "=C1+1"));
        Assert.Equal(SheetError.Name, EnterpriseSheetEngine.EvaluateFormula(sheet, "=NOPE(1)"));
        Assert.Equal(SheetError.Name, EnterpriseSheetEngine.EvaluateFormula(sheet, "=1+"));
    }

    [Fact]
    public void StreamWriterAndReaderHandleManyRowsAcrossSheets()
    {
        using var buffer = new MemoryStream();
        using (var writer = new EnterpriseSheetStreamWriter(buffer, leaveOpen: true))
        {
            writer.BeginSheet("Data");
            writer.WriteRow("Id", "Name", "When");
            for (var i = 1; i <= 5000; i++) writer.WriteRow(i, "Name " + i, new DateTime(2026, 1, 1).AddDays(i % 30));
            writer.WriteRow(null, "gap", new SheetFormula("SUM(A2:A5001)"));
            writer.BeginSheet("Data");
            writer.WriteRow("second");
            Assert.Equal(2, writer.SheetCount);
        }

        buffer.Position = 0;
        using var reader = new EnterpriseSheetStreamReader(buffer);
        Assert.Equal(new[] { "Data", "Data_2" }, reader.SheetNames);

        var rows = reader.ReadRows("Data").ToList();
        Assert.Equal(5002, rows.Count);
        Assert.Equal("Id", rows[0].Values[0]);
        Assert.Equal(5000d, rows[5000].Values[0]);
        Assert.Equal(new DateTime(2026, 1, 1).AddDays(5000 % 30), rows[5000].Values[2]);
        Assert.Null(rows[5001].Values[0]);
        Assert.Equal("gap", rows[5001].Values[1]);
        Assert.Equal("second", reader.ReadRows("Data_2").Single().Values[0]);
        Assert.Throws<ArgumentException>(() => reader.ReadRows("Missing").ToList());
    }

    [Fact]
    public void StreamWriterRequiresASheetAndRejectsWritesAfterCompletion()
    {
        using var buffer = new MemoryStream();
        var writer = new EnterpriseSheetStreamWriter(buffer, leaveOpen: true);
        Assert.Throws<InvalidOperationException>(() => writer.WriteRow("x"));
        writer.Complete();
        Assert.Throws<InvalidOperationException>(() => writer.BeginSheet("Late"));
        writer.Dispose();
    }

    [Theory]
    [InlineData(1, "A")]
    [InlineData(26, "Z")]
    [InlineData(27, "AA")]
    [InlineData(16384, "XFD")]
    public void ColumnNamesRoundTrip(int index, string name)
    {
        Assert.Equal(name, SheetReference.ColumnName(index));
        Assert.Equal(index, SheetReference.ColumnIndex(name));
    }

    [Theory]
    [InlineData("A0")]
    [InlineData("XFE1")]
    [InlineData("1A")]
    [InlineData("")]
    public void InvalidReferencesAreRejected(string reference) =>
        Assert.False(SheetReference.TryParse(reference, out _, out _));
}

public class DateAndTimeEditorModelTests
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    [Theory]
    [InlineData("3:45 PM", 15, 45)]
    [InlineData("15:45", 15, 45)]
    [InlineData("1545", 15, 45)]
    [InlineData("930", 9, 30)]
    public void TimeTextParsesCommonForms(string text, int hour, int minute)
    {
        Assert.True(TimeText.TryParse(text, Us, out var time));
        Assert.Equal(new TimeOnly(hour, minute), time);
    }

    [Theory]
    [InlineData("")]
    [InlineData("25:00")]
    [InlineData("2599")]
    [InlineData("noon")]
    public void TimeTextRejectsInvalidInput(string text) => Assert.False(TimeText.TryParse(text, Us, out _));

    [Fact]
    public void TimePickerKeepsTheDateAndRespectsTheRange()
    {
        var picker = new EnterpriseTimePicker { Value = new DateTime(2026, 5, 4, 8, 0, 0), Minimum = new TimeSpan(8, 0, 0), Maximum = new TimeSpan(17, 0, 0), MinuteStep = 15 };
        Assert.True(picker.TrySetText("2:30 PM", Us));
        Assert.Equal(new DateTime(2026, 5, 4, 14, 30, 0), picker.Value);
        Assert.False(picker.TrySetText("6:00 PM", Us));
        Assert.Equal(new DateTime(2026, 5, 4, 14, 30, 0), picker.Value);

        picker.Increment(100);
        Assert.Equal(new TimeSpan(17, 0, 0), picker.TimeOfDay);
        picker.Increment(-1000);
        Assert.Equal(new TimeSpan(8, 0, 0), picker.TimeOfDay);

        Assert.True(picker.TrySetText("  ", Us));
        Assert.Null(picker.Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => picker.MinuteStep = 7);
    }

    [Fact]
    public void TimeOnlyPickerTruncatesToPrecisionAndWraps()
    {
        var picker = new EnterpriseTimeOnlyPicker { Precision = TimeOnlyPrecision.Minutes };
        Assert.True(picker.TrySetValue(new TimeOnly(10, 20, 59)));
        Assert.Equal(new TimeOnly(10, 20), picker.Value);

        picker.Value = new TimeOnly(23, 59);
        picker.Increment(1);
        Assert.Equal(new TimeOnly(0, 0), picker.Value);

        picker.Precision = TimeOnlyPrecision.Seconds;
        Assert.Equal("12:00:05", Format(picker, new TimeOnly(12, 0, 5)));
    }

    private static string Format(EnterpriseTimeOnlyPicker picker, TimeOnly time)
    {
        picker.Is24Hour = true;
        picker.Value = time;
        return picker.DisplayText(Us);
    }

    [Fact]
    public void CalculatorEvaluationDividesAndFailsOnDivisionByZeroWithAnExpectedException()
    {
        var calculator = new EnterpriseCalculatorDropDown();
        Assert.Equal(2.5m, calculator.EvaluateExpression("5/2"));
        Assert.Equal(0.3m, calculator.EvaluateExpression("0.1+0.2"));
        Assert.Equal(-18m, calculator.EvaluateExpression("(-9)*2"));

        var failure = Record.Exception(() => calculator.EvaluateExpression("1/0"));
        Assert.True(failure is System.Data.DataException or OverflowException or InvalidCastException or FormatException or ArithmeticException, "Unexpected exception: " + failure);
    }

    [Fact]
    public void DateOnlyPickerValidatesRangeAndClampsMovement()
    {
        var picker = new EnterpriseDateOnlyPicker { MinDate = new DateOnly(2026, 1, 1), MaxDate = new DateOnly(2026, 12, 31) };
        Assert.True(picker.TrySetText("3/14/2026", Us));
        Assert.Equal(new DateOnly(2026, 3, 14), picker.Value);
        Assert.False(picker.TrySetText("3/14/2027", Us));
        Assert.False(picker.TrySetText("not a date", Us));

        picker.AddDays(1000);
        Assert.Equal(picker.MaxDate, picker.Value);
        picker.AddMonths(-100);
        Assert.Equal(picker.MinDate, picker.Value);
        picker.Value = new DateOnly(2026, 1, 31);
        picker.AddMonths(1);
        Assert.Equal(new DateOnly(2026, 2, 28), picker.Value);
    }
}
