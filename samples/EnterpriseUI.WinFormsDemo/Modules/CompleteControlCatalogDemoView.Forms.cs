using System;
using System.Drawing;
using System.Windows.Forms;
using EnterpriseUI.Core.Mvvm;
using EnterpriseUI.Editors.Interactivity;
using EnterpriseUI.Layout;
using EnterpriseUI.Navigation;
using EnterpriseUI.Notifications;
using EnterpriseUI.WinForms;

namespace EnterpriseUI.WinFormsDemo.Modules;

public partial class CompleteControlCatalogDemoView
{
    private EnterpriseToastNotification? _toast;
    private EnterpriseTaskbarButton? _taskbar;
    private EnterpriseStatusStrip? _formsStatus;

    private static GroupBox FormsGroup(string title, params (string Text, Action Click)[] actions)
    {
        var group = new GroupBox { Text = title, Size = new Size(250, 34 + actions.Length * 40), Font = new Font("Segoe UI", 9f, FontStyle.Bold), Margin = new Padding(0, 0, 12, 12) };
        var y = 26;
        foreach (var (text, click) in actions)
        {
            var button = new EnterpriseButtonControl { Text = text, Location = new Point(12, y), Size = new Size(226, 32), Variant = EnterpriseButtonVariant.Outline };
            button.Click += (_, _) => click();
            group.Controls.Add(button);
            y += 40;
        }

        return group;
    }

    private void Status(string eventName, string text, string log)
    {
        _formsStatus?.SetText("state", text);
        _baseView.LogEvent(eventName, log);
    }

    private TabPage CreateFormsPage()
    {
        var tab = new TabPage("Forms, Dialogs & Overlays");
        _formsStatus = new EnterpriseStatusStrip();
        _formsStatus.AddPanel("state", "Ready", spring: true);
        _formsStatus.AddPanel("progress", string.Empty, 140, kind: EnterpriseStatusPanelKind.Progress);
        var docs = _formsStatus.AddPanel("docs", "Docs", 64, kind: EnterpriseStatusPanelKind.Link);
        docs.ToolTip = "docs/Controls/FormsAndDialogs.md";
        _formsStatus.PanelClick += (_, e) => _baseView.LogEvent("StatusStrip.PanelClick", e.Panel.Name);

        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };
        flow.Controls.Add(FormsGroup("Framed forms",
            ("EnterpriseForm", ShowFramedForm), ("EnterpriseRibbonForm", ShowRibbonForm), ("EnterpriseTabbedForm", ShowTabbedForm),
            ("EnterpriseToolbarForm", ShowToolbarForm), ("EnterpriseShapedForm", ShowShapedForm)));
        flow.Controls.Add(FormsGroup("Dialogs",
            ("EnterpriseAboutBox", () => EnterpriseAboutBox.ShowAbout(FindForm())), ("EnterpriseMarkupDialog", ShowMarkupDialog),
            ("EnterpriseTaskDialogForm", ShowTaskDialog), ("EnterpriseMessageBox", ShowMessageBox)));
        flow.Controls.Add(FormsGroup("Alerts and shell",
            ("EnterpriseDesktopAlertWindow", ShowDesktopAlert), ("EnterpriseToastNotification", ShowToast),
            ("EnterpriseSplashScreen", ShowSplash), ("EnterpriseTaskbarButton progress", ShowTaskbarProgress)));
        flow.Controls.Add(FormsGroup("Overlays",
            ("EnterpriseOverlay (3 seconds)", ShowOverlay), ("EnterpriseFlyout (right edge)", () => ShowFlyout(EnterpriseFlyoutEdge.Right)),
            ("EnterpriseFlyout (bottom edge)", () => ShowFlyout(EnterpriseFlyoutEdge.Bottom))));
        flow.Controls.Add(FormsGroup("Developer tools",
            ("EnterpriseControlSpy", ShowControlSpy)));

        tab.Controls.Add(flow);
        tab.Controls.Add(_formsStatus);
        return tab;
    }

    private void ShowFramedForm()
    {
        var form = new EnterpriseForm { Text = "EnterpriseForm", ClientSize = new Size(560, 320), StartPosition = FormStartPosition.CenterParent };
        form.TitleBar.AddButton("theme", "◐", "Custom title bar button");
        form.TitleBar.ButtonClick += (_, e) => Status("EnterpriseForm.ButtonClick", e.Button.Id, "Title bar button " + e.Button.Id);
        form.Controls.Add(new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Text = "Custom caption, sizing frame, snap and maximize behavior, DPI scaling and theme sync." });
        form.FormClosed += (_, _) => form.Dispose();
        form.Show(FindForm());
        Status("EnterpriseForm.Show", "Opened", "Framed form opened");
    }

    private void ShowRibbonForm()
    {
        var form = new EnterpriseRibbonForm { Text = "Editor", ClientSize = new Size(900, 420), StartPosition = FormStartPosition.CenterParent };
        var home = new RibbonTab { Title = "Home" };
        var clipboard = new RibbonGroup { Title = "Clipboard" };
        clipboard.Items.Add(new RibbonItem { Text = "Paste", Icon = "📋", Command = new RelayCommand(() => Status("Ribbon.Paste", "Paste", "Paste command ran")) });
        clipboard.Items.Add(new RibbonItem { Text = "Cut", Icon = "✂", IsLarge = false });
        clipboard.Items.Add(new RibbonItem { Text = "Copy", Icon = "⧉", IsLarge = false });
        home.Groups.Add(clipboard);
        var picture = new RibbonTab { Title = "Picture Tools", ContextualGroup = "Picture", ContextualColor = "#C0504D" };
        picture.Groups.Add(new RibbonGroup { Title = "Adjust", Items = { new RibbonItem { Text = "Crop", Icon = "⛶" } } });
        form.Ribbon.Tabs.Add(home);
        form.Ribbon.Tabs.Add(new RibbonTab { Title = "View", Groups = { new RibbonGroup { Title = "Zoom", Items = { new RibbonItem { Text = "Zoom in", Icon = "＋" } } } } });
        form.Ribbon.Tabs.Add(picture);
        form.QuickAccessItems.Add(new RibbonItem { Text = "Save", Icon = "💾" });
        form.QuickAccessClick += (_, e) => Status("RibbonForm.QuickAccess", e.Item.Text, "Quick access " + e.Item.Text);
        form.Ribbon.RefreshRibbon();
        var toggle = new EnterpriseButtonControl { Text = "Show contextual tab", Location = new Point(20, 20), Size = new Size(190, 34) };
        toggle.Click += (_, _) => form.ShowContextualTab("Picture");
        form.Controls.Add(toggle);
        form.FormClosed += (_, _) => form.Dispose();
        form.Show(FindForm());
    }

    private void ShowTabbedForm()
    {
        var form = new EnterpriseTabbedForm { Text = "Documents", ClientSize = new Size(820, 420), StartPosition = FormStartPosition.CenterParent };
        for (var i = 1; i <= 3; i++) form.AddTab("Document " + i, new Label { Text = "Content of document " + i, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 14f) }, pinned: i == 1);
        var counter = 3;
        form.TabAdding += (_, e) => e.Tab = new EnterpriseTabbedFormTab("Document " + (++counter), new Label { Text = "New document " + counter, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 14f) });
        form.TabDetached += (_, e) => Status("TabbedForm.TabDetached", e.Tab.Title, "Detached " + e.Tab.Title);
        form.FormClosed += (_, _) => form.Dispose();
        form.Show(FindForm());
    }

    private void ShowToolbarForm()
    {
        var form = new EnterpriseToolbarForm { Text = "Mail", ShowSearch = true, ClientSize = new Size(860, 400), StartPosition = FormStartPosition.CenterParent };
        var fileMenu = new ContextMenuStrip();
        fileMenu.Items.Add("New message");
        fileMenu.Items.Add("Open...");
        form.AddMenu("file", "File", fileMenu);
        form.Toolbar.AddSeparator();
        form.AddToolbarItem("reply", "Reply", "↩", "Reply to sender");
        form.AddToolbarItem("delete", "Delete", "🗑", "Delete the message");
        form.AddToolbarItem("flag", "Flag", "⚑");
        form.ToolbarItemClick += (_, e) => Status("ToolbarForm.ItemClick", e.Item.Id, "Toolbar item " + e.Item.Id);
        form.SearchSubmitted += (_, text) => Status("ToolbarForm.Search", text, "Search for '" + text + "'");
        form.FormClosed += (_, _) => form.Dispose();
        form.Show(FindForm());
    }

    private void ShowShapedForm()
    {
        var form = new EnterpriseShapedForm { Shape = EnterpriseFormShape.Ellipse, ClientSize = new Size(360, 260), StartPosition = FormStartPosition.CenterParent };
        form.Controls.Add(new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Text = "Drag the empty surface to move.\nThe window is an ellipse." });
        var close = new EnterpriseButtonControl { Text = "Close", Size = new Size(90, 32), Location = new Point(135, 200) };
        close.Click += (_, _) => form.Close();
        form.Controls.Add(close);
        close.BringToFront();
        form.FormClosed += (_, _) => form.Dispose();
        form.Show(FindForm());
    }

    private void ShowMarkupDialog()
    {
        var result = EnterpriseMarkupDialog.ShowMarkup("**Version 2.0** is ready.\n\nRead the [release notes](https://example.org/notes) before updating.\n\nUse `Install now` to continue.", "Update available", MessageBoxButtons.YesNo, MessageBoxIcon.Information, owner: FindForm());
        Status("EnterpriseMarkupDialog.Result", result.ToString(), "Markup dialog returned " + result);
    }

    private void ShowTaskDialog()
    {
        var model = new EnterpriseTaskDialog { WindowTitle = "Deployment", MainInstruction = "Choose a rollout ring", Content = "Pick **how** the build is released. See the [runbook](https://example.org/runbook).", VerificationText = "Do not ask again for this workspace", ExpandedInformation = "Canary rolls back automatically when error rates rise." };
        model.CommandLinks.Add(new EnterpriseTaskDialog.CommandLinkOption("canary", "Canary ring (5% traffic)", "Deploy with automated rollback telemetry"));
        model.CommandLinks.Add(new EnterpriseTaskDialog.CommandLinkOption("global", "Global production", "Immediate rollout across all regions"));
        using var dialog = new EnterpriseTaskDialogForm(model) { DialogIcon = MessageBoxIcon.Question, CommonButtons = EnterpriseTaskDialogButtons.Cancel, FooterText = "Build 1.1.0" };
        var result = dialog.Execute(FindForm());
        Status("EnterpriseTaskDialogForm.Execute", model.SelectedCommandId ?? result.ToString(), $"Command '{model.SelectedCommandId}', verification {model.IsVerificationChecked}");
    }

    private void ShowMessageBox()
    {
        var result = EnterpriseMessageBox.ShowMessage("The export finished with warnings.", "Export", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, "3 rows were skipped:\nrow 12, row 40, row 77", FindForm());
        Status("EnterpriseMessageBox.Result", result.ToString(), "Message box returned " + result);
    }

    private void ShowDesktopAlert()
    {
        var alert = new EnterpriseDesktopAlertWindow(new EnterpriseDesktopAlert { CaptionText = "Build finished", ContentText = "All 120 tests passed on main.", AutoCloseDelaySeconds = 8 }) { Severity = NotificationSeverity.Success };
        alert.Actions.Add(new NotificationAction("report", "Open report", true));
        alert.ActionClicked += (_, e) => Status("DesktopAlert.Action", e.Action.Id, "Alert action " + e.Action.Id);
        alert.ShowAlert();
    }

    private void ShowToast()
    {
        _toast ??= new EnterpriseToastNotification();
        _toast.Show("Saved", "Your changes were stored.", NotificationSeverity.Success, TimeSpan.FromSeconds(5), new NotificationAction("undo", "Undo"));
        Status("ToastNotification.Show", "Toast", "Toast shown, unread " + _toast.Center.UnreadCount);
    }

    private void ShowSplash()
    {
        var splash = new EnterpriseSplashScreen { Title = "EnterpriseUI", Subtitle = "Loading workspace", FooterText = "Version 1.1.0", MinimumDuration = 600 };
        splash.ShowSplash();
        var step = 0;
        var timer = new System.Windows.Forms.Timer { Interval = 140 };
        timer.Tick += (_, _) =>
        {
            step += 8;
            splash.ReportProgress(step, step < 40 ? "Reading settings..." : step < 80 ? "Loading modules..." : "Starting...");
            if (step < 100) return;
            timer.Stop();
            timer.Dispose();
            splash.Close();
        };
        timer.Start();
    }

    private void ShowTaskbarProgress()
    {
        var form = FindForm();
        if (form == null) return;
        _taskbar ??= new EnterpriseTaskbarButton { Form = form };
        var value = 0;
        var timer = new System.Windows.Forms.Timer { Interval = 120 };
        timer.Tick += (_, _) =>
        {
            value += 5;
            _taskbar.SetProgress(value);
            _formsStatus?.SetProgress("progress", value);
            if (value < 100) return;
            timer.Stop();
            timer.Dispose();
            _taskbar.ProgressState = EnterpriseTaskbarProgressState.None;
            _formsStatus?.SetProgress("progress", 0);
        };
        timer.Start();
        _baseView.LogEvent("TaskbarButton.Progress", "0 to 100 percent");
    }

    private void ShowOverlay()
    {
        var host = FindForm();
        if (host == null) return;
        var overlay = new EnterpriseOverlay { Caption = "Loading data...", Indicator = EnterpriseOverlayIndicator.Ring };
        overlay.Show(host);
        var timer = new System.Windows.Forms.Timer { Interval = 3000 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            timer.Dispose();
            overlay.Hide();
            overlay.Dispose();
        };
        timer.Start();
        _baseView.LogEvent("EnterpriseOverlay.Show", "Host dimmed and blocked for 3 seconds");
    }

    private void ShowControlSpy()
    {
        var host = FindForm();
        if (host == null) return;
        var spy = new EnterpriseControlSpy();
        spy.Inspect(host);
        spy.FormClosed += (_, _) => spy.Dispose();
        spy.Show(host);
        _baseView.LogEvent("EnterpriseControlSpy.Inspect", host.Name.Length > 0 ? host.Name : host.GetType().Name);
    }

    private void ShowFlyout(EnterpriseFlyoutEdge edge)
    {
        var host = FindForm();
        if (host == null) return;
        var flyout = new EnterpriseFlyout { Text = "Filters", Edge = edge, FlyoutSize = edge == EnterpriseFlyoutEdge.Right ? 340 : 200 };
        flyout.Content.Controls.Add(new Label { Dock = DockStyle.Top, Height = 60, Padding = new Padding(16, 14, 16, 0), Text = "Content hosted in the flyout. Press Escape or click the dimmed area to close." });
        var apply = new EnterpriseButtonControl { Text = "Apply", Location = new Point(16, 80), Size = new Size(100, 34) };
        apply.Click += (_, _) => flyout.HideFlyout();
        flyout.Content.Controls.Add(apply);
        flyout.FlyoutClosed += (_, _) =>
        {
            _baseView.LogEvent("EnterpriseFlyout.Closed", edge.ToString());
            flyout.Dispose();
        };
        flyout.ShowFlyout(host);
        _baseView.LogEvent("EnterpriseFlyout.Show", edge + " edge, modal");
    }
}
