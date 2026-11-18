using EnterpriseUI.Editors.Interactivity;
using EnterpriseUI.Tests.Support;
using EnterpriseUI.WinForms;
using Xunit;
using static EnterpriseUI.Tests.Support.WinFormsTestSupport;

namespace EnterpriseUI.Tests.UI;

/// <summary>Forms, dialogs and shell controls: framed forms, title bar, status strip, about and markup dialogs.</summary>
public class ShellFormTests
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

    internal static IEnumerable<Control> All(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in All(child)) yield return nested;
        }
    }

    internal static T Offscreen<T>(T form) where T : Form
    {
        form.ShowInTaskbar = false;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-4000, 0);
        form.Show();
        Pump(80);
        return form;
    }

    [Fact]
    public void FormHostsTitleBarAndContentBelowIt()
    {
        RunSta(() =>
        {
            using var form = new EnterpriseForm { Text = "Orders", ClientSize = new Size(640, 420) };
            var fill = new Panel { Dock = DockStyle.Fill };
            form.Controls.Add(fill);
            Offscreen(form);

            Assert.Equal("Orders", form.TitleBar.Text);
            Assert.Equal(DockStyle.Top, form.TitleBar.Dock);
            Assert.True(fill.Top >= form.TitleBar.Bottom);
            AssertPaints(form.TitleBar, "shell-titlebar");

            form.Text = "Invoices";
            Assert.Equal("Invoices", form.TitleBar.Text);
        });
    }

    [Fact]
    public void FrameHitTestFindsCaptionEdgesAndButtons()
    {
        RunSta(() =>
        {
            using var form = new EnterpriseForm { Text = "Hit", ClientSize = new Size(640, 420) };
            Offscreen(form);
            var bar = form.TitleBar;
            var at = (Point p) => bar.PointToScreen(p);

            Assert.Equal(EnterpriseFormHit.Caption, form.FrameHitTest(at(new Point(bar.Width / 2, bar.Height / 2))));
            var close = bar.GetPartBounds(TitleBarPart.Close);
            Assert.False(close.IsEmpty);
            Assert.Equal(EnterpriseFormHit.Client, form.FrameHitTest(at(new Point(close.X + 4, close.Y + close.Height / 2))));
            Assert.Equal(EnterpriseFormHit.Top, form.FrameHitTest(at(new Point(bar.Width / 2, 0))));
            Assert.Equal(EnterpriseFormHit.TopLeft, form.FrameHitTest(new Point(form.Left, form.Top)));
            Assert.Equal(EnterpriseFormHit.BottomRight, form.FrameHitTest(new Point(form.Right - 1, form.Bottom - 1)));
            Assert.Equal(EnterpriseFormHit.Left, form.FrameHitTest(new Point(form.Left, form.Top + form.Height / 2)));

            form.AllowResize = false;
            Pump(40);
            Assert.NotEqual(EnterpriseFormHit.Left, form.FrameHitTest(new Point(form.Left, form.Top + form.Height / 2)));
        });
    }

    [Fact]
    public void SystemButtonsMinimizeMaximizeAndCloseTheForm()
    {
        RunSta(() =>
        {
            using var form = new EnterpriseForm { ClientSize = new Size(500, 300) };
            Offscreen(form);
            var bar = form.TitleBar;
            var states = 0;
            form.WindowStateChanged += (_, _) => states++;

            var max = bar.GetPartBounds(TitleBarPart.Maximize);
            Ui.Click(bar, max.X + 5, max.Y + 5);
            Pump(100);
            Assert.Equal(FormWindowState.Maximized, form.WindowState);
            Assert.True(states >= 1);
            Assert.Equal(EnterpriseFormHit.Client, form.FrameHitTest(new Point(form.Left, form.Top + 200)));

            bar.Restore();
            Pump(100);
            Assert.Equal(FormWindowState.Normal, form.WindowState);

            var closed = false;
            form.FormClosed += (_, _) => closed = true;
            var close = bar.GetPartBounds(TitleBarPart.Close);
            Ui.Click(bar, close.X + 5, close.Y + 5);
            Pump(100);
            Assert.True(closed);
        });
    }

    [Fact]
    public void DialogFormsHideMinimizeAndMaximize()
    {
        RunSta(() =>
        {
            using var form = new EnterpriseForm { MinimizeBox = false, MaximizeBox = false };
            Offscreen(form);
            Assert.True(form.TitleBar.GetPartBounds(TitleBarPart.Minimize).IsEmpty);
            Assert.True(form.TitleBar.GetPartBounds(TitleBarPart.Maximize).IsEmpty);
            Assert.False(form.TitleBar.GetPartBounds(TitleBarPart.Close).IsEmpty);
        });
    }

    [Fact]
    public void TitleBarCommandButtonsRaiseEventsAndSkipDisabledOnes()
    {
        RunSta(() =>
        {
            using var form = new EnterpriseForm();
            Offscreen(form);
            var bar = form.TitleBar;
            var menu = bar.AddButton("menu", "☰", "Menu");
            var gear = bar.AddButton("gear", "⚙", "Settings");
            var clicked = new List<string>();
            bar.ButtonClick += (_, e) => clicked.Add(e.Button.Id);

            var b = bar.GetPartBounds(TitleBarPart.Button, menu);
            Ui.Click(bar, b.X + 4, b.Y + 4);
            gear.Enabled = false;
            var g = bar.GetPartBounds(TitleBarPart.Button, gear);
            Ui.Click(bar, g.X + 4, g.Y + 4);
            Assert.Equal(new[] { "menu" }, clicked);
            Assert.True(b.X < g.X, "buttons keep their insertion order from left to right");
            AssertPaints(bar, "shell-titlebar-buttons");
        });
    }

    [Fact]
    public void TitleBarContentControlDecidesWhichAreasDragTheWindow()
    {
        RunSta(() =>
        {
            using var form = new EnterpriseForm();
            var content = new ProbeContent();
            form.TitleBar.ContentControl = content;
            Offscreen(form);
            var bar = form.TitleBar;
            Assert.True(content.Width > 50);

            Assert.Equal(EnterpriseFormHit.Caption, form.FrameHitTest(content.PointToScreen(new Point(content.Width - 4, content.Height / 2))));
            Assert.Equal(EnterpriseFormHit.Client, form.FrameHitTest(content.PointToScreen(new Point(4, content.Height / 2))));
            Assert.Equal(TitleBarPart.Content, bar.HitTest(new Point(content.Left + 4, 10), out _));
        });
    }

    private sealed class ProbeContent : EnterpriseTitleBarContentControl
    {
        public override bool IsCaptionArea(Point clientPoint) => clientPoint.X > Width / 2;
    }

    [Fact]
    public void StatusStripLaysOutSpringFixedAndProgressPanels()
    {
        RunSta(() =>
        {
            using var form = new Form { ClientSize = new Size(600, 200), ShowInTaskbar = false };
            var strip = new EnterpriseStatusStrip();
            var status = strip.AddPanel("status", "Ready", spring: true);
            var user = strip.AddPanel("user", "admin", 90);
            var progress = strip.AddPanel("progress", string.Empty, 100, kind: EnterpriseStatusPanelKind.Progress);
            var help = strip.AddPanel("help", "Help", kind: EnterpriseStatusPanelKind.Link);
            form.Controls.Add(strip);
            Offscreen(form);

            Assert.True(strip.GetPanelBounds(status).Width > strip.GetPanelBounds(user).Width);
            Assert.Equal(strip.GetPanelBounds(user).Right, strip.GetPanelBounds(progress).Left);
            Assert.True(strip.GetPanelBounds(help).Right <= strip.Width - 10);

            strip.SetProgress("progress", 60);
            strip.SetText("status", "Saving...");
            Assert.Equal(60, progress.Progress);
            Assert.Equal("Saving...", status.Text);

            var clicked = new List<string>();
            strip.PanelClick += (_, e) => clicked.Add(e.Panel.Name);
            var b = strip.GetPanelBounds(help);
            Ui.Click(strip, b.X + 5, 10);
            Assert.Equal(new[] { "help" }, clicked);

            help.Visible = false;
            Assert.NotEqual("help", strip.PanelAt(new Point(b.X + 5, 10))?.Name);
            AssertPaints(strip, "shell-statusstrip");
        });
    }

    [Fact]
    public void AboutBoxLoadsAssemblyInfoAndCopiesIt()
    {
        RunSta(() =>
        {
            using var box = new EnterpriseAboutBox { LinkUrl = "https://example.org/product", Description = "A **fine** product." };
            box.LoadFromAssembly(typeof(EnterpriseAboutBox).Assembly);
            Assert.False(string.IsNullOrWhiteSpace(box.ProductName));
            Assert.Equal("About " + box.ProductName, box.Text);
            Assert.Contains("Runtime:", box.InfoText);
            Assert.Contains(box.ProductName, box.InfoText);

            string? requested = null;
            box.SystemInfoRequested += (_, e) =>
            {
                e.Text += Environment.NewLine + "Database: online";
                requested = e.Text;
            };
            Assert.Contains("Database: online", box.InfoText);
            Assert.NotNull(requested);

            var handled = new List<string>();
            box.LinkClicked += (_, e) =>
            {
                handled.Add(e.Url);
                e.Handled = true;
            };
            Offscreen(box);
            var link = All(box).OfType<LinkLabel>().Single();
            Invoke(link, "OnLinkClicked", new LinkLabelLinkClickedEventArgs(new LinkLabel.Link(0, 3)));
            Assert.Equal(new[] { "https://example.org/product" }, handled);
            AssertPaints(box, "shell-aboutbox");
        });
    }

    [Fact]
    public void MarkupDialogParsesMarkupAndExposesPlainText()
    {
        RunSta(() =>
        {
            using var dialog = new EnterpriseMarkupDialog
            {
                Text = "Update",
                Markup = "Version **2.0** is ready.\n\nRead the [notes](https://example.org/notes).",
                MessageIcon = MessageBoxIcon.Information,
                Buttons = MessageBoxButtons.YesNo
            };
            Assert.Contains("Version 2.0 is ready", dialog.PlainText);
            Assert.DoesNotContain("**", dialog.PlainText);
            Offscreen(dialog);

            var buttons = All(dialog).OfType<EnterpriseButtonControl>().ToList();
            Assert.Equal(new[] { "No", "Yes" }, buttons.Select(b => b.Text).OrderBy(t => t).ToArray());
            Assert.Same(buttons.Single(b => b.Text == "Yes"), dialog.AcceptButton);

            var results = new List<DialogResult>();
            dialog.ButtonClicked += (_, r) => results.Add(r);
            buttons.Single(b => b.Text == "No").PerformClick();
            Assert.Equal(new[] { DialogResult.No }, results);
            Assert.Equal(DialogResult.No, dialog.DialogResult);
            AssertPaints(dialog, "shell-markupdialog");
        });
    }

    [Fact]
    public void MarkupDialogDefaultButtonCanBeTheSaferChoice()
    {
        RunSta(() =>
        {
            using var dialog = new EnterpriseMarkupDialog { Markup = "Delete **3** records?", Buttons = MessageBoxButtons.YesNo, DefaultButton = MessageBoxDefaultButton.Button2 };
            Offscreen(dialog);
            Assert.Equal("No", ((EnterpriseButtonControl)dialog.AcceptButton!).Text);
            Assert.Equal("No", ((EnterpriseButtonControl)dialog.CancelButton!).Text);
            dialog.DefaultButton = MessageBoxDefaultButton.Button1;
            Assert.Equal("Yes", ((EnterpriseButtonControl)dialog.AcceptButton!).Text);
        });
    }
}
