using EnterpriseUI.Grids;
using EnterpriseUI.Tests.Support;
using EnterpriseUI.WinForms;
using Xunit;
using static EnterpriseUI.Tests.Support.WinFormsTestSupport;

namespace EnterpriseUI.Tests.UI;

/// <summary>Drives controls like a user: hosts them off-screen and calls their input handlers.</summary>
internal static class Ui
{
    public static T Host<T>(T control, int width = 420, int height = 320) where T : Control
    {
        var form = new Form { ClientSize = new Size(width + 20, height + 20), ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-4000, 0) };
        control.Location = new Point(10, 10);
        control.Size = new Size(width, height);
        form.Controls.Add(control);
        form.Show();
        Pump(60);
        control.Focus();
        return control;
    }

    public static void Click(Control control, int x, int y, MouseButtons button = MouseButtons.Left)
    {
        Invoke(control, "OnMouseMove", new MouseEventArgs(MouseButtons.None, 0, x, y, 0));
        Invoke(control, "OnMouseDown", new MouseEventArgs(button, 1, x, y, 0));
        Invoke(control, "OnMouseUp", new MouseEventArgs(button, 1, x, y, 0));
    }

    public static void DoubleClick(Control control, int x, int y)
    {
        Click(control, x, y);
        Invoke(control, "OnMouseDoubleClick", new MouseEventArgs(MouseButtons.Left, 2, x, y, 0));
    }

    public static void Key(Control control, Keys keys) => Invoke(control, "OnKeyDown", new KeyEventArgs(keys));

    public static void Type(Control control, char ch) => Invoke(control, "OnKeyPress", new KeyPressEventArgs(ch));

    public static Bitmap Render(Control control)
    {
        var bitmap = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, new Rectangle(0, 0, control.Width, control.Height));
        return bitmap;
    }

    /// <summary>Number of distinct colors, a cheap proof that something other than a blank surface was painted.</summary>
    public static int DistinctColors(Bitmap bitmap)
    {
        var colors = new HashSet<int>();
        for (var y = 0; y < bitmap.Height; y += 2)
        {
            for (var x = 0; x < bitmap.Width; x += 2) colors.Add(bitmap.GetPixel(x, y).ToArgb());
        }

        return colors.Count;
    }

    public static void Dispose(Control control)
    {
        var form = control.FindForm();
        control.Dispose();
        form?.Dispose();
    }
}

public class ListControlTests
{
    private sealed record Person(int Id, string Name);

    [Fact]
    public void ListBindsAndSelectsByMouseAndKeyboard()
    {
        RunSta(() =>
        {
            var list = Ui.Host(new EnterpriseListControl { DisplayMember = "Name", ValueMember = "Id" });
            var people = Enumerable.Range(1, 100).Select(i => new Person(i, $"Person {i:000}")).ToList();
            list.DataSource = people;
            var changes = 0;
            list.SelectionChanged += (_, _) => changes++;
            Pump(40);

            Assert.Equal(100, list.Count);
            Assert.Equal("Person 001", list.GetItemText(0));

            var h = (int)Math.Round(28 * list.DeviceDpi / 96f);
            Ui.Click(list, 40, 1 + h + h / 2);
            Assert.Equal(1, list.SelectedIndex);
            Assert.Equal(2, list.SelectedValue);

            Ui.Key(list, Keys.Down);
            Ui.Key(list, Keys.Down);
            Assert.Equal(3, list.SelectedIndex);
            Ui.Key(list, Keys.End);
            Assert.Equal(99, list.SelectedIndex);
            Assert.True(list.ScrollY > 0);
            Ui.Key(list, Keys.Home);
            Assert.Equal(0, list.SelectedIndex);
            Assert.Equal(0, list.ScrollY);
            Assert.True(changes >= 4);

            Ui.Type(list, 'p');
            Ui.Type(list, 'e');
            Assert.NotEqual(-1, list.SelectedIndex);
            using var bitmap = Ui.Render(list);
            Assert.True(Ui.DistinctColors(bitmap) > 6);
            Ui.Dispose(list);
        });
    }

    [Fact]
    public void ListSupportsExtendedSelectionFilteringAndSorting()
    {
        RunSta(() =>
        {
            var list = Ui.Host(new EnterpriseListControl { SelectionMode = ListSelectionMode.MultiExtended });
            list.AddRange(Enumerable.Range(1, 20).Select(i => (object)$"Item {i}"));
            Ui.Key(list, Keys.Down);
            Ui.Key(list, Keys.Down);
            Ui.Key(list, Keys.Down);
            Invoke(list, "OnKeyDown", new KeyEventArgs(Keys.Down | Keys.Shift));
            Assert.True(list.SelectedIndices.Count >= 1);

            list.SelectAll();
            Assert.Equal(20, list.SelectedItems.Count);
            list.ClearSelection();
            Assert.Empty(list.SelectedIndices);

            list.Filter = item => ((string)item).EndsWith('1');
            Assert.Equal(2, list.VisibleCount);
            list.Filter = null;
            list.SortComparison = (a, b) => string.Compare((string)b, (string)a, StringComparison.Ordinal);
            Assert.Equal(20, list.VisibleCount);
            Ui.Dispose(list);
        });
    }

    [Fact]
    public void CheckedListTogglesChecksAndRaisesEvents()
    {
        RunSta(() =>
        {
            var list = Ui.Host(new EnterpriseCheckedListBoxControl());
            list.AddRange(new object[] { "Alpha", "Beta", "Gamma" });
            var raised = 0;
            list.ItemCheck += (_, _) => raised++;

            var h = (int)Math.Round(28 * list.DeviceDpi / 96f);
            Ui.Click(list, 20, 1 + h / 2);
            Assert.True(list.GetItemChecked(0));
            Ui.Click(list, 20, 1 + h / 2);
            Assert.False(list.GetItemChecked(0));

            list.CheckAll();
            Assert.Equal(3, list.CheckedItems.Count);
            list.InvertChecks();
            Assert.Empty(list.CheckedIndices);

            list.SetItemChecked(2, true);
            Ui.Key(list, Keys.Home);
            Ui.Key(list, Keys.Space);
            Assert.True(list.GetItemChecked(0));
            Assert.True(raised >= 6);
            using var bitmap = Ui.Render(list);
            Assert.True(Ui.DistinctColors(bitmap) > 6);
            Ui.Dispose(list);
        });
    }
}

public class TreeViewControlTests
{
    [Fact]
    public void TreeExpandsSelectsAndNavigatesByKeyboard()
    {
        RunSta(() =>
        {
            var tree = Ui.Host(new EnterpriseTreeViewControl());
            var root = tree.Nodes.Add("Root");
            var a = root.Nodes.Add("A");
            a.Nodes.Add("A1");
            root.Nodes.Add("B");
            Pump(30);

            Assert.False(root.Expanded);
            Ui.Key(tree, Keys.Down);
            Assert.Same(root, tree.SelectedNode);
            Ui.Key(tree, Keys.Right);
            Assert.True(root.Expanded);
            Ui.Key(tree, Keys.Right);
            Assert.Same(a, tree.SelectedNode);
            Ui.Key(tree, Keys.Multiply);
            Assert.True(a.Expanded);
            Ui.Key(tree, Keys.End);
            Assert.Equal("B", tree.SelectedNode!.Text);
            Ui.Key(tree, Keys.Left);
            Assert.Same(root, tree.SelectedNode);
            Ui.Key(tree, Keys.Left);
            Assert.False(root.Expanded);

            var h = (int)Math.Round(28 * tree.DeviceDpi / 96f);
            Ui.Click(tree, 1 + (int)Math.Round(15 * tree.DeviceDpi / 96f), 1 + h / 2);
            Assert.True(root.Expanded);
            using var bitmap = Ui.Render(tree);
            Assert.True(Ui.DistinctColors(bitmap) > 6);
            Ui.Dispose(tree);
        });
    }

    [Fact]
    public void TreeCascadesTriStateChecksAndLoadsLazily()
    {
        RunSta(() =>
        {
            var tree = Ui.Host(new EnterpriseTreeViewControl { CheckBoxes = true });
            var root = tree.Nodes.Add("Root");
            var one = root.Nodes.Add("One");
            var two = root.Nodes.Add("Two");

            one.Checked = true;
            Assert.Equal(CheckState.Indeterminate, root.CheckState);
            two.Checked = true;
            Assert.Equal(CheckState.Checked, root.CheckState);
            root.Checked = false;
            Assert.False(one.Checked);
            Assert.False(two.Checked);
            root.Checked = true;
            Assert.Equal(2, tree.CheckedNodes().Count(n => n.Parent == root));

            var lazy = tree.Nodes.Add("Lazy");
            lazy.HasChildrenHint = true;
            tree.NodeExpanding += (_, e) =>
            {
                if (e.Node.Nodes.Count == 0) e.Node.Nodes.Add("Loaded child");
            };
            lazy.Expanded = true;
            Assert.True(lazy.Expanded);
            Assert.Single(lazy.Nodes);

            tree.LoadHierarchy(new[] { 1, 2 }, i => $"N{i}", i => i == 1 ? new[] { 10, 11 } : null);
            Assert.Equal(2, tree.Nodes.Count);
            Assert.Equal(2, tree.Nodes[0].Nodes.Count);
            Ui.Dispose(tree);
        });
    }
}

public class ListViewControlTests
{
    private sealed record Order(string Customer, string Region, decimal Total);

    private static EnterpriseListViewControl Create(out int dpiH)
    {
        var view = Ui.Host(new EnterpriseListViewControl { ItemWidth = 0 }, 520, 300);
        view.AddColumn("Customer", 160, "Customer");
        view.AddColumn("Region", 100, "Region");
        view.AddColumn("Total", 100, "Total", ContentAlignment.MiddleRight);
        view.DataSource = new[]
        {
            new Order("Contoso", "EMEA", 120m), new Order("Fabrikam", "APAC", 30m), new Order("Northwind", "AMER", 2000m), new Order("Adventure", "EMEA", 9m)
        };
        Pump(40);
        dpiH = (int)Math.Round(view.DeviceDpi / 96f * 100);
        return view;
    }

    [Fact]
    public void DetailsViewBindsSortsAndSelects()
    {
        RunSta(() =>
        {
            var view = Create(out _);
            var scale = view.DeviceDpi / 96f;
            Assert.Equal(4, view.Items.Count);
            Assert.Equal("Contoso", view.Items[0].Text);
            Assert.Equal("EMEA", view.Items[0].SubItems[0]);

            var header = (int)Math.Round(34 * scale);
            var row = (int)Math.Round(30 * scale);
            Ui.Click(view, 20, 1 + header + row / 2);
            Assert.Same(view.Items[0], view.SelectedItem);

            Ui.Click(view, (int)(1 + 160 * scale + 100 * scale + 40), 1 + header / 2);
            Assert.Equal(2, view.SortColumn);
            Assert.Equal(ListViewSortOrder.Ascending, view.SortOrder);
            Ui.Key(view, Keys.Home);
            Assert.Equal("Adventure", view.SelectedItem!.Text);
            Ui.Key(view, Keys.Down);
            Assert.Equal("Fabrikam", view.SelectedItem!.Text);

            view.Sort(2, ListViewSortOrder.Descending);
            Ui.Key(view, Keys.Home);
            Assert.Equal("Northwind", view.SelectedItem!.Text);

            using var bitmap = Ui.Render(view);
            Assert.True(Ui.DistinctColors(bitmap) > 8);
            Ui.Dispose(view);
        });
    }

    [Fact]
    public void ViewSupportsGroupsCheckBoxesAndOtherModes()
    {
        RunSta(() =>
        {
            var view = Create(out _);
            view.CheckBoxes = true;
            view.GroupSelector = item => item.SubItems[0];
            var activated = 0;
            view.ItemActivate += (_, _) => activated++;
            Pump(30);

            view.Items[0].Checked = true;
            Assert.Single(view.CheckedItems);
            view.ToggleGroup("EMEA");
            view.ToggleGroup("EMEA");

            view.View = ListViewMode.Tiles;
            Ui.Key(view, Keys.Home);
            Ui.Key(view, Keys.Right);
            Ui.Key(view, Keys.Enter);
            Assert.Equal(1, activated);
            using (var tiles = Ui.Render(view)) Assert.True(Ui.DistinctColors(tiles) > 8);

            view.View = ListViewMode.List;
            using (var list = Ui.Render(view)) Assert.True(Ui.DistinctColors(list) > 8);

            view.SelectAll();
            Assert.Equal(4, view.SelectedItems.Count);
            view.Filter = i => i.Text.StartsWith('F');
            Ui.Key(view, Keys.End);
            Assert.Equal("Fabrikam", view.SelectedItem!.Text);
            Ui.Dispose(view);
        });
    }
}

public sealed class InspectedSettings
{
    [System.ComponentModel.Category("General"), System.ComponentModel.Description("Shown in the title bar."), System.ComponentModel.DefaultValue("Untitled")]
    public string Title { get; set; } = "Untitled";

    [System.ComponentModel.Category("General")]
    public int Count { get; set; } = 3;

    [System.ComponentModel.Category("General"), System.ComponentModel.ReadOnly(true)]
    public string Id { get; set; } = "A-1";

    [System.ComponentModel.Category("Appearance")]
    public bool Visible { get; set; } = true;

    [System.ComponentModel.Category("Appearance")]
    public Color Accent { get; set; } = Color.Crimson;

    [System.ComponentModel.Category("Appearance")]
    public DockStyle Dock { get; set; } = DockStyle.Left;

    [System.ComponentModel.Category("Layout")]
    public Size Extent { get; set; } = new Size(40, 20);
}

public class PropertyGridControlTests
{
    private static TextBox EditorOf(Control grid) => grid.Controls.OfType<TextBox>().Last();

    [Fact]
    public void PropertyGridEditsValuesAndResetsToDefaults()
    {
        RunSta(() =>
        {
            var settings = new InspectedSettings();
            var grid = Ui.Host(new EnterprisePropertyGridControl { SelectedObject = settings }, 420, 480);
            var changed = new List<string>();
            grid.PropertyValueChanged += (_, entry) => changed.Add(entry.Name);

            Ui.Key(grid, Keys.Down);
            Ui.Key(grid, Keys.Down);
            Ui.Key(grid, Keys.Down);
            Ui.Key(grid, Keys.Down);
            Assert.Equal("Visible", grid.SelectedProperty!.Name);
            Ui.Key(grid, Keys.Space);
            Assert.False(settings.Visible);

            grid.SearchText = "Title";
            Ui.Key(grid, Keys.Down);
            Ui.Key(grid, Keys.Down);
            Assert.Equal("Title", grid.SelectedProperty!.Name);
            Ui.Key(grid, Keys.Enter);
            var editor = EditorOf(grid);
            Assert.True(editor.Visible);
            editor.Text = "Hello";
            PressInEditor(editor, Keys.Enter);
            Assert.Equal("Hello", settings.Title);
            Assert.Contains("Title", changed);
            Assert.False(EditorOf(grid).Visible);

            Ui.Key(grid, Keys.Delete);
            Assert.Equal("Untitled", settings.Title);

            using var bitmap = Ui.Render(grid);
            Assert.True(Ui.DistinctColors(bitmap) > 8);
            Ui.Dispose(grid);
        });
    }

    [Fact]
    public void PropertyGridRejectsInvalidTextAndCancelsEditing()
    {
        RunSta(() =>
        {
            var settings = new InspectedSettings();
            var grid = Ui.Host(new EnterprisePropertyGridControl { SelectedObject = settings, SearchText = "Count" }, 420, 480);
            Ui.Key(grid, Keys.Down);
            Ui.Key(grid, Keys.Down);
            Ui.Key(grid, Keys.Enter);
            var editor = EditorOf(grid);
            editor.Text = "not a number";
            PressInEditor(editor, Keys.Enter);
            Assert.Equal(3, settings.Count);

            Ui.Key(grid, Keys.Enter);
            editor = EditorOf(grid);
            editor.Text = "42";
            PressInEditor(editor, Keys.Escape);
            Assert.Equal(3, settings.Count);

            Ui.Key(grid, Keys.Enter);
            editor = EditorOf(grid);
            editor.Text = 42.ToString(System.Globalization.CultureInfo.CurrentCulture);
            PressInEditor(editor, Keys.Enter);
            Assert.Equal(42, settings.Count);
            Ui.Dispose(grid);
        });
    }

    [Fact]
    public void PropertyGridExpandsNestedObjectsAndSwitchesLayout()
    {
        RunSta(() =>
        {
            var settings = new InspectedSettings();
            var grid = Ui.Host(new EnterprisePropertyGridControl { SelectedObject = settings, SearchText = "Extent" }, 420, 480);
            Ui.Key(grid, Keys.Down);
            Ui.Key(grid, Keys.Down);
            Assert.Equal("Extent", grid.SelectedProperty!.Name);
            Ui.Key(grid, Keys.Right);
            Ui.Key(grid, Keys.End);
            Assert.NotNull(grid.SelectedProperty!.Parent);

            grid.SearchText = string.Empty;
            grid.Categorized = false;
            Ui.Key(grid, Keys.Home);
            Assert.NotNull(grid.SelectedProperty);
            Assert.Null(grid.SelectedProperty!.Parent);
            using var bitmap = Ui.Render(grid);
            Assert.True(Ui.DistinctColors(bitmap) > 8);
            Ui.Dispose(grid);
        });
    }
}

public sealed class Employee
{
    [System.ComponentModel.DataAnnotations.Display(Name = "Full name", GroupName = "Profile", Order = 1)]
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(30)]
    public string Name { get; set; } = "Ada";

    [System.ComponentModel.DataAnnotations.Display(GroupName = "Profile", Order = 2)]
    [System.ComponentModel.DataAnnotations.EmailAddress]
    public string Email { get; set; } = "ada@example.com";

    [System.ComponentModel.DataAnnotations.Display(GroupName = "Work", Order = 3)]
    [System.ComponentModel.DataAnnotations.Range(18, 70)]
    public int Age { get; set; } = 30;

    [System.ComponentModel.DataAnnotations.Display(GroupName = "Work", Order = 4)]
    public bool Active { get; set; } = true;

    [System.ComponentModel.DataAnnotations.Display(GroupName = "Work", Order = 5)]
    public DayOfWeek Day { get; set; } = DayOfWeek.Monday;

    [System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.MultilineText)]
    [System.ComponentModel.DataAnnotations.Display(Order = 6)]
    public string? Notes { get; set; }
}

public class DataEntryControlTests
{
    [Fact]
    public void DataEntryValidatesAppliesAndResets()
    {
        RunSta(() =>
        {
            var employee = new Employee();
            var entry = Ui.Host(new EnterpriseDataEntryControl(), 760, 520);
            var failures = 0;
            var applied = 0;
            entry.ValidationFailed += (_, _) => failures++;
            entry.Applied += (_, _) => applied++;
            entry.Entity = employee;
            Pump(40);

            Assert.Equal(6, entry.Model!.Fields.Count);
            var name = (EnterpriseInputControl)entry.GetEditor("Name")!;
            var age = (EnterpriseInputControl)entry.GetEditor("Age")!;
            var active = (EnterpriseCheckControl)entry.GetEditor("Active")!;
            var day = (EnterpriseDropDownControl)entry.GetEditor("Day")!;
            Assert.Equal("Ada", name.Text);
            Assert.True(active.Checked);
            Assert.Equal("Monday", day.SelectedItem);

            name.Text = string.Empty;
            Assert.False(entry.Apply());
            Assert.Equal(1, failures);
            Assert.True(name.HasError);
            Assert.Equal("Ada", employee.Name);

            name.Text = "Grace";
            age.Text = "99";
            Assert.True(age.HasError);
            Assert.False(entry.Apply());
            age.Text = 35.ToString(System.Globalization.CultureInfo.CurrentCulture);
            active.Checked = false;
            day.SelectedItem = "Friday";
            Assert.True(entry.Model.HasChanges);

            using (var bitmap = Ui.Render(entry)) Assert.True(Ui.DistinctColors(bitmap) > 10);
            Assert.True(entry.Apply());
            Assert.Equal(1, applied);
            Assert.Equal("Grace", employee.Name);
            Assert.Equal(35, employee.Age);
            Assert.False(employee.Active);
            Assert.Equal(DayOfWeek.Friday, employee.Day);

            name.Text = "Changed";
            entry.ResetChanges();
            Assert.Equal("Grace", name.Text);
            Assert.False(entry.Model.HasChanges);
            Ui.Dispose(entry);
        });
    }

    [Fact]
    public void DataEntryUsesMultipleColumnsWhenWide()
    {
        RunSta(() =>
        {
            var wide = Ui.Host(new EnterpriseDataEntryControl { Entity = new Employee() }, 1100, 500);
            var narrow = Ui.Host(new EnterpriseDataEntryControl { Entity = new Employee() }, 420, 500);
            var wideName = wide.GetEditor("Name")!;
            var wideEmail = wide.GetEditor("Email")!;
            var narrowName = narrow.GetEditor("Name")!;
            var narrowEmail = narrow.GetEditor("Email")!;
            Assert.True(wideEmail.Left > wideName.Left);
            Assert.Equal(narrowName.Left, narrowEmail.Left);
            Ui.Dispose(wide);
            Ui.Dispose(narrow);
        });
    }
}

public class DataLayoutControlTests
{
    [Fact]
    public void LayoutFlowsGroupsIntoColumnsAndCollapses()
    {
        RunSta(() =>
        {
            var layout = Ui.Host(new EnterpriseDataLayoutControl(), 1000, 420);
            var first = new EnterpriseInputControl();
            var second = new EnterpriseInputControl();
            var wide = new EnterpriseInputControl();
            var other = new EnterpriseInputControl();
            layout.AddItem("First", first, "Contact", required: true);
            layout.AddItem("Second", second, "Contact", description: "Shown below");
            layout.AddItem("Wide", wide, "Contact", columnSpan: 3);
            layout.AddItem("Other", other, "Billing");
            Pump(30);

            Assert.True(layout.ColumnCount >= 3);
            Assert.True(second.Left > first.Left);
            Assert.True(wide.Top > first.Top);
            Assert.True(wide.Width > first.Width);
            Assert.True(other.Top > wide.Top);

            layout.ToggleGroup("Contact");
            Assert.True(layout.IsGroupCollapsed("Contact"));
            Assert.False(first.Visible);
            Assert.True(other.Visible);
            layout.ToggleGroup("Contact");
            Assert.True(first.Visible);

            layout.LabelPosition = DataLayoutLabelPosition.Left;
            Assert.True(first.Left > 100);
            using var bitmap = Ui.Render(layout);
            Assert.True(Ui.DistinctColors(bitmap) > 8);
            Ui.Dispose(layout);
        });
    }

    [Fact]
    public void LayoutCollapsesToOneColumnWhenNarrowAndScrolls()
    {
        RunSta(() =>
        {
            var layout = Ui.Host(new EnterpriseDataLayoutControl(), 360, 160);
            var editors = Enumerable.Range(0, 8).Select(_ => new EnterpriseInputControl()).ToList();
            for (var i = 0; i < editors.Count; i++) layout.AddItem($"Field {i}", editors[i]);
            Pump(30);

            Assert.Equal(1, layout.ColumnCount);
            Assert.Equal(editors[0].Left, editors[5].Left);
            layout.ScrollTo(0, 100000);
            Assert.True(layout.ScrollY > 0);
            Assert.True(editors[7].Visible);
            Assert.False(editors[0].Visible);
            Ui.Dispose(layout);
        });
    }
}

public class CardViewControlTests
{
    private sealed record Contact(string Name, string Company, string City, string Status, decimal Revenue);

    private static List<Contact> Contacts(int count) => Enumerable.Range(1, count).Select(i =>
        new Contact($"Contact {i:000}", $"Company {i % 7}", i % 2 == 0 ? "Oslo" : "Lima", i % 3 == 0 ? "Lead" : "Customer", i * 100m)).ToList();

    private static EnterpriseCardViewControl Create(int count = 40)
    {
        var cards = Ui.Host(new EnterpriseCardViewControl { TitleField = "Name", SubtitleField = "Company", BadgeField = "Status", StretchCards = false }, 820, 420);
        cards.Fields.Add(new CardField("City", "City"));
        cards.Fields.Add(new CardField("Revenue", "Revenue", "N0"));
        cards.DataSource = Contacts(count);
        Pump(40);
        return cards;
    }

    [Fact]
    public void CardsWrapSelectAndNavigate()
    {
        RunSta(() =>
        {
            var cards = Create();
            Assert.Equal(40, cards.VisibleCount);
            Assert.True(cards.Columns >= 2);

            var first = cards.CardAt(new Point(40, 40));
            Assert.NotNull(first);
            Ui.Click(cards, 40, 40);
            Assert.Same(first, cards.SelectedItem);

            Ui.Key(cards, Keys.Right);
            Assert.NotSame(first, cards.SelectedItem);
            Ui.Key(cards, Keys.Down);
            Ui.Key(cards, Keys.End);
            Assert.Equal("Contact 040", ((Contact)cards.SelectedItem!).Name);
            Assert.True(cards.ScrollY > 0);
            Ui.Key(cards, Keys.Home);
            Assert.Equal("Contact 001", ((Contact)cards.SelectedItem!).Name);

            var activated = 0;
            cards.CardActivate += (_, _) => activated++;
            Ui.Key(cards, Keys.Enter);
            Assert.Equal(1, activated);
            using var bitmap = Ui.Render(cards);
            Assert.True(Ui.DistinctColors(bitmap) > 12);
            Ui.Dispose(cards);
        });
    }

    [Fact]
    public void CardsGroupSortFilterAndCheck()
    {
        RunSta(() =>
        {
            var cards = Create();
            cards.CheckBoxes = true;
            cards.SelectionMode = ListSelectionMode.MultiExtended;

            cards.GroupField = "City";
            cards.SortField = "Revenue";
            cards.SortDescending = true;
            cards.FilterText = "Contact 01";
            Assert.Equal(10, cards.VisibleCount);
            cards.FilterText = "nothing matches";
            Assert.Equal(0, cards.VisibleCount);
            cards.FilterText = string.Empty;
            Assert.Equal(40, cards.VisibleCount);

            cards.ToggleGroup("Oslo");
            Assert.Equal(20, cards.VisibleCount);
            cards.ToggleGroup("Oslo");

            var item = cards.CardAt(new Point(60, 80));
            if (item is null)
            {
                cards.Fields.Clear();
                Assert.Fail("A card should be at the first position.");
            }

            cards.SetChecked(item!, true);
            Assert.Single(cards.CheckedItems);
            cards.SelectAll();
            Assert.Equal(40, cards.SelectedItems.Count);
            using var bitmap = Ui.Render(cards);
            Assert.True(Ui.DistinctColors(bitmap) > 12);
            Ui.Dispose(cards);
        });
    }
}

public class FilterViewControlTests
{
    private sealed record Product(string Name, string Category, string Region, decimal Price);

    private static List<Product> Products() => new()
    {
        new("Pen", "Office", "EMEA", 2m), new("Desk", "Furniture", "EMEA", 250m), new("Chair", "Furniture", "APAC", 120m),
        new("Paper", "Office", "AMER", 8m), new("Lamp", "Furniture", "AMER", 40m), new("Stapler", "Office", "APAC", 12m)
    };

    [Fact]
    public void FilterViewTogglesValuesAndNarrowsData()
    {
        RunSta(() =>
        {
            var view = Ui.Host(new EnterpriseFilterViewControl(), 300, 520);
            view.AddListFacet("Category");
            view.AddListFacet("Region");
            view.AddRangeFacet("Price");
            var changes = 0;
            view.FilterChanged += (_, _) => changes++;
            var products = Products();
            view.DataSource = products;
            Pump(40);

            Assert.Equal(6, view.Apply(products.Cast<object>()).Count());
            Assert.False(view.HasSelection);

            // Move to the first value of the first facet and toggle it with the keyboard.
            Ui.Key(view, Keys.Down);
            Ui.Key(view, Keys.Down);
            Ui.Key(view, Keys.Space);
            Assert.True(view.HasSelection);
            Assert.Equal(1, view.ActiveCount);
            Assert.Equal(3, view.Apply(products.Cast<object>()).Count());
            Assert.True(changes > 0);

            view.ToggleValue("Region", "EMEA", true);
            Assert.Equal(2, view.ActiveCount);
            Assert.Single(view.Apply(products.Cast<object>()));
            Assert.NotEmpty(view.ToDescriptors());

            view.ClearAll();
            Assert.False(view.HasSelection);
            Assert.Equal(6, view.Apply(products.Cast<object>()).Count());
            using var bitmap = Ui.Render(view);
            Assert.True(Ui.DistinctColors(bitmap) > 8);
            Ui.Dispose(view);
        });
    }

    [Fact]
    public void FilterViewRangeInputsAndSectionsWork()
    {
        RunSta(() =>
        {
            var view = Ui.Host(new EnterpriseFilterViewControl(), 300, 520);
            view.AddListFacet("Category");
            var price = view.AddRangeFacet("Price");
            var products = Products();
            view.DataSource = products;
            Pump(40);

            var inputs = view.Controls.OfType<EnterpriseInputControl>().ToList();
            Assert.Equal(2, inputs.Count);
            inputs[0].Text = 10.ToString(System.Globalization.CultureInfo.CurrentCulture);
            Invoke(inputs[0].InnerTextBox, "OnKeyDown", new KeyEventArgs(Keys.Enter));
            Assert.NotNull(price.From);
            Assert.Equal(4, view.Apply(products.Cast<object>()).Count());

            inputs[1].Text = "abc";
            Invoke(inputs[1].InnerTextBox, "OnKeyDown", new KeyEventArgs(Keys.Enter));
            Assert.True(inputs[1].HasError);

            view.ToggleSection("Price");
            Assert.False(inputs[0].Visible);
            view.ClearAll();
            Assert.Equal(string.Empty, inputs[0].Text);
            Ui.Dispose(view);
        });
    }
}

public class DataFilterControlTests
{
    public enum Level { Low, High }

    public sealed record Deal(string Region, decimal Total, bool Active, Level Level);

    private static readonly Deal[] Deals =
    {
        new("EMEA", 50m, true, Level.Low), new("EMEA", 500m, false, Level.High), new("APAC", 500m, true, Level.High), new("AMER", 5m, true, Level.Low)
    };

    private static EnterpriseDataFilterControl Create()
    {
        var filter = Ui.Host(new EnterpriseDataFilterControl(), 820, 360);
        filter.ItemType = typeof(Deal);
        Pump(30);
        return filter;
    }

    [Fact]
    public void FilterBuilderCreatesConditionsAndMatchesItems()
    {
        RunSta(() =>
        {
            var filter = Create();
            var changes = 0;
            filter.FilterChanged += (_, _) => changes++;
            Assert.Equal(1, filter.RowCount);
            Assert.Equal(4, filter.Apply(Deals.Cast<object>()).Count());

            filter.AddCondition();
            Assert.Equal(2, filter.RowCount);
            var drops = filter.Controls.OfType<EnterpriseDropDownControl>().ToList();
            Assert.Equal(2, drops.Count);
            drops[0].SelectedItem = "Region";
            drops = filter.Controls.OfType<EnterpriseDropDownControl>().ToList();
            drops[1].SelectedItem = "is equal to";
            var input = filter.Controls.OfType<EnterpriseInputControl>().Single();
            input.Text = "EMEA";

            Assert.Equal(2, filter.Apply(Deals.Cast<object>()).Count());
            Assert.Contains("Region is equal to EMEA", filter.FilterText);
            Assert.Empty(filter.Validate());
            Assert.NotNull(filter.ToDescriptor());
            Assert.True(changes >= 3);

            Assert.True(filter.PressButton(1, "NOT"));
            Assert.Equal(2, filter.Apply(Deals.Cast<object>()).Count());
            Assert.StartsWith("NOT", filter.FilterText);
            Assert.True(filter.PressButton(1, "NOT"));

            filter.AddCondition();
            Assert.Equal(3, filter.RowCount);
            var all = filter.Controls.OfType<EnterpriseDropDownControl>().ToList();
            all[2].SelectedItem = "Total";
            all = filter.Controls.OfType<EnterpriseDropDownControl>().ToList();
            all[3].SelectedItem = "is greater than";
            filter.Controls.OfType<EnterpriseInputControl>().Last().Text = 100.ToString(System.Globalization.CultureInfo.CurrentCulture);
            Assert.Single(filter.Apply(Deals.Cast<object>()));

            Assert.True(filter.PressButton(0, "OR"));
            Assert.Equal(3, filter.Apply(Deals.Cast<object>()).Count());
            using var bitmap = Ui.Render(filter);
            Assert.True(Ui.DistinctColors(bitmap) > 10);
            Ui.Dispose(filter);
        });
    }

    [Fact]
    public void FilterBuilderSupportsGroupsBetweenValidationAndRoundTrip()
    {
        RunSta(() =>
        {
            var filter = Create();
            var group = filter.AddGroup();
            Assert.Equal(3, filter.RowCount);
            Assert.Equal(EnterpriseUI.Data.FilterLogicalOperator.Or, group.Operator);

            var drops = filter.Controls.OfType<EnterpriseDropDownControl>().ToList();
            drops[0].SelectedItem = "Total";
            drops = filter.Controls.OfType<EnterpriseDropDownControl>().ToList();
            drops[1].SelectedItem = "is between";
            var inputs = filter.Controls.OfType<EnterpriseInputControl>().ToList();
            Assert.Equal(2, inputs.Count);
            inputs[0].Text = 10.ToString(System.Globalization.CultureInfo.CurrentCulture);
            inputs[1].Text = "nope";
            Assert.True(inputs[1].HasError);
            Assert.NotEmpty(filter.Validate());
            inputs[1].Text = 100.ToString(System.Globalization.CultureInfo.CurrentCulture);
            Assert.Empty(filter.Validate());
            Assert.Single(filter.Apply(Deals.Cast<object>()));

            var descriptor = filter.ToDescriptor();
            Assert.NotNull(descriptor);
            filter.Clear();
            Assert.Equal(1, filter.RowCount);
            Assert.Equal(4, filter.Apply(Deals.Cast<object>()).Count());
            filter.Load(descriptor);
            Assert.Single(filter.Apply(Deals.Cast<object>()));

            Assert.True(filter.PressButton(1, "✕"));
            Assert.Equal(1, filter.RowCount);
            Ui.Dispose(filter);
        });
    }
}

public class PivotGridControlTests
{
    private sealed record Sale(string Region, string Product, string Quarter, decimal Amount);

    private static List<Sale> Sales() => new()
    {
        new("East", "Pens", "Q1", 10m), new("East", "Pens", "Q2", 20m), new("East", "Desks", "Q1", 100m),
        new("West", "Pens", "Q1", 5m), new("West", "Desks", "Q2", 200m), new("West", "Chairs", "Q2", 50m)
    };

    private static EnterprisePivotGridControl Create()
    {
        var pivot = Ui.Host(new EnterprisePivotGridControl(), 760, 380);
        pivot.RowFields.Add("Region");
        pivot.RowFields.Add("Product");
        pivot.ColumnFields.Add("Quarter");
        pivot.Values.Add(new PivotValueField("Amount", EnterpriseUI.Data.AggregateFunction.Sum, "Amount", "N0"));
        pivot.DataSource = Sales();
        Pump(40);
        return pivot;
    }

    [Fact]
    public void PivotComputesRendersAndNavigates()
    {
        RunSta(() =>
        {
            var pivot = Create();
            Assert.NotNull(pivot.Result);
            Assert.Contains(pivot.Result!.Rows, r => r.Kind == PivotRowKind.GrandTotal);
            Assert.Contains(pivot.Result.LeafHeaders, h => h.IsTotal);

            Ui.Key(pivot, Keys.Down);
            Ui.Key(pivot, Keys.Right);
            Ui.Key(pivot, Keys.Right);
            Assert.Equal(0, pivot.SelectedRow);
            Assert.Equal(2, pivot.SelectedColumn);
            Ui.Key(pivot, Keys.End | Keys.Control);
            Assert.Equal(pivot.Result.Rows.Count - 1, pivot.SelectedRow);
            Assert.Equal(pivot.Result.ColumnCount - 1, pivot.SelectedColumn);
            Assert.Equal(385m, Convert.ToDecimal(pivot.SelectedValue));

            using var bitmap = Ui.Render(pivot);
            Assert.True(Ui.DistinctColors(bitmap) > 10);
            Ui.Dispose(pivot);
        });
    }

    [Fact]
    public void PivotCollapsesGroupsByClickAndByApi()
    {
        RunSta(() =>
        {
            var pivot = Create();
            var before = pivot.Result!.Rows.Count;
            var toggled = 0;
            pivot.GroupToggled += (_, _) => toggled++;

            var index = pivot.Result.Rows.ToList().FindIndex(r => r.IsExpandable);
            Assert.True(index >= 0);
            var slot = Array.FindIndex(pivot.Result.Rows[index].GroupKeys, k => k is not null);
            var scale = pivot.DeviceDpi / 96f;
            var header = (int)Math.Round(pivot.Result.HeaderRows.Count * 34 * scale);
            Ui.Click(pivot, 1 + (int)(slot * 150 * scale + 12), 1 + header + (int)(index * 30 * scale + 15 * scale));
            Assert.Equal(1, toggled);
            Assert.True(pivot.Result!.Rows.Count < before);

            pivot.ExpandAll();
            Assert.Equal(before, pivot.Result!.Rows.Count);
            pivot.CollapseAll();
            Assert.True(pivot.Result!.Rows.Count < before);
            pivot.ShowGrandTotals = false;
            Assert.DoesNotContain(pivot.Result!.Rows, r => r.Kind == PivotRowKind.GrandTotal);
            Ui.Dispose(pivot);
        });
    }
}

public class TreeMapControlTests
{
    private sealed record Stock(string Sector, string Name, double Cap, double Change);

    private static List<Stock> Stocks() => new()
    {
        new("Tech", "Alpha", 500, 2.5), new("Tech", "Beta", 300, -1.2), new("Tech", "Gamma", 100, 0.4),
        new("Energy", "Delta", 250, -3.1), new("Energy", "Epsilon", 150, 1.0), new("Health", "Zeta", 200, 0.8)
    };

    private static EnterpriseTreeMapControl Create()
    {
        var map = Ui.Host(new EnterpriseTreeMapControl(), 640, 400);
        map.Bind(Stocks(), "Name", "Cap", "Change", "Sector");
        Pump(30);
        return map;
    }

    [Fact]
    public void TreeMapBuildsHierarchyRendersAndSelects()
    {
        RunSta(() =>
        {
            var map = Create();
            Assert.Equal(3, map.Items.Count);
            Assert.Equal(900, map.Items.First(i => i.Name == "Tech").TotalValue);

            var tile = map.ItemAt(new Point(map.Width / 2, map.Height / 2));
            Assert.NotNull(tile);
            Ui.Click(map, map.Width / 2, map.Height / 2);
            Assert.Same(tile, map.SelectedItem);

            foreach (var mode in new[] { TreeMapColorMode.Category, TreeMapColorMode.Sequential, TreeMapColorMode.Diverging })
            {
                map.ColorMode = mode;
                using var bitmap = Ui.Render(map);
                Assert.True(Ui.DistinctColors(bitmap) > 10, mode.ToString());
            }

            Ui.Key(map, Keys.Right);
            Ui.Key(map, Keys.Down);
            Assert.NotNull(map.SelectedItem);
            Ui.Dispose(map);
        });
    }

    [Fact]
    public void TreeMapDrillsDownAndUpWithBreadcrumb()
    {
        RunSta(() =>
        {
            var map = Create();
            var tech = map.Items.First(i => i.Name == "Tech");
            var changes = 0;
            map.CurrentChanged += (_, _) => changes++;

            map.DrillInto(tech);
            Assert.Same(tech, map.CurrentItem);
            Assert.Equal(1, changes);
            Assert.Null(map.ItemAt(new Point(5, 5)));

            Ui.Click(map, 20, 10);
            Assert.Null(map.CurrentItem);
            Assert.Equal(2, changes);

            map.DrillInto(tech);
            Ui.Key(map, Keys.Back);
            Assert.Null(map.CurrentItem);

            map.Levels = 1;
            using var bitmap = Ui.Render(map);
            Assert.True(Ui.DistinctColors(bitmap) > 8);
            Ui.Dispose(map);
        });
    }
}

public class HeatMapControlTests
{
    private sealed record Hit(string Day, string Hour, int Count);

    private static List<Hit> Hits()
    {
        var list = new List<Hit>();
        foreach (var day in new[] { "Mon", "Tue", "Wed" })
        {
            foreach (var hour in new[] { "09", "10", "11", "12" }) list.Add(new Hit(day, hour, day.Length * 3 + int.Parse(hour, System.Globalization.CultureInfo.InvariantCulture)));
        }

        list.Add(new Hit("Mon", "09", 100));
        return list;
    }

    [Fact]
    public void HeatMapAggregatesRendersAndSelects()
    {
        RunSta(() =>
        {
            var map = Ui.Host(new EnterpriseHeatMapControl { Title = "Hits" }, 640, 360);
            map.Bind(Hits(), "Day", "Hour", "Count");
            Pump(30);

            Assert.Equal(3, map.RowLabels.Count);
            Assert.Equal(4, map.ColumnLabels.Count);
            var monRow = map.RowLabels.ToList().IndexOf("Mon");
            Assert.Equal(118d, map.ValueAt(monRow, 0));

            var cell = new Point(1 + 150, 1 + 40 + 34 + 15);
            Ui.Click(map, cell.X, cell.Y);
            Assert.True(map.SelectedRow >= 0);
            Ui.Key(map, Keys.Right);
            Ui.Key(map, Keys.End | Keys.Control);
            Assert.Equal(2, map.SelectedRow);
            Assert.Equal(3, map.SelectedColumn);
            Assert.NotNull(map.SelectedValue);

            foreach (var scale in new[] { HeatMapScale.Sequential, HeatMapScale.Diverging })
            {
                map.Scale = scale;
                using var bitmap = Ui.Render(map);
                Assert.True(Ui.DistinctColors(bitmap) > 10, scale.ToString());
            }

            map.SortRowsByColumn(0, true);
            Assert.Equal("Mon", map.RowLabels[0]);
            Ui.Dispose(map);
        });
    }

    [Fact]
    public void HeatMapHandlesEmptyDataMissingCellsAndLargeMatrices()
    {
        RunSta(() =>
        {
            var map = Ui.Host(new EnterpriseHeatMapControl(), 500, 300);
            using (var empty = Ui.Render(map)) Assert.True(Ui.DistinctColors(empty) >= 2);

            var values = new double?[40, 30];
            for (var r = 0; r < 40; r++)
            {
                for (var c = 0; c < 30; c++) values[r, c] = r == c ? null : r * c;
            }

            map.SetMatrix(Enumerable.Range(0, 40).Select(i => $"Row {i}"), Enumerable.Range(0, 30).Select(i => $"Column {i}"), values);
            Assert.Null(map.ValueAt(3, 3));
            map.ScrollTo(100000, 100000);
            Assert.True(map.ScrollY > 0);
            Assert.True(map.ScrollX > 0);
            using (var bitmap = Ui.Render(map)) Assert.True(Ui.DistinctColors(bitmap) > 10);

            Assert.Throws<ArgumentException>(() => map.SetMatrix(new[] { "a" }, new[] { "b", "c" }, new double?[1, 1]));
            Ui.Dispose(map);
        });
    }
}

public class VirtualGridControlTests
{
    [Fact]
    public void VirtualGridServesOnlyVisibleCellsAndNavigatesAcrossAMillionRows()
    {
        RunSta(() =>
        {
            var grid = Ui.Host(new EnterpriseVirtualGridControl { UseSampleData = false, RowCount = 1_000_000, ColumnCount = 8 }, 700, 320);
            var requested = new HashSet<int>();
            grid.CellValueNeeded += (_, e) =>
            {
                requested.Add(e.RowIndex);
                e.Value = e.ColumnIndex == 0 ? e.RowIndex : $"r{e.RowIndex}c{e.ColumnIndex}";
            };
            grid.ColumnTitles.Add("Id");
            Pump(30);

            using (var bitmap = Ui.Render(grid)) Assert.True(Ui.DistinctColors(bitmap) > 8);
            Assert.True(requested.Count > 0 && requested.Count < 60, $"requested {requested.Count}");
            Assert.All(requested, r => Assert.InRange(r, 0, 50));

            grid.SelectCell(500_000, 3);
            Assert.True(grid.ScrollY > 1_000_000);
            Assert.Equal(500_000, grid.SelectedRow);
            Assert.Equal("r500000c3", grid.GetCellValue(500_000, 3));

            Ui.Key(grid, Keys.End | Keys.Control);
            Assert.Equal(999_999, grid.SelectedRow);
            Assert.Equal(7, grid.SelectedCol);
            Ui.Key(grid, Keys.Home | Keys.Control);
            Assert.Equal(0, grid.SelectedRow);
            Assert.Equal(0, grid.ScrollY);
            Ui.Dispose(grid);
        });
    }

    [Fact]
    public void VirtualGridEditsCellsAndPushesValues()
    {
        RunSta(() =>
        {
            var grid = Ui.Host(new EnterpriseVirtualGridControl { UseSampleData = false, RowCount = 100, ColumnCount = 4 }, 600, 300);
            var pushed = new List<(int, int, object?)>();
            grid.CellValuePushed += (_, e) => pushed.Add((e.RowIndex, e.ColumnIndex, e.Value));
            grid.SelectCell(2, 1);

            Ui.Key(grid, Keys.F2);
            var editor = grid.Controls.OfType<TextBox>().Single();
            Assert.True(editor.Visible);
            editor.Text = "edited";
            PressInEditor(editor, Keys.Enter);

            Assert.Single(pushed);
            Assert.Equal((2, 1, (object?)"edited"), pushed[0]);
            Assert.Equal("edited", grid.GetCellValue(2, 1));
            Assert.Equal(3, grid.SelectedRow);

            Ui.Key(grid, Keys.F2);
            PressInEditor(grid.Controls.OfType<TextBox>().Single(), Keys.Escape);
            Assert.Single(pushed);
            grid.AllowEdit = false;
            Ui.Key(grid, Keys.F2);
            Assert.False(grid.Controls.OfType<TextBox>().Single().Visible);
            Ui.Dispose(grid);
        });
    }
}

public class FormControlTests
{
    [Fact]
    public void ButtonsRaiseClicksAndPaintAllVariants()
    {
        RunSta(() =>
        {
            var host = new Form { ClientSize = new Size(700, 200), ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-4000, 0) };
            var clicks = 0;
            var buttons = Enum.GetValues<EnterpriseButtonVariant>().Select((v, i) => new EnterpriseButtonControl { Text = v.ToString(), Variant = v, BadgeText = i == 0 ? "3" : null, Location = new Point(10 + i * 130, 10) }).ToList();
            foreach (var b in buttons)
            {
                b.Click += (_, _) => clicks++;
                host.Controls.Add(b);
            }

            host.Show();
            Pump(40);
            Ui.Click(buttons[0], 10, 10);
            Ui.Key(buttons[1], Keys.Space);
            Ui.Key(buttons[2], Keys.Enter);
            buttons[3].PerformClick();
            Assert.Equal(4, clicks);
            foreach (var b in buttons)
            {
                using var bitmap = Ui.Render(b);
                Assert.True(Ui.DistinctColors(bitmap) >= 2, b.Variant.ToString());
            }

            host.Dispose();
        });
    }

    [Fact]
    public void ToggleRepeatSplitAndProgressBehave()
    {
        RunSta(() =>
        {
            var host = new Form { ClientSize = new Size(700, 200), ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-4000, 0) };
            var toggle = new EnterpriseToggleSwitchControl { Location = new Point(10, 10) };
            var repeat = new EnterpriseRepeatButtonControl { Location = new Point(110, 10), RepeatInterval = 20 };
            var split = new EnterpriseSplitButtonControl { Location = new Point(300, 10) };
            var progress = new EnterpriseProgressBarControl { Location = new Point(10, 60), Value = 40 };
            host.Controls.AddRange(new Control[] { toggle, repeat, split, progress });
            host.Show();
            Pump(40);

            var states = new List<bool>();
            toggle.Toggled += on => states.Add(on);
            Ui.Click(toggle, 10, 10);
            Ui.Key(toggle, Keys.Space);
            Assert.Equal(new[] { false, true }, states);

            var ticks = 0;
            repeat.RepeatedClick += _ => ticks++;
            Invoke(repeat, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
            PumpUntil(() => ticks >= 3, 2000);
            Invoke(repeat, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
            var stopped = ticks;
            Pump(120);
            Assert.Equal(stopped, ticks);

            var primary = 0;
            split.PrimaryActionClicked += (_, _) => primary++;
            Ui.Click(split, 20, 10);
            Assert.Equal(1, primary);

            progress.Value = 150;
            Assert.Equal(100, progress.Value);
            foreach (Control c in new Control[] { toggle, repeat, split, progress })
            {
                using var bitmap = Ui.Render(c);
                Assert.True(Ui.DistinctColors(bitmap) >= 2, c.GetType().Name);
            }

            host.Dispose();
        });
    }

    [Fact]
    public void TextBoxesAndCollapsiblePanelWork()
    {
        RunSta(() =>
        {
            var host = new Form { ClientSize = new Size(700, 360), ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-4000, 0) };
            var text = new EnterpriseTextBoxControl { Location = new Point(10, 10), Width = 300, Text = "hello" };
            var browse = new EnterpriseButtonTextBoxControl { Location = new Point(10, 60), Width = 320 };
            var panel = new EnterpriseCollapsiblePanelControl { Location = new Point(10, 110), Size = new Size(400, 150), HeaderTitle = "Options" };
            var font = new EnterpriseFontDropDownControl { Location = new Point(10, 290) };
            host.Controls.AddRange(new Control[] { text, browse, panel, font });
            host.Show();
            Pump(40);

            var changed = 0;
            text.ValueChanged += (_, _) => changed++;
            text.Text = "changed";
            Assert.Equal(1, changed);
            Ui.Click(text, text.Width - 10, 10);
            Assert.Equal(string.Empty, text.Text);
            text.HasValidationError = true;
            using (var bitmap = Ui.Render(text)) Assert.True(Ui.DistinctColors(bitmap) >= 2);

            var left = 0;
            var right = 0;
            browse.LeftButtonClick += (_, _) => left++;
            browse.RightButtonClick += (_, _) => right++;
            Ui.Click(browse, 12, 10);
            Ui.Click(browse, browse.Width - 20, 10);
            Assert.Equal(1, left);
            Assert.Equal(1, right);

            var toggles = new List<bool>();
            panel.ExpandedChanged += expanded => toggles.Add(expanded);
            var height = panel.Height;
            Ui.Click(panel.Controls.OfType<Control>().First(c => c is not Panel), 40, 10);
            Assert.False(panel.IsExpanded);
            Assert.True(panel.Height < height);
            panel.IsExpanded = true;
            Assert.Equal(new[] { false, true }, toggles);
            Assert.Equal(height, panel.Height);
            Assert.True(font.Items.Count > 5);
            host.Dispose();
        });
    }
}

/// <summary>Renders every data control with representative data; set EUI_SHOTS to a folder to keep the images for a visual review.</summary>
[Collection("Theme")]
public class DataControlGalleryTests
{
    private sealed record Row(string Region, string Product, string Status, decimal Total, int Qty, string Customer);

    private static List<Row> Rows()
    {
        var regions = new[] { "North America", "EMEA", "APAC", "LATAM" };
        var products = new[] { "Cloud Suite", "Telemetry Node", "Security Agent", "Workflow Core" };
        var statuses = new[] { "Open", "Shipped", "Paid" };
        var list = new List<Row>();
        for (var i = 0; i < 96; i++) list.Add(new Row(regions[i % 4], products[i / 4 % 4], statuses[i % 3], 1000 + i * 137 % 5000, 1 + i % 9, $"Customer {i:00}"));
        return list;
    }

    private static void Save(Control control, string name)
    {
        using var bitmap = Ui.Render(control);
        Assert.True(Ui.DistinctColors(bitmap) > 8, name);
        if (Environment.GetEnvironmentVariable("EUI_SHOTS") is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder);
            bitmap.Save(Path.Combine(folder, name + ".png"), System.Drawing.Imaging.ImageFormat.Png);
        }
    }

    [Fact]
    public void AllDataControlsRenderWithRepresentativeData()
    {
        RunSta(() =>
        {
            var rows = Rows();

            var list = Ui.Host(new EnterpriseListControl { DisplayMember = "Customer" }, 360, 300);
            list.DataSource = rows;
            list.SelectedIndex = 2;
            Save(list, "list");

            var checks = Ui.Host(new EnterpriseCheckedListBoxControl(), 360, 300);
            checks.AddRange(new object[] { "Read", "Edit", "Delete", "Export", "Approve", "Publish" });
            checks.SetItemChecked(0, true);
            checks.SetItemChecked(3, true);
            Save(checks, "checkedlist");

            var view = Ui.Host(new EnterpriseListViewControl { CheckBoxes = true }, 620, 300);
            view.AddColumn("Customer", 170, "Customer");
            view.AddColumn("Region", 120, "Region");
            view.AddColumn("Status", 100, "Status");
            view.AddColumn("Total", 110, "Total", ContentAlignment.MiddleRight);
            view.DataSource = rows;
            view.Sort(3, ListViewSortOrder.Descending);
            view.SelectItem(view.Items[3]);
            Save(view, "listview-details");
            view.View = ListViewMode.Tiles;
            Save(view, "listview-tiles");

            var tree = Ui.Host(new EnterpriseTreeViewControl { CheckBoxes = true }, 320, 300);
            var root = tree.Nodes.Add("Company");
            var fin = root.Nodes.Add("Finance");
            fin.Nodes.Add("Accounting");
            fin.Nodes.Add("Treasury");
            root.Nodes.Add("Engineering").Nodes.Add("Platform");
            root.Expanded = true;
            fin.Expanded = true;
            fin.Nodes[0].Checked = true;
            tree.SelectedNode = fin.Nodes[1];
            Save(tree, "treeview");

            var props = Ui.Host(new EnterprisePropertyGridControl { SelectedObject = new InspectedSettings() }, 420, 460);
            Save(props, "propertygrid");

            var entry = Ui.Host(new EnterpriseDataEntryControl { Entity = new EntryProfile() }, 760, 420);
            Save(entry, "dataentry");

            var layout = Ui.Host(new EnterpriseDataLayoutControl(), 760, 340);
            layout.AddItem("First name", new EnterpriseInputControl { Text = "Alexander" }, "Contact", required: true);
            layout.AddItem("Last name", new EnterpriseInputControl { Text = "Hamilton" }, "Contact");
            layout.AddItem("E-mail", new EnterpriseInputControl { PlaceholderText = "name@company.com" }, "Contact", description: "Used for invoices");
            var plan = new EnterpriseDropDownControl();
            plan.SetItems(new[] { "Starter", "Business" });
            plan.SelectedIndex = 1;
            layout.AddItem("Plan", plan, "Subscription");
            layout.AddItem("Renew", new EnterpriseCheckControl { Text = "Renew every year", Checked = true }, "Subscription");
            Save(layout, "datalayout");

            var cards = Ui.Host(new EnterpriseCardViewControl { TitleField = "Customer", SubtitleField = "Region", BadgeField = "Status", CheckBoxes = true }, 820, 420);
            cards.Fields.Add(new CardField("Product"));
            cards.Fields.Add(new CardField("Total", format: "C0"));
            cards.DataSource = rows;
            cards.SelectCard(cards.CardAt(new Point(40, 40))!);
            Save(cards, "cardview");

            var filter = Ui.Host(new EnterpriseFilterViewControl(), 300, 520);
            filter.AddListFacet("Region");
            filter.AddListFacet("Status");
            filter.AddRangeFacet("Total");
            filter.DataSource = rows;
            filter.ToggleValue("Region", "EMEA", true);
            Save(filter, "filterview");

            var dataFilter = Ui.Host(new EnterpriseDataFilterControl { ItemType = typeof(Row) }, 820, 320);
            var condition = dataFilter.AddCondition();
            condition.Field = "Region";
            condition.Value = "EMEA";
            var group = dataFilter.AddGroup();
            group.Children.Clear();
            group.AddCondition("Total", EnterpriseUI.Data.FilterOperator.IsGreaterThan, "2000");
            group.AddCondition("Qty", EnterpriseUI.Data.FilterOperator.IsLessThan, "4");
            dataFilter.Load(dataFilter.ToDescriptor());
            Save(dataFilter, "datafilter");

            var pivot = Ui.Host(new EnterprisePivotGridControl(), 820, 380);
            pivot.RowFields.Add("Region");
            pivot.RowFields.Add("Product");
            pivot.ColumnFields.Add("Status");
            pivot.Values.Add(new PivotValueField("Total", EnterpriseUI.Data.AggregateFunction.Sum, "Revenue", "C0"));
            pivot.DataSource = rows;
            pivot.SelectionChanged += (_, _) => { };
            Ui.Key(pivot, Keys.Down);
            Save(pivot, "pivotgrid");

            var map = Ui.Host(new EnterpriseTreeMapControl { ValueFormat = "C0" }, 760, 400);
            map.Bind(rows, "Customer", "Total", "Qty", "Region", "Product");
            Save(map, "treemap-category");
            map.ColorMode = TreeMapColorMode.Sequential;
            Save(map, "treemap-sequential");

            var heat = Ui.Host(new EnterpriseHeatMapControl { Title = "Revenue by region and product", ValueFormat = "C0" }, 760, 300);
            heat.Bind(rows, "Region", "Product", "Total");
            Save(heat, "heatmap");

            var virtualGrid = Ui.Host(new EnterpriseVirtualGridControl(), 760, 300);
            virtualGrid.SelectCell(12, 2);
            Save(virtualGrid, "virtualgrid");

            var grid = CreateGrid(40);
            Save(grid, "gridview");
            grid.FindForm()?.Dispose();

            foreach (var control in new Control[] { list, checks, view, tree, props, entry, layout, cards, filter, dataFilter, pivot, map, heat, virtualGrid }) Ui.Dispose(control);
        });
    }

    [Fact]
    public void AllDataControlsRenderInDarkAndHighContrastThemes()
    {
        RunSta(() =>
        {
            var themes = EnterpriseUI.Theming.ThemeManager.Instance.AvailableThemes.ToList();
            var original = EnterpriseUI.Theming.ThemeManager.Instance.CurrentTheme;
            try
            {
                foreach (var theme in themes)
                {
                    EnterpriseUI.Theming.ThemeManager.Instance.ApplyTheme(theme.Name);
                    var list = Ui.Host(new EnterpriseListControl(), 260, 160);
                    list.AddRange(new object[] { "One", "Two", "Three" });
                    list.SelectedIndex = 1;
                    var tree = Ui.Host(new EnterpriseTreeViewControl { CheckBoxes = true }, 260, 160);
                    tree.Nodes.Add("Root").Nodes.Add("Child");
                    var heat = Ui.Host(new EnterpriseHeatMapControl(), 360, 200);
                    heat.SetMatrix(new[] { "A", "B" }, new[] { "X", "Y", "Z" }, new double?[,] { { 1, 2, 3 }, { 4, null, 6 } });
                    foreach (var control in new Control[] { list, tree, heat })
                    {
                        using var bitmap = Ui.Render(control);
                        Assert.True(Ui.DistinctColors(bitmap) > 4, $"{theme.Name}: {control.GetType().Name}");
                        Ui.Dispose(control);
                    }
                }
            }
            finally
            {
                EnterpriseUI.Theming.ThemeManager.Instance.ApplyTheme(original.Name);
            }
        });
    }
}

public sealed class EntryProfile
{
    [System.ComponentModel.DataAnnotations.Display(Name = "Full name", GroupName = "Identity", Order = 1)]
    [System.ComponentModel.DataAnnotations.Required]
    public string Name { get; set; } = "Alexander Hamilton";

    [System.ComponentModel.DataAnnotations.Display(GroupName = "Identity", Order = 2)]
    [System.ComponentModel.DataAnnotations.EmailAddress]
    public string Email { get; set; } = "not-an-email";

    [System.ComponentModel.DataAnnotations.Display(GroupName = "Employment", Order = 3)]
    [System.ComponentModel.DataAnnotations.Range(18, 70)]
    public int Age { get; set; } = 34;

    [System.ComponentModel.DataAnnotations.Display(GroupName = "Employment", Order = 4)]
    public DayOfWeek PreferredDay { get; set; } = DayOfWeek.Tuesday;

    [System.ComponentModel.DataAnnotations.Display(GroupName = "Employment", Order = 5)]
    public bool Remote { get; set; } = true;
}
