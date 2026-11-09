using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EnterpriseUI.Data;

namespace EnterpriseUI.Grids;

public sealed class PivotValueField
{
    public PivotValueField(string field, AggregateFunction function = AggregateFunction.Sum, string? caption = null, string? format = null)
    {
        Field = field;
        Function = function;
        Caption = caption ?? $"{function} of {field}";
        Format = format;
    }

    public string Field { get; set; }
    public AggregateFunction Function { get; set; }
    public string Caption { get; set; }
    public string? Format { get; set; }
}

public enum PivotRowKind { Detail, Subtotal, GrandTotal }

public sealed record PivotHeaderCell(string Text, int Start, int Span, int Level, bool IsTotal);

public sealed class PivotRow
{
    public PivotRow(PivotRowKind kind, int level, string key, string[] labels, object?[] values)
    {
        Kind = kind;
        Level = level;
        Key = key;
        Labels = labels;
        Values = values;
    }

    public PivotRowKind Kind { get; }

    /// <summary>Row field index the row belongs to: detail rows use the last field, a subtotal uses the field it closes.</summary>
    public int Level { get; }

    /// <summary>Identifies the row (and the group of a subtotal) for collapsing.</summary>
    public string Key { get; }

    /// <summary>One label per row field; repeated labels of the previous row are blank so groups read hierarchically.</summary>
    public string[] Labels { get; }

    public object?[] Values { get; }
    public bool IsCollapsed { get; internal set; }

    /// <summary>Per label slot, the group key to toggle collapse with <see cref="PivotEngine{TItem}.CollapsedGroups"/>; null when the slot is not a group header.</summary>
    public string?[] GroupKeys { get; internal set; } = Array.Empty<string?>();

    public bool IsExpandable => GroupKeys.Any(k => k != null);
}

public sealed class PivotResult
{
    public PivotResult(IReadOnlyList<string> rowCaptions, IReadOnlyList<IReadOnlyList<PivotHeaderCell>> headerRows, IReadOnlyList<PivotHeaderCell> leafHeaders, IReadOnlyList<PivotRow> rows, IReadOnlyList<PivotValueField> valueFields)
    {
        RowCaptions = rowCaptions;
        HeaderRows = headerRows;
        LeafHeaders = leafHeaders;
        Rows = rows;
        ValueFields = valueFields;
    }

    public IReadOnlyList<string> RowCaptions { get; }
    public IReadOnlyList<IReadOnlyList<PivotHeaderCell>> HeaderRows { get; }

    /// <summary>One header per value column, left to right, in the same order as <see cref="PivotRow.Values"/>.</summary>
    public IReadOnlyList<PivotHeaderCell> LeafHeaders { get; }

    public IReadOnlyList<PivotRow> Rows { get; }
    public IReadOnlyList<PivotValueField> ValueFields { get; }
    public int ColumnCount => LeafHeaders.Count;

    public string FormatValue(int column, object? value)
    {
        if (value == null) return string.Empty;
        var valueField = ValueFields[column % ValueFields.Count];
        return !string.IsNullOrEmpty(valueField.Format) && value is IFormattable formattable ? formattable.ToString(valueField.Format, CultureInfo.CurrentCulture) : Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty;
    }
}

/// <summary>
/// Cross-tab engine: groups items by row fields and column fields and aggregates value fields. Supports subtotals per row level, grand totals
/// for rows and columns, several value fields and collapsing of row groups.
/// </summary>
public sealed class PivotEngine<TItem>
{
    private const char Separator = '\u001F';

    public List<string> RowFields { get; } = new();
    public List<string> ColumnFields { get; } = new();
    public List<PivotValueField> Values { get; } = new();
    public bool ShowSubtotals { get; set; } = true;
    public bool ShowGrandTotals { get; set; } = true;

    /// <summary>Keys of row groups shown collapsed (only their subtotal row). Keys come from <see cref="PivotRow.Key"/>.</summary>
    public HashSet<string> CollapsedGroups { get; } = new(StringComparer.Ordinal);

    public PivotResult Compute(IEnumerable<TItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var values = Values.Count > 0 ? Values.ToList() : new List<PivotValueField> { new("*", AggregateFunction.Count, "Count") };
        var list = items.Where(i => i != null).ToList();

        var rowKeys = list.Select(i => Key(i, RowFields)).ToList();
        var colKeys = list.Select(i => Key(i, ColumnFields)).ToList();
        var distinctColumns = Enumerable.Range(0, list.Count).GroupBy(i => colKeys[i].Text).Select(g => colKeys[g.First()]).ToList();
        distinctColumns.Sort(CompareKeys);

        var cells = new Dictionary<(string Row, string Column), List<TItem>>();
        for (var i = 0; i < list.Count; i++)
        {
            var id = (rowKeys[i].Text, colKeys[i].Text);
            if (!cells.TryGetValue(id, out var bucket)) cells[id] = bucket = new List<TItem>();
            bucket.Add(list[i]);
        }

        var headerRows = BuildHeaders(distinctColumns, values, out var leafHeaders, out var leafColumns);
        var rows = BuildRows(list, rowKeys, colKeys, leafColumns, values);

        var captions = RowFields.Select(f => f).ToList();
        return new PivotResult(captions, headerRows, leafHeaders, rows, values);
    }

    private sealed record CompositeKey(string Text, object?[] Parts);

    private sealed record LeafColumn(string? ColumnKey, int ValueIndex);

    private static CompositeKey Key(TItem item, List<string> fields)
    {
        var parts = fields.Select(f => PropertyPath.GetValue(item, f)).ToArray();
        return new CompositeKey(string.Join(Separator, parts.Select(p => Convert.ToString(p, CultureInfo.InvariantCulture) ?? string.Empty)), parts);
    }

    private static int CompareKeys(CompositeKey a, CompositeKey b)
    {
        for (var i = 0; i < Math.Min(a.Parts.Length, b.Parts.Length); i++)
        {
            var c = ValueComparer.Instance.Compare(a.Parts[i], b.Parts[i]);
            if (c != 0) return c;
        }
        return 0;
    }

    private static string Text(object? part) => Convert.ToString(part, CultureInfo.InvariantCulture) ?? string.Empty;

    private static bool SamePrefix(CompositeKey a, CompositeKey b, int length)
    {
        for (var i = 0; i < length; i++)
        {
            if (Text(a.Parts[i]) != Text(b.Parts[i])) return false;
        }
        return true;
    }

    private static string PrefixKey(CompositeKey key, int length) => string.Join(Separator, key.Parts.Take(length).Select(Text));

    private List<IReadOnlyList<PivotHeaderCell>> BuildHeaders(List<CompositeKey> columns, List<PivotValueField> values, out List<PivotHeaderCell> leafHeaders, out List<LeafColumn> leafColumns)
    {
        var levels = Enumerable.Range(0, ColumnFields.Count).Select(_ => new List<PivotHeaderCell>()).ToList();
        var showValueLevel = values.Count > 1 || ColumnFields.Count == 0;
        var valueLevel = new List<PivotHeaderCell>();
        var headers = new List<PivotHeaderCell>();
        var leaves = new List<LeafColumn>();
        var position = 0;

        void AddLeaves(string? columnKey, bool isTotal)
        {
            for (var v = 0; v < values.Count; v++)
            {
                leaves.Add(new LeafColumn(columnKey, v));
                var leaf = new PivotHeaderCell(values[v].Caption, position++, 1, ColumnFields.Count, isTotal);
                headers.Add(leaf);
                valueLevel.Add(leaf);
            }
        }

        CompositeKey? previous = null;
        foreach (var column in columns)
        {
            var start = position;
            AddLeaves(column.Text, false);
            for (var l = 0; l < ColumnFields.Count; l++)
            {
                if (previous != null && SamePrefix(previous, column, l + 1))
                {
                    levels[l][^1] = levels[l][^1] with { Span = levels[l][^1].Span + values.Count };
                }
                else
                {
                    levels[l].Add(new PivotHeaderCell(Convert.ToString(column.Parts[l], CultureInfo.CurrentCulture) ?? string.Empty, start, values.Count, l, false));
                }
            }
            previous = column;
        }

        if (ColumnFields.Count > 0 && ShowGrandTotals)
        {
            var start = position;
            AddLeaves(null, true);
            levels[0].Add(new PivotHeaderCell("Grand total", start, values.Count, 0, true));
            for (var l = 1; l < ColumnFields.Count; l++) levels[l].Add(new PivotHeaderCell(string.Empty, start, values.Count, l, true));
        }
        else if (ColumnFields.Count == 0)
        {
            leaves.Clear();
            headers.Clear();
            valueLevel.Clear();
            position = 0;
            AddLeaves(string.Empty, false);
        }

        leafHeaders = headers;
        leafColumns = leaves;
        var result = new List<IReadOnlyList<PivotHeaderCell>>(levels);
        if (showValueLevel) result.Add(valueLevel);
        return result;
    }

    private List<PivotRow> BuildRows(List<TItem> items, List<CompositeKey> rowKeys, List<CompositeKey> colKeys, List<LeafColumn> leafColumns, List<PivotValueField> values)
    {
        var rows = new List<PivotRow>();
        var fieldCount = RowFields.Count;
        if (fieldCount == 0)
        {
            rows.Add(new PivotRow(PivotRowKind.GrandTotal, 0, string.Empty, new[] { "Total" }, AggregateRows(Enumerable.Range(0, items.Count), items, colKeys, leafColumns, values)));
            return rows;
        }

        var byRow = Enumerable.Range(0, items.Count).GroupBy(i => rowKeys[i].Text).ToDictionary(g => g.Key, g => g.ToList());
        var distinct = byRow.Keys.Select(k => rowKeys[byRow[k][0]]).ToList();
        distinct.Sort(CompareKeys);
        var emitted = new HashSet<string>(StringComparer.Ordinal);

        IEnumerable<int> IndexesUnder(List<CompositeKey> keys) => keys.SelectMany(k => byRow[k.Text]);

        void EmitGroup(List<CompositeKey> keys, int level)
        {
            foreach (var group in keys.GroupBy(k => Text(k.Parts[level])))
            {
                var members = group.ToList();
                var first = members[0];
                var prefix = PrefixKey(first, level + 1);
                var label = Convert.ToString(first.Parts[level], CultureInfo.CurrentCulture) ?? string.Empty;

                if (level == fieldCount - 1)
                {
                    foreach (var key in members)
                    {
                        rows.Add(CreateRow(PivotRowKind.Detail, key, level, null, byRow[key.Text], items, colKeys, leafColumns, values, emitted));
                    }
                    continue;
                }

                if (CollapsedGroups.Contains(prefix))
                {
                    rows.Add(CreateRow(PivotRowKind.Subtotal, first, level, label, IndexesUnder(members), items, colKeys, leafColumns, values, emitted, collapsed: true));
                    continue;
                }

                EmitGroup(members, level + 1);
                if (ShowSubtotals) rows.Add(CreateRow(PivotRowKind.Subtotal, first, level, "Total " + label, IndexesUnder(members), items, colKeys, leafColumns, values, emitted));
            }
        }

        EmitGroup(distinct, 0);

        if (ShowGrandTotals)
        {
            var labels = Enumerable.Range(0, fieldCount).Select(i => i == 0 ? "Grand total" : string.Empty).ToArray();
            rows.Add(new PivotRow(PivotRowKind.GrandTotal, 0, string.Empty, labels, AggregateRows(Enumerable.Range(0, items.Count), items, colKeys, leafColumns, values)));
        }

        return rows;
    }

    private PivotRow CreateRow(PivotRowKind kind, CompositeKey key, int level, string? subtotalLabel, IEnumerable<int> indexes, List<TItem> items, List<CompositeKey> colKeys, List<LeafColumn> leafColumns, List<PivotValueField> values, HashSet<string> emitted, bool collapsed = false)
    {
        var fieldCount = RowFields.Count;
        var labels = Enumerable.Repeat(string.Empty, fieldCount).ToArray();
        var groupKeys = new string?[fieldCount];

        if (kind == PivotRowKind.Detail)
        {
            for (var l = 0; l < fieldCount; l++)
            {
                var prefix = PrefixKey(key, l + 1);
                if (!emitted.Add(prefix)) continue;
                labels[l] = Convert.ToString(key.Parts[l], CultureInfo.CurrentCulture) ?? string.Empty;
                if (l < fieldCount - 1) groupKeys[l] = prefix;
            }
        }
        else
        {
            labels[level] = subtotalLabel ?? string.Empty;
            if (collapsed)
            {
                var prefix = PrefixKey(key, level + 1);
                emitted.Add(prefix);
                groupKeys[level] = prefix;
            }
            else
            {
                groupKeys[level] = null;
            }
            for (var l = 0; l <= level; l++) emitted.Add(PrefixKey(key, l + 1));
        }

        var row = new PivotRow(kind, level, PrefixKey(key, kind == PivotRowKind.Detail ? fieldCount : level + 1), labels, AggregateRows(indexes, items, colKeys, leafColumns, values))
        {
            IsCollapsed = collapsed,
            GroupKeys = groupKeys
        };
        return row;
    }

    private static object?[] AggregateRows(IEnumerable<int> indexes, List<TItem> items, List<CompositeKey> colKeys, List<LeafColumn> leafColumns, List<PivotValueField> values)
    {
        var rowIndexes = indexes.ToList();
        var byColumn = rowIndexes.GroupBy(i => colKeys[i].Text).ToDictionary(g => g.Key, g => g.Select(i => items[i]).ToList());
        var result = new object?[leafColumns.Count];
        for (var c = 0; c < leafColumns.Count; c++)
        {
            var leaf = leafColumns[c];
            var matching = leaf.ColumnKey == null ? rowIndexes.Select(i => items[i]).ToList() : byColumn.GetValueOrDefault(leaf.ColumnKey);
            result[c] = matching == null || matching.Count == 0 ? null : Aggregate(matching, values[leaf.ValueIndex]);
        }
        return result;
    }

    private static object? Aggregate(List<TItem> items, PivotValueField field)
    {
        if (field.Function == AggregateFunction.Count && field.Field == "*") return items.Count;

        var raw = items.Select(i => PropertyPath.GetValue(i, field.Field)).ToList();
        switch (field.Function)
        {
            case AggregateFunction.Count:
                return raw.Count(v => v != null);
            case AggregateFunction.CountDistinct:
                return raw.Where(v => v != null).Distinct().Count();
            case AggregateFunction.Min:
                return raw.Where(v => v != null).OrderBy(v => v, ValueComparer.Instance).FirstOrDefault();
            case AggregateFunction.Max:
                return raw.Where(v => v != null).OrderBy(v => v, ValueComparer.Instance).LastOrDefault();
        }

        var numbers = new List<decimal>();
        foreach (var value in raw)
        {
            if (NumericValue.TryGet(value, out var number)) numbers.Add(number);
        }
        if (numbers.Count == 0) return null;

        return field.Function switch
        {
            AggregateFunction.Average => numbers.Average(),
            AggregateFunction.First => numbers[0],
            AggregateFunction.Last => numbers[^1],
            AggregateFunction.Median => Median(numbers),
            AggregateFunction.StdDev => (decimal)Math.Sqrt((double)Variance(numbers)),
            AggregateFunction.Variance => Variance(numbers),
            _ => numbers.Sum()
        };
    }

    private static decimal Median(List<decimal> numbers)
    {
        var sorted = numbers.OrderBy(n => n).ToList();
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }

    private static decimal Variance(List<decimal> numbers)
    {
        var mean = numbers.Average();
        return numbers.Sum(n => (n - mean) * (n - mean)) / numbers.Count;
    }
}
