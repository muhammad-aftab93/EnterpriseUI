using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using EnterpriseUI.Documents;
using EnterpriseUI.Editors;
using EnterpriseUI.Layout;
using EnterpriseUI.Navigation;
using EnterpriseUI.WinForms;
using EnterpriseUI.WinFormsDemo.Infrastructure;

namespace EnterpriseUI.WinFormsDemo.Modules;

public class CompleteControlCatalogDemoView : UserControl
{
    private readonly BaseDemoView _baseView;
    private readonly TabControl _tabs;
    private readonly EnterpriseVirtualGridControl _virtualGrid;
    private readonly EnterpriseSyntaxEditorControl _syntaxEditor;
    private readonly EnterpriseBulletGraphControl _bulletGraph;
    private readonly EnterpriseCarouselControl _carousel;
    private readonly EnterpriseRatingAndWaitingControl _ratingWaiting;
    private readonly EnterpriseClockControl _clock;
    private readonly EnterpriseCalloutControl _callout;
    private readonly EnterpriseRangeSelectorControl _rangeSelector;
    private readonly EnterpriseSlideRotatorPipsControl _slideRotatorPips;
    private readonly System.Windows.Forms.Timer _marqueeTimer;

    public CompleteControlCatalogDemoView(DemoItem metadata, int initialTabIndex = 0)
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 9f);

        _baseView = new BaseDemoView { Dock = DockStyle.Fill };
        _baseView.BindDemoMetadata(metadata);
        Controls.Add(_baseView);

        _tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9f),
            Padding = new Point(8, 4)
        };

        // =====================================================================
        // TAB 1: Complete 118-Control Telerik Documentation Catalog & Launcher
        // =====================================================================
        var tabCatalog = new TabPage("📋 All 118 Controls Catalog");
        var catalogTop = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 42,
            Padding = new Padding(8, 6, 8, 6),
            BackColor = Color.FromArgb(245, 247, 250),
            WrapContents = false
        };

        var lblFilter = new Label { Text = "Search 118 Controls:", AutoSize = true, Padding = new Padding(0, 5, 4, 0), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        var txtSearchCatalog = new System.Windows.Forms.TextBox { Width = 220, PlaceholderText = "e.g., RadAIPrompt, RadGridView, RadClock..." };
        var cmbCatFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 210 };
        cmbCatFilter.Items.Add("All Categories (118 Controls)");
        foreach (var cat in TelerikWinFormsControlRegistry.AllControls.Select(c => c.Category).Distinct())
        {
            cmbCatFilter.Items.Add(cat);
        }
        cmbCatFilter.SelectedIndex = 0;

        var btnLaunchSelected = new Button
        {
            Text = "🚀 Open Selected Control Demo",
            Size = new Size(210, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White
        };

        var lblCatalogCount = new Label
        {
            Text = $"Showing {TelerikWinFormsControlRegistry.AllControls.Count} / {TelerikWinFormsControlRegistry.AllControls.Count} Official Controls (100% Parity)",
            AutoSize = true,
            Padding = new Padding(10, 5, 0, 0),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(16, 124, 65)
        };

        var lvCatalog = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Font = new Font("Segoe UI", 9f)
        };
        lvCatalog.Columns.Add("Telerik Control", 200);
        lvCatalog.Columns.Add("EnterpriseUI Implementation", 250);
        lvCatalog.Columns.Add("Category", 185);
        lvCatalog.Columns.Add("Key Enterprise Features", 390);
        lvCatalog.Columns.Add("Docs Path", 260);

        void PopulateCatalogList()
        {
            lvCatalog.BeginUpdate();
            lvCatalog.Items.Clear();
            var q = txtSearchCatalog.Text.Trim();
            var cat = cmbCatFilter.SelectedIndex <= 0 ? null : cmbCatFilter.SelectedItem?.ToString();

            var filtered = TelerikWinFormsControlRegistry.AllControls.Where(c =>
                (cat == null || string.Equals(c.Category, cat, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrEmpty(q) ||
                 c.TelerikName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                 c.EnterpriseUIName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                 c.KeyFeatures.Contains(q, StringComparison.OrdinalIgnoreCase))).ToList();

            foreach (var entry in filtered)
            {
                var item = new ListViewItem(entry.TelerikName) { Tag = entry };
                item.SubItems.Add(entry.EnterpriseUIName);
                item.SubItems.Add(entry.Category);
                item.SubItems.Add(entry.KeyFeatures);
                item.SubItems.Add(entry.DocumentationPath);
                lvCatalog.Items.Add(item);
            }
            lvCatalog.EndUpdate();
            lblCatalogCount.Text = $"Showing {filtered.Count} / {TelerikWinFormsControlRegistry.AllControls.Count} Official Controls (100% Parity)";
        }

        txtSearchCatalog.TextChanged += (_, _) => PopulateCatalogList();
        cmbCatFilter.SelectedIndexChanged += (_, _) => PopulateCatalogList();
        btnLaunchSelected.Click += (_, _) =>
        {
            if (lvCatalog.SelectedItems.Count > 0 && lvCatalog.SelectedItems[0].Tag is TelerikControlCatalogEntry entry)
            {
                _baseView.LogEvent("Catalog.LaunchControl", $"{entry.TelerikName} -> {entry.EnterpriseUIName}");
                var targetDemo = DemoMetadataRegistry.GetById(entry.DemoSuiteId);
                DemoNavigationManager.Instance.NavigateTo(targetDemo);
            }
        };
        lvCatalog.DoubleClick += (_, _) => btnLaunchSelected.PerformClick();

        PopulateCatalogList();
        catalogTop.Controls.AddRange(new Control[] { lblFilter, txtSearchCatalog, cmbCatFilter, btnLaunchSelected, lblCatalogCount });
        tabCatalog.Controls.Add(lvCatalog);
        tabCatalog.Controls.Add(catalogTop);

        // =====================================================================
        // TAB 2: AI Prompt, SmartPasteButton & SpeechToTextButton
        // =====================================================================
        var tabAiSmart = new TabPage("🤖 AIPrompt, SmartPaste & SpeechToText");
        var aiPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };

        var grpAiPrompt = new GroupBox
        {
            Text = "RadAIPrompt & RadAIPromptButton — Generative Prompt, Output & Commands View",
            Location = new Point(14, 10),
            Size = new Size(520, 250),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        var txtPromptInput = new System.Windows.Forms.TextBox
        {
            Text = "Summarize Q4 EMEA high-priority financial anomalies and generate C# filter rules",
            Location = new Point(14, 28),
            Width = 370,
            Font = new Font("Segoe UI", 9f)
        };
        var btnRunPrompt = new Button
        {
            Text = "✨ Execute AIPrompt",
            Location = new Point(392, 26),
            Size = new Size(114, 28),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.75f)
        };
        var rtbAiOutput = new RichTextBox
        {
            Location = new Point(14, 64),
            Size = new Size(492, 136),
            ReadOnly = true,
            Font = new Font("Consolas", 8.75f),
            BackColor = Color.FromArgb(248, 250, 252),
            Text = "[AIPrompt Output View]\n• Detected 3 critical variance spikes in EMEA Q4 ledger (+18.4% above baseline).\n• Generated Expression: Region == 'EMEA' AND Priority == 'Critical' AND GrossAmount > 25000\n• Commands: [Copy Code] [Insert into DataGrid] [Retry] [👍 Rate Helpful]"
        };
        var btnAiTone = new Button { Text = "⚡ Command: Professional Tone", Location = new Point(14, 208), Size = new Size(190, 28), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f) };
        var btnAiFixGrammar = new Button { Text = "⚡ Command: Translate to SQL", Location = new Point(214, 208), Size = new Size(180, 28), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f) };

        btnRunPrompt.Click += (_, _) =>
        {
            rtbAiOutput.Text = $"[AIPrompt Executed @ {DateTime.Now:HH:mm:ss}]\nPrompt: \"{txtPromptInput.Text}\"\n\nResult:\ngrid.ApplyFilter(r => r.Region == \"EMEA\" && r.Priority == \"Critical\");\n// Latency: 1.4ms (Offline deterministic NLP engine)";
            _baseView.LogEvent("AIPrompt.PromptRequest", txtPromptInput.Text);
        };
        btnAiTone.Click += (_, _) =>
        {
            rtbAiOutput.Text = "[AIPrompt Command: Executive Summary Tone]\nEMEA Q4 operations achieved a 99.94% SLA adherence rate with $14.2M net margin expansion.";
            _baseView.LogEvent("AIPrompt.CommandExecuted", "Professional Tone");
        };
        btnAiFixGrammar.Click += (_, _) =>
        {
            rtbAiOutput.Text = "[AIPrompt Command: SQL Query Synthesis]\nSELECT OrderId, Customer, GrossAmount FROM EnterpriseOrders WHERE Region = 'EMEA' AND Priority = 'Critical' ORDER BY GrossAmount DESC;";
            _baseView.LogEvent("AIPrompt.CommandExecuted", "Translate to SQL");
        };
        grpAiPrompt.Controls.AddRange(new Control[] { txtPromptInput, btnRunPrompt, rtbAiOutput, btnAiTone, btnAiFixGrammar });

        var grpSmartPaste = new GroupBox
        {
            Text = "RadSmartPasteButton & RadSpeechToTextButton — Intelligent Form Extraction",
            Location = new Point(550, 10),
            Size = new Size(510, 250),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        var txtRawClipboard = new System.Windows.Forms.TextBox
        {
            Multiline = true,
            Location = new Point(14, 28),
            Size = new Size(480, 62),
            Font = new Font("Consolas", 8.5f),
            Text = "CustomerName: Contoso Global Holdings\r\nEmail: treasury@contoso.com | Phone: (415) 890-4421\r\nCompany: Contoso FinTech | Amount: $48,950.00"
        };
        var btnSmartPaste = new Button
        {
            Text = "📋 SmartPaste from Text",
            Location = new Point(14, 98),
            Size = new Size(190, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(16, 124, 65),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 8.75f, FontStyle.Bold)
        };
        var btnSpeechToText = new Button
        {
            Text = "🎙️ SpeechToText Dictate",
            Location = new Point(214, 98),
            Size = new Size(190, 30),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.75f, FontStyle.Bold)
        };
        var lblSmartResult = new Label
        {
            Location = new Point(14, 138),
            Size = new Size(480, 100),
            Font = new Font("Segoe UI", 8.75f),
            Text = "Extracted Form Fields:\n• Customer: (Click SmartPaste)\n• Email: —\n• Phone: —\n• Amount: —"
        };

        btnSmartPaste.Click += (_, _) =>
        {
            var fields = SmartPasteEngine.ExtractFieldsFromText(txtRawClipboard.Text);
            lblSmartResult.Text =
                $"Extracted Form Fields (SmartPasteEngine):\n" +
                $"• Customer: {fields.GetValueOrDefault("CustomerName", "N/A")}\n" +
                $"• Email:    {fields.GetValueOrDefault("Email", "N/A")}\n" +
                $"• Phone:    {fields.GetValueOrDefault("Phone", "N/A")}\n" +
                $"• Amount:   {fields.GetValueOrDefault("Amount", "N/A")}";
            _baseView.LogEvent("SmartPasteButton.Pasted", $"Extracted {fields.Count} structured fields");
        };

        btnSpeechToText.Click += (_, _) =>
        {
            txtRawClipboard.Text = "CustomerName: Alpine Ski House\r\nEmail: procurement@alpineskihouse.com | Phone: (206) 555-0199\r\nAmount: $125,400.00";
            btnSmartPaste.PerformClick();
            _baseView.LogEvent("SpeechToTextButton.Transcribed", "Voice dictation captured and parsed");
        };

        grpSmartPaste.Controls.AddRange(new Control[] { txtRawClipboard, btnSmartPaste, btnSpeechToText, lblSmartResult });
        aiPanel.Controls.AddRange(new Control[] { grpAiPrompt, grpSmartPaste });
        tabAiSmart.Controls.Add(aiPanel);

        // =====================================================================
        // TAB 3: DataEntry, DataFilter, EnterpriseFilterView, EnterpriseCardView & ValidationProvider
        // =====================================================================
        var tabDataControls = new TabPage("🗂️ DataEntry, DataFilter, EnterpriseCardView & Validation");
        var dataSplit = new System.Windows.Forms.SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 520
        };

        var leftDataPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), AutoScroll = true };
        var lblDataFilterTitle = new Label
        {
            Text = "RadDataFilter & RadFilterView — Hierarchical Expression Tree",
            Location = new Point(12, 10),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };
        var tvFilter = new System.Windows.Forms.TreeView
        {
            Location = new Point(12, 36),
            Size = new Size(480, 145),
            Font = new Font("Segoe UI", 9f),
            CheckBoxes = true
        };
        var rootAnd = new TreeNode("AND (All conditions must match)") { Checked = true };
        rootAnd.Nodes.Add(new TreeNode("[Region] IsEqualTo 'North America' OR 'EMEA'") { Checked = true });
        rootAnd.Nodes.Add(new TreeNode("[AnnualRevenue] IsGreaterThanOrEqualTo $50,000") { Checked = true });
        var subOr = new TreeNode("OR (Nested Group)") { Checked = true };
        subOr.Nodes.Add(new TreeNode("[AccountTier] IsEqualTo 'Enterprise Platinum'") { Checked = true });
        subOr.Nodes.Add(new TreeNode("[SLAStatus] IsEqualTo 'Verified'") { Checked = true });
        rootAnd.Nodes.Add(subOr);
        tvFilter.Nodes.Add(rootAnd);
        tvFilter.ExpandAll();

        var lblDataEntryTitle = new Label
        {
            Text = "RadDataEntry & RadValidationProvider — Auto-Bound Entity Form",
            Location = new Point(12, 192),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };
        var txtEntityName = new System.Windows.Forms.TextBox { Text = "Northwind Cloud Logistics", Location = new Point(150, 220), Width = 220 };
        var lblEntityName = new Label { Text = "Company Name *:", Location = new Point(12, 223), AutoSize = true };
        var numEntityQuota = new NumericUpDown { Minimum = 0, Maximum = 10000000, Value = 250000, Location = new Point(150, 252), Width = 220, ThousandsSeparator = true };
        var lblEntityQuota = new Label { Text = "Credit Limit ($):", Location = new Point(12, 254), AutoSize = true };
        var btnValidateEntity = new Button { Text = "✔ Validate via RadValidationProvider", Location = new Point(150, 286), Size = new Size(220, 28), FlatStyle = FlatStyle.Flat };
        var lblValidationOut = new Label { Text = "Validation Status: Ready", Location = new Point(12, 320), AutoSize = true, ForeColor = Color.FromArgb(16, 124, 65), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };

        btnValidateEntity.Click += (_, _) =>
        {
            bool valid = !string.IsNullOrWhiteSpace(txtEntityName.Text) && numEntityQuota.Value >= 1000;
            lblValidationOut.Text = valid
                ? $"✔ Validation Passed: '{txtEntityName.Text}' (${numEntityQuota.Value:N0})"
                : "✖ Validation Failed: Company Name required and Credit Limit >= $1,000";
            lblValidationOut.ForeColor = valid ? Color.FromArgb(16, 124, 65) : Color.FromArgb(220, 53, 69);
            _baseView.LogEvent("ValidationProvider.Validate", lblValidationOut.Text);
        };

        leftDataPanel.Controls.AddRange(new Control[]
        {
            lblDataFilterTitle, tvFilter,
            lblDataEntryTitle, lblEntityName, txtEntityName, lblEntityQuota, numEntityQuota, btnValidateEntity, lblValidationOut
        });

        var rightCardPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), AutoScroll = true };
        var lblCardViewTitle = new Label
        {
            Text = "RadCardView & RadListView — Multi-Mode Executive Contact Cards",
            Dock = DockStyle.Top,
            Height = 28,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };
        var flowCards = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(4)
        };

        var cardRecords = new[]
        {
            ("Elena Rostova", "VP of Platform Architecture", "EMEA • Zurich", "$1.45M Budget", Color.FromArgb(0, 120, 212)),
            ("Marcus Vance", "Director of FinTech Systems", "NA • New York", "$2.80M Budget", Color.FromArgb(16, 124, 65)),
            ("Kenji Takahashi", "Principal AI Strategist", "APAC • Tokyo", "$1.95M Budget", Color.FromArgb(136, 23, 152)),
            ("Sofia Lindqvist", "Lead Security Auditor", "EMEA • Stockholm", "$980K Budget", Color.FromArgb(216, 59, 1))
        };

        foreach (var c in cardRecords)
        {
            var card = new Panel
            {
                Size = new Size(235, 115),
                Margin = new Padding(6),
                BackColor = Color.FromArgb(248, 250, 252),
                BorderStyle = BorderStyle.FixedSingle
            };
            var stripe = new Panel { Dock = DockStyle.Left, Width = 6, BackColor = c.Item5 };
            var l1 = new Label { Text = c.Item1, Location = new Point(14, 10), AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
            var l2 = new Label { Text = c.Item2, Location = new Point(14, 34), AutoSize = true, Font = new Font("Segoe UI", 8.5f) };
            var l3 = new Label { Text = c.Item3, Location = new Point(14, 58), AutoSize = true, ForeColor = Color.DimGray };
            var l4 = new Label { Text = c.Item4, Location = new Point(14, 82), AutoSize = true, ForeColor = c.Item5, Font = new Font("Segoe UI", 8.75f, FontStyle.Bold) };
            card.Controls.AddRange(new Control[] { stripe, l1, l2, l3, l4 });
            flowCards.Controls.Add(card);
        }

        rightCardPanel.Controls.Add(flowCards);
        rightCardPanel.Controls.Add(lblCardViewTitle);
        dataSplit.Panel1.Controls.Add(leftDataPanel);
        dataSplit.Panel2.Controls.Add(rightCardPanel);
        tabDataControls.Controls.Add(dataSplit);

        // =====================================================================
        // TAB 4: SlideView, Rotator, PipsPager, EnterpriseImageEditor, PictureBox & EnterpriseWebCam
        // =====================================================================
        var tabSlideMedia = new TabPage("🎞️ SlideView, Rotator, PipsPager, EnterpriseImageEditor & EnterpriseWebCam");
        var mediaPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), AutoScroll = true };

        _slideRotatorPips = new EnterpriseSlideRotatorPipsControl
        {
            Dock = DockStyle.Top,
            Height = 195
        };
        _slideRotatorPips.SlideChanged += (idx, title) =>
        {
            _baseView.LogEvent("SlideView.SlideChanged", $"Slide #{idx + 1}: {title}");
        };

        var bottomMediaBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(4, 12, 4, 4),
            AutoScroll = true
        };

        var grpImageEditor = new GroupBox
        {
            Text = "RadImageEditor & RadPictureBox — Live Filter & Transform Pipeline",
            Size = new Size(510, 140),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        var lblImgStatus = new Label { Text = "Active Image State: 1920×1080 RGBA • Zoom: 100% • Rotation: 0°", Location = new Point(14, 28), AutoSize = true, Font = new Font("Segoe UI", 8.75f) };
        int rotationDeg = 0;
        var btnRotate90 = new Button { Text = "⟳ Rotate 90°", Location = new Point(14, 60), Size = new Size(110, 30), FlatStyle = FlatStyle.Flat };
        var btnFlipH = new Button { Text = "⇄ Flip Horiz", Location = new Point(132, 60), Size = new Size(110, 30), FlatStyle = FlatStyle.Flat };
        var btnInvert = new Button { Text = "◐ Invert / Sepia", Location = new Point(250, 60), Size = new Size(125, 30), FlatStyle = FlatStyle.Flat };
        var btnCrop = new Button { Text = "✂ Smart Crop", Location = new Point(383, 60), Size = new Size(110, 30), FlatStyle = FlatStyle.Flat };

        btnRotate90.Click += (_, _) =>
        {
            rotationDeg = (rotationDeg + 90) % 360;
            lblImgStatus.Text = $"Active Image State: Rotated to {rotationDeg}° • Bicubic Interpolation Applied";
            _baseView.LogEvent("EnterpriseImageEditor.Rotate90", $"Angle={rotationDeg}°");
        };
        btnFlipH.Click += (_, _) =>
        {
            lblImgStatus.Text = "Active Image State: Horizontal Mirror Transform Applied";
            _baseView.LogEvent("EnterpriseImageEditor.FlipHorizontal", "Matrix(-1, 0, 0, 1)");
        };
        btnInvert.Click += (_, _) =>
        {
            lblImgStatus.Text = "Active Image State: High-Contrast Sepia & Luminance Filter Applied";
            _baseView.LogEvent("EnterpriseImageEditor.ApplyFilter", "SepiaLuminanceFilter");
        };
        btnCrop.Click += (_, _) =>
        {
            lblImgStatus.Text = "Active Image State: Cropped to 16:9 Golden Ratio Viewport (1600×900)";
            _baseView.LogEvent("EnterpriseImageEditor.Crop", "1600x900");
        };
        grpImageEditor.Controls.AddRange(new Control[] { lblImgStatus, btnRotate90, btnFlipH, btnInvert, btnCrop });

        var grpWebCam = new GroupBox
        {
            Text = "RadWebCam — Enterprise Badge & Telemetry Stream",
            Size = new Size(460, 140),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        var webcamEngine = new EnterpriseUI.Media.EnterpriseWebCam();
        var lblCamStatus = new Label { Text = $"Device: {webcamEngine.DeviceName} (Snapshots: {webcamEngine.CapturedSnapshotsCount})", Location = new Point(14, 28), AutoSize = true, Font = new Font("Segoe UI", 8.75f) };
        var btnCamStart = new Button { Text = "▶ Start Stream", Location = new Point(14, 60), Size = new Size(130, 30), FlatStyle = FlatStyle.Flat };
        var btnCamSnap = new Button { Text = "📸 Capture Snapshot", Location = new Point(152, 60), Size = new Size(150, 30), FlatStyle = FlatStyle.Flat };
        var btnCamStop = new Button { Text = "⏹ Stop", Location = new Point(310, 60), Size = new Size(110, 30), FlatStyle = FlatStyle.Flat };

        btnCamStart.Click += (_, _) =>
        {
            webcamEngine.StartStream();
            lblCamStatus.Text = $"Status: STREAMING LIVE — {webcamEngine.DeviceName}";
            _baseView.LogEvent("EnterpriseWebCam.StartStream", webcamEngine.DeviceName);
        };
        btnCamSnap.Click += (_, _) =>
        {
            if (!webcamEngine.IsStreaming) webcamEngine.StartStream();
            webcamEngine.TakeSnapshot();
            lblCamStatus.Text = $"Status: Snapshot #{webcamEngine.CapturedSnapshotsCount} Captured (1920×1080 RGBA frame buffered)";
            _baseView.LogEvent("EnterpriseWebCam.TakeSnapshot", $"Snapshot #{webcamEngine.CapturedSnapshotsCount}");
        };
        btnCamStop.Click += (_, _) =>
        {
            webcamEngine.StopStream();
            lblCamStatus.Text = "Status: Camera Stream Stopped";
            _baseView.LogEvent("EnterpriseWebCam.StopStream", "Stopped");
        };
        grpWebCam.Controls.AddRange(new Control[] { lblCamStatus, btnCamStart, btnCamSnap, btnCamStop });

        bottomMediaBar.Controls.AddRange(new Control[] { grpImageEditor, grpWebCam });
        mediaPanel.Controls.Add(bottomMediaBar);
        mediaPanel.Controls.Add(_slideRotatorPips);
        tabSlideMedia.Controls.Add(mediaPanel);

        // =====================================================================
        // TAB 5: Clock, Callout & RangeSelector
        // =====================================================================
        var tabClockCallout = new TabPage("🕒 Clock, Callout & RangeSelector");
        var clockPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };

        _clock = new EnterpriseClockControl { Location = new Point(16, 14), Size = new Size(240, 260) };

        var cmbTz = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(280, 18), Width = 260 };
        cmbTz.Items.AddRange(new object[]
        {
            "UTC+0 (London / Enterprise HQ)",
            "UTC-5 (New York / Wall Street)",
            "UTC+1 (Zurich / Frankfurt)",
            "UTC+9 (Tokyo / Nikkei Desk)"
        });
        cmbTz.SelectedIndex = 0;
        cmbTz.SelectedIndexChanged += (_, _) =>
        {
            (_clock.TimeZoneOffsetHours, _clock.TimeZoneLabel) = cmbTz.SelectedIndex switch
            {
                1 => (-5, "UTC-5 (New York)"),
                2 => (1, "UTC+1 (Zurich)"),
                3 => (9, "UTC+9 (Tokyo)"),
                _ => (0, "UTC+0 (London HQ)")
            };
            _clock.Invalidate();
            _baseView.LogEvent("Clock.TimeZoneChanged", _clock.TimeZoneLabel);
        };

        _callout = new EnterpriseCalloutControl
        {
            Location = new Point(280, 56),
            Size = new Size(560, 105)
        };

        _rangeSelector = new EnterpriseRangeSelectorControl
        {
            Location = new Point(280, 174),
            Size = new Size(560, 100)
        };
        _rangeSelector.RangeChanged += (s, e) =>
        {
            _callout.CalloutTitle = $"RadCallout — Live Range Window: {s:F1}% to {e:F1}%";
            _callout.Invalidate();
            _baseView.LogEvent("RangeSelector.RangeChanged", $"{s:F1}% .. {e:F1}%");
        };

        clockPanel.Controls.AddRange(new Control[] { _clock, cmbTz, _callout, _rangeSelector });
        tabClockCallout.Controls.Add(clockPanel);

        // =====================================================================
        // TAB 6: NavigationView, OfficeNavigationBar, Forms, Overlays & Tools
        // =====================================================================
        var tabFormsOverlays = new TabPage("🪟 NavigationView, Forms, Overlays & Tools");
        var formsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };

        var grpNavModes = new GroupBox
        {
            Text = "RadNavigationView & RadOfficeNavigationBar",
            Size = new Size(470, 165),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        var lblNavState = new Label { Text = "NavigationView DisplayMode: Expanded (Hamburger Rail Active)\nOfficeNavigationBar Items: Mail (12) | Calendar | Contacts | Tasks", Location = new Point(14, 28), AutoSize = true };
        var btnNavCompact = new Button { Text = "☰ Toggle Compact / Expanded", Location = new Point(14, 82), Size = new Size(210, 30), FlatStyle = FlatStyle.Flat };
        var btnOfficePeek = new Button { Text = "👁 Show OfficeNav Peek Window", Location = new Point(234, 82), Size = new Size(215, 30), FlatStyle = FlatStyle.Flat };
        btnNavCompact.Click += (_, _) =>
        {
            lblNavState.Text = "NavigationView DisplayMode: Compact (48px Icon Rail Active)\nOfficeNavigationBar: Synchronized with Selected EnterprisePageView";
            _baseView.LogEvent("NavigationView.DisplayMode", "Compact");
        };
        btnOfficePeek.Click += (_, _) =>
        {
            lblNavState.Text = "OfficeNavigationBar PeekPopup: Showing 'Calendar: 4 Upcoming Executive Briefings'";
            _baseView.LogEvent("OfficeNavigationBar.PeekPopup", "Calendar Preview Shown");
        };
        grpNavModes.Controls.AddRange(new Control[] { lblNavState, btnNavCompact, btnOfficePeek });

        var grpSplashOverlay = new GroupBox
        {
            Text = "RadSplashScreen, RadOverlay, RadFlyout & RadToastNotification",
            Size = new Size(490, 165),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        var lblOverlayState = new Label { Text = "Ready to trigger non-blocking Overlays, Flyouts, Splash & Toast Notifications", Location = new Point(14, 28), AutoSize = true };
        var btnTriggerOverlay = new Button { Text = "⏳ Show Busy Overlay", Location = new Point(14, 72), Size = new Size(145, 30), FlatStyle = FlatStyle.Flat };
        var btnTriggerFlyout = new Button { Text = "📥 Open Slide Flyout", Location = new Point(168, 72), Size = new Size(145, 30), FlatStyle = FlatStyle.Flat };
        var btnTriggerToast = new Button { Text = "🔔 Send OS Toast & Tray Badge", Location = new Point(322, 72), Size = new Size(155, 30), FlatStyle = FlatStyle.Flat };
        var btnTaskbarProgress = new Button { Text = "📊 Set TaskbarButton Progress (85%)", Location = new Point(14, 112), Size = new Size(240, 30), FlatStyle = FlatStyle.Flat };
        var btnControlSpy = new Button { Text = "🔍 Launch ControlSpy & ShapeEditor", Location = new Point(264, 112), Size = new Size(213, 30), FlatStyle = FlatStyle.Flat };

        btnTriggerOverlay.Click += (_, _) =>
        {
            lblOverlayState.Text = "RadOverlay Active: Host viewport dimmed at 45% opacity with rotating EnterpriseWaitingBar";
            _baseView.LogEvent("RadOverlay.Show", "Dimmer + Busy Indicator active");
        };
        btnTriggerFlyout.Click += (_, _) =>
        {
            lblOverlayState.Text = "RadFlyout Active: Interactive modal slide-in drawer opened over workspace";
            _baseView.LogEvent("RadFlyout.Show", "Flyout drawer opened");
        };
        btnTriggerToast.Click += (_, _) =>
        {
            lblOverlayState.Text = "RadToastNotification & RadNotifyIcon: Dispatched toast + Tray Icon Badge = 3";
            _baseView.LogEvent("ToastNotificationManager.Show", "Toast + NotifyIcon Badge=3");
        };
        btnTaskbarProgress.Click += (_, _) =>
        {
            lblOverlayState.Text = "RadTaskbarButton: Windows Taskbar Progress State = Normal (85% Complete)";
            _baseView.LogEvent("TaskbarButton.Progress", "85% Normal");
        };
        btnControlSpy.Click += (_, _) =>
        {
            lblOverlayState.Text = "ControlSpy & ShapeEditor: Inspected 118 controls • Bezier Shape Path Validated";
            _baseView.LogEvent("PresentationTools.ControlSpy", "Visual tree hierarchy inspected");
        };

        grpSplashOverlay.Controls.AddRange(new Control[]
        {
            lblOverlayState, btnTriggerOverlay, btnTriggerFlyout, btnTriggerToast, btnTaskbarProgress, btnControlSpy
        });

        formsPanel.Controls.AddRange(new Control[] { grpNavModes, grpSplashOverlay });
        tabFormsOverlays.Controls.Add(formsPanel);

        // =====================================================================
        // TAB 7: EnterpriseVirtualGrid (1M×12), EnterpriseSyntaxEditor, EnterpriseBulletGraph & EnterpriseCarousel
        // =====================================================================
        var tabVirtualGrid = new TabPage("⚡ EnterpriseVirtualGrid (1M×12), EnterpriseSyntaxEditor & EnterpriseCarousel");
        var vgSplit = new System.Windows.Forms.SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 190
        };

        _virtualGrid = new EnterpriseVirtualGridControl { Dock = DockStyle.Fill };
        _virtualGrid.CellSelected += (r, c, val) =>
        {
            _baseView.LogEvent("EnterpriseVirtualGrid.CellSelected", $"Row {r:N0}, Col {c} => '{val}'");
        };

        var bottomVgSplit = new System.Windows.Forms.SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 500
        };

        _syntaxEditor = new EnterpriseSyntaxEditorControl { Dock = DockStyle.Fill };
        var rightVisualsPanel = new Panel { Dock = DockStyle.Fill };
        _bulletGraph = new EnterpriseBulletGraphControl { Dock = DockStyle.Top, Height = 68 };
        _ratingWaiting = new EnterpriseRatingAndWaitingControl { Dock = DockStyle.Top, Height = 72 };
        _carousel = new EnterpriseCarouselControl { Dock = DockStyle.Fill };

        _ratingWaiting.RatingChanged += stars =>
        {
            _bulletGraph.FeaturedMeasure = stars * 18.5;
            _bulletGraph.Invalidate();
            _baseView.LogEvent("EnterpriseRatingControl.Value", $"{stars:F1} stars");
        };

        rightVisualsPanel.Controls.Add(_carousel);
        rightVisualsPanel.Controls.Add(_ratingWaiting);
        rightVisualsPanel.Controls.Add(_bulletGraph);

        bottomVgSplit.Panel1.Controls.Add(_syntaxEditor);
        bottomVgSplit.Panel2.Controls.Add(rightVisualsPanel);

        vgSplit.Panel1.Controls.Add(_virtualGrid);
        vgSplit.Panel2.Controls.Add(bottomVgSplit);
        tabVirtualGrid.Controls.Add(vgSplit);

        _tabs.TabPages.AddRange(new[]
        {
            tabCatalog,
            tabAiSmart,
            tabDataControls,
            tabSlideMedia,
            tabClockCallout,
            tabFormsOverlays,
            tabVirtualGrid
        });

        if (initialTabIndex >= 0 && initialTabIndex < _tabs.TabPages.Count)
        {
            _tabs.SelectedIndex = initialTabIndex;
        }

        _baseView.DemoContainer.Controls.Add(_tabs);

        _marqueeTimer = new System.Windows.Forms.Timer { Interval = 120 };
        _marqueeTimer.Tick += (_, _) => _ratingWaiting.StepMarquee();
        _marqueeTimer.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _marqueeTimer?.Stop();
            _marqueeTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
}
