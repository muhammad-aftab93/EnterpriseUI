using System.Drawing.Drawing2D;
using EnterpriseUI.Core.Mvvm;
using EnterpriseUI.Navigation;
using EnterpriseUI.Tests.Support;
using EnterpriseUI.WinForms;
using Xunit;
using static EnterpriseUI.Tests.Support.WinFormsTestSupport;

namespace EnterpriseUI.Tests.UI;

/// <summary>Toolbar, ribbon, tabbed and shaped forms.</summary>
public class ShellVariantFormTests
{
    private static void AssertPaints(Control control, string name)
    {
        using var bitmap = Ui.Render(control);
        Assert.True(Ui.DistinctColors(bitmap) >= 3, name + " rendered a blank surface");
        var shots = Environment.GetEnvironmentVariable("EUI_SHOTS");
        if (string.IsNullOrEmpty(shots)) return;
        Directory.CreateDirectory(shots);
        bitmap.Save(Path.Combine(shots, name + ".png"), System.Drawing.Imaging.ImageFormat.Png);
    }

    private static void Drag(Control strip, Point from, Point to)
    {
        Invoke(strip, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, from.X, from.Y, 0));
        Invoke(strip, "OnMouseMove", new MouseEventArgs(MouseButtons.Left, 0, to.X, to.Y, 0));
        Invoke(strip, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, to.X, to.Y, 0));
    }

    // ---- toolbar form ----

    [Fact]
    public void ToolbarFormRaisesItemsOpensMenusAndSearches()
    {
        RunSta(() =>
        {
            using var form = new EnterpriseToolbarForm { Text = "Mail", ShowSearch = true, ClientSize = new Size(900, 400) };
            var save = form.AddToolbarItem("save", "Save", "💾", "Save the file");
            var menu = new ContextMenuStrip();
            menu.Items.Add("Open");
            var file = form.AddMenu("file", "File", menu);
            form.Toolbar.AddSeparator();
            var disabled = form.AddToolbarItem("print", "Print", "🖶");
            disabled.Enabled = false;
            var clicked = new List<string>();
            form.ToolbarItemClick += (_, e) => clicked.Add(e.Item.Id);
            var searches = 0;
            form.SearchTextChanged += (_, _) => searches++;
            ShellFormTests.Offscreen(form);

            var strip = form.Toolbar;
            Assert.False(strip.GetItemBounds(save).IsEmpty);
            Assert.True(strip.GetItemBounds(save).Right <= strip.GetItemBounds(file).Left);
            var b = strip.GetItemBounds(save);
            Ui.Click(strip, b.X + 5, b.Y + 5);
            var d = strip.GetItemBounds(disabled);
            Ui.Click(strip, d.X + 5, d.Y + 5);
            Assert.Equal(new[] { "save" }, clicked);

            form.SearchText = "invoice";
            Assert.True(searches >= 1);
            Assert.Equal("invoice", strip.SearchText);
            Assert.False(strip.SearchBounds.IsEmpty);

            Assert.False(strip.IsCaptionArea(new Point(b.X + 5, b.Y + 5)));
            Assert.True(strip.IsCaptionArea(new Point(strip.SearchBounds.Left - 4, 4)) || strip.OverflowItems.Count > 0);
            AssertPaints(form.TitleBar, "shell-toolbarform");
        });
    }

    [Fact]
    public void ToolbarOverflowsWhenTheStripIsTooNarrow()
    {
        RunSta(() =>
        {
            using var form = new EnterpriseToolbarForm { Text = "Narrow", ClientSize = new Size(420, 300), ShowSearch = true };
            for (var i = 0; i < 8; i++) form.AddToolbarItem("item" + i, "Command " + i, "★");
            ShellFormTests.Offscreen(form);

            var strip = form.Toolbar;
            Assert.NotEmpty(strip.OverflowItems);
            Assert.False(strip.OverflowBounds.IsEmpty);
            Assert.All(strip.OverflowItems, item => Assert.True(strip.GetItemBounds(item).IsEmpty));
            Assert.True(strip.GetItemBounds(form.ToolbarItems[0]).Right < strip.OverflowBounds.Left);

            form.ClientSize = new Size(1600, 300);
            Pump(80);
            Assert.Empty(strip.OverflowItems);
        });
    }

    // ---- ribbon form ----

    private static EnterpriseRibbonForm NewRibbonForm(out int[] executed, out RibbonTab home, out RibbonTab picture)
    {
        var counter = new int[1];
        var form = new EnterpriseRibbonForm { Text = "Editor", ClientSize = new Size(1000, 500) };
        home = new RibbonTab { Title = "Home" };
        var clipboard = new RibbonGroup { Title = "Clipboard" };
        clipboard.Items.Add(new RibbonItem { Text = "Paste", Icon = "📋", Command = new RelayCommand(() => counter[0]++) });
        clipboard.Items.Add(new RibbonItem { Text = "Cut", Icon = "✂", IsLarge = false });
        clipboard.Items.Add(new RibbonItem { Text = "Copy", Icon = "⧉", IsLarge = false });
        home.Groups.Add(clipboard);
        var view = new RibbonTab { Title = "View" };
        view.Groups.Add(new RibbonGroup { Title = "Zoom", Items = { new RibbonItem { Text = "Zoom in", Icon = "+" } } });
        picture = new RibbonTab { Title = "Picture Tools", ContextualGroup = "Picture", ContextualColor = "#C0504D" };
        picture.Groups.Add(new RibbonGroup { Title = "Adjust", Items = { new RibbonItem { Text = "Crop", Icon = "⛶" } } });
        form.Ribbon.Tabs.Add(home);
        form.Ribbon.Tabs.Add(view);
        form.Ribbon.Tabs.Add(picture);
        form.QuickAccessItems.Add(new RibbonItem { Text = "Save", Icon = "💾" });
        form.Ribbon.RefreshRibbon();
        executed = counter;
        return form;
    }

    [Fact]
    public void RibbonFormSharesTheTitleBarWithTabsAndQuickAccess()
    {
        RunSta(() =>
        {
            using var form = NewRibbonForm(out var executed, out var home, out var view);
            var picture = view;
            var quick = new List<string>();
            form.QuickAccessClick += (_, e) => quick.Add(e.Item.Text);
            var contextual = new List<string>();
            form.ContextualTabSelected += (_, e) => contextual.Add(e.Tab.Title);
            ShellFormTests.Offscreen(form);

            var ribbon = form.Ribbon;
            Assert.False(ribbon.ShowTabHeaders);
            Assert.Same(ribbon.TabHeaders, form.TitleBar.ContentControl);
            Assert.True(ribbon.Top >= form.TitleBar.Bottom);
            Assert.Equal(new[] { "Home", "View" }, ribbon.VisibleTabs.Select(t => t.Title));
            Assert.Same(home, ribbon.SelectedTab);

            var headers = ribbon.TabHeaders!;
            var save = ribbon.GetQuickAccessBounds(form.QuickAccessItems[0]);
            Ui.Click(headers, save.X + 4, save.Y + 4);
            Assert.Equal(new[] { "Save" }, quick);

            var paste = ribbon.GetItemBounds(home.Groups[0].Items[0]);
            Assert.False(paste.IsEmpty);
            Ui.Click(ribbon, paste.X + 6, paste.Y + 6);
            Assert.Equal(1, executed[0]);

            form.ShowContextualTab("Picture");
            Assert.Equal(new[] { "Home", "View", "Picture Tools" }, ribbon.VisibleTabs.Select(t => t.Title));
            Assert.Same(picture, ribbon.SelectedTab);
            Assert.Equal(new[] { "Picture Tools" }, contextual);
            Assert.Single(form.ContextualTabs);

            var tab = ribbon.GetTabBounds(home);
            Ui.Click(headers, tab.X + 4, tab.Y + 4);
            Assert.Same(home, ribbon.SelectedTab);
            form.HideContextualTab("Picture");
            Assert.Equal(2, ribbon.VisibleTabs.Count);
            AssertPaints(form.TitleBar, "shell-ribbonform-header");
            AssertPaints(ribbon, "shell-ribbonform-body");
        });
    }

    [Fact]
    public void RibbonCollapsesToTheHeadersAndExpandsWhenATabIsChosen()
    {
        RunSta(() =>
        {
            using var form = NewRibbonForm(out _, out var home, out _);
            var collapsedEvents = 0;
            form.RibbonCollapsedChanged += (_, _) => collapsedEvents++;
            ShellFormTests.Offscreen(form);
            var expandedHeight = form.Ribbon.Height;
            Assert.True(expandedHeight > 60);

            form.CollapseRibbon();
            Assert.True(form.IsRibbonCollapsed);
            Assert.Equal(0, form.Ribbon.Height);

            var tab = form.Ribbon.GetTabBounds(form.Ribbon.Tabs[1]);
            Ui.Click(form.Ribbon.TabHeaders!, tab.X + 4, tab.Y + 4);
            Assert.False(form.IsRibbonCollapsed);
            Assert.Equal(expandedHeight, form.Ribbon.Height);
            Assert.Equal("View", form.Ribbon.SelectedTab!.Title);
            Assert.Equal(2, collapsedEvents);
            Assert.NotNull(home);
        });
    }

    [Fact]
    public void RibbonFormHitTestKeepsTabsClickableAndTheRestDraggable()
    {
        RunSta(() =>
        {
            using var form = NewRibbonForm(out _, out var home, out _);
            ShellFormTests.Offscreen(form);
            var headers = form.Ribbon.TabHeaders!;
            var tab = form.Ribbon.GetTabBounds(home);
            Assert.Equal(EnterpriseFormHit.Client, form.FrameHitTest(headers.PointToScreen(new Point(tab.X + 4, tab.Y + 4))));
            var lastTab = form.Ribbon.GetTabBounds(form.Ribbon.Tabs[1]);
            Assert.Equal(EnterpriseFormHit.Caption, form.FrameHitTest(headers.PointToScreen(new Point(lastTab.Right + 40, lastTab.Y + 4))));
        });
    }

    [Fact]
    public void StandaloneRibbonDrawsItsOwnHeaders()
    {
        RunSta(() =>
        {
            using var host = new Form { ClientSize = new Size(800, 300), ShowInTaskbar = false };
            var ribbon = new EnterpriseRibbonBarControl();
            var tab = new RibbonTab { Title = "Data" };
            tab.Groups.Add(new RibbonGroup { Title = "Query", Items = { new RibbonItem { Text = "Refresh", Icon = "⟳", ToolTip = "Reload" } } });
            ribbon.Tabs.Add(tab);
            host.Controls.Add(ribbon);
            ShellFormTests.Offscreen(host);

            Assert.True(ribbon.ShowTabHeaders);
            Assert.True(ribbon.Height > ribbon.TabHeaders!.Height);
            Assert.False(ribbon.GetTabBounds(tab).IsEmpty);
            ribbon.IsCollapsed = true;
            Assert.Equal(ribbon.TabHeaders!.Height, ribbon.Height);
            AssertPaints(ribbon, "shell-ribbon-standalone");
        });
    }

    // ---- tabbed form ----

    private static EnterpriseTabbedForm NewTabbedForm()
    {
        var form = new EnterpriseTabbedForm { Text = "Docs", ClientSize = new Size(900, 500) };
        form.AddTab("Orders", new Label { Text = "orders" });
        form.AddTab("Invoices", new Label { Text = "invoices" });
        form.AddTab("Reports", new Label { Text = "reports" }, select: false);
        return form;
    }

    [Fact]
    public void TabbedFormSelectsTabsAndShowsOnlyTheirContent()
    {
        RunSta(() =>
        {
            using var form = NewTabbedForm();
            var changes = 0;
            form.SelectedTabChanged += (_, _) => changes++;
            ShellFormTests.Offscreen(form);

            Assert.Equal("Invoices", form.SelectedTab!.Title);
            Assert.True(form.SelectedTab.Content!.Visible);
            Assert.False(form.Tabs[0].Content!.Visible);

            var b = form.GetTabBounds(form.Tabs[0]);
            Ui.Click(form.TabStrip, b.X + 8, b.Y + 8);
            Assert.Equal("Orders", form.SelectedTab!.Title);
            Assert.True(form.Tabs[0].Content!.Visible);
            Assert.False(form.Tabs[1].Content!.Visible);
            Assert.True(changes >= 1);
            AssertPaints(form.TitleBar, "shell-tabbedform");
        });
    }

    [Fact]
    public void TabbedFormClosesTabsWithVetoAndSelectsANeighbour()
    {
        RunSta(() =>
        {
            using var form = NewTabbedForm();
            var veto = true;
            var closed = new List<string>();
            form.TabClosing += (_, e) => e.Cancel = veto;
            form.TabClosed += (_, e) => closed.Add(e.Tab.Title);
            ShellFormTests.Offscreen(form);
            var invoices = form.Tabs[1];
            var content = invoices.Content!;

            var close = form.GetCloseBounds(invoices);
            Assert.False(close.IsEmpty);
            Ui.Click(form.TabStrip, close.X + 4, close.Y + 4);
            Assert.Equal(3, form.Tabs.Count);

            veto = false;
            Ui.Click(form.TabStrip, close.X + 4, close.Y + 4);
            Assert.Equal(2, form.Tabs.Count);
            Assert.Equal(new[] { "Invoices" }, closed);
            Assert.True(content.IsDisposed);
            Assert.NotNull(form.SelectedTab);
            Assert.True(form.SelectedTab!.Content!.Visible);

            form.Tabs[0].Closable = false;
            Assert.True(form.GetCloseBounds(form.Tabs[0]).IsEmpty);
        });
    }

    [Fact]
    public void TabbedFormNewTabButtonCanCustomiseOrCancel()
    {
        RunSta(() =>
        {
            using var form = NewTabbedForm();
            var cancel = true;
            form.TabAdding += (_, e) =>
            {
                e.Cancel = cancel;
                e.Tab = new EnterpriseTabbedFormTab("Custom", new Label { Text = "custom" });
            };
            ShellFormTests.Offscreen(form);
            var b = form.GetNewTabBounds();
            Assert.False(b.IsEmpty);

            Ui.Click(form.TabStrip, b.X + 5, b.Y + 5);
            Assert.Equal(3, form.Tabs.Count);
            cancel = false;
            Ui.Click(form.TabStrip, b.X + 5, b.Y + 5);
            Assert.Equal(4, form.Tabs.Count);
            Assert.Equal("Custom", form.SelectedTab!.Title);

            form.ShowNewTabButton = false;
            Assert.True(form.GetNewTabBounds().IsEmpty);
        });
    }

    [Fact]
    public void TabbedFormPinsReordersAndKeepsPinnedTabsFirst()
    {
        RunSta(() =>
        {
            using var form = NewTabbedForm();
            ShellFormTests.Offscreen(form);
            var pins = 0;
            form.TabPinnedChanged += (_, _) => pins++;
            var reports = form.Tabs[2];

            form.PinTab(reports, true);
            Assert.Same(reports, form.Tabs[0]);
            Assert.True(form.GetTabBounds(reports).Width < form.GetTabBounds(form.Tabs[1]).Width);
            Assert.True(form.GetCloseBounds(reports).IsEmpty);
            Assert.Equal(1, pins);

            var orders = form.Tabs[1];
            var from = form.GetTabBounds(orders);
            var to = form.GetTabBounds(form.Tabs[2]);
            Drag(form.TabStrip, new Point(from.X + 20, from.Y + 10), new Point(to.Right - 4, to.Y + 10));
            Assert.Equal(new[] { "Reports", "Invoices", "Orders" }, form.Tabs.Select(t => t.Title));

            form.MoveTab(reports, 2);
            Assert.Same(reports, form.Tabs[0]);
            form.PinTab(reports, false);
            Assert.Equal(1, pins - 1);
            Assert.False(reports.Pinned);
        });
    }

    [Fact]
    public void TabbedFormDetachesATabIntoItsOwnWindow()
    {
        RunSta(() =>
        {
            using var form = NewTabbedForm();
            ShellFormTests.Offscreen(form);
            var detached = new List<EnterpriseTabbedFormDetachedEventArgs>();
            form.TabDetached += (_, e) => detached.Add(e);
            var tab = form.Tabs[2];
            var content = tab.Content!;

            var other = form.DetachTab(tab, new Point(-3800, 40));
            Assert.NotNull(other);
            try
            {
                Assert.Equal(2, form.Tabs.Count);
                Assert.Single(other!.Tabs);
                Assert.Same(tab, other.SelectedTab);
                Assert.Same(other, tab.Form);
                Assert.False(content.IsDisposed);
                Assert.Same(other, content.FindForm());
                Assert.Single(detached);
                Assert.Same(other, detached[0].NewForm);

                Assert.Null(other.DetachTab(tab));
                other.CloseTab(tab);
                Pump(60);
                Assert.True(other.IsDisposed);
            }
            finally
            {
                other?.Dispose();
            }
        });
    }

    // ---- shaped form ----

    [Fact]
    public void ShapedFormClipsToItsShapeAndScalesWithTheWindow()
    {
        RunSta(() =>
        {
            using var form = new EnterpriseShapedForm { ClientSize = new Size(300, 200), Shape = EnterpriseFormShape.RoundedRectangle, CornerRadius = 40 };
            var changes = 0;
            form.ShapeChanged += (_, _) => changes++;
            ShellFormTests.Offscreen(form);
            Assert.NotNull(form.Region);

            using (var rounded = form.CreateShapePath())
            {
                Assert.False(rounded.IsVisible(0, 0));
                Assert.True(rounded.IsVisible(150, 100));
            }

            form.Shape = EnterpriseFormShape.Ellipse;
            using (var ellipse = form.CreateShapePath())
            {
                Assert.False(ellipse.IsVisible(4, 4));
                Assert.True(ellipse.IsVisible(150, 100));
            }

            form.SetPolygon(new[] { new PointF(0.5f, 0), new PointF(1, 1), new PointF(0, 1) });
            using (var triangle = form.CreateShapePath())
            {
                Assert.False(triangle.IsVisible(10, 10));
                Assert.True(triangle.IsVisible(150, 150));
            }

            form.ClientSize = new Size(600, 400);
            Pump(60);
            using (var scaled = form.CreateShapePath())
            {
                Assert.False(scaled.IsVisible(20, 20));
                Assert.True(scaled.IsVisible(300, 300));
            }

            form.ClearShape();
            using (var plain = form.CreateShapePath()) Assert.True(plain.IsVisible(0, 0));
            Assert.True(changes >= 4);
            Assert.Throws<ArgumentException>(() => form.SetPolygon(new[] { new PointF(0, 0), new PointF(1, 1) }));
            AssertPaints(form, "shell-shapedform");
        });
    }

    [Fact]
    public void ShapedFormTakesCustomPathsEditorsAndImageMasks()
    {
        RunSta(() =>
        {
            using var form = new EnterpriseShapedForm { ClientSize = new Size(200, 200) };
            ShellFormTests.Offscreen(form);

            using (var path = new GraphicsPath())
            {
                path.AddEllipse(0, 0, 100, 100);
                form.SetShape(path, new SizeF(100, 100));
            }

            using (var custom = form.CreateShapePath())
            {
                Assert.True(custom.IsVisible(100, 100));
                Assert.False(custom.IsVisible(3, 3));
            }

            using var editor = new EnterpriseShapeEditor { Size = new Size(200, 200) };
            editor.SetPoints(new[] { new PointF(0.1f, 0.1f), new PointF(0.9f, 0.1f), new PointF(0.5f, 0.9f) });
            form.SetShape(editor);
            using (var fromEditor = form.CreateShapePath())
            {
                Assert.Equal(EnterpriseFormShape.Path, form.Shape);
                Assert.True(fromEditor.IsVisible(100, 60));
                Assert.False(fromEditor.IsVisible(5, 195));
            }

            using var mask = new Bitmap(20, 20);
            using (var g = Graphics.FromImage(mask)) g.FillRectangle(Brushes.Black, 10, 0, 10, 20);
            form.MaskImage = mask;
            form.Shape = EnterpriseFormShape.Mask;
            using (var masked = form.CreateShapePath())
            {
                Assert.False(masked.IsVisible(30, 100));
                Assert.True(masked.IsVisible(170, 100));
            }
        });
    }

    [Fact]
    public void ShapedFormDragsByTheEmptyBodyOnly()
    {
        RunSta(() =>
        {
            using var form = new EnterpriseShapedForm { ClientSize = new Size(300, 200), Shape = EnterpriseFormShape.Ellipse };
            var button = new Button { Location = new Point(120, 80), Size = new Size(60, 30) };
            form.Controls.Add(button);
            ShellFormTests.Offscreen(form);

            Assert.True(form.IsDragPoint(form.PointToScreen(new Point(60, 100))));
            Assert.False(form.IsDragPoint(form.PointToScreen(new Point(150, 95))));
            Assert.False(form.IsDragPoint(form.PointToScreen(new Point(2, 2))));
            form.AllowDragByBody = false;
            Assert.False(form.IsDragPoint(form.PointToScreen(new Point(60, 100))));
        });
    }
}
