using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Layout;
using EnterpriseUI.Navigation;
using EnterpriseUI.Notifications;
using EnterpriseUI.Scheduling;
using EnterpriseUI.Theming;
using EnterpriseUI.WinForms;
using EnterpriseUI.WinFormsDemo.Infrastructure;

namespace EnterpriseUI.WinFormsDemo.Modules;

public class NavigationAndLayoutDemoView : UserControl
{

    private static readonly string[] CachedValues1 = new[]
        {
            "Customer #1001 — Global FinTech Corp ($1.4M ARR)",
            "Customer #1002 — Apex Healthcare Systems ($890K ARR)",
            "Customer #1003 — Pacific Logistics Group ($2.1M ARR)",
            "Customer #1004 — Sovereign Cloud Defense ($3.6M ARR)"
        };
    private readonly BaseDemoView _baseView=new BaseDemoView();
    private readonly EnterpriseBreadcrumb _breadcrumbModel = new();
    private readonly EnterpriseStepProgressBar _stepModel = new();
    private readonly EnterpriseBindingNavigator<string> _bindingNav = new();
    private readonly EnterpriseWizardControl _wizardModel = new();
    private readonly EnterprisePanorama _panoramaModel = new();
    private readonly EnterpriseCarouselControl _carouselControl=new EnterpriseCarouselControl();
    private readonly EnterpriseRatingAndWaitingControl _waitingControl=new EnterpriseRatingAndWaitingControl();
    private readonly System.Windows.Forms.Timer _liveTimer=new System.Windows.Forms.Timer();

    public NavigationAndLayoutDemoView(DemoItem metadata)
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 9f);

        _baseView.Dock = DockStyle.Fill ;
        _baseView.BindDemoMetadata(metadata);
        Controls.Add(_baseView);

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.25f),
            Padding = new Point(10, 5)
        };

        // ====================================================================
        // Tab 1: EnterpriseBreadcrumb, EnterpriseStepProgressBar, EnterprisePageView & EnterpriseBindingNavigator
        // ====================================================================
        var tabNav = new TabPage("🧭 EnterpriseBreadcrumb, StepProgress & EnterpriseBindingNavigator");
        var navPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };

        // 1. EnterpriseBreadcrumb
        _breadcrumbModel.Items.AddRange(new[]
        {
            new BreadcrumbItem { Text = "Enterprise Root" },
            new BreadcrumbItem { Text = "North America Division" },
            new BreadcrumbItem { Text = "Cloud Infrastructure" },
            new BreadcrumbItem { Text = "Cluster-US-East-01" }
        });

        var lblBcHeader = new Label { Text = "RadBreadcrumb Hierarchy Bar:", Location = new Point(14, 12), AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
        var lblBcRender = new Label
        {
            Text = string.Join($" {_breadcrumbModel.Separator} ", _breadcrumbModel.Items.Select(i => i.Text)),
            Location = new Point(230, 12),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 102, 180)
        };
        var btnPushBc = new Button { Text = "+ Drill Down Node", Location = new Point(14, 40), Size = new Size(140, 28), FlatStyle = FlatStyle.Flat };
        var btnPopBc = new Button { Text = "↑ Pop Node", Location = new Point(162, 40), Size = new Size(110, 28), FlatStyle = FlatStyle.Flat };

        btnPushBc.Click += (_, _) =>
        {
            var next = $"Node-L{_breadcrumbModel.Items.Count + 1}";
            _breadcrumbModel.Items.Add(new BreadcrumbItem { Text = next });
            lblBcRender.Text = string.Join($" {_breadcrumbModel.Separator} ", _breadcrumbModel.Items.Select(i => i.Text));
            _baseView.LogEvent("EnterpriseBreadcrumb.Push", lblBcRender.Text);
        };
        btnPopBc.Click += (_, _) =>
        {
            if (_breadcrumbModel.Items.Count > 1)
            {
                _breadcrumbModel.Items.RemoveAt(_breadcrumbModel.Items.Count - 1);
                lblBcRender.Text = string.Join($" {_breadcrumbModel.Separator} ", _breadcrumbModel.Items.Select(i => i.Text));
                _baseView.LogEvent("EnterpriseBreadcrumb.Pop", lblBcRender.Text);
            }
        };

        // 2. EnterpriseStepProgressBar
        _stepModel.Steps.AddRange(new[]
        {
            new StepItem { Title = "1. Code Commit", State = StepState.Completed },
            new StepItem { Title = "2. Static Analysis", State = StepState.Completed },
            new StepItem { Title = "3. Security Audit", State = StepState.InProgress },
            new StepItem { Title = "4. Canary Rollout", State = StepState.NotStarted },
            new StepItem { Title = "5. Global GA", State = StepState.NotStarted }
        });
        _stepModel.SelectedIndex = 2;

        var lblStepHeader = new Label { Text = "RadStepProgressBar Pipeline:", Location = new Point(14, 86), AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
        var stepCanvas = new Panel { Location = new Point(14, 112), Size = new Size(760, 64), BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White };
        stepCanvas.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            int count = _stepModel.Steps.Count;
            int stepW = Math.Max(110, stepCanvas.Width / count);
            using var completeBrush = new SolidBrush(Color.FromArgb(16, 124, 65));
            using var activeBrush = new SolidBrush(Color.FromArgb(0, 120, 212));
            using var pendingBrush = new SolidBrush(Color.FromArgb(190, 195, 205));
            using var textBrush = new SolidBrush(Color.FromArgb(40, 44, 52));
            using var whiteBrush = new SolidBrush(Color.White);
            using var linePen = new Pen(Color.FromArgb(200, 205, 212), 3f);
            using var boldFont = new Font("Segoe UI", 8.5f, FontStyle.Bold);

            for (int i = 0; i < count; i++)
            {
                int cx = 55 + i * stepW;
                int cy = 22;
                if (i < count - 1)
                {
                    g.DrawLine(linePen, cx + 14, cy, cx + stepW - 14, cy);
                }
                var brush = i < _stepModel.SelectedIndex ? completeBrush : (i == _stepModel.SelectedIndex ? activeBrush : pendingBrush);
                g.FillEllipse(brush, cx - 13, cy - 13, 26, 26);
                g.DrawString((i + 1).ToString(), boldFont, whiteBrush, cx - 5, cy - 7);
                g.DrawString(_stepModel.Steps[i].Title, boldFont, textBrush, cx - 42, cy + 16);
            }
        };

        var btnPrevStep = new Button { Text = "◀ Previous Stage", Location = new Point(14, 184), Size = new Size(135, 28), FlatStyle = FlatStyle.Flat };
        var btnNextStep = new Button { Text = "Next Stage ▶", Location = new Point(156, 184), Size = new Size(135, 28), FlatStyle = FlatStyle.Flat };
        btnPrevStep.Click += (_, _) =>
        {
            _stepModel.SelectedIndex = Math.Max(0, _stepModel.SelectedIndex - 1);
            stepCanvas.Invalidate();
            _baseView.LogEvent("EnterpriseStepProgressBar.SelectedIndex", _stepModel.Steps[_stepModel.SelectedIndex].Title);
        };
        btnNextStep.Click += (_, _) =>
        {
            _stepModel.SelectedIndex = Math.Min(_stepModel.Steps.Count - 1, _stepModel.SelectedIndex + 1);
            stepCanvas.Invalidate();
            _baseView.LogEvent("EnterpriseStepProgressBar.SelectedIndex", _stepModel.Steps[_stepModel.SelectedIndex].Title);
        };

        // 3. EnterpriseBindingNavigator
        _bindingNav.Items.AddRange(CachedValues1);
        _bindingNav.NewItemFactory = () => $"Customer #{1001 + _bindingNav.Items.Count} — New Enterprise Account ($500K ARR)";

        var lblBindHeader = new Label { Text = "RadBindingNavigator Control:", Location = new Point(14, 228), AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
        var lblBindCurrent = new Label
        {
            Text = $"[{_bindingNav.PositionText}]  {_bindingNav.CurrentItem}",
            Location = new Point(230, 228),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(16, 124, 65)
        };

        void SyncBindingLabel(string action)
        {
            lblBindCurrent.Text = $"[{_bindingNav.PositionText}]  {_bindingNav.CurrentItem ?? "(Empty)"}";
            _baseView.LogEvent($"EnterpriseBindingNavigator.{action}", lblBindCurrent.Text);
        }

        var btnFirst = new Button { Text = "|◀ First", Location = new Point(14, 256), Size = new Size(76, 28), FlatStyle = FlatStyle.Flat };
        var btnPrev = new Button { Text = "◀ Prev", Location = new Point(96, 256), Size = new Size(76, 28), FlatStyle = FlatStyle.Flat };
        var btnNext = new Button { Text = "Next ▶", Location = new Point(178, 256), Size = new Size(76, 28), FlatStyle = FlatStyle.Flat };
        var btnLast = new Button { Text = "Last ▶|", Location = new Point(260, 256), Size = new Size(76, 28), FlatStyle = FlatStyle.Flat };
        var btnAdd = new Button { Text = "+ Add Record", Location = new Point(342, 256), Size = new Size(105, 28), FlatStyle = FlatStyle.Flat };
        var btnDel = new Button { Text = "✕ Delete", Location = new Point(453, 256), Size = new Size(85, 28), FlatStyle = FlatStyle.Flat };

        btnFirst.Click += (_, _) => { _bindingNav.MoveFirst(); SyncBindingLabel("MoveFirst"); };
        btnPrev.Click += (_, _) => { _bindingNav.MovePrevious(); SyncBindingLabel("MovePrevious"); };
        btnNext.Click += (_, _) => { _bindingNav.MoveNext(); SyncBindingLabel("MoveNext"); };
        btnLast.Click += (_, _) => { _bindingNav.MoveLast(); SyncBindingLabel("MoveLast"); };
        btnAdd.Click += (_, _) => { _bindingNav.AddNew(); SyncBindingLabel("AddNew"); };
        btnDel.Click += (_, _) => { _bindingNav.DeleteCurrent(); SyncBindingLabel("DeleteCurrent"); };

        navPanel.Controls.AddRange(new Control[]
        {
            lblBcHeader, lblBcRender, btnPushBc, btnPopBc,
            lblStepHeader, stepCanvas, btnPrevStep, btnNextStep,
            lblBindHeader, lblBindCurrent, btnFirst, btnPrev, btnNext, btnLast, btnAdd, btnDel
        });
        tabNav.Controls.Add(navPanel);

        // ====================================================================
        // Tab 2: 3D EnterpriseCarousel & EnterprisePanorama Live Tile Hub
        // ====================================================================
        var tabCarouselPanorama = new TabPage("🎠 3D EnterpriseCarousel & EnterprisePanorama Live Tiles");
        var cpSplit = new System.Windows.Forms.SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 190
        };

        _carouselControl.Dock = DockStyle.Fill ;
        _carouselControl.SelectedItemChanged += item => _baseView.LogEvent("EnterpriseCarousel.SelectedItem", item);
        cpSplit.Panel1.Controls.Add(_carouselControl);

        // EnterprisePanorama Live Tiles
        _panoramaModel.Tiles.AddRange(new[]
        {
            new EnterprisePanorama.LiveTileItem { Group = "Core Operations", Title = "1M-Row DataGrid", BadgeText = "60 FPS", Frames = { "1,000,000 Rows Active", "Sub-ms Query AST", "Excel/PDF Export Ready" } },
            new EnterprisePanorama.LiveTileItem { Group = "Core Operations", Title = "AI Copilot Studio", BadgeText = "ONLINE", Frames = { "NL -> Filter AST", "Executive BI Summary", "Anomaly Detection" } },
            new EnterprisePanorama.LiveTileItem { Group = "Telemetry", Title = "Global GIS Fleet", BadgeText = "428 GPS", Frames = { "428 Active Vehicles", "99.98% SLA Uptime", "Zero Alerts" } },
            new EnterprisePanorama.LiveTileItem { Group = "Compliance", Title = "WCAG 2.2 AA Audit", BadgeText = "100%", Frames = { "Contrast >= 4.5:1", "UIA Live Regions", "RTL Mirroring Verified" } }
        });

        var panoramaPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(245, 247, 250) };
        panoramaPanel.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var headerFont = new Font("Segoe UI", 10f, FontStyle.Bold);
            using var titleFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            using var bodyFont = new Font("Segoe UI", 8.75f);
            using var whiteBrush = new SolidBrush(Color.White);
            using var fgBrush = new SolidBrush(Color.FromArgb(40, 44, 52));

            g.DrawString("RadPanorama Live Tiles (Auto-Cycling Frames Every 2s — Click to Cycle Now):", headerFont, fgBrush, 14, 8);
            Color[] tileColors = { Color.FromArgb(0, 120, 212), Color.FromArgb(16, 124, 65), Color.FromArgb(135, 100, 184), Color.FromArgb(216, 59, 1) };

            for (int i = 0; i < _panoramaModel.Tiles.Count; i++)
            {
                var t = _panoramaModel.Tiles[i];
                var rect = new Rectangle(14 + i * 192, 34, 180, 96);
                using var tb = new SolidBrush(tileColors[i % tileColors.Length]);
                g.FillRectangle(tb, rect);
                g.DrawString($"[{t.Group}]", bodyFont, whiteBrush, rect.X + 8, rect.Y + 8);
                g.DrawString(t.BadgeText, titleFont, whiteBrush, rect.Right - 58, rect.Y + 8);
                g.DrawString(t.Title, titleFont, whiteBrush, rect.X + 8, rect.Y + 32);
                g.DrawString(t.CurrentFrameText, bodyFont, whiteBrush, new RectangleF(rect.X + 8, rect.Y + 56, rect.Width - 16, 34));
            }
        };
        panoramaPanel.MouseClick += (_, _) =>
        {
            _panoramaModel.CycleAllTileFrames();
            panoramaPanel.Invalidate();
            _baseView.LogEvent("EnterprisePanorama.CycleAllTileFrames", "Advanced live tile frames");
        };

        cpSplit.Panel2.Controls.Add(panoramaPanel);
        tabCarouselPanorama.Controls.Add(cpSplit);

        // ====================================================================
        // Tab 3: EnterpriseWizardControl, EnterpriseDataLayout & EnterpriseCollapsiblePanel
        // ====================================================================
        var tabLayoutWizard = new TabPage("🧙 EnterpriseWizardControl, EnterpriseDataLayout & EnterpriseCollapsiblePanel");
        var lwPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };

        _wizardModel.Pages.AddRange(new[]
        {
            new EnterpriseWizardControl.WizardPage { Title = "Step 1: Welcome to EnterpriseUI Setup Wizard", HeaderDescription = "Configure target platform, theme tokens, and telemetry options.", IsWelcomePage = true },
            new EnterpriseWizardControl.WizardPage { Title = "Step 2: Data Virtualization & Caching", HeaderDescription = "Enable O(1) windowed virtualization and compiled expression trees." },
            new EnterpriseWizardControl.WizardPage { Title = "Step 3: Security, RBAC & WCAG 2.2 Compliance", HeaderDescription = "Configure role-based permission guards and screen reader live regions." },
            new EnterpriseWizardControl.WizardPage { Title = "Step 4: Deployment Ready (Completion Page)", HeaderDescription = "All configuration checks passed. Click Finish to commit.", IsCompletionPage = true }
        });

        var wizardBox = new GroupBox
        {
            Text = "RadWizard Interactive Multi-Step Assistant",
            Location = new Point(14, 10),
            Size = new Size(760, 145),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };
        var lblWizTitle = new Label { Text = _wizardModel.SelectedPage?.Title, Location = new Point(16, 28), AutoSize = true, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = Color.FromArgb(0, 102, 180) };
        var lblWizDesc = new Label { Text = _wizardModel.SelectedPage?.HeaderDescription, Location = new Point(16, 56), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Regular) };
        var btnWizPrev = new Button { Text = "◀ Back", Location = new Point(16, 96), Size = new Size(95, 30), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f) };
        var btnWizNext = new Button { Text = "Next ▶", Location = new Point(118, 96), Size = new Size(95, 30), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f) };
        var btnWizFinish = new Button { Text = "✓ Finish", Location = new Point(220, 96), Size = new Size(95, 30), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f) };

        void SyncWizardUI()
        {
            lblWizTitle.Text = _wizardModel.SelectedPage?.Title;
            lblWizDesc.Text = _wizardModel.SelectedPage?.HeaderDescription;
            btnWizPrev.Enabled = _wizardModel.CanMovePrevious;
            btnWizNext.Enabled = _wizardModel.CanMoveNext;
        }
        btnWizPrev.Click += (_, _) => { _wizardModel.MovePrevious(); SyncWizardUI(); _baseView.LogEvent("EnterpriseWizardControl.MovePrevious", lblWizTitle.Text ?? ""); };
        btnWizNext.Click += (_, _) => { _wizardModel.MoveNext(); SyncWizardUI(); _baseView.LogEvent("EnterpriseWizardControl.MoveNext", lblWizTitle.Text ?? ""); };
        btnWizFinish.Click += (_, _) =>
        {
            _wizardModel.Finish();
            lblWizDesc.Text = "STATUS: Wizard Completed Successfully!";
            _baseView.LogEvent("EnterpriseWizardControl.Finish", "Completed");
        };
        SyncWizardUI();
        wizardBox.Controls.AddRange(new Control[] { lblWizTitle, lblWizDesc, btnWizPrev, btnWizNext, btnWizFinish });

        // Auto-generated EnterpriseDataLayout inspection
        var dataLayout = new EnterpriseDataLayout<Appointment>();
        var lblDlHeader = new Label
        {
            Text = $"RadDataLayout<Appointment> Auto-Generated Form Schema ({dataLayout.Fields.Count} reflected fields):",
            Location = new Point(14, 168),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };
        var lvDataLayout = new ListView
        {
            Location = new Point(14, 194),
            Size = new Size(760, 140),
            View = View.Details,
            FullRowSelect = true,
            GridLines = true
        };
        lvDataLayout.Columns.Add("Property Name", 160);
        lvDataLayout.Columns.Add("Display Label", 180);
        lvDataLayout.Columns.Add("CLR Type", 160);
        lvDataLayout.Columns.Add("Auto-Selected Editor Control", 220);
        foreach (var f in dataLayout.Fields)
        {
            lvDataLayout.Items.Add(new ListViewItem(new[] { f.PropertyName, f.Label, f.PropertyType.Name, f.EditorKind }));
        }

        lwPanel.Controls.AddRange(new Control[] { wizardBox, lblDlHeader, lvDataLayout });
        tabLayoutWizard.Controls.Add(lwPanel);

        // ====================================================================
        // Tab 4: Notifications, EnterpriseDesktopAlert, EnterpriseTaskDialog & EnterpriseWaitingBar
        // ====================================================================
        var tabAlerts = new TabPage("🔔 EnterpriseDesktopAlert, EnterpriseTaskDialog & EnterpriseWaitingBar");
        var alertPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };

        _waitingControl.Location = new Point(14, 14);

        _waitingControl.Size = new Size(740, 90) ;
        _waitingControl.RatingChanged += r => _baseView.LogEvent("EnterpriseRatingControl.Value", $"{r} stars");

        var btnDesktopAlert = new Button
        {
            Text = "🔔 Trigger RadDesktopAlert Toast",
            Location = new Point(14, 120),
            Size = new Size(230, 34),
            FlatStyle = FlatStyle.Flat
        };
        var btnTaskDialog = new Button
        {
            Text = "📋 Launch RadTaskDialog Command Links",
            Location = new Point(256, 120),
            Size = new Size(260, 34),
            FlatStyle = FlatStyle.Flat
        };
        var lblAlertStatus = new Label
        {
            Text = "Notification History: Ready",
            Location = new Point(14, 168),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };

        btnDesktopAlert.Click += (_, _) =>
        {
            var alert = new EnterpriseDesktopAlert
            {
                CaptionText = "EnterpriseUI SLA Monitor",
                ContentText = $"Cluster latency nominal at {DateTime.Now:HH:mm:ss}. Zero anomalies detected."
            };
            alert.ShowAlert();
            lblAlertStatus.Text = $"Dispatched EnterpriseDesktopAlert #{ToastService.Instance.NotificationHistory.Count}: {alert.ContentText}";
            _baseView.LogEvent("EnterpriseDesktopAlert.ShowAlert", alert.ContentText);
        };

        btnTaskDialog.Click += (_, _) =>
        {
            var td = new EnterpriseUI.Layout.EnterpriseTaskDialog
            {
                WindowTitle = "EnterpriseUI Deployment EnterpriseTaskDialog",
                MainInstruction = "Choose production release channel",
                Content = "Select how the compiled WinForms bundle should be staged."
            };
            td.CommandLinks.Add(new EnterpriseUI.Layout.EnterpriseTaskDialog.CommandLinkOption("canary", "Canary Ring (5% Traffic)", "Deploy with automated rollback telemetry"));
            td.CommandLinks.Add(new EnterpriseUI.Layout.EnterpriseTaskDialog.CommandLinkOption("global", "Global Production GA", "Immediate rollout across all regions"));
            td.SelectedCommandId = "canary";
            lblAlertStatus.Text = $"EnterpriseTaskDialog executed: Selected CommandLink '{td.SelectedCommandId}' ({td.CommandLinks[0].Title})";
            _baseView.LogEvent("EnterpriseTaskDialog.Execute", td.SelectedCommandId);
            MessageBox.Show($"{td.MainInstruction}\n\n1. {td.CommandLinks[0].Title} — {td.CommandLinks[0].Description}\n2. {td.CommandLinks[1].Title} — {td.CommandLinks[1].Description}", td.WindowTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        alertPanel.Controls.AddRange(new Control[] { _waitingControl, btnDesktopAlert, btnTaskDialog, lblAlertStatus });
        tabAlerts.Controls.Add(alertPanel);

        tabs.TabPages.AddRange(new[] { tabNav, tabCarouselPanorama, tabLayoutWizard, tabAlerts });
        _baseView.DemoContainer.Controls.Add(tabs);

        _liveTimer.Interval = 140 ;
        int tickCounter = 0;
        _liveTimer.Tick += (_, _) =>
        {
            _waitingControl.StepMarquee();
            if (++tickCounter % 15 == 0)
            {
                _panoramaModel.CycleAllTileFrames();
                panoramaPanel.Invalidate();
            }
        };
        _liveTimer.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _liveTimer?.Stop();
            _liveTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
}
