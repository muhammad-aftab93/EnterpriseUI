using System.Text;
using EnterpriseUI.WinFormsDemo.Modules;

namespace EnterpriseUI.WinFormsDemo.Infrastructure;

internal static class DemoVerification
{
    private static readonly Dictionary<Type, string> CanonicalOwners = new()
    {
        [typeof(EnterpriseUI.WinForms.EnterpriseVirtualGridControl)] = "data-virtualgrid",
        [typeof(EnterpriseUI.WinForms.EnterprisePivotGridControl)] = "data-pivotgrid",
        [typeof(EnterpriseUI.WinForms.EnterpriseTreeMapControl)] = "data-treemap",
        [typeof(EnterpriseUI.WinForms.EnterpriseDataEntryControl)] = "data-dataentry",
        [typeof(EnterpriseUI.WinForms.EnterpriseDataLayoutControl)] = "data-datalayout",
        [typeof(EnterpriseUI.WinForms.EnterpriseCardViewControl)] = "data-cardview",
        [typeof(EnterpriseUI.WinForms.EnterpriseDataFilterControl)] = "data-datafilter",
        [typeof(EnterpriseUI.WinForms.EnterpriseFilterViewControl)] = "data-filterview",
        [typeof(EnterpriseUI.WinForms.EnterpriseSyntaxEditorControl)] = "editors-fluent-validation",
        [typeof(EnterpriseUI.WinFormsDemo.Controls.EnterpriseSignaturePadControl)] = "editors-fluent-validation",
        [typeof(EnterpriseUI.WinFormsDemo.Controls.EnterpriseVirtualKeyboardControl)] = "editors-fluent-validation",
        [typeof(EnterpriseUI.WinFormsDemo.Controls.EnterpriseBarcodeControl)] = "specialized-gantt-barcode-kanban",
        [typeof(EnterpriseUI.WinForms.EnterpriseCarouselControl)] = "navigation-layout-alerts-suite",
        [typeof(EnterpriseUI.WinForms.EnterpriseBulletGraphControl)] = "gauges-radial-linear",
        [typeof(EnterpriseUI.WinForms.EnterpriseRatingAndWaitingControl)] = "indicators-rating-waiting-suite"
    };

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }

    private static IEnumerable<TreeNode> NavigationNodes(TreeNodeCollection nodes)
    {
        foreach (TreeNode node in nodes)
        {
            yield return node;
            foreach (var child in NavigationNodes(node.Nodes))
                yield return child;
        }
    }

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
                if (ids.Count != DemoMetadataRegistry.AllDemos.Count)
                    throw new InvalidOperationException("Duplicate demo IDs.");
                foreach (var entry in EnterpriseUI.Editors.EnterpriseControlRegistry.AllControls)
                {
                    var target = DemoMetadataRegistry.GetCatalogDemo(entry);
                    if (!ids.Contains(target.Id) || target.Id == "component-catalog")
                        throw new InvalidOperationException($"Missing dedicated catalog target: {entry.Name} / {entry.DemoSuiteId}");
                }
                foreach (var removedId in new[] { "grid-pivot-olap", "dataentry-filter-cards-suite", "data-gridview" })
                    if (ids.Contains(removedId) || !ids.Contains(DemoMetadataRegistry.GetById(removedId).Id))
                        throw new InvalidOperationException("Invalid retired demo redirect: " + removedId);
                log.AppendLine("PASS: Component catalog demo targets.");
                var ownersSeen = new HashSet<Type>();
                var suitePages = new HashSet<string>(StringComparer.Ordinal);
                foreach (var demo in DemoMetadataRegistry.AllDemos)
                {
                    using var view = demo.ViewFactory();
                    view.Dock = DockStyle.Fill;
                    host.Controls.Add(view);
                    host.PerformLayout();
                    await Task.Delay(30);
                    view.PerformLayout();
                    var baseView = view as BaseDemoView ?? Descendants(view).OfType<BaseDemoView>().SingleOrDefault();
                    var demoControls = Descendants(baseView?.DemoContainer ?? view).ToList();
                    if (view is CompleteControlCatalogDemoView)
                    {
                        var suiteTabs = demoControls.OfType<TabControl>().Single();
                        if (suiteTabs.TabCount != 1 || !suitePages.Add(suiteTabs.TabPages[0].Text))
                            throw new InvalidOperationException("Duplicate catalog suite page: " + demo.Id);
                    }
                    foreach (var control in demoControls)
                    {
                        if (demo.Category is DemoCategoryType.BusinessScenarios or DemoCategoryType.ThemingAndStyling or DemoCategoryType.PerformanceLab or DemoCategoryType.AccessibilityCenter or DemoCategoryType.QAValidation)
                            continue;
                        if (!CanonicalOwners.TryGetValue(control.GetType(), out var owner)) continue;
                        if (owner != demo.Id || !ownersSeen.Add(control.GetType()))
                            throw new InvalidOperationException($"Duplicate control demo: {control.GetType().Name} in {demo.Id}; owner is {owner}");
                    }
                    foreach (var tabs in demoControls.OfType<TabControl>())
                    {
                        for (var tabIndex = 0; tabIndex < tabs.TabCount; tabIndex++)
                        {
                            tabs.SelectedIndex = tabIndex;
                            await Task.Delay(30);
                            view.PerformLayout();
                            using var tabImage = new Bitmap(view.Width, view.Height);
                            view.DrawToBitmap(tabImage, view.ClientRectangle);
                        }
                        tabs.SelectedIndex = 0;
                    }
                    using var image = new Bitmap(view.Width, view.Height);
                    view.DrawToBitmap(image, view.ClientRectangle);
                    if (demo.Id is "theming-design-tokens" or "component-catalog" or "grid-basics" || demo.Id.StartsWith("data-", StringComparison.Ordinal) || demo.Id == "dataentry-filter-cards-suite")
                        image.Save(Path.Combine(directory, demo.Id + ".png"), System.Drawing.Imaging.ImageFormat.Png);
                    host.Controls.Remove(view);
                    log.AppendLine("PASS: " + demo.Id);
                }
                if (ownersSeen.Count != CanonicalOwners.Count)
                    throw new InvalidOperationException("A canonical control demonstration is missing.");
                log.AppendLine("PASS: Unique suite pages and canonical control demonstrations; every demo tab rendered.");
                using var main = new MainForm();
                main.Show();
                await Task.Delay(100);
                main.PerformLayout();
                var navigationIds = Descendants(main).OfType<TreeView>()
                    .SelectMany(tree => NavigationNodes(tree.Nodes))
                    .Select(node => node.Tag).OfType<DemoItem>().Select(demo => demo.Id).ToList();
                if (navigationIds.Count != ids.Count || navigationIds.Distinct(StringComparer.Ordinal).Count() != ids.Count)
                    throw new InvalidOperationException("Sidebar must list each registered demo exactly once.");
                log.AppendLine("PASS: Every registered demo appears exactly once in sidebar navigation.");
                foreach (var query in new[] { "editor", "EnterpriseTextBoxControl", "EnterpriseCarousel", "no-match-dedup" })
                {
                    main.ApplySearchFilter(query);
                    var searchIds = Descendants(main).OfType<TreeView>()
                        .SelectMany(tree => NavigationNodes(tree.Nodes))
                        .Select(node => node.Tag).OfType<DemoItem>().Select(demo => demo.Id).ToList();
                    if (searchIds.Count != searchIds.Distinct(StringComparer.Ordinal).Count())
                        throw new InvalidOperationException("Duplicate search destinations: " + query);
                }
                main.ApplySearchFilter(string.Empty);
                log.AppendLine("PASS: Search merges suite and control matches into unique demo destinations.");
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