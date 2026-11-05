using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnterpriseUI.Data;
using EnterpriseUI.Grids;
using Xunit;
using static EnterpriseUI.Tests.Unit.GridTestSupport;

namespace EnterpriseUI.Tests.Unit;

public class DataGridEditingTests
{
    private static async Task<(DataGrid<Order> Grid, List<Order> Orders)> Create()
    {
        var orders = Orders();
        var grid = OrderGrid(orders);
        await grid.Loaded();
        return (grid, orders);
    }

    [Fact]
    public async Task EditCellConvertsTextToThePropertyTypeAndRecordsUndo()
    {
        var (grid, orders) = await Create();

        Assert.True(grid.EditCell(orders[0], "Quantity", "12"));
        Assert.Equal(12, orders[0].Quantity);
        Assert.True(grid.EditCell(orders[0], "Discount", "5"));
        Assert.Equal(5m, orders[0].Discount);
        Assert.True(grid.EditCell(orders[0], "Discount", ""));
        Assert.Null(orders[0].Discount);
        Assert.True(grid.EditCell(orders[0], "Date", "2025-03-04"));
        Assert.Equal(new DateTime(2025, 3, 4), orders[0].Date);

        Assert.False(grid.EditCell(orders[0], "Quantity", "abc"));
        Assert.Equal(12, orders[0].Quantity);
        Assert.Contains(grid.ValidationSummary, e => e.Field == "Quantity");
        Assert.False(grid.EditCell(orders[0], "Quantity", ""));
        Assert.False(grid.EditCell(orders[0], "NoSuchField", 1));
        Assert.False(grid.EditCell(default!, "Quantity", 1));
    }

    [Fact]
    public async Task UnchangedValuesDoNotCreateUndoStepsAndUndoRedoRestoreValues()
    {
        var (grid, orders) = await Create();

        Assert.True(grid.EditCell(orders[0], "Status", "Open"));
        Assert.False(grid.CanUndo);

        grid.EditCell(orders[0], "Status", "Closed");
        grid.EditCell(orders[0], "Status", "Shipped");
        Assert.True(grid.UndoLastEdit());
        Assert.Equal("Closed", orders[0].Status);
        Assert.True(grid.CanRedo);
        Assert.True(grid.UndoLastEdit());
        Assert.Equal("Open", orders[0].Status);
        Assert.False(grid.CanUndo);
        Assert.False(grid.UndoLastEdit());

        Assert.True(grid.RedoLastEdit());
        Assert.True(grid.RedoLastEdit());
        Assert.Equal("Shipped", orders[0].Status);
        Assert.False(grid.RedoLastEdit());

        grid.UndoLastEdit();
        grid.EditCell(orders[0], "Status", "New");
        Assert.False(grid.CanRedo);
    }

    [Fact]
    public async Task UndoHistoryIsBounded()
    {
        var (grid, orders) = await Create();
        grid.MaxUndoDepth = 3;

        for (var i = 0; i < 10; i++) grid.EditCell(orders[0], "Quantity", 100 + i);

        var undone = 0;
        while (grid.UndoLastEdit()) undone++;
        Assert.Equal(3, undone);
        Assert.Equal(106, orders[0].Quantity);
    }

    [Fact]
    public async Task ReadOnlyAndCalculatedColumnsRefuseEdits()
    {
        var (grid, orders) = await Create();
        grid.Columns.First(c => c.Field == "Region").ReadOnly = true;
        grid.Columns.Add(new GridColumn { Field = "Amount", Expression = "[UnitPrice] * [Quantity]" });

        Assert.False(grid.EditCell(orders[0], "Region", "APAC"));
        Assert.False(grid.EditCell(orders[0], "Amount", 5));
        Assert.Equal("EMEA", orders[0].Region);
    }

    [Fact]
    public async Task RequiredRuleRejectsAndAValidEditClearsTheError()
    {
        var (grid, orders) = await Create();
        grid.Columns.First(c => c.Field == "Status").ValidationRules.Add(new RequiredRule { Message = "Status is mandatory" });
        GridValidationEventArgs? raised = null;
        grid.ValidationFailed += (_, e) => raised = e;

        var result = grid.TryEditCell(orders[0], "Status", " ");

        Assert.False(result.Success);
        Assert.Equal("Status is mandatory", Assert.Single(result.Errors).Message);
        Assert.Equal("Open", orders[0].Status);
        Assert.True(grid.HasErrors);
        Assert.True(grid.HasCellError(orders[0], "Status"));
        Assert.NotNull(raised);
        Assert.Equal("Status is mandatory", Assert.Single(grid.ValidationSummary).Message);
        Assert.Single(grid.GetRowErrors(orders[0]));

        Assert.True(grid.EditCell(orders[0], "Status", "Closed"));
        Assert.False(grid.HasErrors);
        Assert.False(grid.HasCellError(orders[0], "Status"));
    }

    [Fact]
    public async Task InvalidEditsCanBeAcceptedAndFlaggedInstead()
    {
        var (grid, orders) = await Create();
        grid.RejectInvalidEdits = false;
        grid.Columns.First(c => c.Field == "Quantity").ValidationRules.Add(new RangeRule(1, 100));

        var result = grid.TryEditCell(orders[0], "Quantity", 500);

        Assert.True(result.Success);
        Assert.Equal(500, orders[0].Quantity);
        Assert.True(grid.HasCellError(orders[0], "Quantity"));
        Assert.Contains("between 1 and 100", grid.ValidationSummary.Single().Message);

        grid.EditCell(orders[0], "Quantity", 50);
        Assert.False(grid.HasErrors);
    }

    [Fact]
    public async Task WarningsDoNotBlockEditsButAreReported()
    {
        var (grid, orders) = await Create();
        grid.Columns.First(c => c.Field == "UnitPrice").ValidationRules.Add(new RangeRule(0m, 1000m) { Severity = GridValidationSeverity.Warning, Message = "Unusually high" });

        var result = grid.TryEditCell(orders[0], "UnitPrice", 5000);

        Assert.True(result.Success);
        Assert.Equal(GridValidationSeverity.Warning, Assert.Single(result.Errors).Severity);
        Assert.Equal(5000m, orders[0].UnitPrice);
    }

    [Fact]
    public async Task RegexLengthAndCrossFieldRulesValidate()
    {
        var (grid, orders) = await Create();
        grid.Columns.First(c => c.Field == "Status").ValidationRules.Add(new RegexRule("^[A-Z][a-z]+$"));
        grid.Columns.First(c => c.Field == "Customer").ValidationRules.Add(new LengthRule(3, 8));
        grid.Columns.First(c => c.Field == "Discount").ValidationRules.Add(new DelegateRule(ctx =>
            ctx.NewValue is decimal d && d > (decimal)ctx.GetSibling("UnitPrice")! ? "Discount exceeds the price" : null));

        Assert.False(grid.EditCell(orders[0], "Status", "closed"));
        Assert.True(grid.EditCell(orders[0], "Status", "Closed"));
        Assert.False(grid.EditCell(orders[0], "Customer", "ab"));
        Assert.False(grid.EditCell(orders[0], "Customer", "far too long"));
        Assert.True(grid.EditCell(orders[0], "Customer", "Okay"));
        Assert.False(grid.EditCell(orders[0], "Discount", 999));
        Assert.True(grid.EditCell(orders[0], "Discount", 5));
    }

    [Fact]
    public async Task RegexRulesTimeOutInsteadOfHanging()
    {
        var (grid, orders) = await Create();
        orders[0].Status = new string('a', 40) + "!";
        grid.Columns.First(c => c.Field == "Status").ValidationRules.Add(new RegexRule("^(a+)+$", TimeSpan.FromMilliseconds(20)));

        var result = grid.TryEditCell(orders[0], "Status", new string('a', 40) + "!");

        Assert.True(result.Success);
        Assert.Equal(1, grid.ValidateRow(orders[0]).Count(e => e.Field == "Status"));
    }

    [Fact]
    public async Task AsyncRulesOnlyRunThroughTheAsyncApis()
    {
        var (grid, orders) = await Create();
        var calls = 0;
        grid.Columns.First(c => c.Field == "Customer").ValidationRules.Add(new AsyncDelegateRule(async (ctx, ct) =>
        {
            Interlocked.Increment(ref calls);
            await Task.Delay(5, ct);
            return (string?)ctx.NewValue == "Taken" ? "Name already exists" : null;
        }));

        Assert.True(grid.EditCell(orders[0], "Customer", "Taken"));
        Assert.Equal(0, calls);
        grid.UndoLastEdit();

        var rejected = await grid.TryEditCellAsync(orders[0], "Customer", "Taken");
        Assert.False(rejected.Success);
        Assert.Equal("Name already exists", rejected.Errors.Single().Message);
        Assert.Equal("Customer 1", orders[0].Customer);

        var accepted = await grid.TryEditCellAsync(orders[0], "Customer", "Free");
        Assert.True(accepted.Success);
        Assert.Equal("Free", orders[0].Customer);

        orders[1].Customer = "Taken";
        var all = await grid.ValidateAllAsync();
        Assert.Single(all);
        Assert.Single(grid.ValidationSummary);
    }

    [Fact]
    public async Task RowBusinessRulesRevertTheEdit()
    {
        var (grid, orders) = await Create();
        grid.RowValidators.Add(o => o.Quantity > 100 ? new[] { new GridValidationError(o, "Quantity", "Quantity above the limit") } : Array.Empty<GridValidationError>());
        grid.AsyncRowValidators.Add((o, ct) => Task.FromResult<IEnumerable<GridValidationError>>(o.UnitPrice > 900 ? new[] { new GridValidationError(o, "UnitPrice", "Too expensive") } : Array.Empty<GridValidationError>()));

        Assert.False(grid.EditCell(orders[0], "Quantity", 500));
        Assert.Equal(1, orders[0].Quantity);
        Assert.False(grid.CanUndo);

        var async = await grid.TryEditCellAsync(orders[0], "UnitPrice", 1000);
        Assert.False(async.Success);
        Assert.Equal(10m, orders[0].UnitPrice);
        Assert.Equal("Too expensive", async.Errors.Single().Message);
    }

    [Fact]
    public async Task ValidateRowRecordsErrorsForExistingBadData()
    {
        var (grid, orders) = await Create();
        grid.Columns.First(c => c.Field == "Status").ValidationRules.Add(new RequiredRule());
        orders[2].Status = "";

        var errors = grid.ValidateRow(orders[2]);

        Assert.Single(errors);
        Assert.True(grid.HasCellError(orders[2], "Status"));
        grid.ValidateRow(orders[0]);
        Assert.Single(grid.ValidationSummary);
        grid.ClearValidationErrors();
        Assert.False(grid.HasErrors);
    }

    [Fact]
    public async Task TransactionsHoldChangesUntilCommitAndRollBackCompletely()
    {
        var (grid, orders) = await Create();
        grid.BeginTransaction();

        grid.EditCell(orders[0], "Quantity", 10);
        grid.EditCell(orders[0], "Quantity", 20);
        grid.EditCell(orders[1], "Status", "Closed");

        var pending = grid.PendingChanges;
        Assert.Equal(2, pending.Count);
        var quantityChange = pending.Single(p => p.Field == "Quantity");
        Assert.Equal(1, quantityChange.OriginalValue);
        Assert.Equal(20, quantityChange.CurrentValue);
        Assert.True(grid.IsCellDirty(orders[0], "Quantity"));
        Assert.True(grid.IsInTransaction);

        Assert.Equal(3, grid.RollbackTransaction());
        Assert.Equal(1, orders[0].Quantity);
        Assert.Equal("Open", orders[1].Status);
        Assert.False(grid.HasPendingChanges);
        Assert.False(grid.CanUndo);
        Assert.False(grid.IsInTransaction);
    }

    [Fact]
    public async Task CommittedTransactionsRaiseEventAndStayUndoable()
    {
        var (grid, orders) = await Create();
        IReadOnlyList<GridPendingChange>? committed = null;
        grid.ChangesCommitted += (_, e) => committed = e.Changes;
        grid.BeginTransaction();
        grid.EditCell(orders[0], "Quantity", 9);

        var result = grid.CommitTransaction();

        Assert.True(result.Success);
        Assert.Single(committed!);
        Assert.False(grid.HasPendingChanges);
        Assert.False(grid.IsInTransaction);
        Assert.Equal(9, orders[0].Quantity);
        Assert.True(grid.UndoLastEdit());
        Assert.Equal(1, orders[0].Quantity);
    }

    [Fact]
    public async Task CommitFailsWhileRowsAreInvalidAndKeepsTheTransactionOpen()
    {
        var (grid, orders) = await Create();
        grid.RejectInvalidEdits = false;
        grid.Columns.First(c => c.Field == "Quantity").ValidationRules.Add(new RangeRule(1, 10));
        grid.BeginTransaction();
        grid.EditCell(orders[0], "Quantity", 50);

        var result = grid.CommitTransaction();

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
        Assert.True(grid.IsInTransaction);
        Assert.True(grid.HasPendingChanges);

        grid.EditCell(orders[0], "Quantity", 5);
        Assert.True(grid.CommitTransaction().Success);
    }

    [Fact]
    public async Task BatchEditModeKeepsChangesPendingWithoutExplicitTransaction()
    {
        var (grid, orders) = await Create();
        grid.EditMode = GridEditMode.Batch;

        grid.EditCell(orders[0], "Quantity", 9);
        Assert.True(grid.HasPendingChanges);
        Assert.True(grid.IsBatchMode);

        grid.UndoLastEdit();
        Assert.False(grid.HasPendingChanges);

        grid.EditCell(orders[0], "Quantity", 9);
        Assert.True(grid.CommitChanges().Success);
        Assert.False(grid.HasPendingChanges);

        grid.EditMode = GridEditMode.InCell;
        grid.EditCell(orders[0], "Quantity", 3);
        Assert.False(grid.HasPendingChanges);
    }

    [Fact]
    public async Task RowEditSessionsCommitAtomicallyAsOneUndoStep()
    {
        var (grid, orders) = await Create();
        grid.Columns.First(c => c.Field == "Status").ValidationRules.Add(new RequiredRule());
        var session = grid.BeginRowEdit(orders[0]);
        Assert.False(session.IsDirty);

        session.SetValue("Quantity", "7");
        session.SetValue("Status", "");
        Assert.True(session.IsDirty);
        Assert.False(session.TryCommit(out var errors));
        Assert.Single(errors);
        Assert.Equal(1, orders[0].Quantity);

        session.SetValue("Status", "Closed");
        Assert.True(session.TryCommit(out _));
        Assert.Equal(7, orders[0].Quantity);
        Assert.Equal("Closed", orders[0].Status);

        Assert.True(grid.UndoLastEdit());
        Assert.Equal(1, orders[0].Quantity);
        Assert.Equal("Open", orders[0].Status);
        Assert.False(grid.CanUndo);

        session.SetValue("Quantity", "99");
        session.Cancel();
        Assert.False(session.IsDirty);
    }

    [Fact]
    public async Task AddingAndRemovingRowsSupportsUndo()
    {
        var (grid, orders) = await Create();

        Assert.True(grid.AddRow(new Order { Id = 99 }, 0));
        Assert.Equal(7, grid.TotalItemsCount);
        Assert.Equal(99, grid.Ids()[0]);
        Assert.True(grid.UndoLastEdit());
        Assert.Equal(6, grid.TotalItemsCount);
        Assert.True(grid.RedoLastEdit());
        Assert.Equal(7, grid.TotalItemsCount);

        var snapshot = orders.ToList();
        Assert.Equal(2, grid.RemoveRows(new[] { orders[1], orders[2] }));
        Assert.Equal(5, grid.TotalItemsCount);
        Assert.True(grid.UndoLastEdit());
        Assert.Equal(snapshot, orders);

        grid.DataSource = orders.Select(o => o);
        Assert.False(grid.AddRow(new Order { Id = 100 }));
        Assert.Equal(0, grid.RemoveRows(new[] { orders[0] }));
    }

    [Fact]
    public async Task BatchModeTracksStructuralChangesAndRollsThemBack()
    {
        var (grid, orders) = await Create();
        grid.BeginTransaction();

        grid.AddRow(new Order { Id = 99 });
        grid.RemoveRows(new[] { orders[0] });
        Assert.True(grid.HasPendingChanges);

        Assert.Equal(2, grid.RollbackTransaction());
        Assert.Equal(6, orders.Count);
        Assert.Equal(1, orders[0].Id);
    }

    [Fact]
    public async Task RowsCanBeReorderedAndTransferredBetweenGrids()
    {
        var (source, sourceOrders) = await Create();
        var targetOrders = new List<Order>();
        var target = OrderGrid(targetOrders);
        await target.Loaded();

        Assert.True(source.ReorderRow(0, 2));
        Assert.Equal(new[] { 2, 3, 1 }, sourceOrders.Take(3).Select(o => o.Id));
        Assert.True(source.UndoLastEdit());
        Assert.Equal(1, sourceOrders[0].Id);
        Assert.False(source.ReorderRow(0, 99));

        var moved = source.TransferRows(new[] { sourceOrders[0], sourceOrders[1] }, target);
        Assert.Equal(2, moved);
        Assert.Equal(4, sourceOrders.Count);
        Assert.Equal(2, targetOrders.Count);
        Assert.Equal(2, target.TotalItemsCount);

        var copied = source.TransferRows(new[] { sourceOrders[0] }, target, 0, copy: true, clone: o => new Order { Id = o.Id + 1000 });
        Assert.Equal(1, copied);
        Assert.Equal(4, sourceOrders.Count);
        Assert.Equal(1003, targetOrders[0].Id);
    }

    [Fact]
    public async Task EditEventsAndRowStyleCallbacksFire()
    {
        var (grid, orders) = await Create();
        GridCellEditedEventArgs? edited = null;
        grid.CellEdited += (_, e) => edited = e;
        grid.RowStyleNeeded += (_, e) =>
        {
            if (e.Item.Quantity > 3) { e.BackColorHex = "#ffeeee"; e.Bold = true; }
        };

        grid.EditCell(orders[0], "Quantity", 4);

        Assert.Equal("Quantity", edited!.Field);
        Assert.Equal(1, edited.OldValue);
        Assert.Equal(4, edited.NewValue);
        Assert.Equal("#ffeeee", grid.GetRowStyle(orders[0], 0)!.BackColorHex);
        Assert.Null(grid.GetRowStyle(orders[1], 1)!.BackColorHex);
    }
}

public class DataGridSummaryAndCalculationTests
{
    [Fact]
    public async Task FooterSummariesCoverAllMatchingRowsAndFollowFilters()
    {
        var grid = OrderGrid(Orders());
        grid.Columns.First(c => c.Field == "Quantity").FooterSummaries.Add(new GridColumnSummary(AggregateFunction.Sum));
        var price = grid.Columns.First(c => c.Field == "UnitPrice");
        price.FooterSummaries.Add(new GridColumnSummary(AggregateFunction.Average, "Avg {0:N0}"));
        price.FooterSummaries.Add(new GridColumnSummary { Custom = values => values.Count, Label = "Rows" });
        await grid.Loaded();

        Assert.Equal("Sum: 21", grid.GetFooterValues("Quantity").Single().Text);
        Assert.StartsWith("Avg 35", grid.GetFooterValues("UnitPrice")[0].Text);
        Assert.Equal("Rows: 6", grid.GetFooterValues("UnitPrice")[1].Text);

        grid.FilterBy("Region", FilterOperator.IsEqualTo, "EMEA");
        Assert.Equal("Sum: 10", grid.GetFooterValues("Quantity").Single().Text);
        Assert.Equal(3, grid.FooterValues.Count);
    }

    [Fact]
    public async Task FooterSummariesWorkWithClientPagingAndGrouping()
    {
        var grid = OrderGrid(Orders());
        grid.Columns.First(c => c.Field == "Quantity").FooterSummaries.Add(new GridColumnSummary(AggregateFunction.Sum));
        grid.PagingMode = GridPagingMode.Client;
        grid.PageSize = 2;
        await grid.Loaded();
        Assert.Equal("Sum: 21", grid.FooterValues.Single().Text);

        grid.GroupBy("Region");
        Assert.Equal("Sum: 21", grid.FooterValues.Single().Text);
    }

    [Fact]
    public async Task CalculatedColumnsSortFilterGroupAndSummarize()
    {
        var grid = OrderGrid(Orders());
        var amount = new GridColumn { Field = "Amount", Title = "Amount", Expression = "[UnitPrice] * [Quantity]" };
        amount.FooterSummaries.Add(new GridColumnSummary(AggregateFunction.Sum));
        grid.Columns.Add(amount);
        await grid.Loaded();

        Assert.Equal(160m, grid.GetCellValue(grid.Rows()[3].Item, amount));

        grid.SortBy("Amount", ListSortDirection.Descending);
        Assert.Equal(new[] { 6, 5, 4, 3, 2, 1 }, grid.Ids());
        Assert.Equal("Sum: 910", grid.FooterValues.Single().Text);

        grid.ClearSort();
        grid.FilterBy("Amount", FilterOperator.IsGreaterThan, 100);
        Assert.Equal(new[] { 4, 5, 6 }, grid.Ids());
        Assert.True(grid.HasFilter("Amount"));

        grid.ClearFilters();
        grid.ApplyExcelFilter("Amount", new object[] { 10m, 40m });
        Assert.Equal(new[] { 1, 2 }, grid.Ids());
        Assert.Contains(10m, grid.GetDistinctValues("Amount"));

        grid.ClearFilters();
        grid.GroupBy("Amount", v => (decimal)v! >= 100 ? "Big" : "Small");
        var headers = grid.Rows().Where(r => r.Kind == GridRowKind.GroupHeader).ToList();
        Assert.Equal(new object[] { "Big", "Small" }, headers.Select(h => h.Group!.Key!));
        Assert.Equal(3, headers[0].Group!.ItemCount);

        grid.ClearGroups();
        grid.SearchPanelText = "250";
        Assert.Equal(new[] { 5 }, grid.Ids());
    }

    [Fact]
    public async Task InvalidExpressionsYieldNullAndAnErrorMessageWithoutThrowing()
    {
        var grid = OrderGrid(Orders());
        var broken = new GridColumn { Field = "Broken", Expression = "[UnitPrice] *" };
        grid.Columns.Add(broken);
        await grid.Loaded();

        Assert.Null(broken.GetValue(grid.Rows()[0].Item));
        Assert.NotNull(broken.ExpressionError);
        grid.SortBy("Broken", ListSortDirection.Ascending);
        Assert.Equal(6, grid.TotalItemsCount);

        broken.Expression = "[UnitPrice] + 1";
        Assert.Null(broken.ExpressionError);
        Assert.Equal(11m, broken.GetValue(grid.Rows()[0].Item));
    }

    [Fact]
    public async Task RunningTotalsAccumulateInSortOrder()
    {
        var grid = OrderGrid(Orders());
        var running = new GridColumn { Field = "RunningQty", Title = "Running", RunningTotalOf = "Quantity" };
        grid.Columns.Add(running);
        await grid.Loaded();

        Assert.Equal(new decimal?[] { 1, 3, 6, 10, 15, 21 }, grid.Rows().Select(r => grid.GetRunningTotal(r, running)));
        Assert.Equal(21m, grid.GetRowCellValue(grid.Rows()[5], running));

        grid.SortBy("Quantity", ListSortDirection.Descending);
        Assert.Equal(new decimal?[] { 6, 11, 15, 18, 20, 21 }, grid.Rows().Select(r => grid.GetRunningTotal(r, running)));
        Assert.False(running.IsEditable);
    }
}

public class DataGridSearchAndRealtimeTests
{
    [Fact]
    public async Task HighlightSearchCountsMatchesWithoutHidingRowsAndNavigates()
    {
        var grid = OrderGrid(Orders());
        grid.SearchMode = GridSearchMode.Highlight;
        await grid.Loaded();
        var requested = new List<int>();
        grid.ScrollToRowRequested += (_, row) => requested.Add(row);

        grid.SearchPanelText = "emea";

        Assert.Equal(6, grid.TotalItemsCount);
        Assert.Equal("emea", grid.HighlightText);
        Assert.Equal(3, grid.SearchResult!.MatchCount);
        Assert.Equal(3, grid.SearchResult.MatchedRowCount);
        Assert.Equal("Region", grid.SearchResult.Matches[0].Field);

        Assert.True(grid.NextMatch());
        Assert.Equal(0, grid.Selection.FocusedRow);
        Assert.True(grid.IsCurrentMatch(0, "Region"));
        Assert.True(grid.NextMatch());
        Assert.True(grid.NextMatch());
        Assert.True(grid.NextMatch());
        Assert.Equal(0, grid.CurrentMatchIndex);
        Assert.True(grid.PreviousMatch());
        Assert.Equal(2, grid.CurrentMatchIndex);
        Assert.Equal(new[] { 0, 2, 5, 0, 5 }, requested);
        Assert.False(grid.GoToMatch(10));

        grid.SearchPanelText = "";
        Assert.Null(grid.SearchResult);
        Assert.False(grid.NextMatch());
    }

    [Fact]
    public void FindRangesLocatesEveryOccurrence()
    {
        Assert.Equal(new[] { (1, 1), (3, 1) }, DataGrid<Order>.FindRanges("aXbxc", "x"));
        Assert.Empty(DataGrid<Order>.FindRanges("abc", "x"));
        Assert.Single(DataGrid<Order>.FindRanges("aXb", "x", ignoreCase: true));
        Assert.Empty(DataGrid<Order>.FindRanges("aXb", "x", ignoreCase: false));
        Assert.Empty(DataGrid<Order>.FindRanges(null, "x"));
    }

    [Fact]
    public async Task SearchAcrossGroupsExpandsOnlyGroupsWithMatches()
    {
        var grid = OrderGrid(Orders());
        await grid.Loaded();
        grid.GroupBy("Region");
        grid.CollapseAllGroups();
        Assert.Equal(3, grid.TotalItemsCount);

        var result = await grid.SearchAsync("customer 3");

        Assert.Equal(2, result.MatchCount);
        Assert.Equal(3 + 3, grid.TotalItemsCount);
        Assert.True(grid.IsGroupExpanded("/Region=EMEA"));
        Assert.False(grid.IsGroupExpanded("/Region=APAC"));

        var groupSearch = await grid.SearchAsync("Region: AMER");
        Assert.Equal(1, groupSearch.MatchCount);
    }

    [Fact]
    public async Task IncrementalSearchFindsRowsByPrefixAndWraps()
    {
        var grid = OrderGrid(Orders());
        await grid.Loaded();

        Assert.Equal(2, await grid.FindRowByPrefixAsync("Customer", "customer 3"));
        Assert.Equal(5, await grid.FindRowByPrefixAsync("Customer", "customer 3", 3));
        Assert.Equal(2, await grid.FindRowByPrefixAsync("Customer", "customer 3", 6));
        Assert.Equal(-1, await grid.FindRowByPrefixAsync("Customer", "zzz"));
        Assert.Equal(-1, await grid.FindRowByPrefixAsync("Nope", "a"));

        grid.GroupBy("Region");
        Assert.True(await grid.FindRowByPrefixAsync("Customer", "customer 1") >= 0);
    }

    [Fact]
    public async Task CollectionChangesTriggerADebouncedReload()
    {
        var items = new ObservableCollection<Order>(Orders());
        var loads = 0;
        var grid = OrderGrid();
        grid.AutoRefreshDelay = TimeSpan.Zero;
        grid.DataSource = items;
        await grid.Loaded();
        var refreshed = 0;
        grid.DataRefreshed += (_, _) => refreshed++;

        items.Add(new Order { Id = 7 });
        Assert.Equal(7, grid.TotalItemsCount);
        items.RemoveAt(0);
        Assert.Equal(6, grid.TotalItemsCount);
        Assert.Equal(2, refreshed);

        grid.AutoRefreshOnCollectionChange = false;
        items.Add(new Order { Id = 8 });
        Assert.Equal(6, grid.TotalItemsCount);

        var counted = OrderGrid();
        counted.AutoRefreshDelay = TimeSpan.FromMilliseconds(60);
        var source = new ObservableCollection<Order>(Orders());
        counted.CustomDataSource = new RemoteDataSource<Order>((request, _) =>
        {
            Interlocked.Increment(ref loads);
            return Task.FromResult(new DataSourceResult<Order> { Data = source.Skip(request.EffectiveSkip).Take(request.EffectiveTake).ToList(), Total = source.Count });
        });
        await counted.Loaded();
        var before = loads;
        counted.NotifyDataChanged();
        counted.NotifyDataChanged();
        counted.NotifyDataChanged();
        await WaitUntil(() => loads > before);
        await Task.Delay(150);
        Assert.Equal(before + 1, loads);
    }

    [Fact]
    public async Task BatchUpdatesDeferReloadsUntilTheLastEnd()
    {
        var loads = 0;
        var source = Orders();
        var grid = OrderGrid();
        grid.CustomDataSource = new RemoteDataSource<Order>((request, _) =>
        {
            Interlocked.Increment(ref loads);
            return Task.FromResult(QueryEngine.Execute(source.AsQueryable(), request));
        });
        await grid.Loaded();
        var before = loads;

        grid.BeginUpdate();
        grid.BeginUpdate();
        grid.SortBy("Quantity", ListSortDirection.Descending);
        grid.FilterBy("Region", FilterOperator.IsEqualTo, "EMEA");
        grid.EndUpdate();
        Assert.Equal(before, loads);
        grid.EndUpdate();

        Assert.Equal(before + 1, loads);
        Assert.Equal(3, grid.TotalItemsCount);
    }

    [Fact]
    public async Task AnnouncementsDescribeSortFilterAndPageChanges()
    {
        var grid = OrderGrid(Orders());
        grid.PagingMode = GridPagingMode.Client;
        grid.PageSize = 2;
        await grid.Loaded();
        var messages = new List<string>();
        grid.Announcement += (_, e) => messages.Add(e.Message);

        grid.CycleSort("Quantity");
        grid.FilterBy("Region", FilterOperator.IsEqualTo, "EMEA");
        grid.NextPage();

        Assert.Contains("Sorted by Quantity ascending", messages);
        Assert.Contains("1 filter applied", messages);
        Assert.Contains(messages, m => m.StartsWith("Page 2 of"));
    }
}
