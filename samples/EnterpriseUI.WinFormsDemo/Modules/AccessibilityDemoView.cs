using System;
using System.Drawing;
using System.Windows.Forms;
using EnterpriseUI.Accessibility;
using EnterpriseUI.WinFormsDemo.Infrastructure;

namespace EnterpriseUI.WinFormsDemo.Modules;

public class AccessibilityDemoView : UserControl
{

    private static readonly string[] CachedValues5 = new[] { "4.1.3 Status Messages", "Dynamic notifications announced via IAccessibilityAnnouncer live regions", "COMPLIANT" };

    private static readonly string[] CachedValues4 = new[] { "4.1.2 Name, Role, Value", "AriaRole, AriaLabel, and expanded states exposed to accessibility tree", "COMPLIANT" };

    private static readonly string[] CachedValues3 = new[] { "2.4.7 Focus Visible", "High-visibility focus halo rendered on active controls", "COMPLIANT" };

    private static readonly string[] CachedValues2 = new[] { "2.1.1 Keyboard Accessible", "All grid navigation, editing and commands reachable via Tab/Arrows", "COMPLIANT" };

    private static readonly string[] CachedValues1 = new[] { "1.4.3 Contrast (Minimum)", "Text contrast ratio >= 4.5:1 against surface background", "COMPLIANT" };
    private readonly BaseDemoView _baseView=new BaseDemoView();
    private readonly ListBox _announcementLog=new ListBox();

    public AccessibilityDemoView(DemoItem metadata)
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 9f);

        _baseView.Dock = DockStyle.Fill ;
        _baseView.BindDemoMetadata(metadata);
        Controls.Add(_baseView);

        var mainPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };

        var lblHeader = new Label
        {
            Text = "Enterprise Accessibility Center (WCAG 2.2 AA & Section 508 Verification)",
            Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 32
        };

        var actionsPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            Padding = new Padding(4)
        };

        var btnAnnouncePolite = new Button
        {
            Text = "Trigger Polite Announcement",
            Location = new Point(4, 8),
            Size = new Size(200, 28),
            FlatStyle = FlatStyle.Flat
        };
        btnAnnouncePolite.Click += (_, _) =>
        {
            AccessibilityManager.Instance.Announce("DataGrid filtered. 14 of 100 records match criteria.", LiveRegionPoliteness.Polite);
        };

        var btnAnnounceAssertive = new Button
        {
            Text = "Trigger Assertive Warning",
            Location = new Point(212, 8),
            Size = new Size(200, 28),
            FlatStyle = FlatStyle.Flat
        };
        btnAnnounceAssertive.Click += (_, _) =>
        {
            AccessibilityManager.Instance.Announce("CRITICAL ALERT: Connection to primary cluster failed. Retrying in 5 seconds.", LiveRegionPoliteness.Assertive);
        };

        actionsPanel.Controls.AddRange(new Control[] { btnAnnouncePolite, btnAnnounceAssertive });

        // Audit Checklist
        var auditList = new ListView
        {
            Dock = DockStyle.Top,
            Height = 180,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true
        };
        auditList.Columns.Add("WCAG 2.2 Criterion", 240);
        auditList.Columns.Add("Requirement Description", 320);
        auditList.Columns.Add("Audit Result", 100);

        auditList.Items.Add(new ListViewItem(CachedValues1));
        auditList.Items.Add(new ListViewItem(CachedValues2));
        auditList.Items.Add(new ListViewItem(CachedValues3));
        auditList.Items.Add(new ListViewItem(CachedValues4));
        auditList.Items.Add(new ListViewItem(CachedValues5));

        // Live Log of announcements
        var logLabel = new Label
        {
            Text = "Live Screen Reader & Assistive Technology Stream:",
            Dock = DockStyle.Top,
            Height = 24,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };

        _announcementLog.            Dock = DockStyle.Fill;

        _announcementLog.            Font = new Font("Consolas", 9f)
;

        AccessibilityManager.Instance.AnnouncementRequested += (msg, politeness) =>
        {
            _announcementLog.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] [{politeness.ToString().ToUpperInvariant()}] {msg}");
        };

        mainPanel.Controls.Add(_announcementLog);
        mainPanel.Controls.Add(logLabel);
        mainPanel.Controls.Add(auditList);
        mainPanel.Controls.Add(actionsPanel);
        mainPanel.Controls.Add(lblHeader);

        _baseView.DemoContainer.Controls.Add(mainPanel);
    }
}

