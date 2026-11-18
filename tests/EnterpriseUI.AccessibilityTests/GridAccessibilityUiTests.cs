using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Grids;
using EnterpriseUI.Tests.Support;
using EnterpriseUI.Theming;
using EnterpriseUI.Theming.Themes;
using EnterpriseUI.WinForms;
using Xunit;
using static EnterpriseUI.Tests.Support.WinFormsTestSupport;

namespace EnterpriseUI.Tests.Accessibility;

public class CustomControlAccessibilityTests
{
    private sealed class PropertyProfile
    {
        [System.ComponentModel.DataAnnotations.Display(Name = "Email", GroupName = "Identity")]
        public string Email { get; set; } = "not-an-email";
    }

    private sealed record PivotSample(string Region, int Amount);
    private sealed record CardSample(string Name, string Status);

    [Fact]
    public void ListItemsExposeNamesSelectionAndCheckedState()
    {
        RunSta(() =>
        {
            using var list = new EnterpriseListControl();
            list.AddRange(new object[] { "First", "Second" });
            var accessibleList = list.AccessibilityObject;

            Assert.Equal(2, accessibleList.GetChildCount());
            Assert.Equal("First", accessibleList.GetChild(0)!.Name);
            list.SelectedIndex = 1;
            Assert.True(accessibleList.GetChild(1)!.State.HasFlag(AccessibleStates.Selected));

            using var checks = new EnterpriseCheckedListBoxControl();
            checks.AddRange(new object[] { "Read", "Write" });
            checks.SetItemChecked(0, true);
            var checkedItem = checks.AccessibilityObject.GetChild(0)!;
            Assert.Equal("Read", checkedItem.Name);
            Assert.True(checkedItem.State.HasFlag(AccessibleStates.Checked));
            Assert.Equal("Uncheck", checkedItem.DefaultAction);
        });
    }

    [Fact]
    public void TreeNodesExposeHierarchyExpansionAndCheckState()
    {
        RunSta(() =>
        {
            using var tree = new EnterpriseTreeViewControl { CheckBoxes = true };
            var root = tree.Nodes.Add("Root");
            var child = root.Nodes.Add("Child");
            var accessibleRoot = tree.AccessibilityObject.GetChild(0)!;

            Assert.Equal(AccessibleRole.OutlineItem, accessibleRoot.Role);
            Assert.Equal("Root", accessibleRoot.Name);
            Assert.True(accessibleRoot.State.HasFlag(AccessibleStates.Collapsed));
            Assert.Equal("Expand", accessibleRoot.DefaultAction);

            accessibleRoot.DoDefaultAction();
            Assert.True(root.Expanded);
            Assert.True(accessibleRoot.State.HasFlag(AccessibleStates.Expanded));

            var accessibleChild = accessibleRoot.GetChild(0)!;
            Assert.Equal("Child", accessibleChild.Name);
            child.Checked = true;
            Assert.True(accessibleChild.State.HasFlag(AccessibleStates.Checked));
            accessibleChild.DoDefaultAction();
            Assert.Same(child, tree.SelectedNode);
            Assert.True(accessibleChild.State.HasFlag(AccessibleStates.Selected));
        });
    }

    [Fact]
    public void VirtualGridExposesHeadersVisibleRowsAndCellValues()
    {
        RunSta(() =>
        {
            using var grid = new EnterpriseVirtualGridControl { RowCount = 3, ColumnCount = 2, UseSampleData = false };
            grid.ColumnTitles.AddRange(new[] { "Name", "Amount" });
            grid.SetCellValue(0, 0, "Alice");
            grid.SetCellValue(0, 1, 125m);
            var accessible = grid.AccessibilityObject;

            Assert.Equal(AccessibleRole.Table, accessible.Role);
            Assert.Equal(4, accessible.GetChildCount());
            var headers = accessible.GetChild(0)!;
            Assert.Equal("Name", headers.GetChild(0)!.Name);
            var firstRow = accessible.GetChild(1)!;
            Assert.Equal("Alice", firstRow.GetChild(0)!.Value);
            Assert.Equal("125", firstRow.GetChild(1)!.Value);

            grid.SelectCell(0, 1);
            Assert.True(firstRow.GetChild(1)!.State.HasFlag(AccessibleStates.Selected));
        });
    }

    [Fact]
    public void PropertyGridExposesPropertyRowsAndValues()
    {
        RunSta(() =>
        {
            using var grid = new EnterprisePropertyGridControl { SelectedObject = new PropertyProfile() };
            var accessible = grid.AccessibilityObject;
            var emailRow = Enumerable.Range(0, accessible.GetChildCount())
                .Select(accessible.GetChild)
                .First(row => row?.Name == "Email")!;

            Assert.Equal(AccessibleRole.Table, accessible.Role);
            Assert.Equal(AccessibleRole.Row, emailRow.Role);
            Assert.Equal(2, emailRow.GetChildCount());
            Assert.Equal("Email", emailRow.GetChild(0)!.Name);
            Assert.Equal("not-an-email", emailRow.GetChild(1)!.Value);

            emailRow.Select(AccessibleSelection.TakeSelection);
            Assert.Equal("Email", grid.SelectedProperty?.Name);
            Assert.True(emailRow.State.HasFlag(AccessibleStates.Selected));
        });
    }

    [Fact]
    public void PivotGridExposesHeadersAndFormattedResultValues()
    {
        RunSta(() =>
        {
            using var pivot = new EnterprisePivotGridControl();
            pivot.RowFields.Add(nameof(PivotSample.Region));
            pivot.Values.Add(new PivotValueField(nameof(PivotSample.Amount)));
            pivot.DataSource = new[] { new PivotSample("EMEA", 5), new PivotSample("AMER", 9) };
            var accessible = pivot.AccessibilityObject;
            var result = pivot.Result!;

            Assert.Equal(AccessibleRole.Table, accessible.Role);
            Assert.Equal(result.Rows.Count + 1, accessible.GetChildCount());
            var headers = accessible.GetChild(0)!;
            Assert.Equal("Region", headers.GetChild(0)!.Name);

            var row = accessible.GetChild(1)!;
            Assert.Equal(2, row.GetChildCount());
            Assert.Equal(result.Rows[0].Labels[0], row.GetChild(0)!.Value);
            Assert.Equal(result.FormatValue(0, result.Rows[0].Values[0]), row.GetChild(1)!.Value);
        });
    }

    [Fact]
    public void ListViewExposesDetailsCellsSelectionAndCheckedState()
    {
        RunSta(() =>
        {
            using var list = new EnterpriseListViewControl { View = ListViewMode.Details, CheckBoxes = true };
            list.AddColumn("Name", 140);
            list.AddColumn("Status", 100);
            var item = list.Items.Add("Alice", "Active");
            item.Checked = true;
            var accessible = list.AccessibilityObject;

            Assert.Equal(AccessibleRole.Table, accessible.Role);
            Assert.Equal(2, accessible.GetChildCount());
            Assert.Equal("Name", accessible.GetChild(0)!.GetChild(0)!.Name);
            var row = accessible.GetChild(1)!;
            Assert.Equal("Alice", row.Name);
            Assert.Equal("Active", row.GetChild(1)!.Value);
            Assert.True(row.State.HasFlag(AccessibleStates.Checked));

            row.Select(AccessibleSelection.TakeSelection);
            Assert.True(accessible.GetChild(1)!.State.HasFlag(AccessibleStates.Selected));
        });
    }

    [Fact]
    public void HeatMapExposesRowAndColumnLabelsWithCellValues()
    {
        RunSta(() =>
        {
            using var heatMap = new EnterpriseHeatMapControl { Title = "Orders" };
            heatMap.SetMatrix(new[] { "North", "South" }, new[] { "Q1", "Q2" }, new double?[,] { { 12, 18 }, { 7, 9 } });
            var accessible = heatMap.AccessibilityObject;

            Assert.Equal(AccessibleRole.Table, accessible.Role);
            Assert.Contains("2 rows and 2 columns", accessible.Description);
            Assert.Equal(3, accessible.GetChildCount());
            Assert.Equal("Q1", accessible.GetChild(0)!.GetChild(1)!.Name);
            var cell = accessible.GetChild(1)!.GetChild(1)!;
            Assert.Equal("North, Q1", cell.Name);
            Assert.Equal("12", cell.Value);
            cell.Select(AccessibleSelection.TakeSelection);
            Assert.True(cell.State.HasFlag(AccessibleStates.Selected));
        });
    }

    [Fact]
    public void CompositeInputNamesReachTheFocusableChild()
    {
        RunSta(() =>
        {
            using var input = new EnterpriseInputControl { AccessibleName = "Account name", AccessibleDescription = "Name on the customer account" };
            using var textBox = new EnterpriseTextBoxControl { AccessibleName = "Search", AccessibleDescription = "Search customer records" };
            using var combo = new EnterpriseDropDownControl { AccessibleName = "Region" };
            combo.SetItems(new[] { "North", "South" });

            Assert.Equal("Account name", input.InnerTextBox.AccessibleName);
            Assert.Equal("Name on the customer account", input.InnerTextBox.AccessibleDescription);
            Assert.Equal("Search", textBox.Controls.OfType<TextBox>().Single().AccessibleName);
            Assert.Equal(AccessibleRole.ComboBox, combo.AccessibilityObject.Role);
            Assert.Equal("North", (combo.AccessibilityObject.Value = "North") is null ? null : combo.SelectedItem);
        });
    }

    [Fact]
    public void StandaloneScrollBarExposesValueAndKeyboardRangeCommands()
    {
        RunSta(() =>
        {
            using var scrollBar = new EnterpriseScrollBar(Orientation.Vertical) { Minimum = 0, Maximum = 100, LargeChange = 10 };
            var accessible = scrollBar.AccessibilityObject;

            Assert.Equal(AccessibleRole.ScrollBar, accessible.Role);
            Assert.True(scrollBar.TabStop);
            Assert.Equal("Vertical scroll bar", accessible.Name);
            Assert.Contains("0 to 91", accessible.Description);

            scrollBar.Value = 20;
            Assert.Equal("20", accessible.Value);
            Invoke(scrollBar, "OnKeyDown", new KeyEventArgs(Keys.End));
            Assert.Equal(scrollBar.MaxValue, scrollBar.Value);
        });
    }

    [Fact]
    public void CardViewExposesAccessibleTitlesDetailsAndState()
    {
        RunSta(() =>
        {
            var sample = new CardSample("Northwind", "Active");
            using var cards = new EnterpriseCardViewControl
            {
                DataSource = new[] { sample },
                TitleField = nameof(CardSample.Name),
                SubtitleField = nameof(CardSample.Status),
                CheckBoxes = true
            };
            cards.SetChecked(sample, true);
            var accessible = cards.AccessibilityObject;
            var card = accessible.GetChild(0)!;

            Assert.Equal(AccessibleRole.List, accessible.Role);
            Assert.Equal(1, accessible.GetChildCount());
            Assert.Equal("Northwind", card.Name);
            Assert.Contains("Active", card.Value);
            Assert.True(card.State.HasFlag(AccessibleStates.Checked));

            card.Select(AccessibleSelection.TakeSelection);
            Assert.True(cards.SelectedItems.Contains(sample));
            Assert.True(card.State.HasFlag(AccessibleStates.Selected));
        });
    }

    [Fact]
    public void TreeMapExposesHierarchyValuesAndDrillAction()
    {
        RunSta(() =>
        {
            var root = new TreeMapItem("Regions");
            var north = root.Add("North", 12);
            root.Add("South", 8);
            using var map = new EnterpriseTreeMapControl();
            map.SetItems(new[] { root });
            var accessible = map.AccessibilityObject;
            var accessibleRoot = accessible.GetChild(0)!;

            Assert.Equal(AccessibleRole.Graphic, accessible.Role);
            Assert.Equal("Regions", accessibleRoot.Name);
            Assert.Equal("20", accessibleRoot.Value);
            Assert.Equal("Drill into", accessibleRoot.DefaultAction);

            accessibleRoot.DoDefaultAction();
            Assert.Same(root, map.CurrentItem);
            var accessibleNorth = accessible.GetChild(0)!;
            Assert.Equal("North", accessibleNorth.Name);
            Assert.Equal("12", accessibleNorth.Value);
            accessibleNorth.Select(AccessibleSelection.TakeSelection);
            Assert.Same(north, map.SelectedItem);
            Assert.True(accessibleNorth.State.HasFlag(AccessibleStates.Selected));
        });
    }

    [Fact]
    public void FilterViewExposesFacetValuesAndSelectionActions()
    {
        RunSta(() =>
        {
            using var filters = new EnterpriseFilterViewControl
            {
                DataSource = new[] { new PivotSample("EMEA", 5), new PivotSample("AMER", 9) }
            };
            var facet = filters.AddListFacet(nameof(PivotSample.Region));
            var accessible = filters.AccessibilityObject;
            var value = Enumerable.Range(0, accessible.GetChildCount())
                .Select(accessible.GetChild)
                .First(row => row?.Name == "EMEA")!;

            Assert.Equal(AccessibleRole.Grouping, accessible.Role);
            Assert.Equal(AccessibleRole.CheckButton, value.Role);
            Assert.Equal("Select", value.DefaultAction);
            value.DoDefaultAction();
            Assert.Contains("EMEA", facet.Selected);
            Assert.True(value.State.HasFlag(AccessibleStates.Checked));
        });
    }

    [Fact]
    public void CheckControlExposesCheckedStateAndAccessibleDefaultAction()
    {
        RunSta(() =>
        {
            using var check = new EnterpriseCheckControl { Text = "Enable reports" };
            var accessible = check.AccessibilityObject;

            Assert.Equal(AccessibleRole.CheckButton, accessible.Role);
            Assert.False(accessible.State.HasFlag(AccessibleStates.Checked));
            Assert.Equal("Check", accessible.DefaultAction);

            accessible.DoDefaultAction();

            Assert.True(check.Checked);
            Assert.True(accessible.State.HasFlag(AccessibleStates.Checked));
            Assert.Equal("Uncheck", accessible.DefaultAction);
        });
    }
}

public class AccessibleTreeTests
{
    [Fact]
    public void TheGridIsATableWithAHeaderRowAndOneRowPerVisibleDataRow()
    {
        RunSta(() =>
        {
            using var grid = CreateGrid(40);
            var accessible = grid.AccessibilityObject;

            Assert.Equal(AccessibleRole.Table, accessible.Role);
            Assert.StartsWith("Test rows, 40 rows, 7 columns", accessible.Name);
            Assert.NotNull(accessible.Description);

            var children = accessible.GetChildCount();
            Assert.True(children >= 8 && children <= 41, $"{children} children");

            var header = accessible.GetChild(0)!;
            Assert.Equal(AccessibleRole.Row, header.Role);
            Assert.Equal(7, header.GetChildCount());
            Assert.Equal(AccessibleRole.ColumnHeader, header.GetChild(1)!.Role);
            Assert.Contains("Name", header.GetChild(1)!.Name);

            var row = accessible.GetChild(1)!;
            Assert.Equal(7, row.GetChildCount());
            Assert.Equal(AccessibleRole.Cell, row.GetChild(1)!.Role);
            Assert.Equal("Name 0001", row.GetChild(1)!.Value);
        });
    }

    [Fact]
    public void NamesReflectSortFilterSelectionAndFocusState()
    {
        RunSta(() =>
        {
            using var grid = CreateGrid(40, g => g.SelectionMode = GridSelectionMode.MultipleRows);
            grid.SortBy("Name", System.ComponentModel.ListSortDirection.Descending);
            grid.FilterBy("Region", EnterpriseUI.Data.FilterOperator.IsEqualTo, "EMEA");
            PumpUntil(() => grid.TotalItemsCount < 40);
            grid.Selection.Click(1, 1);

            var accessible = grid.AccessibilityObject;
            Assert.Contains("sorted by Name descending", accessible.Name);
            Assert.Contains("1 filter applied", accessible.Name);

            var header = accessible.GetChild(0)!;
            Assert.Contains("sorted descending", header.GetChild(1)!.Name);
            Assert.Contains("filtered", header.GetChild(2)!.Name);

            var row = accessible.GetChild(2)!;
            Assert.Contains("selected", row.Name);
            Assert.True(row.State.HasFlag(AccessibleStates.Selected));
            Assert.True(row.GetChild(1)!.State.HasFlag(AccessibleStates.Selected));
        });
    }

    [Fact]
    public void ValidationErrorsAreTextInTheCellNameNotOnlyAColorMarker()
    {
        RunSta(() =>
        {
            using var grid = CreateGrid(10);
            var name = grid.Columns.First(c => c.Field == "Name");
            name.ValidationRules.Add(new RequiredRule { Message = "Name is required" });
            Assert.True(grid.TryGetItemAt(0, out var item));

            Assert.False(grid.TryEditCell(item, "Name", string.Empty).Success);
            grid.Invalidate();

            var cell = grid.AccessibilityObject.GetChild(1)!.GetChild(1)!;
            Assert.Contains("error: Name is required", cell.Name);
        });
    }

    [Fact]
    public void ReadOnlyCellsAreFlaggedAndEditableCellsOfferAnEditAction()
    {
        RunSta(() =>
        {
            using var grid = CreateGrid(10);
            var id = grid.AccessibilityObject.GetChild(1)!.GetChild(0)!;
            var nameCell = grid.AccessibilityObject.GetChild(1)!.GetChild(1)!;

            Assert.True(id.State.HasFlag(AccessibleStates.ReadOnly));
            Assert.Null(id.DefaultAction);
            Assert.False(nameCell.State.HasFlag(AccessibleStates.ReadOnly));
            Assert.Equal("Edit", nameCell.DefaultAction);

            nameCell.DoDefaultAction();
            Assert.True(grid.IsEditing);
        });
    }

    [Fact]
    public void GroupRowsExposeExpandedStateAndTheDefaultActionTogglesIt()
    {
        RunSta(() =>
        {
            using var grid = CreateGrid(30);
            grid.GroupBy("Region");
            PumpUntil(() => grid.TotalItemsCount > 30);
            var before = grid.TotalItemsCount;

            var group = grid.AccessibilityObject.GetChild(1)!;
            Assert.True(group.State.HasFlag(AccessibleStates.Expanded));
            Assert.Equal("Collapse", group.DefaultAction);
            Assert.Contains("group level 1, expanded", group.Name);

            group.DoDefaultAction();
            PumpUntil(() => grid.TotalItemsCount < before);
            Assert.True(grid.AccessibilityObject.GetChild(1)!.State.HasFlag(AccessibleStates.Collapsed));
        });
    }

    [Fact]
    public void CellBoundsAreOnScreenInsideTheGridAndHitTestFindsTheCell()
    {
        RunSta(() =>
        {
            using var grid = CreateGrid(20);
            var accessible = grid.AccessibilityObject;
            var gridBounds = grid.RectangleToScreen(grid.ClientRectangle);
            var cell = accessible.GetChild(3)!.GetChild(1)!;

            Assert.False(cell.Bounds.IsEmpty);
            Assert.True(gridBounds.Contains(cell.Bounds), $"{cell.Bounds} not inside {gridBounds}");

            var center = new Point(cell.Bounds.X + cell.Bounds.Width / 2, cell.Bounds.Y + cell.Bounds.Height / 2);
            var hit = accessible.HitTest(center.X, center.Y);
            Assert.Equal(cell.Name, hit!.Name);
        });
    }

    [Fact]
    public void GetFocusedReturnsTheFocusCell()
    {
        RunSta(() =>
        {
            using var grid = CreateGrid(20);
            grid.Selection.SetFocus(4, 2);

            var focused = grid.AccessibilityObject.GetFocused();
            Assert.NotNull(focused);
            Assert.Contains("row 5, column 3 of 7", focused!.Name);
        });
    }
}

public class AnnouncementTests
{
    [Fact]
    public void SortFilterAndPageChangesAreAnnouncedToAssistiveTechnology()
    {
        RunSta(() =>
        {
            using var grid = CreateGrid(95, g => { g.PagingMode = GridPagingMode.Client; g.PageSize = 10; });
            var messages = new List<string>();
            grid.Announcement += (_, e) => messages.Add(e.Message);

            grid.SortBy("Name", System.ComponentModel.ListSortDirection.Ascending);
            grid.GoToPage(2);
            grid.FilterBy("Region", EnterpriseUI.Data.FilterOperator.IsEqualTo, "AMER");
            PumpUntil(() => messages.Count >= 3);

            Assert.Contains(messages, m => m.StartsWith("Sorted by Name ascending"));
            Assert.Contains(messages, m => m.StartsWith("Page 3 of"));
            Assert.Contains(messages, m => m.Contains("filter", StringComparison.OrdinalIgnoreCase));
        });
    }
}

public class KeyboardOnlyTests
{
    [Fact]
    public void EveryPrimaryTaskCanBeDoneWithoutAMouse()
    {
        RunSta(() =>
        {
            using var grid = CreateGrid(60, g => g.SelectionMode = GridSelectionMode.MultipleRows);
            grid.Focus();
            grid.Selection.SetFocus(0, 0);

            // navigate (arrow keys select the row) and toggle the selection with Space
            grid.Key(Keys.Down);
            grid.Key(Keys.Right);
            Assert.Contains(1, grid.SelectedIndices);
            grid.Key(Keys.Space);
            Assert.DoesNotContain(1, grid.SelectedIndices);
            grid.Key(Keys.Space);
            Assert.Contains(1, grid.SelectedIndices);

            // edit
            grid.Key(Keys.F2);
            var editor = OpenEditor(grid)!;
            editor.Text = "Keyboard";
            PressInEditor(editor, Keys.Enter);
            Assert.True(grid.TryGetItemAt(1, out var item));
            Assert.Equal("Keyboard", item.Name);

            // undo
            grid.Key(Keys.Z | Keys.Control);
            Assert.Equal("Name 0002", item.Name);

            // search
            grid.Key(Keys.F | Keys.Control);
            Assert.True(grid.ShowSearchPanel);
            grid.Key(Keys.Escape);

            // page navigation and group expansion
            grid.GroupBy("Region");
            PumpUntil(() => grid.TotalItemsCount > 60);
            grid.Selection.SetFocus(0, 0);
            var expanded = grid.TotalItemsCount;
            grid.Key(Keys.Left | Keys.Alt);
            PumpUntil(() => grid.TotalItemsCount < expanded);
            grid.Key(Keys.Right | Keys.Alt);
            PumpUntil(() => grid.TotalItemsCount == expanded);
        });
    }

    [Fact]
    public void HomeEndAndPageKeysNavigateTheWholeGrid()
    {
        RunSta(() =>
        {
            using var grid = CreateGrid(200);
            grid.Selection.SetFocus(0, 0);

            grid.Key(Keys.End | Keys.Control);
            Assert.Equal(199, grid.Selection.FocusedRow);
            grid.Key(Keys.Home | Keys.Control);
            Assert.Equal(0, grid.Selection.FocusedRow);
            grid.Key(Keys.PageDown);
            Assert.True(grid.Selection.FocusedRow >= 5);
            grid.Key(Keys.End);
            Assert.Equal(6, grid.Selection.FocusedColumn);
            grid.Key(Keys.Home);
            Assert.Equal(0, grid.Selection.FocusedColumn);
        });
    }
}

public class ThemeDrivenColorTests
{
    public static IEnumerable<object[]> Themes()
    {
        yield return new object[] { BuiltInThemes.CreateFluent(ColorScheme.Light) };
        yield return new object[] { BuiltInThemes.CreateFluent(ColorScheme.Dark) };
        yield return new object[] { BuiltInThemes.CreateOffice(ColorScheme.Light) };
        yield return new object[] { BuiltInThemes.CreateMaterial(ColorScheme.Dark) };
        yield return new object[] { BuiltInThemes.CreateHighContrast(true) };
        yield return new object[] { BuiltInThemes.CreateHighContrast(false) };
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void SelectedRowsAndSurfaceUseThePaletteDerivedFromTheTheme(ITheme theme)
    {
        RunSta(() =>
        {
            var manager = ThemeManager.Instance;
            var original = manager.CurrentTheme;
            try
            {
                manager.ApplyTheme(theme);
                var palette = GridPalette.FromTheme(theme);
                using var grid = CreateGrid(20, g => g.SelectionMode = GridSelectionMode.SingleRow);
                grid.Selection.Click(2, 1);
                Pump(60);
                using var bitmap = grid.Render();

                var y = grid.S(32) + 2 * grid.S(30) + grid.S(25);
                var selected = Pixel(bitmap, grid.S(870), y);
                var plain = Pixel(bitmap, grid.S(870), grid.S(32) + 6 * grid.S(30) + grid.S(5));

                if (SystemInformation.HighContrast) return; // system colors replace the theme, covered by the palette fallback

                AssertNear(ColorTranslator.FromHtml(palette.SelectionBack), selected);
                Assert.True(GridPalette.ContrastRatio(palette.SelectionText, palette.SelectionBack) >= 4.5);
                Assert.True(GridPalette.ContrastRatio(palette.Text, palette.Surface) >= 4.5);
                Assert.NotEqual(selected.ToArgb(), plain.ToArgb());
            }
            finally
            {
                manager.ApplyTheme(original);
            }
        });
    }

    [Fact]
    public void HighContrastThemesDrawTheBorderInTheTextColor()
    {
        RunSta(() =>
        {
            var manager = ThemeManager.Instance;
            var original = manager.CurrentTheme;
            try
            {
                var theme = BuiltInThemes.CreateHighContrast(true);
                manager.ApplyTheme(theme);
                var palette = GridPalette.FromTheme(theme);
                using var grid = CreateGrid(5);
                using var bitmap = grid.Render();

                if (SystemInformation.HighContrast) return;
                Assert.True(palette.HighContrast);
                AssertNear(ColorTranslator.FromHtml(palette.Text), Pixel(bitmap, grid.S(300), 0));
            }
            finally
            {
                manager.ApplyTheme(original);
            }
        });
    }

    private static void AssertNear(Color expected, Color actual, int tolerance = 6)
    {
        Assert.True(
            Math.Abs(expected.R - actual.R) <= tolerance && Math.Abs(expected.G - actual.G) <= tolerance && Math.Abs(expected.B - actual.B) <= tolerance,
            $"expected {expected} but was {actual}");
    }
}
