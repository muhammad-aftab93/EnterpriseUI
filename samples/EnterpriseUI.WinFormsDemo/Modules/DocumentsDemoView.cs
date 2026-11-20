using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using EnterpriseUI.Documents;
using EnterpriseUI.Media;
using EnterpriseUI.Office;
using EnterpriseUI.Pdf;
using EnterpriseUI.WinForms;
using EnterpriseUI.WinFormsDemo.Controls;
using EnterpriseUI.WinFormsDemo.Infrastructure;

namespace EnterpriseUI.WinFormsDemo.Modules;

public class DocumentsDemoView : UserControl
{
    private readonly BaseDemoView _baseView=new BaseDemoView();
    private readonly TabControl _docTabs=new TabControl();

    public DocumentsDemoView(DemoItem metadata)
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 10f);

        _baseView.Dock = DockStyle.Fill ;
        _baseView.BindDemoMetadata(metadata);
        Controls.Add(_baseView);

        _docTabs.Dock = DockStyle.Fill;

        _docTabs.Padding = new Point(12, 5) ;

        // Tab 1: PDF Viewer
        var tabPdf = new TabPage("📄 PDF Document Viewer");
        var pdfPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(240, 240, 240), AutoScroll = true };
        var pdfPage = new Panel
        {
            Size = new Size(520, 680),
            BackColor = Color.White,
            Location = new Point(40, 20),
            BorderStyle = BorderStyle.FixedSingle
        };
        var pdfTitle = new Label
        {
            Text = "ENTERPRISE SERVICE LEVEL AGREEMENT (SLA)\nDocument ID: SLA-2026-X89",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            Location = new Point(24, 24),
            AutoSize = true
        };
        var pdfBody = new Label
        {
            Text = "1. SCOPE OF SERVICES\n" +
                   "This Agreement establishes the operational availability standards and multi-region failover criteria...\n\n" +
                   "2. PERFORMANCE GUARANTEES\n" +
                   "• Core UI Rendering: Sub-16ms frame dispatch (60 FPS minimum).\n" +
                   "• DataGrid Virtualization: 1,000,000 records handled with zero GC pauses under 50MB RAM.\n" +
                   "• WCAG 2.2 Level AA accessibility compliance with automated screen reader live regions.\n\n" +
                   "3. SECURITY & AUDITING\n" +
                   "Full RBAC permission gating, AES-256 data export encryption, and telemetry-free mode support.",
            Font = new Font("Segoe UI", 9.5f),
            Location = new Point(24, 80),
            Size = new Size(460, 400)
        };
        pdfPage.Controls.Add(pdfTitle);
        pdfPage.Controls.Add(pdfBody);
        pdfPanel.Controls.Add(pdfPage);
        tabPdf.Controls.Add(pdfPanel);

        // Tab 2: RichText Editor
        var tabRtf = new TabPage("✍️ RichText WYSIWYG Editor");
        var rtfPanel = new Panel { Dock = DockStyle.Fill };
        var rtfToolbar = new Panel { Dock = DockStyle.Top, Height = 36, Padding = new Padding(4) };
        var btnBold = new Button { Text = "B", Location = new Point(4, 4), Size = new Size(30, 26), Font = new Font("Segoe UI", 9f, FontStyle.Bold), FlatStyle = FlatStyle.Flat };
        var btnItalic = new Button { Text = "I", Location = new Point(38, 4), Size = new Size(30, 26), Font = new Font("Segoe UI", 9f, FontStyle.Italic), FlatStyle = FlatStyle.Flat };
        var rtfBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10.5f),
            Padding = new Padding(12),
            Text = "EnterpriseUI RichText Control offers high-precision formatting, multi-run text styling, embedded tables, and bulleted hierarchy lists.\n\nSelect text to apply real-time typographical treatments."
        };
        btnBold.Click += (_, _) =>
        {
            if (rtfBox.SelectionFont != null)
            {
                var style = rtfBox.SelectionFont.Style ^ FontStyle.Bold;
                rtfBox.SelectionFont = new Font(rtfBox.SelectionFont, style);
                _baseView.LogEvent("EnterpriseRichTextEditor.SelectionBold", style.ToString());
            }
        };
        btnItalic.Click += (_, _) =>
        {
            if (rtfBox.SelectionFont != null)
            {
                var style = rtfBox.SelectionFont.Style ^ FontStyle.Italic;
                rtfBox.SelectionFont = new Font(rtfBox.SelectionFont, style);
                _baseView.LogEvent("EnterpriseRichTextEditor.SelectionItalic", style.ToString());
            }
        };
        rtfToolbar.Controls.Add(btnBold);
        rtfToolbar.Controls.Add(btnItalic);
        rtfPanel.Controls.Add(rtfBox);
        rtfPanel.Controls.Add(rtfToolbar);
        tabRtf.Controls.Add(rtfPanel);

        // Tab 3: Office Spreadsheet & Formula Engine
        var tabSheet = new TabPage("📊 Spreadsheet & Formula Engine");
        var sheetPanel = new Panel { Dock = DockStyle.Fill };
        var sheetBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38, Padding = new Padding(6), BackColor = Color.FromArgb(245, 247, 250) };
        var btnEvalSheet = new Button { Text = "fx Evaluate =SUM(B2,B3) & =AVERAGE(B2,B3)", AutoSize = true, FlatStyle = FlatStyle.Flat };
        var lblFormulaOut = new Label { Text = "Formula Engine Ready", AutoSize = true, Padding = new Padding(8, 5, 0, 0), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };

        var sheetGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            RowCount = 15,
            ColumnCount = 8
        };
        for (int c = 0; c < 8; c++)
        {
            sheetGrid.Columns[c].HeaderText = ((char)('A' + c)).ToString();
            sheetGrid.Columns[c].Width = 95;
        }
        sheetGrid.Rows[0].Cells[0].Value = "Fiscal 2026 Budget";
        sheetGrid.Rows[1].Cells[0].Value = "Q1 Revenue";
        sheetGrid.Rows[1].Cells[1].Value = 1250000;
        sheetGrid.Rows[2].Cells[0].Value = "Q2 Revenue";
        sheetGrid.Rows[2].Cells[1].Value = 1480000;
        sheetGrid.Rows[3].Cells[0].Value = "Total (=SUM)";
        sheetGrid.Rows[3].Cells[1].Value = "=SUM(B2,B3)";

        btnEvalSheet.Click += (_, _) =>
        {
            var dict = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["B2"] = 1250000, ["B3"] = 1480000 };
            double sum = DocumentProcessingSuite.EvaluateSpreadsheetFormula("=SUM(B2,B3)", dict);
            double avg = DocumentProcessingSuite.EvaluateSpreadsheetFormula("=AVERAGE(B2,B3)", dict);
            sheetGrid.Rows[3].Cells[1].Value = sum.ToString("C0");
            lblFormulaOut.Text = $"SUM = {sum:C0} | AVERAGE = {avg:C0}";
            _baseView.LogEvent("SpreadProcessing.Evaluate", lblFormulaOut.Text);
        };
        sheetBar.Controls.AddRange(new Control[] { btnEvalSheet, lblFormulaOut });
        sheetPanel.Controls.Add(sheetGrid);
        sheetPanel.Controls.Add(sheetBar);
        tabSheet.Controls.Add(sheetPanel);

        // Tab 4: ZipLibrary, EnterpriseImageEditor & EnterpriseWebCam
        var tabMediaZip = new TabPage("📷 EnterpriseImageEditor, EnterpriseWebCam & ZipLibrary");
        var mzPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16), AutoScroll = true };

        var imgEditor = new EnterpriseImageEditor { ImageSource = "EnterpriseArchitectureDiagram.png" };
        var webCam = new EnterpriseWebCam();

        var lblImgHeader = new Label { Text = "EnterpriseImageEditor & EnterpriseWebCam Controller:", Location = new Point(16, 14), AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
        var btnRotate = new Button { Text = "↻ Rotate 90°", Location = new Point(16, 42), Size = new Size(110, 28), FlatStyle = FlatStyle.Flat };
        var btnZoomIn = new Button { Text = "🔍+ Zoom In", Location = new Point(134, 42), Size = new Size(110, 28), FlatStyle = FlatStyle.Flat };
        var btnCamSnap = new Button { Text = "📷 EnterpriseWebCam Snapshot", Location = new Point(252, 42), Size = new Size(155, 28), FlatStyle = FlatStyle.Flat };
        var btnZip = new Button { Text = "🗜️ Run ZipLibrary Roundtrip", Location = new Point(415, 42), Size = new Size(185, 28), FlatStyle = FlatStyle.Flat };
        var lblMediaState = new Label
        {
            Text = $"Image Angle: {imgEditor.RotationAngle}° | Zoom: {imgEditor.Zoom:P0} | EnterpriseWebCam Snapshots: 0",
            Location = new Point(16, 82),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 102, 180)
        };

        void SyncMediaLabel(string evt)
        {
            lblMediaState.Text = $"Image Angle: {imgEditor.RotationAngle}° | Zoom: {imgEditor.Zoom:P0} | EnterpriseWebCam Snapshots: {webCam.CapturedSnapshotsCount}";
            _baseView.LogEvent(evt, lblMediaState.Text);
        }

        btnRotate.Click += (_, _) => { imgEditor.RotateClockwise(); SyncMediaLabel("EnterpriseImageEditor.RotateClockwise"); };
        btnZoomIn.Click += (_, _) => { imgEditor.ZoomIn(); SyncMediaLabel("EnterpriseImageEditor.ZoomIn"); };
        btnCamSnap.Click += (_, _) => { webCam.StartStream(); webCam.TakeSnapshot(); SyncMediaLabel("EnterpriseWebCam.TakeSnapshot"); };
        btnZip.Click += (_, _) =>
        {
            var bytes = DocumentProcessingSuite.CreateZipArchive(new Dictionary<string, string> { ["manifest.json"] = "{\"app\":\"EnterpriseUI\"}" });
            var extracted = DocumentProcessingSuite.ExtractZipArchive(bytes);
            lblMediaState.Text = $"ZipLibrary compressed & verified {extracted.Count} entry ({bytes.Length} bytes)";
            _baseView.LogEvent("ZipLibrary.Roundtrip", $"{bytes.Length} bytes");
        };

        mzPanel.Controls.AddRange(new Control[] { lblImgHeader, btnRotate, btnZoomIn, btnCamSnap, btnZip, lblMediaState });
        tabMediaZip.Controls.Add(mzPanel);

        _docTabs.TabPages.AddRange(new[] { tabPdf, tabRtf, tabSheet, tabMediaZip, BuildProcessingTab() });
        _baseView.DemoContainer.Controls.Add(_docTabs);
    }

    private static readonly (int Id, string Name, string Department, decimal Salary)[] SampleStaff =
    {
        (1, "Alexander Hamilton", "Treasury", 182000m), (2, "Abigail Adams", "Legal", 141500m), (3, "Benjamin Franklin", "Engineering", 168250m),
        (4, "Dolley Madison", "Operations", 97800m), (5, "Frederick Douglass", "Communications", 118400m), (6, "Harriet Tubman", "Logistics", 104900m),
        (7, "Thomas Edison", "Engineering", 175000m), (8, "Ada Lovelace", "Engineering", 189600m), (9, "Grace Hopper", "Engineering", 192300m),
        (10, "Katherine Johnson", "Research", 158700m), (11, "Nikola Tesla", "Research", 163100m), (12, "Marie Curie", "Research", 171900m)
    };

    /// <summary>Generates a PDF, DOCX and XLSX from the same data, then reads each file back to prove the round trip.</summary>
    private TabPage BuildProcessingTab()
    {
        var tab = new TabPage("⚙️ PDF, Word & Sheet Processing");
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16), AutoScroll = true };
        var bold = new Font("Segoe UI", 9.5f, FontStyle.Bold);

        panel.Controls.Add(new Label { Text = "Output folder (EnterpriseBrowseBox):", Location = new Point(16, 18), AutoSize = true, Font = bold });
        var folder = new EnterpriseBrowseBox
        {
            Mode = BrowseMode.Folder,
            DialogTitle = "Select the output folder",
            Location = new Point(280, 10),
            Width = 440,
            Text = Path.Combine(Path.GetTempPath(), "EnterpriseUI-Demo"),
            CheckFileExists = false
        };
        panel.Controls.Add(folder);

        var results = new ListBox { Location = new Point(16, 112), Size = new Size(740, 250), IntegralHeight = false, Font = new Font("Consolas", 9.5f) };
        var buttons = new List<Button>();

        Button AddButton(string text, Func<Task> run)
        {
            var button = new Button { Text = text, Location = new Point(16 + buttons.Count * 148, 58), Size = new Size(140, 34), FlatStyle = FlatStyle.Flat };
            button.Click += async (_, _) =>
            {
                foreach (var b in buttons) b.Enabled = false;
                try { await run(); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or InvalidDataException)
                {
                    results.Items.Add("ERROR: " + ex.Message);
                }
                finally { foreach (var b in buttons) b.Enabled = true; }
            };
            buttons.Add(button);
            panel.Controls.Add(button);
            return button;
        }

        string Target(string file)
        {
            Directory.CreateDirectory(folder.Text);
            return Path.Combine(folder.Text, file);
        }

        void Report(string source, string message)
        {
            results.Items.Add(message);
            results.TopIndex = results.Items.Count - 1;
            _baseView.LogEvent(source, message);
        }

        AddButton("Build PDF", () =>
        {
            var lines = new List<string> { "Staff directory", string.Empty };
            lines.AddRange(SampleStaff.Select(s => $"{s.Id,3}  {s.Name,-22} {s.Department,-16} {s.Salary,10:N0}"));
            var document = EnterprisePdfEngine.CreateFromLines("Staff directory", lines);
            var bytes = EnterprisePdfEngine.Write(document);
            File.WriteAllBytes(Target("staff.pdf"), bytes);
            var back = EnterprisePdfEngine.Read(bytes);
            Report("PdfProcessing.Roundtrip", $"PDF   {bytes.Length,9:N0} bytes, {back.Pages.Count} page(s), text identical: {EnterprisePdfEngine.ExtractText(document) == EnterprisePdfEngine.ExtractText(back)}");
            return Task.CompletedTask;
        });

        AddButton("Merge + Split PDF", () =>
        {
            var parts = Enumerable.Range(1, 3).Select(i => EnterprisePdfEngine.CreateFromLines($"Part {i}", Enumerable.Range(1, 120).Select(n => $"Part {i} line {n}"))).ToList();
            var merged = EnterprisePdfEngine.Merge(parts, "merged.pdf");
            var split = EnterprisePdfEngine.Split(merged, 2);
            File.WriteAllBytes(Target("merged.pdf"), EnterprisePdfEngine.Write(merged));
            Report("PdfProcessing.MergeSplit", $"PDF   merged {parts.Count} files into {merged.Pages.Count} pages, split into {split.Count} files of up to 2 pages");
            return Task.CompletedTask;
        });

        AddButton("Build Word (.docx)", () =>
        {
            var document = new WordDocument { Title = "Staff report", Author = "EnterpriseUI Demo" };
            document.AddParagraph("Staff report", WordParagraphStyle.Heading1);
            var intro = document.AddParagraph("Headcount by department, generated with ");
            intro.Runs.Add(new WordRun("EnterpriseWordEngine", bold: true));
            intro.Runs.Add(new WordRun(".", italic: true));
            document.AddTable(new[] { new[] { "Id", "Name", "Department", "Salary" } }.Concat(SampleStaff.Select(s => new[] { s.Id.ToString(), s.Name, s.Department, s.Salary.ToString("N0") })));
            var bytes = EnterpriseWordEngine.Write(document);
            File.WriteAllBytes(Target("staff.docx"), bytes);
            var back = EnterpriseWordEngine.Read(bytes);
            var table = back.Blocks.OfType<WordTable>().Single();
            Report("WordsProcessing.Roundtrip", $"DOCX  {bytes.Length,9:N0} bytes, {back.Blocks.Count} blocks, table {table.Rows.Count}x{table.Rows[0].Count}, header row: {table.HasHeaderRow}");
            return Task.CompletedTask;
        });

        AddButton("Build Excel (.xlsx)", () =>
        {
            var sheet = new SpreadsheetWorksheet { Name = "Staff" };
            string[] headers = { "Id", "Name", "Department", "Salary" };
            for (var c = 0; c < headers.Length; c++) sheet.GetOrCreateCell(SheetReference.Format(1, c + 1)).Value = headers[c];
            for (var r = 0; r < SampleStaff.Length; r++)
            {
                var s = SampleStaff[r];
                sheet.GetOrCreateCell($"A{r + 2}").Value = s.Id;
                sheet.GetOrCreateCell($"B{r + 2}").Value = s.Name;
                sheet.GetOrCreateCell($"C{r + 2}").Value = s.Department;
                sheet.GetOrCreateCell($"D{r + 2}").Value = s.Salary;
            }
            var last = SampleStaff.Length + 1;
            sheet.GetOrCreateCell($"C{last + 1}").Value = "Total";
            sheet.GetOrCreateCell($"D{last + 1}").Formula = $"=SUM(D2:D{last})";
            sheet.GetOrCreateCell($"C{last + 2}").Value = "Average";
            sheet.GetOrCreateCell($"D{last + 2}").Formula = $"=AVERAGE(D2:D{last})";
            var book = new SpreadsheetWorkbook();
            book.Worksheets.Add(sheet);

            var bytes = EnterpriseSheetEngine.Write(book);
            File.WriteAllBytes(Target("staff.xlsx"), bytes);
            var back = EnterpriseSheetEngine.Read(bytes).Worksheets.Single();
            Report("SpreadProcessing.Roundtrip", $"XLSX  {bytes.Length,9:N0} bytes, {back.Cells.Count} cells, total {EnterpriseSheetEngine.EvaluateCell(back, $"D{last + 1}"):N0}, average {EnterpriseSheetEngine.EvaluateCell(back, $"D{last + 2}"):N0}");
            return Task.CompletedTask;
        });

        AddButton("Stream 100k rows", async () =>
        {
            var path = Target("stream.xlsx");
            var watch = Stopwatch.StartNew();
            const int rows = 100_000;
            await Task.Run(() =>
            {
                using var output = File.Create(path);
                using var writer = new EnterpriseSheetStreamWriter(output);
                writer.BeginSheet("Readings");
                writer.WriteRow("Id", "Sensor", "Reading", "At");
                var start = new DateTime(2026, 1, 1);
                for (var i = 1; i <= rows; i++) writer.WriteRow(i, "S-" + i % 50, Math.Round(20 + Math.Sin(i / 50.0) * 5, 3), start.AddSeconds(i));
            });
            var written = watch.ElapsedMilliseconds;
            var counted = await Task.Run(() =>
            {
                using var input = File.OpenRead(path);
                using var reader = new EnterpriseSheetStreamReader(input);
                return reader.ReadRows().Count();
            });
            Report("SpreadStreamProcessing.Roundtrip", $"XLSX  streamed {rows:N0} rows in {written:N0} ms, {new FileInfo(path).Length:N0} bytes, read back {counted:N0} rows");
        });

        var openFolder = new Button { Text = "Open output folder", Location = new Point(16, 372), Size = new Size(170, 30), FlatStyle = FlatStyle.Flat };
        openFolder.Click += (_, _) =>
        {
            if (Directory.Exists(folder.Text)) Process.Start(new ProcessStartInfo(folder.Text) { UseShellExecute = true });
            else Report("Documents.OpenFolder", "The output folder does not exist yet; build a document first.");
        };
        panel.Controls.AddRange(new Control[] { results, openFolder });
        tab.Controls.Add(panel);
        return tab;
    }
}
