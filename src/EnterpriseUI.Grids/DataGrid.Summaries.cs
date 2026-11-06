using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnterpriseUI.Data;

namespace EnterpriseUI.Grids;

public partial class DataGrid<TItem>
{
    private readonly Dictionary<string, decimal[]> _runningTotals = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Raised after running totals were (re)computed so the view can repaint.</summary>
    public event EventHandler? RunningTotalsChanged;

    /// <summary>Footer summaries of all columns, computed over every row matching the filters.</summary>
    public IReadOnlyList<GridFooterValue> FooterValues => _footerValues;

    public IReadOnlyList<GridFooterValue> GetFooterValues(string field) =>
        _footerValues.Where(v => v.Column.Field.Equals(field, StringComparison.OrdinalIgnoreCase)).ToList();

    private IEnumerable<(GridColumn Column, GridColumnSummary Summary, string Alias)> EnumerateSummaries()
    {
        foreach (var column in Columns)
        {
            if (column.IsRunningTotal) continue;
            for (var i = 0; i < column.FooterSummaries.Count; i++)
            {
                yield return (column, column.FooterSummaries[i], $"{column.Field}#{i}");
            }
        }
    }

    private List<AggregateDescriptor> BuildFooterAggregates() =>
        EnumerateSummaries().Select(s => new AggregateDescriptor
        {
            Member = s.Column.Field,
            Function = s.Summary.Function,
            Custom = s.Summary.Custom,
            Alias = s.Alias,
            ValueSelector = s.Column.ValueSelector
        }).ToList();

    private List<GridFooterValue> BuildFooterValues(IReadOnlyList<AggregateResult> results)
    {
        var values = new List<GridFooterValue>();
        foreach (var (column, summary, alias) in EnumerateSummaries())
        {
            var value = results.FirstOrDefault(r => r.Key == alias)?.Value;
            values.Add(new GridFooterValue(column, summary, value, FormatFooter(column, summary, value)));
        }
        return values;
    }

    private string FormatFooter(GridColumn column, GridColumnSummary summary, object? value)
    {
        if (value == null) return string.Empty;
        var label = summary.Label ?? summary.Function switch
        {
            AggregateFunction.Average => "Avg",
            AggregateFunction.CountDistinct => "Distinct",
            AggregateFunction.StdDev => "Std dev",
            _ => summary.Function.ToString()
        };

        if (!string.IsNullOrEmpty(summary.Format))
        {
            if (summary.Format.Contains('{'))
            {
                try
                {
                    return string.Format(CultureInfo.CurrentCulture, summary.Format, value);
                }
                catch (FormatException)
                {
                    // Fall back to the default layout for an invalid format.
                }
            }
            else if (value is IFormattable formattableValue)
            {
                try
                {
                    return $"{label}: {formattableValue.ToString(summary.Format, CultureInfo.CurrentCulture)}";
                }
                catch (FormatException)
                {
                }
            }
        }

        // A count is never in the column's unit, so the column format (e.g. currency) does not apply to it.
        var isCount = summary.Custom == null ? summary.Function is AggregateFunction.Count or AggregateFunction.CountDistinct : value is not (decimal or double or float);
        var text = isCount && value is IFormattable countValue ? countValue.ToString("N0", CultureInfo.CurrentCulture) : FormatCellValue(column, value);
        return $"{label}: {text}";
    }

    // ------------------------------------------------------------------
    // Running totals
    // ------------------------------------------------------------------

    private IEnumerable<GridColumn> RunningTotalColumns() => Columns.Where(c => c.IsRunningTotal);

    private async Task ComputeRunningTotalsSafeAsync()
    {
        try
        {
            await ComputeRunningTotalsAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _lastError = ex;
            DataLoadFailed?.Invoke(this, new GridDataLoadFailedEventArgs(ex));
        }
    }

    /// <summary>Accumulates each running-total column down the current sort order, across all pages.</summary>
    public async Task ComputeRunningTotalsAsync(CancellationToken cancellationToken = default)
    {
        var columns = RunningTotalColumns().ToList();
        if (columns.Count == 0) return;

        IReadOnlyList<TItem> rows = IsMaterialized ? _materialized : await ReadAllAsync(null, cancellationToken).ConfigureAwait(true);
        foreach (var column in columns)
        {
            var source = FindColumn(column.RunningTotalOf!);
            var totals = new decimal[rows.Count];
            decimal running = 0;
            for (var i = 0; i < rows.Count; i++)
            {
                var value = source != null ? source.GetValue(rows[i]) : PropertyPath.GetValue(rows[i], column.RunningTotalOf!);
                if (NumericValue.TryGet(value, out var number)) running += number;
                totals[i] = running;
            }
            _runningTotals[column.Field] = totals;
        }

        RunningTotalsChanged?.Invoke(this, EventArgs.Empty);
    }

    public decimal? GetRunningTotal(GridRow<TItem> row, GridColumn column)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(column);
        return row.DataIndex >= 0 && _runningTotals.TryGetValue(column.Field, out var totals) && row.DataIndex < totals.Length ? totals[row.DataIndex] : null;
    }

    /// <summary>Cell value for a displayed row: running total, calculated value or property of the row object.</summary>
    public object? GetRowCellValue(GridRow<TItem> row, GridColumn column)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(column);
        if (column.IsRunningTotal) return GetRunningTotal(row, column);

        if (row.Kind is GridRowKind.Data or GridRowKind.DetailSummary) return column.GetValue(row.Item);
        return null;
    }
}
