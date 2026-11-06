using System.Text;
using EnterpriseUI.WinFormsDemo.Modules;

namespace EnterpriseUI.WinFormsDemo.Infrastructure;

internal static class DemoVerification
{
    internal static int Run(string directory)
    {
        Directory.CreateDirectory(directory);
        var log = new StringBuilder();
        var failed = false;
        using var host = new Form { ClientSize = new Size(1500, 900), StartPosition = FormStartPosition.CenterScreen, Text = "Demo verification" };
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ThreadExceptionEventHandler handler = (_, e) =>
        {
            failed = true;
            log.AppendLine("FAIL UI event: " + e.Exception);
            host.Close();
        };
        Application.ThreadException += handler;
        host.Shown += async (_, _) =>
        {
            try
            {
                var ids = DemoMetadataRegistry.AllDemos.Select(demo => demo.Id).ToHashSet(StringComparer.Ordinal);
                foreach (var entry in EnterpriseUI.Editors.EnterpriseControlRegistry.AllControls)
                    if (!ids.Contains(entry.DemoSuiteId)) throw new InvalidOperationException($"Missing catalog target: {entry.Name} / {entry.DemoSuiteId}");
                log.AppendLine("PASS: Component catalog demo targets.");
                foreach (var demo in DemoMetadataRegistry.AllDemos)
                {
                    using var view = demo.ViewFactory();
                    view.Dock = DockStyle.Fill;
                    host.Controls.Add(view);
                    host.PerformLayout();
                    await Task.Delay(30);
                    view.PerformLayout();
                    using var image = new Bitmap(view.Width, view.Height);
                    view.DrawToBitmap(image, view.ClientRectangle);
                    if (demo.Id is "theming-design-tokens" or "component-catalog" or "grid-basics")
                        image.Save(Path.Combine(directory, demo.Id + ".png"), System.Drawing.Imaging.ImageFormat.Png);
                    host.Controls.Remove(view);
                    log.AppendLine("PASS: " + demo.Id);
                }
                using var main = new MainForm();
                main.Show();
                await Task.Delay(100);
                main.PerformLayout();
                using var mainImage = new Bitmap(main.Width, main.Height);
                main.DrawToBitmap(mainImage, new Rectangle(Point.Empty, main.Size));
                mainImage.Save(Path.Combine(directory, "demo-center.png"), System.Drawing.Imaging.ImageFormat.Png);
                main.Close();
                log.AppendLine("PASS: Demo Center startup, layout and shutdown.");
            }
            catch (Exception exception)
            {
                failed = true;
                log.AppendLine("FAIL: " + exception);
            }
            finally
            {
                File.WriteAllText(Path.Combine(directory, "verification.txt"), log.ToString());
                completion.TrySetResult();
                host.Close();
            }
        };
        try { Application.Run(host); }
        finally { Application.ThreadException -= handler; }
        if (!completion.Task.IsCompleted) return 1;
        return failed ? 1 : 0;
    }
}