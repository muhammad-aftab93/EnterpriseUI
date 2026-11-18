using EnterpriseUI.Editors.Interactivity;
using EnterpriseUI.Layout;
using EnterpriseUI.Notifications;
using EnterpriseUI.Tests.Support;
using EnterpriseUI.WinForms;
using Xunit;
using static EnterpriseUI.Tests.Support.WinFormsTestSupport;

namespace EnterpriseUI.Tests.UI;

/// <summary>Task dialog, alerts, toasts, overlay, splash screen, flyout and taskbar button.</summary>
public class ShellPopupTests
{
    private static IEnumerable<Control> All(Control root) => ShellFormTests.All(root);

    private static Form HostForm(int width = 600, int height = 400) =>
        ShellFormTests.Offscreen(new Form { ClientSize = new Size(width, height) });

    [Fact]
    public void TaskDialogWritesChoiceAndVerificationBackToTheModel()
    {
        RunSta(() =>
        {
            var model = new EnterpriseTaskDialog
            {
                WindowTitle = "Deploy", MainInstruction = "Pick a ring", Content = "Choose **carefully**.",
                VerificationText = "Do not ask again", ExpandedInformation = "Rollback is automatic."
            };
            model.CommandLinks.Add(new EnterpriseTaskDialog.CommandLinkOption("canary", "Canary", "5% of the traffic"));
            model.CommandLinks.Add(new EnterpriseTaskDialog.CommandLinkOption("global", "Global", "Everyone at once"));
            using var dialog = new EnterpriseTaskDialogForm(model)
            {
                DialogIcon = MessageBoxIcon.Question, CommonButtons = EnterpriseTaskDialogButtons.Cancel, FooterText = "Build 42", ShowProgress = true, ProgressValue = 40
            };
            var chosen = new List<string>();
            dialog.CommandLinkClicked += (_, e) => chosen.Add(e.CommandId);
            ShellFormTests.Offscreen(dialog);

            Assert.Equal("Deploy", dialog.Text);
            Assert.Equal(2, All(dialog).Count(c => c.GetType().Name == "CommandLinkButton"));
            var height = dialog.ClientSize.Height;
            dialog.Expanded = true;
            Assert.True(dialog.ClientSize.Height > height);

            All(dialog).OfType<CheckBox>().Single().Checked = true;
            Assert.True(model.IsVerificationChecked);
            Assert.True(dialog.ClickCommandLink("global"));
            Assert.Equal(new[] { "global" }, chosen);
            Assert.Equal("global", model.SelectedCommandId);
            Assert.Equal(DialogResult.OK, dialog.DialogResult);
            Assert.False(dialog.ClickCommandLink("missing"));
            Ui.Render(dialog).Dispose();
        });
    }

    [Fact]
    public void TaskDialogCommandCanKeepTheDialogOpenAndButtonsReturnTheirResult()
    {
        RunSta(() =>
        {
            var model = new EnterpriseTaskDialog { MainInstruction = "Continue?" };
            model.CommandLinks.Add(new EnterpriseTaskDialog.CommandLinkOption("a", "Apply", "Apply the change"));
            using var dialog = new EnterpriseTaskDialogForm(model) { CommonButtons = EnterpriseTaskDialogButtons.Yes | EnterpriseTaskDialogButtons.No };
            dialog.CommandLinkClicked += (_, e) => e.KeepOpen = true;
            dialog.Shown += (_, _) => dialog.BeginInvoke(new Action(() =>
            {
                dialog.ClickCommandLink("a");
                dialog.ClickButton(EnterpriseTaskDialogButtons.No);
            }));
            Assert.Equal(DialogResult.No, dialog.Execute());
            Assert.Equal("a", model.SelectedCommandId);
        });
    }

    [Fact]
    public void TaskDialogAutoClosesWithCancelAndRaisesTicks()
    {
        RunSta(() =>
        {
            using var dialog = new EnterpriseTaskDialogForm { AutoCloseSeconds = 1, CommonButtons = EnterpriseTaskDialogButtons.OK };
            var ticks = 0;
            dialog.Tick += (_, _) => ticks++;
            Assert.Equal(DialogResult.Cancel, dialog.Execute());
            Assert.True(ticks >= 1);
        });
    }

    [Fact]
    public void DesktopAlertPinsRunsActionsAndCloses()
    {
        RunSta(() =>
        {
            var model = new EnterpriseDesktopAlert { CaptionText = "Build finished", ContentText = "All 120 tests passed.", AutoCloseDelaySeconds = 0 };
            var alert = new EnterpriseDesktopAlertWindow(model) { Severity = NotificationSeverity.Success };
            alert.Actions.Add(new NotificationAction("open", "Open report", true));
            var actions = new List<string>();
            var reasons = new List<ToastCloseReason>();
            var pins = 0;
            alert.ActionClicked += (_, e) => actions.Add(e.Action.Id);
            alert.PinnedChanged += (_, _) => pins++;
            alert.AlertClosed += (_, e) => reasons.Add(e.Reason);
            alert.ShowAlert();
            Pump(80);

            Assert.True(alert.BodySize.Height > 60);
            Assert.False(alert.GetPartBounds(EnterpriseAlertPart.Close).IsEmpty);
            var pin = alert.GetPartBounds(EnterpriseAlertPart.Pin);
            alert.ClickAt(new Point(pin.X + 3, pin.Y + 3));
            Assert.True(model.IsPinned);
            Assert.Equal(1, pins);
            Assert.Equal(EnterpriseAlertPart.Action, alert.HitTest(alert.GetActionBounds()[0].Location, out var action));
            Assert.Equal("open", action!.Id);

            alert.ClickAt(alert.GetActionBounds()[0].Location);
            PumpUntil(() => reasons.Count == 1);
            Assert.Equal(new[] { "open" }, actions);
            Assert.Equal(new[] { ToastCloseReason.Action }, reasons);
        });
    }

    [Fact]
    public void DesktopAlertClosesItselfAfterTheDelayUnlessPinned()
    {
        RunSta(() =>
        {
            var timed = new EnterpriseDesktopAlertWindow(new EnterpriseDesktopAlert { CaptionText = "Timed", ContentText = "Goes away", AutoCloseDelaySeconds = 1, ShowOptionsButton = false });
            var pinned = new EnterpriseDesktopAlertWindow(new EnterpriseDesktopAlert { CaptionText = "Pinned", ContentText = "Stays", AutoCloseDelaySeconds = 1, IsPinned = true });
            ToastCloseReason? reason = null;
            timed.AlertClosed += (_, e) => reason = e.Reason;
            timed.ShowAlert();
            pinned.ShowAlert();
            Assert.True(timed.GetPartBounds(EnterpriseAlertPart.Options).IsEmpty);

            PumpUntil(() => reason != null, 4000);
            Assert.Equal(ToastCloseReason.Timeout, reason);
            Assert.False(pinned.IsClosing);
            pinned.CloseAlert(ToastCloseReason.Programmatic, true);
        });
    }

    [Fact]
    public void ToastNotificationShowsStacksAndFollowsTheToastService()
    {
        RunSta(() =>
        {
            using var toast = new EnterpriseToastNotification { DefaultTimeout = TimeSpan.Zero, Placement = ToastPlacement.TopRight };
            var n = toast.Show("Saved", "Your changes are stored.", NotificationSeverity.Success, null, new NotificationAction("undo", "Undo"));
            Pump(60);
            Assert.Equal(1, toast.VisibleCount);
            Assert.Equal(1, toast.Center.UnreadCount);
            Assert.Single(n.Actions);

            toast.ListenToToastService = true;
            ToastService.Instance.Show("From the service", "Service toast", NotificationType.Warning, 0);
            Pump(60);
            Assert.Equal(2, toast.VisibleCount);
            Assert.Equal(NotificationSeverity.Warning, toast.Center.Items[^1].Severity);

            toast.ListenToToastService = false;
            ToastService.Instance.Show("Ignored");
            Pump(40);
            Assert.Equal(2, toast.VisibleCount);
            toast.CloseAll();
            PumpUntil(() => toast.VisibleCount == 0);
        });
    }

    [Fact]
    public void OverlayDimsBlocksAndRestoresTheHost()
    {
        RunSta(() =>
        {
            using var host = HostForm();
            var button = new Button { Location = new Point(10, 10) };
            host.Controls.Add(button);
            using var overlay = new EnterpriseOverlay { Caption = "Loading data", Indicator = EnterpriseOverlayIndicator.Dots };
            var events = new List<string>();
            overlay.Shown += (_, _) => events.Add("shown");
            overlay.Hidden += (_, _) => events.Add("hidden");

            overlay.Show(host);
            Pump(120);
            Assert.True(overlay.IsVisible);
            Assert.False(button.Enabled);
            overlay.UpdateCaption("Almost done");
            Assert.Equal("Almost done", overlay.Caption);

            overlay.Hide();
            Assert.False(overlay.IsVisible);
            Assert.True(button.Enabled);
            Assert.Equal(new[] { "shown", "hidden" }, events);

            overlay.BlockInput = false;
            overlay.Show(host);
            Assert.True(button.Enabled);
            overlay.Hide();
            Assert.Throws<InvalidOperationException>(() => new EnterpriseOverlay().Show());
        });
    }

    [Fact]
    public void SplashScreenReportsProgressFromAnyThreadAndHonoursMinimumDuration()
    {
        RunSta(() =>
        {
            var splash = new EnterpriseSplashScreen { Title = "Orders", Subtitle = "Enterprise edition", FooterText = "v2.0", FadeOut = false, MinimumDuration = 300 };
            splash.ShowSplash();
            Pump(60);
            Task.Run(() => splash.ReportProgress(40, "Loading modules")).Wait();
            Pump(80);
            Assert.Equal(40, splash.Progress);
            Assert.Equal("Loading modules", splash.StatusText);

            splash.Close();
            Pump(60);
            Assert.False(splash.IsDisposed);
            PumpUntil(() => splash.IsDisposed, 3000);
            Assert.True(splash.IsDisposed);
        });
    }

    [Fact]
    public void FlyoutSlidesInFromTheEdgeDimsTheHostAndCloses()
    {
        RunSta(() =>
        {
            using var host = HostForm();
            var button = new Button { Location = new Point(10, 10) };
            host.Controls.Add(button);
            using var flyout = new EnterpriseFlyout { Text = "Filters", Edge = EnterpriseFlyoutEdge.Right, FlyoutSize = 300, AnimationDuration = 0 };
            flyout.Content.Controls.Add(new Label { Text = "Content", Location = new Point(10, 10) });
            var order = new List<string>();
            flyout.FlyoutOpening += (_, _) => order.Add("opening");
            flyout.FlyoutOpened += (_, _) => order.Add("opened");
            flyout.FlyoutClosed += (_, _) => order.Add("closed");

            flyout.ShowFlyout(host);
            Pump(60);
            var hostRect = host.RectangleToScreen(host.ClientRectangle);
            Assert.True(flyout.IsOpen);
            Assert.Equal(hostRect.Right, flyout.Right);
            Assert.Equal((int)Math.Round(300 * flyout.DeviceDpi / 96.0), flyout.Width);
            Assert.False(button.Enabled);

            flyout.HideFlyout();
            Pump(60);
            Assert.False(flyout.IsOpen);
            Assert.True(button.Enabled);
            Assert.Equal(new[] { "opening", "opened", "closed" }, order);
        });
    }

    [Fact]
    public void FlyoutClosingCanBeCancelledAndEscapeAndAnimationClose()
    {
        RunSta(() =>
        {
            using var host = HostForm();
            using var flyout = new EnterpriseFlyout { Edge = EnterpriseFlyoutEdge.Bottom, FlyoutSize = 160, AnimationDuration = 120, IsModal = false };
            var cancel = true;
            var closed = false;
            flyout.FlyoutClosing += (_, e) => e.Cancel = cancel;
            flyout.FlyoutClosed += (_, _) => closed = true;

            flyout.ShowFlyout(host);
            PumpUntil(() => flyout.Region != null && flyout.Height > 100);
            flyout.HideFlyout();
            Pump(200);
            Assert.True(flyout.IsOpen);
            Assert.False(closed);

            cancel = false;
            Ui.Key(flyout, Keys.Escape);
            PumpUntil(() => closed);
            Assert.False(flyout.IsOpen);
        });
    }

    [Fact]
    public void TaskbarButtonKeepsProgressOverlayAndThumbnailButtons()
    {
        RunSta(() =>
        {
            using var form = HostForm();
            using var taskbar = new EnterpriseTaskbarButton { Form = form, ProgressMaximum = 200 };
            taskbar.SetProgress(150);
            Assert.Equal(EnterpriseTaskbarProgressState.Normal, taskbar.ProgressState);
            Assert.Equal(150, taskbar.ProgressValue);
            taskbar.ProgressValue = 500;
            Assert.Equal(200, taskbar.ProgressValue);
            taskbar.ProgressState = EnterpriseTaskbarProgressState.Indeterminate;
            taskbar.SetOverlay(SystemIcons.Information, "3 unread");
            Assert.Equal("3 unread", taskbar.OverlayDescription);
            taskbar.ClearOverlay();
            Assert.Null(taskbar.OverlayIcon);

            var play = new EnterpriseTaskbarThumbnailButton("play", SystemIcons.Application, "Play");
            var stop = new EnterpriseTaskbarThumbnailButton("stop", SystemIcons.Error, "Stop");
            taskbar.ThumbnailButtons.Add(play);
            taskbar.ThumbnailButtons.Add(stop);
            var clicked = new List<string>();
            taskbar.ThumbnailButtonClick += (_, e) => clicked.Add(e.Button.Id);
            taskbar.PerformThumbnailButtonClick(play);
            stop.Enabled = false;
            taskbar.PerformThumbnailButtonClick(stop);
            Assert.Equal(new[] { "play" }, clicked);

            for (var i = taskbar.ThumbnailButtons.Count; i < EnterpriseTaskbarThumbnailButtonCollection.MaximumButtons; i++)
                taskbar.ThumbnailButtons.Add(new EnterpriseTaskbarThumbnailButton("b" + i, SystemIcons.Warning));
            Assert.Throws<InvalidOperationException>(() => taskbar.ThumbnailButtons.Add(new EnterpriseTaskbarThumbnailButton("extra", SystemIcons.Warning)));
        });
    }
}
