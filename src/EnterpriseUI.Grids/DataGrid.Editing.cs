using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using EnterpriseUI.Data;

namespace EnterpriseUI.Grids;

public partial class DataGrid<TItem>
{
    // ==========================================
    // SELECTION
    // ==========================================

    /// <summary>Selection and focus over displayed rows (by index on the current page) and visible columns.</summary>
    public GridSelectionModel Selection { get; } = new();

    public GridSelectionMode SelectionMode
    {
        get => Selection.Mode;
        set => Selection.Mode = value;
    }

    public event EventHandler? SelectionChanged;

    public TItem? SelectedItem
    {
        get
        {
            var row = Selection.SelectedRows.Count > 0
                ? (Selection.SelectedRows.Contains(Selection.FocusedRow) ? Selection.FocusedRow : Selection.SelectedRows.Min())
                : Selection.CellRanges.Count > 0 ? Selection.CellRanges[0].RowFrom : -1;
            return row >= 0 && TryGetItemAt(row, out var item) ? item : default;
        }
        set
        {
            if (value == null)
            {
                Selection.Clear();
                return;
            }

            var index = IndexOfItem(value);
            if (index >= 0)
            {
                Selection.Click(index, Math.Max(0, Selection.FocusedColumn));
            }
        }
    }

    public IReadOnlyList<TItem> SelectedItems
    {
        get
        {
            var rows = new SortedSet<int>(Selection.SelectedRows);
            foreach (var range in Selection.CellRanges)
            {
                for (var r = range.RowFrom; r <= range.RowTo && rows.Count < 100_000; r++) rows.Add(r);
            }

            var items = new List<TItem>(rows.Count);
            foreach (var row in rows)
            {
                if (TryGetItemAt(row, out var item)) items.Add(item);
            }
            return items;
        }
    }

    /// <summary>Display index of an item among the loaded rows of the page, or -1.</summary>
    public int IndexOfItem(TItem item)
    {
        if (item == null) return -1;
        if (IsMaterialized)
        {
            var offset = PageOffset;
            for (var i = 0; i < TotalItemsCount; i++)
            {
                var r = _rows[offset + i];
                if (r.Kind == GridRowKind.Data && IdentityComparer<TItem>.Instance.Equals(_materialized[r.A], item)) return i;
            }
            return -1;
        }

        for (var i = 0; i < _window.Count; i++)
        {
            if (IdentityComparer<TItem>.Instance.Equals(_window[i], item)) return _windowStart + i;
        }
        return -1;
    }

    // ==========================================
    // EDITING: VALIDATION, UNDO/REDO, TRANSACTIONS
    // ==========================================

    private sealed class UndoEntry
    {
        public UndoEntry(string description, Action undo, Action redo, IReadOnlyList<GridEditRecord<TItem>> records, bool pending)
        {
            Description = description;
            Undo = undo;
            Redo = redo;
            Records = records;
            Pending = pending;
        }

        public string Description { get; }
        public Action Undo { get; }
        public Action Redo { get; }
        public IReadOnlyList<GridEditRecord<TItem>> Records { get; }
        public bool Pending { get; set; }
    }

    private readonly record struct CellKey(object Item, string Field);

    private sealed class CellKeyComparer : IEqualityComparer<CellKey>
    {
        public static CellKeyComparer Instance { get; } = new();

        public bool Equals(CellKey x, CellKey y) =>
            ObjectIdentityComparer.Instance.Equals(x.Item, y.Item) && string.Equals(x.Field, y.Field, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(CellKey key) =>
            HashCode.Combine(ObjectIdentityComparer.Instance.GetHashCode(key.Item), StringComparer.OrdinalIgnoreCase.GetHashCode(key.Field));
    }

    private sealed class EditPlan
    {
        public EditPlan(TItem item, string field) { Item = item; Field = field; }
        public TItem Item { get; }
        public string Field { get; }
        public PropertyInfo? Property { get; set; }
        public GridColumn? Column { get; set; }
        public object? Old { get; set; }
        public object? New { get; set; }
        public GridEditResult? Failure { get; set; }
    }

    private readonly List<UndoEntry> _undoStack = new();
    private readonly List<UndoEntry> _redoStack = new();
    private readonly Dictionary<CellKey, List<GridValidationError>> _errors = new(CellKeyComparer.Instance);
    private readonly Dictionary<CellKey, GridPendingChange> _pending = new(CellKeyComparer.Instance);
    private readonly List<object> _pendingInserted = new();
    private readonly List<object> _pendingDeleted = new();
    private bool _transactionActive;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>Oldest edits are dropped beyond this many undo steps.</summary>
    public int MaxUndoDepth { get; set; } = 1000;

    public GridEditMode EditMode { get; set; } = GridEditMode.None;

    /// <summary>When true (default) an edit that fails an error-level rule is refused; when false it is applied and flagged.</summary>
    public bool RejectInvalidEdits { get; set; } = true;

    /// <summary>Row-level business rules, run after a cell edit has been applied.</summary>
    public List<Func<TItem, IEnumerable<GridValidationError>>> RowValidators { get; } = new();

    /// <summary>Asynchronous row-level rules such as cross-table checks; only run by async edit and validation methods.</summary>
    public List<Func<TItem, CancellationToken, Task<IEnumerable<GridValidationError>>>> AsyncRowValidators { get; } = new();

    public event EventHandler<GridCellEditedEventArgs>? CellEdited;
    public event EventHandler<GridValidationEventArgs>? ValidationFailed;
    public event EventHandler? ValidationChanged;
    public event EventHandler<GridChangesEventArgs>? ChangesCommitted;
    public event EventHandler<GridChangesEventArgs>? ChangesRolledBack;
    public event EventHandler? UndoStackChanged;

    public bool IsBatchMode => EditMode == GridEditMode.Batch || _transactionActive;
    public bool IsInTransaction => _transactionActive;
    public bool HasPendingChanges => _pending.Count > 0 || _pendingInserted.Count > 0 || _pendingDeleted.Count > 0;
    public IReadOnlyList<GridPendingChange> PendingChanges => _pending.Values.ToList();
    public bool IsCellDirty(object item, string field) => item != null && _pending.ContainsKey(new CellKey(item, field));

    // ---- validation state ----

    public bool HasErrors => _errors.Count > 0;

    /// <summary>Every current error across the grid, for a validation summary panel.</summary>
    public IReadOnlyList<GridValidationError> ValidationSummary => _errors.Values.SelectMany(e => e).ToList();

    public bool HasCellError(object item, string field) => item != null && _errors.ContainsKey(new CellKey(item, field));

    public IReadOnlyList<GridValidationError> GetCellErrors(object item, string field) =>
        item != null && _errors.TryGetValue(new CellKey(item, field), out var list) ? list : Array.Empty<GridValidationError>();

    public IReadOnlyList<GridValidationError> GetRowErrors(object item) =>
        item == null ? Array.Empty<GridValidationError>() : _errors.Where(e => ObjectIdentityComparer.Instance.Equals(e.Key.Item, item)).SelectMany(e => e.Value).ToList();

    public void ClearValidationErrors()
    {
        if (_errors.Count == 0) return;
        _errors.Clear();
        ValidationChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetCellErrors(object item, string field, IReadOnlyList<GridValidationError> errors)
    {
        var key = new CellKey(item, field);
        var had = _errors.ContainsKey(key);
        if (errors.Count == 0)
        {
            if (!_errors.Remove(key)) return;
        }
        else
        {
            _errors[key] = errors.ToList();
        }

        if (had || errors.Count > 0) ValidationChanged?.Invoke(this, EventArgs.Empty);
    }

    private static List<GridValidationError> RunCellRules(object item, GridColumn? column, string field, object? old, object? converted)
    {
        var errors = new List<GridValidationError>();
        if (column == null) return errors;

        var context = new GridValidationContext(item, column, field, old, converted);
        foreach (var rule in column.ValidationRules.Where(r => !r.IsAsync))
        {
            var message = rule.Validate(context);
            if (message != null) errors.Add(new GridValidationError(item, field, message, rule.Severity));
        }
        return errors;
    }

    private async static Task<List<GridValidationError>> RunAsyncCellRulesAsync(object item, GridColumn? column, string field, object? old, object? converted, CancellationToken cancellationToken)
    {
        var errors = new List<GridValidationError>();
        if (column == null) return errors;

        var context = new GridValidationContext(item, column, field, old, converted);
        foreach (var rule in column.ValidationRules.Where(r => r.IsAsync))
        {
            var message = await rule.ValidateAsync(context, cancellationToken).ConfigureAwait(true);
            if (message != null) errors.Add(new GridValidationError(item, field, message, rule.Severity));
        }
        return errors;
    }

    private List<GridValidationError> RunRowRules(TItem item)
    {
        var errors = new List<GridValidationError>();
        foreach (var validator in RowValidators)
        {
            errors.AddRange(validator(item));
        }
        return errors;
    }

    private async Task<List<GridValidationError>> RunAsyncRowRulesAsync(TItem item, CancellationToken cancellationToken)
    {
        var errors = new List<GridValidationError>();
        foreach (var validator in AsyncRowValidators)
        {
            errors.AddRange(await validator(item, cancellationToken).ConfigureAwait(true));
        }
        return errors;
    }

    /// <summary>Runs every column rule and row rule against the current values of a row and records the result.</summary>
    public IReadOnlyList<GridValidationError> ValidateRow(TItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var errors = new List<GridValidationError>();
        foreach (var column in Columns.Where(c => c.ValidationRules.Count > 0))
        {
            errors.AddRange(RunCellRules(item, column, column.Field, column.GetValue(item), column.GetValue(item)));
        }
        errors.AddRange(RunRowRules(item));
        StoreRowErrors(item, errors);
        return errors;
    }

    /// <summary>Validates every row matching the current filters, including asynchronous rules, and fills <see cref="ValidationSummary"/>.</summary>
    public async Task<IReadOnlyList<GridValidationError>> ValidateAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = await ReadAllAsync(null, cancellationToken).ConfigureAwait(true);
        var all = new List<GridValidationError>();
        foreach (var item in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var errors = new List<GridValidationError>(ValidateRow(item));
            foreach (var column in Columns.Where(c => c.ValidationRules.Any(r => r.IsAsync)))
            {
                var value = column.GetValue(item);
                errors.AddRange(await RunAsyncCellRulesAsync(item!, column, column.Field, value, value, cancellationToken).ConfigureAwait(true));
            }
            errors.AddRange(await RunAsyncRowRulesAsync(item, cancellationToken).ConfigureAwait(true));
            StoreRowErrors(item, errors);
            all.AddRange(errors);
        }
        return all;
    }

    private void StoreRowErrors(TItem item, List<GridValidationError> errors)
    {
        var stale = _errors.Keys.Where(k => ObjectIdentityComparer.Instance.Equals(k.Item, item)).ToList();
        foreach (var key in stale) _errors.Remove(key);
        foreach (var group in errors.GroupBy(e => e.Field, StringComparer.OrdinalIgnoreCase))
        {
            _errors[new CellKey(item!, group.Key)] = group.ToList();
        }
        if (stale.Count > 0 || errors.Count > 0) ValidationChanged?.Invoke(this, EventArgs.Empty);
    }

    // ---- editing ----

    public bool EditCell(TItem item, string fieldName, object? newValue) => TryEditCell(item, fieldName, newValue).Success;

    /// <summary>Converts and validates the value, writes it, records an undo step and returns the outcome with any errors. Asynchronous rules are skipped; use <see cref="TryEditCellAsync"/>.</summary>
    public GridEditResult TryEditCell(TItem item, string fieldName, object? newValue)
    {
        var plan = PlanEdit(item, fieldName, newValue);
        return plan.Failure ?? CommitEdit(plan, null);
    }

    public async Task<GridEditResult> TryEditCellAsync(TItem item, string fieldName, object? newValue, CancellationToken cancellationToken = default)
    {
        var plan = PlanEdit(item, fieldName, newValue);
        if (plan.Failure != null) return plan.Failure;

        var asyncErrors = await RunAsyncCellRulesAsync(item!, plan.Column, fieldName, plan.Old, plan.New, cancellationToken).ConfigureAwait(true);
        var result = CommitEdit(plan, asyncErrors);
        if (!result.Success || AsyncRowValidators.Count == 0) return result;

        var rowErrors = await RunAsyncRowRulesAsync(item, cancellationToken).ConfigureAwait(true);
        if (rowErrors.Count == 0) return result;

        var hard = rowErrors.Any(e => e.Severity == GridValidationSeverity.Error);
        if (hard && RejectInvalidEdits)
        {
            UndoLastEdit();
            ValidationFailed?.Invoke(this, new GridValidationEventArgs(rowErrors));
            return new GridEditResult(false, plan.Old, plan.New, rowErrors);
        }

        SetCellErrors(item!, fieldName, result.Errors.Concat(rowErrors).ToList());
        return new GridEditResult(true, plan.Old, plan.New, result.Errors.Concat(rowErrors).ToList());
    }

    private EditPlan PlanEdit(TItem item, string fieldName, object? newValue)
    {
        var plan = new EditPlan(item, fieldName);
        if (item == null || string.IsNullOrEmpty(fieldName))
        {
            plan.Failure = new GridEditResult(false, null, null, Array.Empty<GridValidationError>());
            return plan;
        }

        plan.Column = FindColumn(fieldName);
        plan.Property = PropertyPath.FindProperty(item.GetType(), fieldName);
        if (plan.Property is not { CanWrite: true } || (plan.Column != null && !plan.Column.IsEditable))
        {
            plan.Failure = Fail(item!, fieldName, $"{(plan.Column?.Title is { Length: > 0 } t ? t : fieldName)} is read-only.");
            return plan;
        }

        plan.Old = plan.Property.GetValue(item);
        if (!TryConvertEditValue(newValue, plan.Property.PropertyType, out var converted, out var conversionError))
        {
            plan.Failure = Fail(item!, fieldName, conversionError ?? "The value is not valid.", plan.Old);
            return plan;
        }

        plan.New = converted;
        return plan;
    }

    private GridEditResult Fail(object item, string field, string message, object? old = null)
    {
        var errors = new[] { new GridValidationError(item, field, message) };
        SetCellErrors(item, field, errors);
        ValidationFailed?.Invoke(this, new GridValidationEventArgs(errors));
        return new GridEditResult(false, old, null, errors);
    }

    private GridEditResult CommitEdit(EditPlan plan, IReadOnlyList<GridValidationError>? extraErrors)
    {
        var item = plan.Item;
        var field = plan.Field;
        var property = plan.Property!;

        if (Equals(plan.Old, plan.New))
        {
            return new GridEditResult(true, plan.Old, plan.New, Array.Empty<GridValidationError>());
        }

        var errors = RunCellRules(item!, plan.Column, field, plan.Old, plan.New);
        if (extraErrors != null) errors.AddRange(extraErrors);
        var rejected = errors.Any(e => e.Severity == GridValidationSeverity.Error) && RejectInvalidEdits;
        if (rejected)
        {
            SetCellErrors(item!, field, errors);
            ValidationFailed?.Invoke(this, new GridValidationEventArgs(errors));
            return new GridEditResult(false, plan.Old, plan.New, errors);
        }

        try
        {
            property.SetValue(item, plan.New);
        }
        catch (TargetInvocationException ex)
        {
            return Fail(item!, field, ex.InnerException?.Message ?? ex.Message, plan.Old);
        }

        var rowErrors = RunRowRules(item);
        if (rowErrors.Any(e => e.Severity == GridValidationSeverity.Error) && RejectInvalidEdits)
        {
            property.SetValue(item, plan.Old);
            errors.AddRange(rowErrors);
            SetCellErrors(item!, field, errors);
            ValidationFailed?.Invoke(this, new GridValidationEventArgs(errors));
            return new GridEditResult(false, plan.Old, plan.New, errors);
        }

        errors.AddRange(rowErrors);
        SetCellErrors(item!, field, errors);

        var record = new GridEditRecord<TItem>(item, field, plan.Old, plan.New);
        PushUndo(new UndoEntry(
            $"Edit {field}",
            () => ApplyValue(item, field, plan.Old, true),
            () => ApplyValue(item, field, plan.New, true),
            new[] { record },
            IsBatchMode));
        TrackPending(item!, field, plan.Old, plan.New);

        AfterEdit(field);
        CellEdited?.Invoke(this, new GridCellEditedEventArgs(item!, field, plan.Old, plan.New));
        return new GridEditResult(true, plan.Old, plan.New, errors);
    }

    private void ApplyValue(TItem item, string field, object? value, bool track)
    {
        var property = PropertyPath.FindProperty(item!.GetType(), field);
        if (property is not { CanWrite: true }) return;
        var old = property.GetValue(item);
        property.SetValue(item, value);
        if (track && IsBatchMode) TrackPending(item, field, old, value);
        SetCellErrors(item, field, RunCellRules(item, FindColumn(field), field, old, value));
    }

    private void TrackPending(object item, string field, object? original, object? current)
    {
        if (!IsBatchMode) return;
        var key = new CellKey(item, field);
        if (_pending.TryGetValue(key, out var existing))
        {
            if (Equals(existing.OriginalValue, current))
            {
                _pending.Remove(key);
            }
            else
            {
                existing.CurrentValue = current;
            }
        }
        else if (!Equals(original, current))
        {
            _pending[key] = new GridPendingChange(item, field, original, current);
        }
    }

    private void PushUndo(UndoEntry entry)
    {
        _undoStack.Add(entry);
        if (_undoStack.Count > Math.Max(1, MaxUndoDepth)) _undoStack.RemoveAt(0);
        _redoStack.Clear();
        UndoStackChanged?.Invoke(this, EventArgs.Empty);
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    /// <summary>Refreshes what depends on edited values: the source cache, summaries, calculated columns, and the sort, group or filter order.</summary>
    private void AfterEdit(string field)
    {
        (_boundDataSource as IInvalidatableDataSource)?.Invalidate();
        OnPropertyChanged(nameof(RenderedItems));

        bool Touches(string member) =>
            member.Equals(field, StringComparison.OrdinalIgnoreCase) || (FindColumn(member) is { } column && DependsOn(column, field));

        var affectsQuery = CurrentRequest.Sorts.Any(s => Touches(s.Member))
            || CurrentRequest.Groups.Any(g => Touches(g.Member))
            || CurrentRequest.Filters.Any(f => FilterTouches(f, Touches))
            || Columns.Any(c => c.FooterSummaries.Count > 0 && Touches(c.Field))
            || Columns.Any(c => c.IsRunningTotal && Touches(c.RunningTotalOf!));

        if (affectsQuery && _updateDepth == 0)
        {
            _ = RefreshSafeAsync(LoadKind.Data);
        }
        else
        {
            DataWindowChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static bool FilterTouches(IFilterDescriptor filter, Func<string, bool> touches) => filter switch
    {
        FilterDescriptor fd => touches(fd.Member),
        CustomFilterDescriptor custom => custom.Member.Length == 0 || touches(custom.Member),
        CompositeFilterDescriptor composite => composite.FilterDescriptors.Any(f => FilterTouches(f, touches)),
        _ => false
    };

    private static bool DependsOn(GridColumn column, string field) =>
        column.IsCalculated && ExpressionEngine.TryCompile(column.Expression!, out var compiled, out _) && compiled!.Fields.Any(f => f.Equals(field, StringComparison.OrdinalIgnoreCase));

    private static bool TryConvertEditValue(object? value, Type targetType, out object? result, out string? error)
    {
        error = null;
        var underlying = Nullable.GetUnderlyingType(targetType);
        var target = underlying ?? targetType;

        if (value == null || value is string { Length: 0 } && target != typeof(string))
        {
            result = null;
            if (!target.IsValueType || underlying != null) return true;
            error = "A value is required.";
            return false;
        }

        if (target.IsInstanceOfType(value))
        {
            result = value;
            return true;
        }

        if (value is string text)
        {
            var trimmed = text.Trim();
            try
            {
                if (target != typeof(string) && !target.IsEnum && target != typeof(Guid) && target != typeof(bool) && typeof(IConvertible).IsAssignableFrom(target))
                {
                    result = Convert.ChangeType(trimmed, target, CultureInfo.CurrentCulture);
                    return true;
                }
            }
            catch (Exception ex) when (ex is FormatException or OverflowException or InvalidCastException)
            {
                // Fall through to the invariant conversion below.
            }

            if (target == typeof(DateTime) && DateTime.TryParse(trimmed, CultureInfo.CurrentCulture, DateTimeStyles.None, out var date))
            {
                result = date;
                return true;
            }
        }

        if (QueryEngine.TryConvertValue(value, target, out result))
        {
            return true;
        }

        error = $"'{value}' is not a valid {target.Name}.";
        return false;
    }

    // ---- undo / redo ----

    public bool UndoLastEdit()
    {
        if (_undoStack.Count == 0) return false;

        var entry = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        entry.Undo();
        _redoStack.Add(entry);
        FinishUndoRedo(entry);
        return true;
    }

    public bool RedoLastEdit()
    {
        if (_redoStack.Count == 0) return false;

        var entry = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);
        entry.Redo();
        _undoStack.Add(entry);
        FinishUndoRedo(entry);
        return true;
    }

    private void FinishUndoRedo(UndoEntry entry)
    {
        UndoStackChanged?.Invoke(this, EventArgs.Empty);
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        (_boundDataSource as IInvalidatableDataSource)?.Invalidate();
        if (entry.Records.Count > 0) AfterEdit(entry.Records[0].FieldName);
        else _ = RefreshSafeAsync(LoadKind.Data);
    }

    public void ClearUndoHistory()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        UndoStackChanged?.Invoke(this, EventArgs.Empty);
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    // ---- transactions / batch editing ----

    /// <summary>Holds edits as pending changes until <see cref="CommitChanges"/> or <see cref="RollbackChanges"/>.</summary>
    public void BeginTransaction() => _transactionActive = true;

    /// <summary>Commits the transaction; when validation fails it stays open so the edits can be corrected or rolled back.</summary>
    public GridCommitResult CommitTransaction()
    {
        var result = CommitChanges();
        if (result.Success) _transactionActive = false;
        return result;
    }

    public int RollbackTransaction()
    {
        var count = RollbackChanges();
        _transactionActive = false;
        return count;
    }

    /// <summary>Validates the rows that were changed and accepts all pending changes; with error-level failures nothing is committed.</summary>
    public GridCommitResult CommitChanges()
    {
        if (!HasPendingChanges)
        {
            return new GridCommitResult(true, Array.Empty<GridPendingChange>(), Array.Empty<GridValidationError>());
        }

        var errors = new List<GridValidationError>();
        foreach (var item in _pending.Values.Select(p => p.Item).Distinct(ObjectIdentityComparer.Instance).OfType<TItem>())
        {
            errors.AddRange(ValidateRow(item));
        }

        if (errors.Any(e => e.Severity == GridValidationSeverity.Error))
        {
            ValidationFailed?.Invoke(this, new GridValidationEventArgs(errors));
            return new GridCommitResult(false, _pending.Values.ToList(), errors);
        }

        var changes = _pending.Values.ToList();
        _pending.Clear();
        _pendingInserted.Clear();
        _pendingDeleted.Clear();
        foreach (var entry in _undoStack) entry.Pending = false;
        ChangesCommitted?.Invoke(this, new GridChangesEventArgs(changes));
        return new GridCommitResult(true, changes, errors);
    }

    /// <summary>Reverts every uncommitted edit, insert and delete; returns how many undo steps were reverted.</summary>
    public int RollbackChanges()
    {
        var changes = _pending.Values.ToList();
        var count = 0;
        while (_undoStack.Count > 0 && _undoStack[^1].Pending)
        {
            var entry = _undoStack[^1];
            _undoStack.RemoveAt(_undoStack.Count - 1);
            entry.Undo();
            count++;
        }

        _pending.Clear();
        _pendingInserted.Clear();
        _pendingDeleted.Clear();
        _redoStack.Clear();
        UndoStackChanged?.Invoke(this, EventArgs.Empty);
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        (_boundDataSource as IInvalidatableDataSource)?.Invalidate();
        _ = RefreshSafeAsync(LoadKind.Data);
        ChangesRolledBack?.Invoke(this, new GridChangesEventArgs(changes));
        return count;
    }

    // ---- row editing sessions (inline row, popup and form editing) ----

    public GridRowEditSession<TItem> BeginRowEdit(TItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new GridRowEditSession<TItem>(this, item);
    }

    internal IReadOnlyList<GridValidationError> ValidateDraft(TItem item, IReadOnlyDictionary<string, object?> draft, out Dictionary<string, object?> converted)
    {
        converted = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<GridValidationError>();
        foreach (var (field, value) in draft)
        {
            var plan = PlanEditSilently(item, field, value, out var failure);
            if (failure != null)
            {
                errors.Add(new GridValidationError(item!, field, failure));
                continue;
            }

            if (Equals(plan!.Old, plan.New)) continue;
            converted[field] = plan.New;
            errors.AddRange(RunCellRules(item!, plan.Column, field, plan.Old, plan.New));
        }
        return errors;
    }

    private EditPlan? PlanEditSilently(TItem item, string field, object? value, out string? failure)
    {
        failure = null;
        var plan = new EditPlan(item, field)
        {
            Column = FindColumn(field),
            Property = PropertyPath.FindProperty(item!.GetType(), field)
        };

        if (plan.Property is not { CanWrite: true } || (plan.Column != null && !plan.Column.IsEditable))
        {
            failure = $"{field} is read-only.";
            return null;
        }

        plan.Old = plan.Property.GetValue(item);
        if (!TryConvertEditValue(value, plan.Property.PropertyType, out var converted, out var error))
        {
            failure = error ?? "The value is not valid.";
            return null;
        }

        plan.New = converted;
        return plan;
    }

    internal bool CommitDraft(TItem item, IReadOnlyDictionary<string, object?> converted, out IReadOnlyList<GridValidationError> errors)
    {
        var records = new List<GridEditRecord<TItem>>();
        foreach (var (field, value) in converted)
        {
            var property = PropertyPath.FindProperty(item!.GetType(), field)!;
            records.Add(new GridEditRecord<TItem>(item, field, property.GetValue(item), value));
        }

        foreach (var record in records) PropertyPath.TrySetValue(item!, record.FieldName, record.NewValue);

        var rowErrors = RunRowRules(item);
        if (rowErrors.Any(e => e.Severity == GridValidationSeverity.Error) && RejectInvalidEdits)
        {
            foreach (var record in records) PropertyPath.TrySetValue(item!, record.FieldName, record.OldValue);
            errors = rowErrors;
            ValidationFailed?.Invoke(this, new GridValidationEventArgs(rowErrors));
            return false;
        }

        errors = rowErrors;
        if (records.Count == 0) return true;

        PushUndo(new UndoEntry(
            "Edit row",
            () => { foreach (var r in records.AsEnumerable().Reverse()) ApplyValue(item, r.FieldName, r.OldValue, true); },
            () => { foreach (var r in records) ApplyValue(item, r.FieldName, r.NewValue, true); },
            records,
            IsBatchMode));
        foreach (var record in records)
        {
            TrackPending(item!, record.FieldName, record.OldValue, record.NewValue);
            SetCellErrors(item!, record.FieldName, Array.Empty<GridValidationError>());
            CellEdited?.Invoke(this, new GridCellEditedEventArgs(item!, record.FieldName, record.OldValue, record.NewValue));
        }

        AfterEdit(records[0].FieldName);
        return true;
    }

    // ---- structural edits ----

    /// <summary>Adds a row to an in-memory list source (at <paramref name="index"/> when it is an IList); supports undo and batch commit.</summary>
    public bool AddRow(TItem item, int? index = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (_dataSource is not ICollection<TItem> { IsReadOnly: false } collection) return false;

        var list = _dataSource as IList<TItem>;
        void Insert()
        {
            if (list != null && index is { } at && at >= 0 && at <= list.Count) list.Insert(at, item); else collection.Add(item);
        }

        Insert();
        PushUndo(new UndoEntry("Add row", () => collection.Remove(item), Insert, Array.Empty<GridEditRecord<TItem>>(), IsBatchMode));
        if (IsBatchMode) _pendingInserted.Add(item!);
        (_boundDataSource as IInvalidatableDataSource)?.Invalidate();
        _ = RefreshSafeAsync(LoadKind.Data);
        return true;
    }

    /// <summary>Removes rows from an in-memory list source; supports undo and batch commit.</summary>
    public int RemoveRows(IEnumerable<TItem> items)
    {
        if (_dataSource is not ICollection<TItem> { IsReadOnly: false } collection) return 0;

        var list = _dataSource as IList<TItem>;
        var removed = new List<(TItem Item, int Index)>();
        foreach (var item in items.Distinct(IdentityComparer<TItem>.Instance).ToList())
        {
            var index = list?.IndexOf(item) ?? -1;
            if (collection.Remove(item)) removed.Add((item, index));
        }

        if (removed.Count == 0) return 0;

        PushUndo(new UndoEntry(
            "Delete rows",
            () =>
            {
                foreach (var (item, index) in removed.AsEnumerable().Reverse())
                {
                    if (list != null && index >= 0 && index <= list.Count) list.Insert(index, item); else collection.Add(item);
                }
            },
            () => { foreach (var (item, _) in removed) collection.Remove(item); },
            Array.Empty<GridEditRecord<TItem>>(),
            IsBatchMode));
        if (IsBatchMode) _pendingDeleted.AddRange(removed.Select(r => (object)r.Item!));
        (_boundDataSource as IInvalidatableDataSource)?.Invalidate();
        Selection.Clear();
        _ = RefreshSafeAsync(LoadKind.Data);
        return removed.Count;
    }

    // ---- row reordering & drag and drop ----

    /// <summary>Moves a row within an in-memory list source. Indexes are positions in the unfiltered source list.</summary>
    public bool ReorderRow(int fromIndex, int toIndex)
    {
        if (_dataSource is IList<TItem> list && fromIndex >= 0 && fromIndex < list.Count && toIndex >= 0 && toIndex < list.Count && fromIndex != toIndex)
        {
            var item = list[fromIndex];
            list.RemoveAt(fromIndex);
            list.Insert(toIndex, item);
            PushUndo(new UndoEntry(
                "Move row",
                () => { list.RemoveAt(toIndex); list.Insert(fromIndex, item); },
                () => { list.RemoveAt(fromIndex); list.Insert(toIndex, item); },
                Array.Empty<GridEditRecord<TItem>>(),
                false));
            (_boundDataSource as IInvalidatableDataSource)?.Invalidate();
            _ = RefreshSafeAsync(LoadKind.Data);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Moves rows (kept in their source order) so they sit directly before <paramref name="before"/>, or at the end when it is null or not in the list.
    /// One undo step; works on in-memory list sources.
    /// </summary>
    public bool MoveRows(IEnumerable<TItem> items, TItem? before)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (_dataSource is not IList<TItem> { IsReadOnly: false } list) return false;

        var original = list.ToList();
        var selected = new HashSet<TItem>(items, IdentityComparer<TItem>.Instance);
        var moved = original.Where(selected.Contains).ToList();
        if (moved.Count == 0) return false;

        var remaining = original.Where(i => !selected.Contains(i)).ToList();
        var at = before == null ? -1 : remaining.FindIndex(i => ReferenceEquals(i, before) || EqualityComparer<TItem>.Default.Equals(i, before));
        if (at < 0) at = remaining.Count;
        var result = remaining.Take(at).Concat(moved).Concat(remaining.Skip(at)).ToList();
        if (result.SequenceEqual(original, IdentityComparer<TItem>.Instance)) return false;

        void Apply(List<TItem> order)
        {
            list.Clear();
            foreach (var item in order) list.Add(item);
            (_boundDataSource as IInvalidatableDataSource)?.Invalidate();
            _ = RefreshSafeAsync(LoadKind.Data);
        }

        Apply(result);
        PushUndo(new UndoEntry("Move rows", () => Apply(original), () => Apply(result), Array.Empty<GridEditRecord<TItem>>(), false));
        return true;
    }

    /// <summary>Moves or copies rows to another grid with the same item type, optionally before a target row (cross-grid drag and drop).</summary>
    public int TransferRows(IEnumerable<TItem> items, DataGrid<TItem> target, int? targetIndex = null, bool copy = false, Func<TItem, TItem>? clone = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        var list = items.ToList();
        if (list.Count == 0 || ReferenceEquals(target, this) && !copy) return 0;

        var added = 0;
        var at = targetIndex;
        foreach (var item in list)
        {
            var toAdd = copy && clone != null ? clone(item) : item;
            if (target.AddRow(toAdd, at)) added++;
            if (at != null) at++;
        }

        if (!copy && !ReferenceEquals(target, this)) RemoveRows(list);
        return added;
    }
}

/// <summary>A draft of one row's values for inline-row, popup and form editing. Changes reach the item atomically on commit and form one undo step.</summary>
public sealed class GridRowEditSession<TItem>
{
    private readonly DataGrid<TItem> _grid;
    private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);

    internal GridRowEditSession(DataGrid<TItem> grid, TItem item)
    {
        _grid = grid;
        Item = item;
        foreach (var column in grid.Columns.Where(c => c.IsEditable))
        {
            _values[column.Field] = column.GetValue(item);
        }
    }

    public TItem Item { get; }
    public IReadOnlyDictionary<string, object?> Values => _values;

    public void SetValue(string field, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        _values[field] = value;
    }

    /// <summary>Checks the draft against the column rules without touching the item.</summary>
    public IReadOnlyList<GridValidationError> Validate() => _grid.ValidateDraft(Item, Changed(), out _);

    /// <summary>Writes the draft to the item when it is valid; otherwise leaves the item untouched and returns the errors.</summary>
    public bool TryCommit(out IReadOnlyList<GridValidationError> errors)
    {
        var cellErrors = _grid.ValidateDraft(Item, Changed(), out var converted);
        if (cellErrors.Any(e => e.Severity == GridValidationSeverity.Error) && _grid.RejectInvalidEdits)
        {
            errors = cellErrors;
            return false;
        }

        var committed = _grid.CommitDraft(Item, converted, out var rowErrors);
        errors = cellErrors.Concat(rowErrors).ToList();
        return committed;
    }

    public void Cancel()
    {
        _values.Clear();
        foreach (var column in _grid.Columns.Where(c => c.IsEditable))
        {
            _values[column.Field] = column.GetValue(Item);
        }
    }

    public bool IsDirty => Changed().Count > 0;

    private Dictionary<string, object?> Changed()
    {
        var changed = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (field, value) in _values)
        {
            var current = _grid.FindColumn(field)?.GetValue(Item);
            if (!Equals(current, value) && !(current != null && value is string s && string.Equals(Convert.ToString(current, CultureInfo.CurrentCulture), s, StringComparison.Ordinal)))
            {
                changed[field] = value;
            }
        }
        return changed;
    }
}
