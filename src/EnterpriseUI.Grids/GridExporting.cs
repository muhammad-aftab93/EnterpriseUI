using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnterpriseUI.Data;
using EnterpriseUI.Exporting;

namespace EnterpriseUI.Grids;

public sealed class GridExportOptions
{
    public string? Title { get; set; }
    public string SheetName { get; set; } = "Data";

    /// <summary>Export only visible columns, in display order (pinned columns first).</summary>
    public bool VisibleColumnsOnly { get; set; } = true;

    /// <summary>Export only the rows of the current page (flat grids).</summary>
    public bool CurrentPageOnly { get; set; }

    public bool IncludeGroups { get; set; } = true;

    /// <summary>Export the child tables of master/detail relations (all rows, not only expanded ones).</summary>
    public bool IncludeHierarchy { get; set; } = true;

    /// <summary>Write each relation to its own sheet with a Parent column instead of nesting the rows under their parent.</summary>
    public bool HierarchyAsSheets { get; set; }

    public bool IncludeSummaries { get; set; } = true;
    public bool ApplyConditionalFormatting { get; set; } = true;
    public bool IncludeBands { get; set; } = true;
    public int? MaxRows { get; set; }
}

/// <summary>Builds export documents from a <see cref="DataGrid{TItem}"/> and runs them through <see cref="ExportService"/>.</summary>
public static class GridExporter
{
    private const int MaxHierarchyDepth = 32;

    private static readonly Dictionary<string, string> ExtensionFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        [".xlsx"] = "xlsx",
        [".csv"] = "csv",
        [".pdf"] = "pdf",
        [".html"] = "html",
        [".htm"] = "html",
        [".json"] = "json",
        [".xml"] = "xml"
    };

    public static string FormatIdForPath(string path) =>
        ExtensionFormats.TryGetValue(Path.GetExtension(path), out var id) ? id : throw new ArgumentException($"Cannot infer the export format from '{Path.GetExtension(path)}'.", nameof(path));

    public static Task<ExportResult> ExportAsync<TItem>(
        DataGrid<TItem> grid,
        string formatId,
        Stream output,
        GridExportOptions? gridOptions = null,
        ExportOptions? options = null,
        IProgress<ExportProgress>? progress = null,
        ExportService? service = null,
        CancellationToken cancellationToken = default) =>
        ExportCoreAsync(grid, formatId, gridOptions, options, progress, service, cancellationToken, (doc, opts, prog, svc) => svc.ExportAsync(doc, formatId, output, opts, prog, cancellationToken));

    /// <summary>Exports to a file; the format comes from the extension. The file only appears when the export succeeded.</summary>
    public static Task<ExportResult> ExportToFileAsync<TItem>(
        DataGrid<TItem> grid,
        string path,
        GridExportOptions? gridOptions = null,
        ExportOptions? options = null,
        IProgress<ExportProgress>? progress = null,
        ExportService? service = null,
        CancellationToken cancellationToken = default)
    {
        var formatId = FormatIdForPath(path);
        return ExportCoreAsync(grid, formatId, gridOptions, options, progress, service, cancellationToken, (doc, opts, prog, svc) => svc.ExportToFileAsync(doc, formatId, path, opts, prog, cancellationToken));
    }

    private static async Task<ExportResult> ExportCoreAsync<TItem>(
        DataGrid<TItem> grid,
        string formatId,
        GridExportOptions? gridOptions,
        ExportOptions? options,
        IProgress<ExportProgress>? progress,
        ExportService? service,
        CancellationToken cancellationToken,
        Func<ExportDocument, ExportOptions, IProgress<ExportProgress>?, ExportService, Task<ExportResult>> run)
    {
        gridOptions ??= new GridExportOptions();
        options ??= new ExportOptions();
        options.IncludeGroups &= gridOptions.IncludeGroups;
        options.IncludeDetails &= gridOptions.IncludeHierarchy;
        options.IncludeSummaries &= gridOptions.IncludeSummaries;
        options.ApplyFormatting &= gridOptions.ApplyConditionalFormatting;
        service ??= ExportService.Default;

        var document = await CreateDocumentAsync(grid, gridOptions, cancellationToken).ConfigureAwait(false);
        return await run(document, options, progress, service).ConfigureAwait(false);
    }

    public static async Task<ExportDocument> CreateDocumentAsync<TItem>(DataGrid<TItem> grid, GridExportOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grid);
        options ??= new GridExportOptions();

        var columns = ResolveColumns(grid, options);
        var exportColumns = columns.Select(c => ToExportColumn(grid, c, options)).ToList();
        var grouped = options.IncludeGroups && grid.CurrentRequest.Groups.Count > 0;
        var tree = grid.TreeHierarchy;
        var relations = options.IncludeHierarchy ? grid.MasterDetailRelations.ToList() : new List<GridHierarchyRelation>();

        IReadOnlyList<TItem> items;
        IReadOnlyList<GroupResult<TItem>>? groups = null;
        if (grouped)
        {
            (items, groups) = await grid.ReadGroupedAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            items = await grid.ReadAllAsync(options.MaxRows, cancellationToken).ConfigureAwait(false);
            if (options.CurrentPageOnly && grid.IsPaged)
            {
                items = items.Skip(grid.PageIndex * grid.PageSize).Take(grid.PageSize).ToList();
            }
        }

        if (options.MaxRows is { } max && items.Count > max)
        {
            items = items.Take(max).ToList();
        }

        foreach (var lazy in relations.Where(r => r.LoadChildrenAsync != null && r.ChildSource == null && r.DetailItemsSelector == null))
        {
            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await grid.LoadChildrenAsync(lazy, item!, cancellationToken).ConfigureAwait(false);
            }
        }

        var builder = new RowBuilder<TItem>(grid, columns, options, relations, tree);
        var conditionalFormats = options.ApplyConditionalFormatting ? BuildConditionalFormats(grid) : new List<ExportConditionalFormat>();
        var nested = relations.Count > 0 && !options.HierarchyAsSheets;

        var sheet = new ExportSheet(options.SheetName, exportColumns, builder.Rows(items, groups))
        {
            Title = options.Title,
            IsStructured = grouped || tree != null || nested,
            AutoFilter = !(grouped || tree != null || nested)
        };
        if (!nested && tree == null)
        {
            sheet.RowCountHint = items.Count + (groups == null ? 0 : CountGroups(groups));
        }

        foreach (var format in conditionalFormats) sheet.ConditionalFormats.Add(format);
        if (options.IncludeSummaries) sheet.Footer = builder.FooterRows(grid.FooterValues);

        var document = new ExportDocument { Title = options.Title ?? grid.Name ?? "Data Grid" };
        document.Sheets.Add(sheet);

        if (relations.Count > 0 && options.HierarchyAsSheets)
        {
            var parents = items.Cast<object>().ToList();
            foreach (var relation in relations)
            {
                AddRelationSheets(grid, document, relation, parents, relation.Title, 1);
            }
        }

        return document;
    }

    private static int CountGroups<TItem>(IReadOnlyList<GroupResult<TItem>> groups) =>
        groups.Sum(g => 1 + (g.Subgroups == null ? 0 : CountGroups(g.Subgroups)));

    private static List<GridColumn> ResolveColumns<TItem>(DataGrid<TItem> grid, GridExportOptions options)
    {
        var source = options.VisibleColumnsOnly ? GridColumnLayout.OrderVisible(grid.Columns) : grid.Columns.ToList();
        return source.Where(c => c.ColumnType != GridColumnType.Command && c is not GridCommandColumn).ToList();
    }

    private static ExportColumn ToExportColumn<TItem>(DataGrid<TItem> grid, GridColumn column, GridExportOptions options)
    {
        var type = column.IsCalculated || column.IsRunningTotal ? typeof(decimal?) : PropertyPath.GetPathType(typeof(TItem), column.Field);
        var underlying = type == null ? null : Nullable.GetUnderlyingType(type) ?? type;
        var numeric = underlying != null && (underlying.IsPrimitive && underlying != typeof(bool) && underlying != typeof(char) || underlying == typeof(decimal));
        var alignment = column.Alignment switch
        {
            ColumnTextAlignment.Right => ExportAlignment.Right,
            ColumnTextAlignment.Center => ExportAlignment.Center,
            _ => numeric ? ExportAlignment.Right : ExportAlignment.Left
        };

        return new ExportColumn(column.Field, string.IsNullOrEmpty(column.Title) ? column.Field : column.Title)
        {
            Width = GridColumnLayout.WidthOf(column),
            Alignment = alignment,
            Format = column.FormatString,
            DataType = type,
            Bands = options.IncludeBands ? GridColumnLayout.GetBandPath(column, grid.Bands).Select(b => b.Title).ToList() : Array.Empty<string>()
        };
    }

    private static List<ExportConditionalFormat> BuildConditionalFormats<TItem>(DataGrid<TItem> grid)
    {
        var formats = new List<ExportConditionalFormat>();
        foreach (var rule in grid.FormatRules.Where(r => r.Enabled && !r.ApplyToRow && r.RuleType is FormatRuleType.DataBar or FormatRuleType.ColorScale))
        {
            if (!NumericValue.TryGet(rule.Value1, out var min) || !NumericValue.TryGet(rule.Value2, out var max)) continue;
            formats.Add(new ExportConditionalFormat
            {
                ColumnKey = rule.ColumnField,
                Type = rule.RuleType == FormatRuleType.DataBar ? ExportConditionalFormatType.DataBar : ExportConditionalFormatType.ColorScale,
                Minimum = (double)min,
                Maximum = (double)max,
                ColorHex = rule.BackgroundColorHex ?? "#5088D7",
                SecondColorHex = rule.ForegroundColorHex
            });
        }
        return formats;
    }

    private static void AddRelationSheets<TItem>(DataGrid<TItem> grid, ExportDocument document, GridHierarchyRelation relation, IReadOnlyList<object> parents, string name, int depth)
    {
        if (depth > MaxHierarchyDepth) return;

        var first = parents.Select(p => grid.GetChildren(relation, p).FirstOrDefault()).FirstOrDefault(c => c != null);
        var childColumns = grid.GetDetailColumns(relation, first).ToList();
        var columns = new List<ExportColumn> { new("Parent") };
        columns.AddRange(childColumns.Select(c => new ExportColumn(c.Field, string.IsNullOrEmpty(c.Title) ? c.Field : c.Title) { Width = GridColumnLayout.WidthOf(c), Format = c.FormatString }));

        var allChildren = new List<object>();
        IEnumerable<ExportRow> Rows()
        {
            foreach (var parent in parents)
            {
                var parentKey = relation.KeyField.Length > 0 ? PropertyPath.GetValue(parent, relation.KeyField) : null;
                foreach (var child in grid.GetChildren(relation, parent))
                {
                    var cells = new object?[childColumns.Count + 1];
                    cells[0] = parentKey;
                    for (var i = 0; i < childColumns.Count; i++) cells[i + 1] = childColumns[i].GetValue(child);
                    allChildren.Add(child);
                    yield return new ExportRow(ExportRowKind.Data, 0, cells);
                }
            }
        }

        var rows = Rows().ToList();
        document.Sheets.Add(new ExportSheet(name, columns, rows) { RowCountHint = rows.Count });
        foreach (var child in relation.ChildRelations)
        {
            AddRelationSheets(grid, document, child, allChildren, $"{name} - {child.Title}", depth + 1);
        }
    }

    private sealed class RowBuilder<TItem>
    {
        private readonly DataGrid<TItem> _grid;
        private readonly List<GridColumn> _columns;
        private readonly GridExportOptions _options;
        private readonly List<GridHierarchyRelation> _relations;
        private readonly GridHierarchyRelation? _tree;
        private readonly Dictionary<GridColumn, decimal> _running = new();
        private readonly List<GridFormatRule> _cellRules;
        private readonly List<GridFormatRule> _rowRules;
        private int _cursor;

        public RowBuilder(DataGrid<TItem> grid, List<GridColumn> columns, GridExportOptions options, List<GridHierarchyRelation> relations, GridHierarchyRelation? tree)
        {
            _grid = grid;
            _columns = columns;
            _options = options;
            _relations = relations;
            _tree = tree;
            var rules = options.ApplyConditionalFormatting ? grid.FormatRules.Where(r => r.Enabled).ToList() : new List<GridFormatRule>();
            _cellRules = rules.Where(r => !r.ApplyToRow).ToList();
            _rowRules = rules.Where(r => r.ApplyToRow && r.RuleType == FormatRuleType.HighlightCell).ToList();
        }

        public IEnumerable<ExportRow> Rows(IReadOnlyList<TItem> items, IReadOnlyList<GroupResult<TItem>>? groups)
        {
            _running.Clear();
            _cursor = 0;

            if (groups != null)
            {
                foreach (var row in GroupRows(groups)) yield return row;
            }
            else if (_tree != null)
            {
                foreach (var row in TreeRows(items)) yield return row;
            }
            else
            {
                foreach (var item in items)
                {
                    foreach (var row in ItemRows(item, 0)) yield return row;
                }
            }
        }

        private IEnumerable<ExportRow> GroupRows(IReadOnlyList<GroupResult<TItem>> groups)
        {
            foreach (var group in groups)
            {
                var header = new ExportRow(ExportRowKind.GroupHeader, group.Level)
                {
                    Text = _grid.GetGroupHeaderText(group),
                    GroupField = group.Member,
                    GroupKey = group.Key,
                    GroupItemCount = group.ItemCount,
                    Aggregates = group.Aggregates.Select(a => new KeyValuePair<string, object?>(a.Key, a.Value)).ToList()
                };
                yield return header;

                if (group.Subgroups != null)
                {
                    foreach (var row in GroupRows(group.Subgroups)) yield return row;
                }
                else if (group.Items != null)
                {
                    foreach (var item in group.Items)
                    {
                        foreach (var row in ItemRows(item, group.Level + 1)) yield return row;
                    }
                }

                if (_grid.ShowGroupFooters && group.Aggregates.Count > 0)
                {
                    var cells = new object?[_columns.Count];
                    for (var i = 0; i < _columns.Count; i++)
                    {
                        var summary = _grid.GroupSummaries.FirstOrDefault(s => s.Member.Equals(_columns[i].Field, StringComparison.OrdinalIgnoreCase));
                        if (summary != null) cells[i] = group.FindAggregate($"{summary.Function}({summary.Member})")?.Value;
                    }
                    if (cells.Length > 0) cells[0] ??= "Total";
                    yield return new ExportRow(ExportRowKind.GroupFooter, group.Level, cells);
                }
            }
        }

        private IEnumerable<ExportRow> TreeRows(IReadOnlyList<TItem> items)
        {
            var relation = _tree!;
            var n = items.Count;
            var byKey = new Dictionary<object, int>(n, GridKeyComparer.Instance);
            var parents = new object?[n];
            for (var i = 0; i < n; i++)
            {
                var key = PropertyPath.GetValue(items[i], relation.KeyField);
                parents[i] = PropertyPath.GetValue(items[i], relation.ForeignField);
                if (key != null) byKey.TryAdd(key, i);
            }

            var children = new List<int>?[n];
            var roots = new List<int>();
            for (var i = 0; i < n; i++)
            {
                if (parents[i] != null && byKey.TryGetValue(parents[i]!, out var parent) && parent != i)
                {
                    (children[parent] ??= new List<int>()).Add(i);
                }
                else
                {
                    roots.Add(i);
                }
            }

            var visited = new bool[n];
            var stack = new Stack<(int Index, int Level)>();
            IEnumerable<int> Starts()
            {
                foreach (var root in roots) yield return root;
                // Members of a parent cycle have no root; they start their own top-level branch.
                for (var i = 0; i < n; i++) if (!visited[i]) yield return i;
            }

            foreach (var start in Starts())
            {
                if (visited[start]) continue;
                stack.Push((start, 0));
                while (stack.Count > 0)
                {
                    var (index, level) = stack.Pop();
                    if (visited[index]) continue;
                    visited[index] = true;

                    var row = DataRow(items[index], level);
                    row.HasChildren = children[index] is { Count: > 0 };
                    yield return row;

                    if (children[index] is { } list)
                    {
                        for (var c = list.Count - 1; c >= 0; c--) stack.Push((list[c], level + 1));
                    }
                }
            }
        }

        private IEnumerable<ExportRow> ItemRows(TItem item, int level)
        {
            yield return DataRow(item, level);

            if (_relations.Count == 0 || _options.HierarchyAsSheets) yield break;
            foreach (var row in DetailRows(item!, _relations, 1, level)) yield return row;
        }

        private IEnumerable<ExportRow> DetailRows(object parent, IReadOnlyList<GridHierarchyRelation> relations, int depth, int baseOutline)
        {
            if (depth > MaxHierarchyDepth) yield break;

            foreach (var relation in relations)
            {
                var children = _grid.GetChildren(relation, parent);
                var columns = _grid.GetDetailColumns(relation, children.Count > 0 ? children[0] : null);
                yield return new ExportRow(ExportRowKind.DetailHeader, depth)
                {
                    Text = relation.Title,
                    RelationName = relation.Title,
                    OutlineLevel = baseOutline + depth,
                    DetailColumns = columns.Select(c => new ExportColumn(c.Field, string.IsNullOrEmpty(c.Title) ? c.Field : c.Title) { Format = c.FormatString }).ToList()
                };

                foreach (var child in children)
                {
                    var cells = new object?[columns.Count];
                    for (var i = 0; i < columns.Count; i++) cells[i] = columns[i].GetValue(child);
                    yield return new ExportRow(ExportRowKind.DetailData, depth, cells) { OutlineLevel = baseOutline + depth };

                    if (relation.ChildRelations.Count > 0)
                    {
                        foreach (var nested in DetailRows(child, relation.ChildRelations, depth + 1, baseOutline)) yield return nested;
                    }
                }
            }
        }

        private ExportRow DataRow(TItem item, int level)
        {
            var cells = new object?[_columns.Count];
            for (var i = 0; i < _columns.Count; i++)
            {
                var column = _columns[i];
                if (column.IsRunningTotal)
                {
                    var source = _grid.FindColumn(column.RunningTotalOf!)?.GetValue(item) ?? PropertyPath.GetValue(item, column.RunningTotalOf!);
                    _running.TryGetValue(column, out var total);
                    if (NumericValue.TryGet(source, out var number)) total += number;
                    _running[column] = total;
                    cells[i] = total;
                }
                else
                {
                    cells[i] = column.GetValue(item);
                }
            }

            var row = new ExportRow(ExportRowKind.Data, level, cells);
            ApplyFormatting(row, item);
            return row;
        }

        private void ApplyFormatting(ExportRow row, TItem item)
        {
            if (_cellRules.Count == 0 && _rowRules.Count == 0) return;

            foreach (var rule in _rowRules)
            {
                var value = _grid.FindColumn(rule.ColumnField)?.GetValue(item) ?? PropertyPath.GetValue(item, rule.ColumnField);
                if (GridFormatRuleEvaluator.Matches(rule, value))
                {
                    row.Style = new ExportCellStyle { BackColorHex = rule.BackgroundColorHex, ForeColorHex = rule.ForegroundColorHex };
                }
            }

            for (var i = 0; i < _columns.Count; i++)
            {
                ExportCellStyle? style = null;
                foreach (var rule in _cellRules)
                {
                    if (!rule.ColumnField.Equals(_columns[i].Field, StringComparison.OrdinalIgnoreCase)) continue;
                    var value = row.Cells[i];

                    switch (rule.RuleType)
                    {
                        case FormatRuleType.HighlightCell when GridFormatRuleEvaluator.Matches(rule, value):
                            style ??= new ExportCellStyle();
                            style.BackColorHex = rule.BackgroundColorHex ?? style.BackColorHex;
                            style.ForeColorHex = rule.ForegroundColorHex ?? style.ForeColorHex;
                            break;

                        case FormatRuleType.DataBar when GridFormatRuleEvaluator.TryGetNormalizedValue(value, rule.Value1, rule.Value2, out var ratio):
                            style ??= new ExportCellStyle();
                            style.DataBar = ratio;
                            style.DataBarColorHex = rule.BackgroundColorHex ?? "#5088D7";
                            break;

                        case FormatRuleType.ColorScale when GridFormatRuleEvaluator.TryGetNormalizedValue(value, rule.Value1, rule.Value2, out var scale):
                            style ??= new ExportCellStyle();
                            style.BackColorHex = Blend(rule.BackgroundColorHex ?? "#F4F8FC", rule.ForegroundColorHex ?? "#6AA86E", scale);
                            break;
                    }
                }

                if (style != null)
                {
                    row.CellStyles ??= new ExportCellStyle?[_columns.Count];
                    row.CellStyles[i] = style;
                }
            }
        }

        public IReadOnlyList<ExportRow> FooterRows(IReadOnlyList<GridFooterValue> values)
        {
            if (values.Count == 0) return Array.Empty<ExportRow>();

            var depth = _columns.Max(c => c.FooterSummaries.Count);
            var rows = new List<ExportRow>();
            for (var level = 0; level < depth; level++)
            {
                var cells = new object?[_columns.Count];
                for (var i = 0; i < _columns.Count; i++)
                {
                    var forColumn = values.Where(v => ReferenceEquals(v.Column, _columns[i])).ToList();
                    if (level < forColumn.Count) cells[i] = forColumn[level].Value;
                }
                if (cells.Length > 0 && cells[0] == null) cells[0] = depth == 1 ? "Total" : $"Summary {level + 1}";
                rows.Add(new ExportRow(ExportRowKind.Summary, 0, cells));
            }
            return rows;
        }

        private static string Blend(string from, string to, double amount)
        {
            if (!ExportFormatting.TryParseColor(from, out var r1, out var g1, out var b1) || !ExportFormatting.TryParseColor(to, out var r2, out var g2, out var b2)) return from;
            amount = Math.Clamp(amount, 0, 1);
            return $"#{(int)(r1 + (r2 - r1) * amount):X2}{(int)(g1 + (g2 - g1) * amount):X2}{(int)(b1 + (b2 - b1) * amount):X2}";
        }
    }
}
