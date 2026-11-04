using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using EnterpriseUI.Documents;
using EnterpriseUI.Media;
using EnterpriseUI.WinFormsDemo.Controls;
using EnterpriseUI.WinFormsDemo.Infrastructure;

namespace EnterpriseUI.WinFormsDemo.Modules;

public class DocumentsDemoView : UserControl
{
    private readonly BaseDemoView _baseView;
    private readonly TabControl _docTabs;

    public DocumentsDemoView(DemoItem metadata)
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 9f);

        _baseView = new BaseDemoView { Dock = DockStyle.Fill };
        _baseView.BindDemoMetadata(metadata);
        Controls.Add(_baseView);

        _docTabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 5) };

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
                _baseView.LogEvent("RichTextEditor.SelectionBold", style.ToString());
            }
        };
        btnItalic.Click += (_, _) =>
        {
            if (rtfBox.SelectionFont != null)
            {
                var style = rtfBox.SelectionFont.Style ^ FontStyle.Italic;
                rtfBox.SelectionFont = new Font(rtfBox.SelectionFont, style);
                _baseView.LogEvent("RichTextEditor.SelectionItalic", style.ToString());
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

        // Tab 4: ZipLibrary, ImageEditor & WebCam
        var tabMediaZip = new TabPage("📷 ImageEditor, WebCam & ZipLibrary");
        var mzPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16), AutoScroll = true };

        var imgEditor = new ImageEditor { ImageSource = "EnterpriseArchitectureDiagram.png" };
        var webCam = new WebCam();

        var lblImgHeader = new Label { Text = "RadImageEditor & RadWebCam Controller:", Location = new Point(16, 14), AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
        var btnRotate = new Button { Text = "↻ Rotate 90°", Location = new Point(16, 42), Size = new Size(110, 28), FlatStyle = FlatStyle.Flat };
        var btnZoomIn = new Button { Text = "🔍+ Zoom In", Location = new Point(134, 42), Size = new Size(110, 28), FlatStyle = FlatStyle.Flat };
        var btnCamSnap = new Button { Text = "📷 WebCam Snapshot", Location = new Point(252, 42), Size = new Size(155, 28), FlatStyle = FlatStyle.Flat };
        var btnZip = new Button { Text = "🗜️ Run ZipLibrary Roundtrip", Location = new Point(415, 42), Size = new Size(185, 28), FlatStyle = FlatStyle.Flat };
        var lblMediaState = new Label
        {
            Text = $"Image Angle: {imgEditor.RotationAngle}° | Zoom: {imgEditor.Zoom:P0} | WebCam Snapshots: 0",
            Location = new Point(16, 82),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 102, 180)
        };

        void SyncMediaLabel(string evt)
        {
            lblMediaState.Text = $"Image Angle: {imgEditor.RotationAngle}° | Zoom: {imgEditor.Zoom:P0} | WebCam Snapshots: {webCam.CapturedSnapshotsCount}";
            _baseView.LogEvent(evt, lblMediaState.Text);
        }

        btnRotate.Click += (_, _) => { imgEditor.RotateClockwise(); SyncMediaLabel("ImageEditor.RotateClockwise"); };
        btnZoomIn.Click += (_, _) => { imgEditor.ZoomIn(); SyncMediaLabel("ImageEditor.ZoomIn"); };
        btnCamSnap.Click += (_, _) => { webCam.StartStream(); webCam.TakeSnapshot(); SyncMediaLabel("WebCam.TakeSnapshot"); };
        btnZip.Click += (_, _) =>
        {
            var bytes = DocumentProcessingSuite.CreateZipArchive(new Dictionary<string, string> { ["manifest.json"] = "{\"app\":\"EnterpriseUI\"}" });
            var extracted = DocumentProcessingSuite.ExtractZipArchive(bytes);
            lblMediaState.Text = $"ZipLibrary compressed & verified {extracted.Count} entry ({bytes.Length} bytes)";
            _baseView.LogEvent("ZipLibrary.Roundtrip", $"{bytes.Length} bytes");
        };

        var barcodeCtrl = new WinFormsBarcodeControl { Location = new Point(16, 118), Size = new Size(340, 210) };
        mzPanel.Controls.AddRange(new Control[] { lblImgHeader, btnRotate, btnZoomIn, btnCamSnap, btnZip, lblMediaState, barcodeCtrl });
        tabMediaZip.Controls.Add(mzPanel);

        _docTabs.TabPages.AddRange(new[] { tabPdf, tabRtf, tabSheet, tabMediaZip });
        _baseView.DemoContainer.Controls.Add(_docTabs);
    }
}
