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

    private static readonly object[] CachedValues1 = new object[]
        {
            "UTC+0 (London / Enterprise HQ)",
            "UTC-5 (New York / Wall Street)",
            "UTC+1 (Zurich / Frankfurt)",
            "UTC+9 (Tokyo / Nikkei Desk)"
        };
    private readonly BaseDemoView _baseView=new BaseDemoView();
    private readonly TabControl _tabs=new TabControl();

    public CompleteControlCatalogDemoView(DemoItem metadata)
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 9f);

        _baseView.Dock = DockStyle.Fill ;
        _baseView.BindDemoMetadata(metadata);
        Controls.Add(_baseView);

        _tabs.            Dock = DockStyle.Fill;

        _tabs.            Font = new Font("Segoe UI", 9f);

        _tabs.            Padding = new Point(8, 4)
;

        _tabs.TabPages.Add(metadata.Id switch
        {
            "component-catalog" => CreateCatalogPage(),
            "slide-rotator-pips-media-suite" => CreateSlidesPage(),
            "clock-callout-rangeselector-suite" => CreateClockPage(),
            "navigation-forms-overlays-suite" => CreateFormsPage(),
            _ => throw new ArgumentException("Unknown catalog suite.", nameof(metadata))
        });
        _baseView.DemoContainer.Controls.Add(_tabs);
    }

    private TabPage CreateCatalogPage()
    {
        var tabCatalog = new TabPage("Component Catalog");
        var catalogTop = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 42,
            Padding = new Padding(8, 6, 8, 6),
            BackColor = Color.FromArgb(245, 247, 250),
            WrapContents = false
        };

        var lblFilter = new Label { Text = "Search components:", AutoSize = true, Padding = new Padding(0, 5, 4, 0), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        var txtSearchCatalog = new System.Windows.Forms.TextBox { Width = 220, PlaceholderText = "Grid, editor, clock..." };
        var cmbCatFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 210 };
        cmbCatFilter.Items.Add("All Categories");
        foreach (var cat in EnterpriseControlRegistry.AllControls.Select(c => c.Category).Distinct())
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
            Text = $"{EnterpriseControlRegistry.AllControls.Count} catalog entries",
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
        lvCatalog.Columns.Add("Component", 200);
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

            var filtered = EnterpriseControlRegistry.AllControls.Where(c =>
                (cat == null || string.Equals(c.Category, cat, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrEmpty(q) ||
                 c.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                 c.EnterpriseUIName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                 c.KeyFeatures.Contains(q, StringComparison.OrdinalIgnoreCase))).ToList();

            foreach (var entry in filtered)
            {
                var item = new ListViewItem(entry.Name) { Tag = entry };
                item.SubItems.Add(entry.EnterpriseUIName);
                item.SubItems.Add(entry.Category);
                item.SubItems.Add(entry.KeyFeatures);
                item.SubItems.Add(entry.DocumentationPath);
                lvCatalog.Items.Add(item);
            }
            lvCatalog.EndUpdate();
            lblCatalogCount.Text = $"Showing {filtered.Count} of {EnterpriseControlRegistry.AllControls.Count} catalog entries";
        }

        txtSearchCatalog.TextChanged += (_, _) => PopulateCatalogList();
        cmbCatFilter.SelectedIndexChanged += (_, _) => PopulateCatalogList();
        btnLaunchSelected.Click += (_, _) =>
        {
            if (lvCatalog.SelectedItems.Count > 0 && lvCatalog.SelectedItems[0].Tag is ControlCatalogEntry entry)
            {
                _baseView.LogEvent("Catalog.LaunchControl", $"{entry.Name} -> {entry.EnterpriseUIName}");
                var targetDemo = DemoMetadataRegistry.GetCatalogDemo(entry);
                DemoNavigationManager.Instance.NavigateTo(targetDemo);
            }
        };
        lvCatalog.DoubleClick += (_, _) => btnLaunchSelected.PerformClick();

        PopulateCatalogList();
        catalogTop.Controls.AddRange(new Control[] { lblFilter, txtSearchCatalog, cmbCatFilter, btnLaunchSelected, lblCatalogCount });
        tabCatalog.Controls.Add(lvCatalog);
        tabCatalog.Controls.Add(catalogTop);
        return tabCatalog;
    }

    private TabPage CreateSlidesPage()
    {
        var tabSlideMedia = new TabPage("SlideView, Rotator & PipsPager");
        var mediaPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), AutoScroll = true };
        var _slideRotatorPips = new EnterpriseSlideRotatorPipsControl { Dock = DockStyle.Fill };
        _slideRotatorPips.SlideChanged += (idx, title) =>
        {
            _baseView.LogEvent("SlideView.SlideChanged", $"Slide #{idx + 1}: {title}");
        };

        mediaPanel.Controls.Add(_slideRotatorPips);
        tabSlideMedia.Controls.Add(mediaPanel);
        return tabSlideMedia;
    }

    private TabPage CreateClockPage()
    {
        var _clock = new EnterpriseClockControl();
        var _rangeInfo = new Label
        {
            Text = "Drag the range selector thumbs to change the live window.",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            AutoSize = false
        };
        var _rangeSelector = new EnterpriseRangeSelectorControl();
        var tabClockCallout = new TabPage("🕒 Clock & RangeSelector");
        var clockPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };

        _clock.Location = new Point(16, 14);

        _clock.Size = new Size(240, 260) ;

        var cmbTz = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(280, 18), Width = 260 };
        cmbTz.Items.AddRange(CachedValues1);
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

        _rangeInfo.            Location = new Point(280, 56);

        _rangeInfo.            Size = new Size(560, 105)
;

        _rangeSelector.            Location = new Point(280, 174);

        _rangeSelector.            Size = new Size(560, 100)
;
        _rangeSelector.RangeChanged += (s, e) =>
        {
            _rangeInfo.Text = $"Live range window: {s:F1}% to {e:F1}%";
            _baseView.LogEvent("RangeSelector.RangeChanged", $"{s:F1}% .. {e:F1}%");
        };

        clockPanel.Controls.AddRange(new Control[] { _clock, cmbTz, _rangeInfo, _rangeSelector });
        tabClockCallout.Controls.Add(clockPanel);
        return tabClockCallout;
    }

    private TabPage CreateFormsPage()
    {
        var tabFormsOverlays = new TabPage("🪟 NavigationView, Forms, Overlays & Tools");
        var formsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };

        var grpNavModes = new GroupBox
        {
            Text = "EnterpriseNavigationView & EnterpriseOfficeNavigationBar",
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
            Text = "EnterpriseSplashScreen, EnterpriseOverlay, EnterpriseFlyout & EnterpriseToastNotification",
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
            lblOverlayState.Text = "EnterpriseOverlay Active: Host viewport dimmed at 45% opacity with rotating EnterpriseWaitingBar";
            _baseView.LogEvent("EnterpriseOverlay.Show", "Dimmer + Busy Indicator active");
        };
        btnTriggerFlyout.Click += (_, _) =>
        {
            lblOverlayState.Text = "EnterpriseFlyout Active: Interactive modal slide-in drawer opened over workspace";
            _baseView.LogEvent("EnterpriseFlyout.Show", "Flyout drawer opened");
        };
        btnTriggerToast.Click += (_, _) =>
        {
            lblOverlayState.Text = "EnterpriseToastNotification & EnterpriseNotifyIcon: Dispatched toast + Tray Icon Badge = 3";
            _baseView.LogEvent("ToastNotificationManager.Show", "Toast + NotifyIcon Badge=3");
        };
        btnTaskbarProgress.Click += (_, _) =>
        {
            lblOverlayState.Text = "EnterpriseTaskbarButton: Windows Taskbar Progress State = Normal (85% Complete)";
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
        return tabFormsOverlays;
    }
}

