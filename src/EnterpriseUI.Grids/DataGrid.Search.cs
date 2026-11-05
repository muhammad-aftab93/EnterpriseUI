using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnterpriseUI.Data;

namespace EnterpriseUI.Grids;

public enum GridSearchMode
{
    /// <summary>Rows that do not contain the text are hidden (a filter over all visible columns).</summary>
    Filter,
    /// <summary>All rows stay; matches are counted, highlighted and navigable with next/previous.</summary>
    Highlight
}

public sealed class GridSearchOptions
{
    public bool IgnoreCase { get; set; } = true;

    /// <summary>Also search child tables of master/detail relations.</summary>
    public bool IncludeHierarchy { get; set; } = true;

    /// <summary>Expand collapsed groups, tree nodes and detail rows that contain a match.</summary>
    public bool ExpandToReveal { get; set; } = true;

    public int MaxMatches { get; set; } = 100_000;
}

/// <summary>A cell containing the search text. <see cref="RowIndex"/> counts all displayed rows across pages.</summary>
public sealed record GridSearchMatch(int RowIndex, string Field, int Start, int Length);

public sealed class GridSearchResult
{
    public GridSearchResult(string text, IReadOnlyList<GridSearchMatch> matches, bool truncated)
    {
        Text = text;
        Matches = matches;
        Truncated = truncated;
    }

    public string Text { get; }
    public IReadOnlyList<GridSearchMatch> Matches { get; }
    public bool Truncated { get; }
    public int MatchCount => Matches.Count;
    public int MatchedRowCount => Matches.Select(m => m.RowIndex).Distinct().Count();
}

public partial class DataGrid<TItem>
{
    private int _searchVersion;

    public GridSearchOptions SearchOptions { get; set; } = new();

    public GridSearchMode SearchMode { get; set; } = GridSearchMode.Filter;

    public GridSearchResult? SearchResult { get; private set; }

    public int CurrentMatchIndex { get; private set; } = -1;

    /// <summary>Text to highlight in cells; the search panel text while one is set.</summary>
    public string? HighlightText => string.IsNullOrWhiteSpace(_searchPanelText) ? null : _searchPanelText.Trim();

    public event EventHandler? SearchResultChanged;

    /// <summary>Asks the view to scroll to a page-local display row (raised when navigating matches).</summary>
    public event EventHandler<int>? ScrollToRowRequested;

    private async Task SearchSafeAsync(string? text)
    {
        try
        {
            await SearchAsync(text).ConfigureAwait(true);
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

    /// <summary>Character ranges of every case-insensitive occurrence of <paramref name="term"/> in <paramref name="text"/>.</summary>
    public static IReadOnlyList<(int Start, int Length)> FindRanges(string? text, string? term, bool ignoreCase = true)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(term)) return Array.Empty<(int, int)>();

        var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var ranges = new List<(int, int)>();
        var index = 0;
        while ((index = text.IndexOf(term, index, comparison)) >= 0)
        {
            ranges.Add((index, term.Length));
            index += term.Length;
        }
        return ranges;
    }

    private bool CellMatches(object? rowObject, GridColumn column, string term, StringComparison comparison, out string text)
    {
        text = FormatCellValue(column, column.GetValue(rowObject));
        return text.Length > 0 && text.Contains(term, comparison);
    }

    private bool RowMatches(object? rowObject, IReadOnlyList<GridColumn> columns, string term, StringComparison comparison) =>
        columns.Any(c => CellMatches(rowObject, c, term, comparison, out _));

    /// <summary>Counts and locates every cell containing <paramref name="text"/> across all pages, groups and (optionally) child tables.</summary>
    public async Task<GridSearchResult> SearchAsync(string? text, GridSearchOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= SearchOptions;
        var version = Interlocked.Increment(ref _searchVersion);
        text = text?.Trim();

        if (string.IsNullOrEmpty(text) || _boundDataSource == null)
        {
            SearchResult = null;
            CurrentMatchIndex = -1;
            SearchResultChanged?.Invoke(this, EventArgs.Empty);
            return new GridSearchResult(text ?? string.Empty, Array.Empty<GridSearchMatch>(), false);
        }

        var comparison = options.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var columns = GridColumnLayout.OrderVisible(Columns).Where(c => c.Searchable).ToList();
        var matches = new List<GridSearchMatch>();
        var truncated = false;

        var hierarchical = EnableMasterDetail && options.IncludeHierarchy && DetailRelations.Count > 0;
        var viewChanged = false;
        if (hierarchical)
        {
            viewChanged |= await RevealForSearchAsync(options, text, comparison, columns, cancellationToken).ConfigureAwait(true);
        }

        if (!UsesMaterializedRows)
        {
            var all = await ReadAllAsync(null, cancellationToken).ConfigureAwait(true);
            for (var i = 0; i < all.Count && !truncated; i++)
            {
                truncated = AddCellMatches(matches, i, all[i], columns, text, comparison, options.MaxMatches);
            }
        }
        else
        {
            if (!_materializedValid)
            {
                await LoadAsync(LoadKind.Window, cancellationToken).ConfigureAwait(true);
            }

            viewChanged |= RevealGroupsAndTreeForSearch(options, text, comparison, columns);

            for (var i = 0; i < _rows.Length && !truncated; i++)
            {
                var r = _rows[i];
                switch (r.Kind)
                {
                    case GridRowKind.Data:
                        truncated = AddCellMatches(matches, i, _materialized[r.A], columns, text, comparison, options.MaxMatches);
                        break;
                    case GridRowKind.DetailData:
                    {
                        var node = _detailNodes[r.A];
                        var child = node.Items[r.B];
                        truncated = AddCellMatches(matches, i, child, GetDetailColumns(node.Relation, child), text, comparison, options.MaxMatches);
                        break;
                    }
                    case GridRowKind.GroupHeader:
                    {
                        var header = GetGroupHeaderText(MakeRow(in r, i));
                        foreach (var (start, length) in FindRanges(header, text, options.IgnoreCase))
                        {
                            matches.Add(new GridSearchMatch(i, "(group)", start, length));
                        }
                        break;
                    }
                }
            }
        }

        if (version != _searchVersion)
        {
            return new GridSearchResult(text, Array.Empty<GridSearchMatch>(), false);
        }

        if (viewChanged)
        {
            RaiseDataWindowChanged();
        }

        var result = new GridSearchResult(text, matches, truncated);
        SearchResult = result;
        CurrentMatchIndex = -1;
        SearchResultChanged?.Invoke(this, EventArgs.Empty);
        Announce(matches.Count == 0 ? "No matches found" : $"{matches.Count:N0} match{(matches.Count == 1 ? string.Empty : "es")} found", assertive: false);
        return result;
    }

    private bool AddCellMatches(List<GridSearchMatch> matches, int rowIndex, object? rowObject, IReadOnlyList<GridColumn> columns, string term, StringComparison comparison, int max)
    {
        foreach (var column in columns)
        {
            if (!CellMatches(rowObject, column, term, comparison, out var cellText)) continue;
            foreach (var (start, length) in FindRanges(cellText, term, comparison == StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(new GridSearchMatch(rowIndex, column.Field, start, length));
                if (matches.Count >= max) return true;
            }
        }
        return false;
    }

    /// <summary>Expands the detail rows that contain child matches so they show up in the row list.</summary>
    private async Task<bool> RevealForSearchAsync(GridSearchOptions options, string term, StringComparison comparison, IReadOnlyList<GridColumn> columns, CancellationToken cancellationToken)
    {
        if (!(EnableMasterDetail && options.IncludeHierarchy && DetailRelations.Count > 0)) return false;

        var items = _materializedValid ? _materialized : (await ReadAllAsync(null, cancellationToken).ConfigureAwait(true)).ToArray();
        var relations = DetailRelations;
        var changed = false;
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var relation in relations)
            {
                if (RevealChildMatches(item!, relation, term, comparison, options.ExpandToReveal, 1) && options.ExpandToReveal)
                {
                    changed |= _detailExpansions.Add(new DetailKey(item!, relation));
                }
            }
        }

        if (changed && _materializedValid && UsesMaterializedRows)
        {
            BuildRows();
        }

        return changed;
    }

    private bool RevealChildMatches(object parent, GridHierarchyRelation relation, string term, StringComparison comparison, bool reveal, int depth)
    {
        if (depth > 32) return false;

        var children = _hierarchy.GetChildren(relation, parent);
        var columns = GetDetailColumns(relation, children.Count > 0 ? children[0] : null);
        var any = false;
        foreach (var child in children)
        {
            if (RowMatches(child, columns, term, comparison)) any = true;
            foreach (var childRelation in relation.ChildRelations)
            {
                if (!RevealChildMatches(child, childRelation, term, comparison, reveal, depth + 1)) continue;
                any = true;
                if (reveal) _detailExpansions.Add(new DetailKey(child, childRelation));
            }
        }
        return any;
    }

    /// <summary>Expands collapsed groups and tree nodes that contain a matching row.</summary>
    private bool RevealGroupsAndTreeForSearch(GridSearchOptions options, string term, StringComparison comparison, IReadOnlyList<GridColumn> columns)
    {
        if (!options.ExpandToReveal || !_materializedValid) return false;
        var changed = false;

        var matched = new List<int>();
        for (var i = 0; i < _materialized.Length; i++)
        {
            var item = _materialized[i];
            var hit = RowMatches(item, columns, term, comparison) ||
                      (EnableMasterDetail && options.IncludeHierarchy && DetailRelations.Any(r => RevealChildMatches(item!, r, term, comparison, false, 1)));
            if (hit) matched.Add(i);
        }

        if (matched.Count == 0) return false;

        if (CurrentRequest.Groups.Count > 0 && _groupTree != null)
        {
            var cursor = 0;
            changed |= RevealGroups(_groupTree, string.Empty, ref cursor, matched);
        }
        else if (TreeRelation is { } tree)
        {
            changed |= RevealTreeAncestors(tree, matched);
        }

        if (changed) BuildRows();
        return changed;
    }

    private bool RevealGroups(IReadOnlyList<GroupResult<TItem>> groups, string parentPath, ref int cursor, List<int> matched)
    {
        var changed = false;
        foreach (var group in groups)
        {
            var path = $"{parentPath}/{group.Member}={GroupKeyText(group.Key)}";
            var start = cursor;
            var index = matched.BinarySearch(start);
            if (index < 0) index = ~index;
            var contains = index < matched.Count && matched[index] < start + group.ItemCount;

            if (contains && !IsGroupExpanded(path))
            {
                if (!_groupToggles.Remove(path)) _groupToggles.Add(path);
                changed = true;
            }

            if (group.Subgroups != null)
            {
                changed |= RevealGroups(group.Subgroups, path, ref cursor, matched);
            }
            else
            {
                cursor += group.ItemCount;
            }
        }
        return changed;
    }

    private bool RevealTreeAncestors(GridHierarchyRelation relation, List<int> matched)
    {
        var items = _materialized;
        var byKey = new Dictionary<object, int>(items.Length, GridKeyComparer.Instance);
        for (var i = 0; i < items.Length; i++)
        {
            var key = PropertyPath.GetValue(items[i], relation.KeyField);
            if (key != null) byKey.TryAdd(key, i);
        }

        var changed = false;
        foreach (var index in matched)
        {
            var current = index;
            for (var guard = 0; guard < items.Length; guard++)
            {
                var parentKey = PropertyPath.GetValue(items[current], relation.ForeignField);
                if (parentKey == null || !byKey.TryGetValue(parentKey, out var parent) || parent == current) break;

                var expanded = TreeExpandedByDefault ^ _treeToggles.Contains(items[parent]!);
                if (!expanded)
                {
                    if (!_treeToggles.Remove(items[parent]!)) _treeToggles.Add(items[parent]!);
                    changed = true;
                }
                current = parent;
            }
        }
        return changed;
    }

    /// <summary>Selects the next match (wrapping), switching page and scrolling as needed.</summary>
    public bool NextMatch() => MoveMatch(1);

    public bool PreviousMatch() => MoveMatch(-1);

    private bool MoveMatch(int step)
    {
        var result = SearchResult;
        if (result == null || result.MatchCount == 0) return false;

        var next = CurrentMatchIndex < 0 ? (step > 0 ? 0 : result.MatchCount - 1) : (CurrentMatchIndex + step + result.MatchCount) % result.MatchCount;
        return GoToMatch(next);
    }

    public bool GoToMatch(int matchIndex)
    {
        var result = SearchResult;
        if (result == null || matchIndex < 0 || matchIndex >= result.MatchCount) return false;

        CurrentMatchIndex = matchIndex;
        var match = result.Matches[matchIndex];
        if (IsPaged) GoToPage(match.RowIndex / _pageSize);
        var localRow = IsPaged ? match.RowIndex % _pageSize : match.RowIndex;

        var column = GridColumnLayout.OrderVisible(Columns).FindIndex(c => c.Field.Equals(match.Field, StringComparison.OrdinalIgnoreCase));
        Selection.SetFocus(localRow, Math.Max(0, column));
        ScrollToRowRequested?.Invoke(this, localRow);
        Announce($"Match {matchIndex + 1} of {result.MatchCount}");
        OnPropertyChanged(nameof(CurrentMatchIndex));
        return true;
    }

    /// <summary>True if the cell at a page-local row and field is the active search match.</summary>
    public bool IsCurrentMatch(int localRow, string field)
    {
        if (SearchResult == null || CurrentMatchIndex < 0) return false;
        var match = SearchResult.Matches[CurrentMatchIndex];
        var row = IsPaged ? match.RowIndex - PageOffset : match.RowIndex;
        return row == localRow && match.Field.Equals(field, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Type-ahead: the first row at or after <paramref name="startRow"/> (wrapping) whose value in <paramref name="field"/> starts with the typed prefix; -1 if none.</summary>
    public async Task<int> FindRowByPrefixAsync(string field, string prefix, int startRow = 0, CancellationToken cancellationToken = default)
    {
        var column = FindColumn(field);
        if (column == null || string.IsNullOrEmpty(prefix)) return -1;

        bool Starts(object? row) => FormatCellValue(column, column.GetValue(row)).StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase);

        if (IsMaterialized)
        {
            var total = _rows.Length;
            for (var n = 0; n < total; n++)
            {
                var i = (startRow + n) % total;
                var r = _rows[i];
                if (r.Kind == GridRowKind.Data && Starts(_materialized[r.A])) return i;
            }
            return -1;
        }

        var all = await ReadAllAsync(null, cancellationToken).ConfigureAwait(true);
        for (var n = 0; n < all.Count; n++)
        {
            var i = (startRow + n) % all.Count;
            if (Starts(all[i])) return i;
        }
        return -1;
    }

    /// <summary>Focuses an absolute row (all pages), switching page and requesting a scroll.</summary>
    public void FocusRow(int absoluteRow, int column = 0)
    {
        if (absoluteRow < 0) return;
        if (IsPaged) GoToPage(absoluteRow / _pageSize);
        var local = IsPaged ? absoluteRow % _pageSize : absoluteRow;
        Selection.Click(local, column);
        ScrollToRowRequested?.Invoke(this, local);
    }
}
