using System.ComponentModel;
using EnterpriseUI.Abstractions.Accessibility;
using EnterpriseUI.Abstractions.Virtualization;
using EnterpriseUI.Data;
using EnterpriseUI.Grids;

namespace EnterpriseUI.WinForms;

public partial class EnterpriseDataGrid<TItem>
{
    public IReadOnlyList<int> PageSizeOptions { get => _engine.PageSizeOptions; set => _engine.PageSizeOptions = value; }
    public bool IsPaged => _engine.IsPaged;
    public bool HasPreviousPage => _engine.HasPreviousPage;
    public bool HasNextPage => _engine.HasNextPage;
    public bool HasMoreRows => _engine.HasMoreRows;
    public int PageFirstRecord => _engine.PageFirstRecord;
    public int PageLastRecord => _engine.PageLastRecord;
    public string PagerText => _engine.PagerText;
    public bool FirstPage() => _engine.FirstPage();
    public bool PreviousPage() => _engine.PreviousPage();
    public bool NextPage() => _engine.NextPage();
    public bool LastPage() => _engine.LastPage();
    public void SetPageSize(int size) => _engine.SetPageSize(size);
    public Task<bool> LoadMoreAsync(CancellationToken cancellationToken = default) => _engine.LoadMoreAsync(cancellationToken);
    public static IReadOnlyList<int> GetPageButtons(int pageIndex, int pageCount, int maxButtons = 7) => GridPagingHelper.GetPageButtons(pageIndex, pageCount, maxButtons);

    public Exception? LastError => _engine.LastError;
    public string Id => _engine.Id;
    public bool IsEnabled { get => Enabled; set => Enabled = value; }
    public bool IsVisible { get => Visible; set => Visible = value; }
    public double ViewportWidth => _engine.ViewportWidth;
    public double ViewportHeight => _engine.ViewportHeight;
    public IndexRange VisibleRowRange => _engine.VisibleRowRange;
    public IndexRange VisibleColumnRange => _engine.VisibleColumnRange;
    public TimeSpan AutoRefreshDelay { get => _engine.AutoRefreshDelay; set => _engine.AutoRefreshDelay = value; }
    public bool AutoRefreshOnCollectionChange { get => _engine.AutoRefreshOnCollectionChange; set => _engine.AutoRefreshOnCollectionChange = value; }
    public AriaRole Role { get => _engine.Role; set => _engine.Role = value; }
    public string? AriaDescription { get => _engine.AriaDescription; set { _engine.AriaDescription = value; AccessibleDescription = value; } }
    public string? AriaLabelledBy { get => _engine.AriaLabelledBy; set => _engine.AriaLabelledBy = value; }
    public string? AriaDescribedBy { get => _engine.AriaDescribedBy; set => _engine.AriaDescribedBy = value; }
    public bool AriaHidden { get => _engine.AriaHidden; set => _engine.AriaHidden = value; }
    public bool AriaExpanded { get => _engine.AriaExpanded; set => _engine.AriaExpanded = value; }
    public bool AriaDisabled { get => _engine.AriaDisabled; set => _engine.AriaDisabled = value; }
    public bool AriaRequired { get => _engine.AriaRequired; set => _engine.AriaRequired = value; }
    public bool AriaInvalid { get => _engine.AriaInvalid; set => _engine.AriaInvalid = value; }

    public event PropertyChangedEventHandler? PropertyChanged { add => _engine.PropertyChanged += value; remove => _engine.PropertyChanged -= value; }
    public event EventHandler<GridDataLoadFailedEventArgs>? DataLoadFailed { add => _engine.DataLoadFailed += value; remove => _engine.DataLoadFailed -= value; }
    public event EventHandler? DataRefreshed { add => _engine.DataRefreshed += value; remove => _engine.DataRefreshed -= value; }
    public event EventHandler<GridCellEditedEventArgs>? CellEditCompleted { add => _engine.CellEdited += value; remove => _engine.CellEdited -= value; }
    public event EventHandler? ValidationChanged { add => _engine.ValidationChanged += value; remove => _engine.ValidationChanged -= value; }
    public event EventHandler<GridChangesEventArgs>? ChangesCommitted { add => _engine.ChangesCommitted += value; remove => _engine.ChangesCommitted -= value; }
    public event EventHandler<GridChangesEventArgs>? ChangesRolledBack { add => _engine.ChangesRolledBack += value; remove => _engine.ChangesRolledBack -= value; }
    public event EventHandler? UndoStackChanged { add => _engine.UndoStackChanged += value; remove => _engine.UndoStackChanged -= value; }
    public event EventHandler? SearchResultChanged { add => _engine.SearchResultChanged += value; remove => _engine.SearchResultChanged -= value; }
    public event EventHandler<int>? ScrollToRowRequested { add => _engine.ScrollToRowRequested += value; remove => _engine.ScrollToRowRequested -= value; }
    public event EventHandler? RunningTotalsChanged { add => _engine.RunningTotalsChanged += value; remove => _engine.RunningTotalsChanged -= value; }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => _engine.InitializeAsync(cancellationToken);
    public void BeginUpdate() => _engine.BeginUpdate();
    public void EndUpdate() => _engine.EndUpdate();
    public void NotifyDataChanged() => _engine.NotifyDataChanged();
    public GridRowStyleEventArgs<TItem>? GetRowStyle(TItem item, int rowIndex) => _engine.GetRowStyle(item, rowIndex);
    public GridColumn? FindColumn(string field) => _engine.FindColumn(field);
    public object? GetCellValue(object? item, GridColumn column) => _engine.GetCellValue(item, column);
    public string FormatCellValue(GridColumn column, object? value) => _engine.FormatCellValue(column, value);
    public void FilterBy(string fieldName, FilterOperator op, object? value, object? value2) => _engine.FilterBy(fieldName, op, value, value2);
    public bool HasFilter(string fieldName) => _engine.HasFilter(fieldName);
    public void AddFilter(IFilterDescriptor filter) => _engine.AddFilter(filter);
    public void RemoveFilter(IFilterDescriptor filter) => _engine.RemoveFilter(filter);
    public void FilterByPredicate(string fieldName, string operatorName, Func<object?, bool> predicate) => _engine.FilterByPredicate(fieldName, operatorName, predicate);
    public string ExportFilterPresets() => _engine.ExportFilterPresets();
    public IReadOnlyList<string> ImportFilterPresets(string json) => _engine.ImportFilterPresets(json);
    public void GroupBy(string fieldName, Func<object?, object?> keySelector) => _engine.GroupBy(fieldName, keySelector);
    public bool MoveGroup(string fieldName, int newIndex) => _engine.MoveGroup(fieldName, newIndex);
    public string GetGroupHeaderText(GridRow<TItem> row) => _engine.GetGroupHeaderText(row);
    public string GetGroupHeaderText(GroupResult<TItem> group) => _engine.GetGroupHeaderText(group);
    public IReadOnlyList<string> GetGroupSummaryTexts(GroupResult<TItem> group) => _engine.GetGroupSummaryTexts(group);

    public int MaxUndoDepth { get => _engine.MaxUndoDepth; set => _engine.MaxUndoDepth = value; }
    public List<Func<TItem, IEnumerable<GridValidationError>>> RowValidators => _engine.RowValidators;
    public List<Func<TItem, CancellationToken, Task<IEnumerable<GridValidationError>>>> AsyncRowValidators => _engine.AsyncRowValidators;
    public bool IsBatchMode => _engine.IsBatchMode;
    public bool IsInTransaction => _engine.IsInTransaction;
    public bool HasPendingChanges => _engine.HasPendingChanges;
    public IReadOnlyList<GridPendingChange> PendingChanges => _engine.PendingChanges;
    public bool HasErrors => _engine.HasErrors;
    public IReadOnlyList<GridValidationError> ValidationSummary => _engine.ValidationSummary;
    public int IndexOfItem(TItem item) => _engine.IndexOfItem(item);
    public bool IsCellDirty(object item, string field) => _engine.IsCellDirty(item, field);
    public bool HasCellError(object item, string field) => _engine.HasCellError(item, field);
    public IReadOnlyList<GridValidationError> GetCellErrors(object item, string field) => _engine.GetCellErrors(item, field);
    public IReadOnlyList<GridValidationError> GetRowErrors(object item) => _engine.GetRowErrors(item);
    public void ClearValidationErrors() => _engine.ClearValidationErrors();
    public IReadOnlyList<GridValidationError> ValidateRow(TItem item) => _engine.ValidateRow(item);
    public Task<IReadOnlyList<GridValidationError>> ValidateAllAsync(CancellationToken cancellationToken = default) => _engine.ValidateAllAsync(cancellationToken);
    public GridEditResult TryEditCell(TItem item, string fieldName, object? newValue) => _engine.TryEditCell(item, fieldName, newValue);
    public Task<GridEditResult> TryEditCellAsync(TItem item, string fieldName, object? newValue, CancellationToken cancellationToken = default) => _engine.TryEditCellAsync(item, fieldName, newValue, cancellationToken);
    public void ClearUndoHistory() => _engine.ClearUndoHistory();
    public void BeginTransaction() => _engine.BeginTransaction();
    public GridCommitResult CommitTransaction() => _engine.CommitTransaction();
    public int RollbackTransaction() => _engine.RollbackTransaction();
    public GridRowEditSession<TItem> BeginRowEdit(TItem item) => _engine.BeginRowEdit(item);
    public bool MoveRows(IEnumerable<TItem> items, TItem? before) => _engine.MoveRows(items, before);
    public int TransferRows(IEnumerable<TItem> items, EnterpriseDataGrid<TItem> target, int? targetIndex = null, bool copy = false, Func<TItem, TItem>? clone = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        return _engine.TransferRows(items, target._engine, targetIndex, copy, clone);
    }

    public bool TreeExpandedByDefault { get => _engine.TreeExpandedByDefault; set { _engine.TreeExpandedByDefault = value; RefreshLayout(); } }
    public int MaxMaterializedRows { get => _engine.MaxMaterializedRows; set => _engine.MaxMaterializedRows = value; }
    public bool IsTreeMode => _engine.IsTreeMode;
    public IReadOnlyList<GridHierarchyRelation> MasterDetailRelations => _engine.MasterDetailRelations;
    public GridHierarchyRelation? TreeHierarchy => _engine.TreeHierarchy;
    public Task<(IReadOnlyList<TItem> Items, IReadOnlyList<GroupResult<TItem>>? Groups)> ReadGroupedAsync(CancellationToken cancellationToken = default) => _engine.ReadGroupedAsync(cancellationToken);
    public void PrepareHierarchy(object? sampleParent) => _engine.PrepareHierarchy(sampleParent);
    public Task<IReadOnlyList<object>> LoadChildrenAsync(GridHierarchyRelation relation, object parent, CancellationToken cancellationToken = default) => _engine.LoadChildrenAsync(relation, parent, cancellationToken);
    public IReadOnlyList<object> GetChildren(GridHierarchyRelation relation, object parent) => _engine.GetChildren(relation, parent);
    public IReadOnlyList<GridColumn> GetDetailColumns(GridHierarchyRelation relation, object? sample) => _engine.GetDetailColumns(relation, sample);
    public void ToggleRelationExpansion(object parent, GridHierarchyRelation relation) => _engine.ToggleRelationExpansion(parent, relation);
    public bool IsRelationExpanded(object parent, GridHierarchyRelation relation) => _engine.IsRelationExpanded(parent, relation);
    public void ToggleRowExpansion(int displayIndex) => _engine.ToggleRowExpansion(displayIndex);
    public bool IsGroupExpanded(string groupPath) => _engine.IsGroupExpanded(groupPath);
    public void ToggleTreeNode(TItem item) => _engine.ToggleTreeNode(item);
    public void ExpandAllTreeNodes() => _engine.ExpandAllTreeNodes();
    public void CollapseAllTreeNodes() => _engine.CollapseAllTreeNodes();
    public double CalculateBestFitWidth(GridColumn column, Func<string, bool, double> measureText, double padding = 24, int maxSamples = 500) => _engine.CalculateBestFitWidth(column, measureText, padding, maxSamples);
    public bool BestFitColumn(string fieldName, Func<string, bool, double> measureText) => _engine.BestFitColumn(fieldName, measureText);
    public bool BestFitColumn(string fieldName) => _engine.BestFitColumn(fieldName, MeasureLogical);
    public void BestFitAllColumns(Func<string, bool, double> measureText) => _engine.BestFitAllColumns(measureText);

    public GridSearchOptions SearchOptions { get => _engine.SearchOptions; set => _engine.SearchOptions = value; }
    public GridSearchMode SearchMode { get => _engine.SearchMode; set => _engine.SearchMode = value; }
    public GridSearchResult? SearchResult => _engine.SearchResult;
    public int CurrentMatchIndex => _engine.CurrentMatchIndex;
    public string? HighlightText => _engine.HighlightText;
    public Task<GridSearchResult> SearchAsync(string? text, GridSearchOptions? options = null, CancellationToken cancellationToken = default) => _engine.SearchAsync(text, options, cancellationToken);
    public bool NextMatch() => _engine.NextMatch();
    public bool PreviousMatch() => _engine.PreviousMatch();
    public bool GoToMatch(int matchIndex) => _engine.GoToMatch(matchIndex);
    public bool IsCurrentMatch(int localRow, string field) => _engine.IsCurrentMatch(localRow, field);
    public Task<int> FindRowByPrefixAsync(string field, string prefix, int startRow = 0, CancellationToken cancellationToken = default) => _engine.FindRowByPrefixAsync(field, prefix, startRow, cancellationToken);
    public void FocusRow(int absoluteRow, int column = 0) => _engine.FocusRow(absoluteRow, column);
    public static IReadOnlyList<(int Start, int Length)> FindRanges(string? text, string? term, bool ignoreCase = true) => DataGrid<TItem>.FindRanges(text, term, ignoreCase);

    public IReadOnlyList<GridFooterValue> FooterValues => _engine.FooterValues;
    public IReadOnlyList<GridFooterValue> GetFooterValues(string field) => _engine.GetFooterValues(field);
    public Task ComputeRunningTotalsAsync(CancellationToken cancellationToken = default) => _engine.ComputeRunningTotalsAsync(cancellationToken);
    public decimal? GetRunningTotal(GridRow<TItem> row, GridColumn column) => _engine.GetRunningTotal(row, column);
    public object? GetRowCellValue(GridRow<TItem> row, GridColumn column) => _engine.GetRowCellValue(row, column);
    public string DescribeGrid() => GridAccessibility.DescribeGrid(_engine);
    public string DescribeColumnHeader(GridColumn column) => GridAccessibility.DescribeColumnHeader(_engine, column);
    public string DescribeRow(GridRow<TItem> row) => GridAccessibility.DescribeRow(_engine, row);
    public string DescribeCell(GridRow<TItem> row, GridColumn column, int columnIndex, int columnCount) => GridAccessibility.DescribeCell(_engine, row, column, columnIndex, columnCount);
}