using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Grids;
using EnterpriseUI.Theming;
using EnterpriseUI.Theming.Themes;
using Xunit;

namespace EnterpriseUI.Tests.Unit;

public class DataGridAndThemesTests
{
    public class SampleRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    [Fact]
    public async Task DataGridMultiSortAndUndoRedoWorksCorrectly()
    {
        var grid = new DataGrid<SampleRow>();
        grid.Columns.Add(new GridColumn { Field = "Name", Title = "Name" });
        grid.Columns.Add(new GridColumn { Field = "Amount", Title = "Amount" });

        var list = new List<SampleRow>
        {
            new() { Id = 1, Name = "Beta", Amount = 200m },
            new() { Id = 2, Name = "Alpha", Amount = 500m },
            new() { Id = 3, Name = "Alpha", Amount = 100m }
        };
        grid.DataSource = list;
        await grid.RefreshDataAsync();

        // Multi-sort: Name ASC (index 0), Amount DESC (index 1)
        grid.CycleSort("Name", allowMultiSort: false);
        grid.CycleSort("Amount", allowMultiSort: true);
        grid.CycleSort("Amount", allowMultiSort: true); // Second cycle -> Descending

        Assert.Equal(ListSortDirection.Ascending, grid.Columns[0].SortDirection);
        Assert.Equal(0, grid.Columns[0].SortIndex);
        Assert.Equal(ListSortDirection.Descending, grid.Columns[1].SortDirection);
        Assert.Equal(1, grid.Columns[1].SortIndex);

        // Cell Edit + Undo/Redo
        var target = list[0];
        Assert.True(grid.EditCell(target, "Name", "Gamma"));
        Assert.Equal("Gamma", target.Name);
        Assert.True(grid.CanUndo);

        Assert.True(grid.UndoLastEdit());
        Assert.Equal("Beta", target.Name);
        Assert.True(grid.CanRedo);

        Assert.True(grid.RedoLastEdit());
        Assert.Equal("Gamma", target.Name);
    }

    [Fact]
    public void DataGridLayoutProfileSavesAndRestoresColumnState()
    {
        var grid = new DataGrid<SampleRow>();
        grid.Columns.Add(new GridColumn { Field = "Id", Title = "ID", Width = 80 });
        grid.Columns.Add(new GridColumn { Field = "Name", Title = "Name", Width = 150 });

        grid.PinColumn("Id", ColumnPinPosition.Left);
        var profile = grid.CreateLayoutProfile("SavedLayout");

        // Mutate state
        grid.PinColumn("Id", ColumnPinPosition.None);
        grid.SetColumnVisibility("Name", false);
        Assert.False(grid.Columns[1].Visible);

        // Restore profile
        grid.ApplyLayoutProfile(profile);
        Assert.Equal(ColumnPinPosition.Left, grid.Columns[0].PinPosition);
        Assert.True(grid.Columns[1].Visible);
    }

    [Fact]
    public void GridFormatRuleEvaluatorMatchesConditionsAndHonorsDisabledRules()
    {
        var rule = new GridFormatRule
        {
            Operator = FormatConditionOperator.Between,
            Value1 = 10,
            Value2 = 20
        };

        Assert.True(GridFormatRuleEvaluator.Matches(rule, 10m));
        Assert.True(GridFormatRuleEvaluator.Matches(rule, 20m));
        Assert.False(GridFormatRuleEvaluator.Matches(rule, 21m));

        rule.Enabled = false;
        Assert.False(GridFormatRuleEvaluator.Matches(rule, 15m));
    }

    [Fact]
    public void GridFormatRuleEvaluatorNormalizesNumericRangesAndClampsValues()
    {
        Assert.True(GridFormatRuleEvaluator.TryGetNormalizedValue(25m, 0, 100, out var midpoint));
        Assert.Equal(0.25, midpoint);

        Assert.True(GridFormatRuleEvaluator.TryGetNormalizedValue(120m, 0, 100, out var clamped));
        Assert.Equal(1, clamped);

        Assert.False(GridFormatRuleEvaluator.TryGetNormalizedValue(50m, 10, 10, out _));
        Assert.False(GridFormatRuleEvaluator.TryGetNormalizedValue("not numeric", 0, 100, out _));
    }

    [Fact]
    public void GridFormatRuleEvaluatorLeavesRankBasedOperatorsUnsupported()
    {
        var rule = new GridFormatRule
        {
            Operator = FormatConditionOperator.TopN,
            Value1 = 5
        };

        Assert.False(GridFormatRuleEvaluator.Matches(rule, 100m));
    }

    [Fact]
    public void WindowsAndHighContrastThemesProvideCompleteTokens()
    {
        var winLight = BuiltInThemes.CreateWindows(ColorScheme.Light);
        var winDark = BuiltInThemes.CreateWindows(ColorScheme.Dark);
        var hcBlack = BuiltInThemes.CreateHighContrast(isDark: true);
        var hcWhite = BuiltInThemes.CreateHighContrast(isDark: false);

        Assert.Equal("Windows-Light", winLight.Name);
        Assert.Equal("Windows-Dark", winDark.Name);
        Assert.Equal("HighContrast-Black", hcBlack.Name);
        Assert.Equal("HighContrast-White", hcWhite.Name);
        Assert.Equal("#000000", hcBlack.GetToken(DesignTokens.Background));
        Assert.Equal("#ffffff", hcWhite.GetToken(DesignTokens.Background));
    }
}

