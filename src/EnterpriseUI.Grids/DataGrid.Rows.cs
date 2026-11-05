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
    private const int FlagExpandable = 1;
    private const int FlagExpanded = 2;
    private const int FlagLoading = 4;

    private struct RowRef
    {
        public GridRowKind Kind;
        public int Level;
        public int A;
        public int B;
        public int Flags;
        public int DataIndex;
    }

    private sealed class GroupNode
    {
        public GroupNode(GroupResult<TItem> group, string path, bool expanded)
        {
            Group = group;
            Path = path;
            Expanded = expanded;
        }

        public GroupResult<TItem> Group { get; }
        public string Path { get; }
        public bool Expanded { get; }
    }

    private sealed class DetailNode
    {
        public DetailNode(object parent, GridHierarchyRelation relation, IReadOnlyList<object> items, int level, bool loading)
        {
            Parent = parent;
            Relation = relation;
            Items = items;
            Level = level;
            Loading = loading;
        }

        public object Parent { get; }
        public GridHierarchyRelation Relation { get; }
        public IReadOnlyList<object> Items { get; }
        public int Level { get; }
        public bool Loading { get; }
    }

    private readonly record struct DetailKey(object Parent, GridHierarchyRelation? Relation);

    private sealed class DetailKeyComparer : IEqualityComparer<DetailKey>
    {
        public static DetailKeyComparer Instance { get; } = new();

        public bool Equals(DetailKey x, DetailKey y) =>
            ObjectIdentityComparer.Instance.Equals(x.Parent, y.Parent) && ReferenceEquals(x.Relation, y.Relation);

        public int GetHashCode(DetailKey key) =>
            HashCode.Combine(ObjectIdentityComparer.Instance.GetHashCode(key.Parent), key.Relation == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(key.Relation));
    }

    private TItem[] _materialized = Array.Empty<TItem>();
    private RowRef[] _rows = Array.Empty<RowRef>();
    private readonly List<GroupNode> _groupNodes = new();
    private readonly List<DetailNode> _detailNodes = new();
    private IReadOnlyList<GroupResult<TItem>>? _groupTree;
    private bool _materializedValid;
    private readonly GridHierarchyResolver _hierarchy = new();
    private readonly HashSet<string> _groupToggles = new(StringComparer.Ordinal);
    private readonly HashSet<DetailKey> _detailExpansions = new(DetailKeyComparer.Instance);
    private readonly HashSet<object> _treeToggles = new(ObjectIdentityComparer.Instance);
    private readonly HashSet<DetailKey> _pendingLoads = new(DetailKeyComparer.Instance);

    /// <summary>Master/detail and self-referencing relations. Call <see cref="NotifyRelationsChanged"/> after editing the list directly, or use <see cref="AddRelation"/>.</summary>
    public List<GridHierarchyRelation> Relations { get; } = new();

    public bool GroupsExpandedByDefault { get; set; } = true;
    public bool TreeExpandedByDefault { get; set; }
    public bool ShowGroupFooters { get; set; }

    /// <summary>Upper bound for rows read into memory for grouping, hierarchy and tree views.</summary>
    public int MaxMaterializedRows { get; set; } = 2_000_000;

    private GridHierarchyRelation? TreeRelation => Relations.FirstOrDefault(r =>
        r.Mode == HierarchyMode.SelfReferencing && !string.IsNullOrEmpty(r.KeyField) && !string.IsNullOrEmpty(r.ForeignField));

    private List<GridHierarchyRelation> DetailRelations => Relations.Where(r => r.Mode != HierarchyMode.SelfReferencing).ToList();

    /// <summary>True when the view needs the whole filtered result in memory: grouping, a tree, or expanded detail rows.</summary>
    private bool UsesMaterializedRows => CurrentRequest.Groups.Count > 0 || TreeRelation != null || _detailExpansions.Count > 0;

    private bool IsMaterialized => _materializedValid && UsesMaterializedRows;

    public bool IsTreeMode => TreeRelation != null;

    /// <summary>Row of the displayed page, including group headers, footers, tree rows and child-table rows.</summary>
    public bool TryGetRowAt(int index, out GridRow<TItem> row)
    {
        row = default!;
        if (index < 0 || index >= TotalItemsCount)
        {
            return false;
        }

        if (IsMaterialized)
        {
            row = MakeRow(in _rows[PageOffset + index], index);
            return true;
        }

        var local = index - _windowStart;
        if (local < 0 || local >= _window.Count)
        {
            return false;
        }

        var item = _window[local];
        row = new GridRow<TItem>
        {
            Kind = GridRowKind.Data,
            Index = index,
            Item = item,
            RowObject = item,
            DataIndex = PageOffset + index,
            IsExpandable = EnableMasterDetail
        };
        return true;
    }

    private GridRow<TItem> MakeRow(in RowRef r, int index)
    {
        var expandable = (r.Flags & FlagExpandable) != 0;
        var expanded = (r.Flags & FlagExpanded) != 0;
        switch (r.Kind)
        {
            case GridRowKind.Data:
            {
                var item = _materialized[r.A];
                return new GridRow<TItem> { Kind = GridRowKind.Data, Index = index, Level = r.Level, Item = item, RowObject = item, DataIndex = r.DataIndex, IsExpandable = expandable, IsExpanded = expanded };
            }

            case GridRowKind.GroupHeader:
            case GridRowKind.GroupFooter:
            {
                var node = _groupNodes[r.A];
                return new GridRow<TItem> { Kind = r.Kind, Index = index, Level = r.Level, Group = node.Group, GroupPath = node.Path, IsExpandable = r.Kind == GridRowKind.GroupHeader, IsExpanded = node.Expanded };
            }

            case GridRowKind.DetailHeader:
            {
                var node = _detailNodes[r.A];
                return new GridRow<TItem> { Kind = r.Kind, Index = index, Level = r.Level, Parent = node.Parent, Relation = node.Relation, IsLoading = (r.Flags & FlagLoading) != 0 };
            }

            case GridRowKind.DetailData:
            {
                var node = _detailNodes[r.A];
                var child = node.Items[r.B];
                return new GridRow<TItem> { Kind = r.Kind, Index = index, Level = r.Level, RowObject = child, Parent = node.Parent, Relation = node.Relation, IsExpandable = expandable, IsExpanded = expanded };
            }

            default:
            {
                var item = _materialized[r.A];
                return new GridRow<TItem> { Kind = GridRowKind.DetailSummary, Index = index, Level = r.Level, Item = item, RowObject = item, Parent = item };
            }
        }
    }

    // ------------------------------------------------------------------
    // Materialized loading
    // ------------------------------------------------------------------

    private async Task LoadMaterializedAsync(int version, LoadKind kind, CancellationToken cancellationToken)
    {
        if (_materializedValid && kind == LoadKind.Window)
        {
            BuildRows();
            _hasResult = true;
            _isLoading = false;
            RaiseDataWindowChanged();
            return;
        }

        var request = BuildMaterializedRequest(LoadKind.Query);

        var result = await _boundDataSource!.ReadAsync(request, cancellationToken).ConfigureAwait(true);
        if (version != _refreshVersion) return;

        if (result.Total > MaxMaterializedRows)
        {
            throw new InvalidOperationException($"Grouping, hierarchy and tree views need all matching rows in memory; {result.Total:N0} rows exceed MaxMaterializedRows ({MaxMaterializedRows:N0}). Add a filter or raise the limit.");
        }

        _materialized = result.Data as TItem[] ?? result.Data?.ToArray() ?? Array.Empty<TItem>();
        _totalRecords = result.Total;
        _groupTree = result.GroupedData as IReadOnlyList<GroupResult<TItem>> ?? result.GroupedData?.ToList();
        _footerValues = BuildFooterValues(result.AggregateResults);
        _materializedValid = true;
        BuildRows();

        _hasResult = true;
        _isLoading = false;
        RaiseDataWindowChanged();

        if (RunningTotalColumns().Any())
        {
            _ = ComputeRunningTotalsSafeAsync();
        }
    }

    private DataSourceRequest BuildMaterializedRequest(LoadKind kind)
    {
        var request = CurrentRequest.Clone();
        request.Skip = 0;
        request.Take = 0;
        request.Groups.Clear();
        foreach (var group in CurrentRequest.Groups)
        {
            var copy = new GroupDescriptor
            {
                Member = group.Member,
                SortDirection = group.SortDirection,
                KeySelector = group.KeySelector,
                KeyComparer = group.KeyComparer,
                ValueSelector = group.ValueSelector
            };
            foreach (var summary in GroupSummaries)
            {
                copy.Aggregates.Add(new AggregateDescriptor
                {
                    Member = summary.Member,
                    Function = summary.Function,
                    ValueSelector = FindColumn(summary.Member)?.ValueSelector
                });
            }
            request.Groups.Add(copy);
        }

        ApplyAggregates(request, kind);
        return request;
    }

    /// <summary>All rows matching the filters in sort order together with the group tree (null when ungrouped), read for export and printing.</summary>
    public async Task<(IReadOnlyList<TItem> Items, IReadOnlyList<GroupResult<TItem>>? Groups)> ReadGroupedAsync(CancellationToken cancellationToken = default)
    {
        if (_boundDataSource == null) return (Array.Empty<TItem>(), null);

        var request = BuildMaterializedRequest(LoadKind.Window);
        var result = await _boundDataSource.ReadAsync(request, cancellationToken).ConfigureAwait(false);
        var items = result.Data as IReadOnlyList<TItem> ?? result.Data?.ToList() ?? new List<TItem>();
        var groups = result.GroupedData as IReadOnlyList<GroupResult<TItem>> ?? result.GroupedData?.ToList();
        return (items, groups);
    }

    /// <summary>Master/detail relations (everything except self-referencing ones).</summary>
    public IReadOnlyList<GridHierarchyRelation> MasterDetailRelations => DetailRelations;

    /// <summary>The relation that nests the grid's own rows, or null when the grid is not a tree.</summary>
    public GridHierarchyRelation? TreeHierarchy => TreeRelation;

    /// <summary>Builds the child lookups of all relations up front so a background export can read them without contention.</summary>
    public void PrepareHierarchy(object? sampleParent)
    {
        if (sampleParent == null) return;
        foreach (var relation in DetailRelations) _hierarchy.GetChildren(relation, sampleParent);
    }

    /// <summary>Loads the children of a lazily loaded relation (no-op for relations that resolve synchronously).</summary>
    public Task<IReadOnlyList<object>> LoadChildrenAsync(GridHierarchyRelation relation, object parent, CancellationToken cancellationToken = default) =>
        _hierarchy.LoadAsync(relation, parent, cancellationToken);

    private void BuildRows()
    {
        _groupNodes.Clear();
        _detailNodes.Clear();
        var rows = new List<RowRef>(_materialized.Length + 16);

        if (CurrentRequest.Groups.Count > 0 && _groupTree != null)
        {
            var cursor = 0;
            EmitGroups(rows, _groupTree, string.Empty, ref cursor);
        }
        else if (TreeRelation is { } tree)
        {
            EmitTree(rows, tree);
        }
        else
        {
            for (var i = 0; i < _materialized.Length; i++)
            {
                EmitDataRow(rows, i, 0);
            }
        }

        _rows = rows.ToArray();
    }

    private static string GroupKeyText(object? key) => key == null ? "\u2205" : Convert.ToString(key, CultureInfo.InvariantCulture) ?? string.Empty;

    private void EmitGroups(List<RowRef> rows, IReadOnlyList<GroupResult<TItem>> groups, string parentPath, ref int cursor)
    {
        foreach (var group in groups)
        {
            var path = $"{parentPath}/{group.Member}={GroupKeyText(group.Key)}";
            var expanded = GroupsExpandedByDefault ^ _groupToggles.Contains(path);
            var nodeIndex = _groupNodes.Count;
            _groupNodes.Add(new GroupNode(group, path, expanded));
            rows.Add(new RowRef { Kind = GridRowKind.GroupHeader, Level = group.Level, A = nodeIndex, Flags = FlagExpandable | (expanded ? FlagExpanded : 0), DataIndex = -1 });

            if (!expanded)
            {
                cursor += group.ItemCount;
                continue;
            }

            if (group.Subgroups != null)
            {
                EmitGroups(rows, group.Subgroups, path, ref cursor);
            }
            else
            {
                for (var i = 0; i < group.ItemCount; i++)
                {
                    EmitDataRow(rows, cursor + i, group.Level + 1);
                }
                cursor += group.ItemCount;
            }

            if (ShowGroupFooters)
            {
                rows.Add(new RowRef { Kind = GridRowKind.GroupFooter, Level = group.Level, A = nodeIndex, DataIndex = -1 });
            }
        }
    }

    private void EmitDataRow(List<RowRef> rows, int itemIndex, int level)
    {
        var item = _materialized[itemIndex];
        var detail = EnableMasterDetail;
        var expanded = detail && IsDetailExpandedCore(item);
        rows.Add(new RowRef
        {
            Kind = GridRowKind.Data,
            Level = level,
            A = itemIndex,
            DataIndex = itemIndex,
            Flags = (detail ? FlagExpandable : 0) | (expanded ? FlagExpanded : 0)
        });

        if (expanded)
        {
            var relations = DetailRelations;
            if (relations.Count == 0)
            {
                rows.Add(new RowRef { Kind = GridRowKind.DetailSummary, Level = level + 1, A = itemIndex, DataIndex = -1 });
            }
            else
            {
                EmitDetails(rows, item!, relations, level + 1);
            }
        }
    }

    private bool IsDetailExpandedCore(TItem item)
    {
        var relations = DetailRelations;
        if (relations.Count == 0) return _detailExpansions.Contains(new DetailKey(item!, null));
        foreach (var relation in relations)
        {
            if (_detailExpansions.Contains(new DetailKey(item!, relation))) return true;
        }
        return false;
    }

    private void EmitDetails(List<RowRef> rows, object parent, IReadOnlyList<GridHierarchyRelation> relations, int level)
    {
        foreach (var relation in relations)
        {
            if (!_detailExpansions.Contains(new DetailKey(parent, relation))) continue;

            var loading = _hierarchy.IsLazy(relation) && !_hierarchy.IsLoaded(parent, relation);
            if (loading) StartLazyLoad(parent, relation);

            var children = loading ? Array.Empty<object>() : _hierarchy.GetChildren(relation, parent);
            var nodeIndex = _detailNodes.Count;
            _detailNodes.Add(new DetailNode(parent, relation, children, level, loading));
            rows.Add(new RowRef { Kind = GridRowKind.DetailHeader, Level = level, A = nodeIndex, Flags = loading ? FlagLoading : 0, DataIndex = -1 });

            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                var childExpandable = relation.ChildRelations.Count > 0;
                var childExpanded = childExpandable && relation.ChildRelations.Any(r => _detailExpansions.Contains(new DetailKey(child, r)));
                rows.Add(new RowRef
                {
                    Kind = GridRowKind.DetailData,
                    Level = level,
                    A = nodeIndex,
                    B = i,
                    DataIndex = -1,
                    Flags = (childExpandable ? FlagExpandable : 0) | (childExpanded ? FlagExpanded : 0)
                });

                if (childExpanded)
                {
                    EmitDetails(rows, child, relation.ChildRelations, level + 1);
                }
            }
        }
    }

    private void StartLazyLoad(object parent, GridHierarchyRelation relation)
    {
        var key = new DetailKey(parent, relation);
        if (!_pendingLoads.Add(key)) return;

        _ = LoadChildrenSafeAsync(parent, relation, key);
    }

    private async Task LoadChildrenSafeAsync(object parent, GridHierarchyRelation relation, DetailKey key)
    {
        try
        {
            await _hierarchy.LoadAsync(relation, parent, CancellationToken.None).ConfigureAwait(true);
            _pendingLoads.Remove(key);
            if (IsMaterialized)
            {
                BuildRows();
                RaiseDataWindowChanged();
            }
        }
        catch (Exception ex)
        {
            _pendingLoads.Remove(key);
            _detailExpansions.Remove(key);
            _lastError = ex;
            DataLoadFailed?.Invoke(this, new GridDataLoadFailedEventArgs(ex));
            if (IsMaterialized)
            {
                BuildRows();
                RaiseDataWindowChanged();
            }
        }
    }

    private void EmitTree(List<RowRef> rows, GridHierarchyRelation relation)
    {
        var items = _materialized;
        var n = items.Length;
        var byKey = new Dictionary<object, int>(n, GridKeyComparer.Instance);
        var keys = new object?[n];
        var parents = new object?[n];
        for (var i = 0; i < n; i++)
        {
            keys[i] = PropertyPath.GetValue(items[i], relation.KeyField);
            parents[i] = PropertyPath.GetValue(items[i], relation.ForeignField);
            if (keys[i] != null) byKey.TryAdd(keys[i]!, i);
        }

        var head = new int[n];
        var next = new int[n];
        Array.Fill(head, -1);
        Array.Fill(next, -1);
        var roots = new List<int>();
        for (var i = n - 1; i >= 0; i--)
        {
            if (parents[i] != null && byKey.TryGetValue(parents[i]!, out var parentIndex) && parentIndex != i)
            {
                next[i] = head[parentIndex];
                head[parentIndex] = i;
            }
        }

        for (var i = 0; i < n; i++)
        {
            if (parents[i] == null || !byKey.TryGetValue(parents[i]!, out var parentIndex) || parentIndex == i)
            {
                roots.Add(i);
            }
        }

        // Rows that only reference each other in a cycle have no root; promote the first of each cycle to the top level instead of dropping them.
        var reachable = new bool[n];
        var scan = new Stack<int>();

        void MarkReachable(int start)
        {
            reachable[start] = true;
            scan.Push(start);
            while (scan.Count > 0)
            {
                var index = scan.Pop();
                for (var child = head[index]; child != -1; child = next[child])
                {
                    if (reachable[child]) continue;
                    reachable[child] = true;
                    scan.Push(child);
                }
            }
        }

        foreach (var root in roots.ToList()) MarkReachable(root);
        for (var i = 0; i < n; i++)
        {
            if (reachable[i]) continue;
            roots.Add(i);
            MarkReachable(i);
        }

        var visited = new bool[n];
        var stack = new Stack<(int Index, int Level)>();

        foreach (var root in roots)
        {
            stack.Push((root, 0));
            while (stack.Count > 0)
            {
                var (index, level) = stack.Pop();
                if (visited[index]) continue;
                visited[index] = true;

                var hasChildren = head[index] != -1;
                var expanded = hasChildren && (TreeExpandedByDefault ^ _treeToggles.Contains(items[index]!));
                rows.Add(new RowRef
                {
                    Kind = GridRowKind.Data,
                    Level = level,
                    A = index,
                    DataIndex = index,
                    Flags = (hasChildren ? FlagExpandable : 0) | (expanded ? FlagExpanded : 0)
                });

                if (!expanded) continue;

                var children = new List<int>();
                for (var child = head[index]; child != -1; child = next[child]) children.Add(child);
                for (var c = children.Count - 1; c >= 0; c--) stack.Push((children[c], level + 1));
            }
        }
    }

    // ------------------------------------------------------------------
    // Expansion
    // ------------------------------------------------------------------

    private void ExpansionChanged()
    {
        OnPropertyChanged(nameof(RenderedItems));
        if (UsesMaterializedRows)
        {
            if (_materializedValid)
            {
                BuildRows();
                RaiseDataWindowChanged();
            }
            else
            {
                _ = RefreshSafeAsync(LoadKind.Window);
            }
            return;
        }

        _materializedValid = false;
        RaiseDataWindowChanged();
        if (!IsWindowSatisfied())
        {
            _ = RefreshSafeAsync(LoadKind.Window);
        }
    }

    /// <summary>Expands or collapses the detail of a row: all its relations, or the property summary when there are no relations.</summary>
    public void ToggleDetailExpansion(TItem rowItem)
    {
        if (rowItem == null) return;

        var relations = DetailRelations;
        if (relations.Count == 0)
        {
            var key = new DetailKey(rowItem, null);
            if (!_detailExpansions.Remove(key)) _detailExpansions.Add(key);
        }
        else
        {
            var anyExpanded = relations.Any(r => _detailExpansions.Contains(new DetailKey(rowItem, r)));
            foreach (var relation in relations)
            {
                var key = new DetailKey(rowItem, relation);
                if (anyExpanded) _detailExpansions.Remove(key); else _detailExpansions.Add(key);
            }
        }

        ExpansionChanged();
    }

    /// <summary>Expands or collapses one relation of a parent object, at any nesting level.</summary>
    public void ToggleRelationExpansion(object parent, GridHierarchyRelation relation)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(relation);
        var key = new DetailKey(parent, relation);
        if (!_detailExpansions.Remove(key)) _detailExpansions.Add(key);
        ExpansionChanged();
    }

    public bool IsDetailExpanded(TItem rowItem) => rowItem != null && IsDetailExpandedCore(rowItem);

    public bool IsRelationExpanded(object parent, GridHierarchyRelation relation) => _detailExpansions.Contains(new DetailKey(parent, relation));

    public void ExpandAllDetails()
    {
        var relations = DetailRelations;
        foreach (var item in RenderedItems.ToList())
        {
            if (item == null) continue;
            if (relations.Count == 0) _detailExpansions.Add(new DetailKey(item, null));
            foreach (var relation in relations) _detailExpansions.Add(new DetailKey(item, relation));
        }
        ExpansionChanged();
    }

    public void CollapseAllDetails()
    {
        _detailExpansions.Clear();
        ExpansionChanged();
    }

    /// <summary>Toggles the group, tree node or detail behind a displayed row.</summary>
    public void ToggleRowExpansion(int displayIndex)
    {
        if (!TryGetRowAt(displayIndex, out var row)) return;

        switch (row.Kind)
        {
            case GridRowKind.GroupHeader when row.GroupPath != null:
                ToggleGroupExpansion(row.GroupPath);
                break;
            case GridRowKind.Data when IsTreeMode && row.Item != null:
                ToggleTreeNode(row.Item);
                break;
            case GridRowKind.Data when row.Item != null && EnableMasterDetail:
                ToggleDetailExpansion(row.Item);
                break;
            case GridRowKind.DetailData when row.Relation is { } relation && row.RowObject != null:
                var any = relation.ChildRelations.Any(r => IsRelationExpanded(row.RowObject, r));
                foreach (var child in relation.ChildRelations)
                {
                    var key = new DetailKey(row.RowObject, child);
                    if (any) _detailExpansions.Remove(key); else _detailExpansions.Add(key);
                }
                ExpansionChanged();
                break;
        }
    }

    public bool IsGroupExpanded(string groupPath) => GroupsExpandedByDefault ^ _groupToggles.Contains(groupPath);

    /// <summary>Toggles a group by <see cref="GridRow{TItem}.GroupPath"/>, or by the key value of a first-level group.</summary>
    public void ToggleGroupExpansion(object groupKey)
    {
        string path;
        if (groupKey is string text && text.StartsWith('/'))
        {
            path = text;
        }
        else if (CurrentRequest.Groups.Count > 0)
        {
            path = $"/{CurrentRequest.Groups[0].Member}={GroupKeyText(groupKey)}";
        }
        else
        {
            return;
        }

        if (!_groupToggles.Remove(path)) _groupToggles.Add(path);
        ExpansionChanged();
    }

    public void ExpandAllGroups()
    {
        GroupsExpandedByDefault = true;
        _groupToggles.Clear();
        ExpansionChanged();
    }

    public void CollapseAllGroups()
    {
        GroupsExpandedByDefault = false;
        _groupToggles.Clear();
        ExpansionChanged();
    }

    public void ToggleTreeNode(TItem item)
    {
        if (item == null) return;
        if (!_treeToggles.Remove(item)) _treeToggles.Add(item);
        ExpansionChanged();
    }

    public void ExpandAllTreeNodes()
    {
        TreeExpandedByDefault = true;
        _treeToggles.Clear();
        ExpansionChanged();
    }

    public void CollapseAllTreeNodes()
    {
        TreeExpandedByDefault = false;
        _treeToggles.Clear();
        ExpansionChanged();
    }

    // ------------------------------------------------------------------
    // Relations (runtime definition)
    // ------------------------------------------------------------------

    /// <summary>Defines a master/detail relation at runtime. The child table is joined on <paramref name="keyField"/> = <paramref name="foreignField"/>.</summary>
    public GridHierarchyRelation AddRelation(string name, string keyField, string foreignField, IEnumerable childSource, Type? childItemType = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(childSource);
        var relation = new GridHierarchyRelation
        {
            RelationName = name,
            KeyField = keyField,
            ForeignField = foreignField,
            ChildSource = childSource,
            ChildItemType = childItemType
        };
        Relations.Add(relation);
        NotifyRelationsChanged();
        return relation;
    }

    /// <summary>Turns the grid into a TreeGrid: rows are nested through their parent id.</summary>
    public GridHierarchyRelation AddSelfReference(string keyField, string parentField)
    {
        var relation = new GridHierarchyRelation
        {
            RelationName = "Tree",
            KeyField = keyField,
            ForeignField = parentField,
            Mode = HierarchyMode.SelfReferencing
        };
        Relations.Add(relation);
        NotifyRelationsChanged();
        return relation;
    }

    public bool RemoveRelation(string name)
    {
        var removed = Relations.RemoveAll(r => string.Equals(r.RelationName, name, StringComparison.OrdinalIgnoreCase)) > 0;
        if (removed) NotifyRelationsChanged();
        return removed;
    }

    /// <summary>Re-evaluates the hierarchy after relations were added, removed or modified.</summary>
    public void NotifyRelationsChanged()
    {
        _hierarchy.Clear();
        _detailExpansions.Clear();
        _treeToggles.Clear();
        _materializedValid = false;
        QueryChanged?.Invoke(this, EventArgs.Empty);
        _ = RefreshSafeAsync(LoadKind.Query);
    }

    /// <summary>Child rows of a parent object for a relation (already loaded children only for lazy relations).</summary>
    public IReadOnlyList<object> GetChildren(GridHierarchyRelation relation, object parent) => _hierarchy.GetChildren(relation, parent);

    private static readonly Dictionary<(Type Type, string Hidden), IReadOnlyList<GridColumn>> AutoColumnCache = new();

    /// <summary>
    /// Columns for a child table: the relation's own columns, or one per simple public property of the child type. Generated columns omit
    /// the foreign key that repeats the parent and use short date, number and right-aligned numeric defaults.
    /// </summary>
    public IReadOnlyList<GridColumn> GetDetailColumns(GridHierarchyRelation relation, object? sample)
    {
        if (relation.ChildColumns.Count > 0) return relation.ChildColumns;

        var type = relation.ChildItemType ?? sample?.GetType();
        if (type == null) return Array.Empty<GridColumn>();

        var hidden = relation.Mode == HierarchyMode.SelfReferencing ? string.Empty : relation.ForeignField;
        lock (AutoColumnCache)
        {
            if (!AutoColumnCache.TryGetValue((type, hidden), out var columns))
            {
                columns = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && IsSimpleType(p.PropertyType) && !string.Equals(p.Name, hidden, StringComparison.OrdinalIgnoreCase))
                    .Select(CreateAutoColumn)
                    .ToList();
                AutoColumnCache[(type, hidden)] = columns;
            }
            return columns;
        }
    }

    private static GridColumn CreateAutoColumn(PropertyInfo property)
    {
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        var column = new GridColumn { Field = property.Name, Title = property.Name };
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset)) column.FormatString = "d";
        else if (type == typeof(decimal) || type == typeof(double) || type == typeof(float))
        {
            column.FormatString = "N2";
            column.Alignment = ColumnTextAlignment.Right;
        }
        else if (type != typeof(bool) && type != typeof(char) && type.IsPrimitive) column.Alignment = ColumnTextAlignment.Right;
        return column;
    }
    private static bool IsSimpleType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime) ||
               type == typeof(DateTimeOffset) || type == typeof(DateOnly) || type == typeof(TimeSpan) || type == typeof(Guid);
    }

    /// <summary>Header text of a group row: "Title: key (count)" followed by its configured summaries.</summary>
    public string GetGroupHeaderText(GridRow<TItem> row) => row.Group is { } group ? GetGroupHeaderText(group) : string.Empty;

    public string GetGroupHeaderText(GroupResult<TItem> group)
    {
        var column = FindColumn(group.Member);
        var title = column == null || string.IsNullOrEmpty(column.Title) ? group.Member : column.Title;
        var key = group.Key == null ? "(blank)" : column != null ? FormatCellValue(column, group.Key) : Convert.ToString(group.Key, CultureInfo.CurrentCulture);
        var text = $"{title}: {key} ({group.ItemCount:N0})";

        var summaries = GetGroupSummaryTexts(group);
        return summaries.Count == 0 ? text : $"{text}  \u2013  {string.Join("; ", summaries)}";
    }

    public IReadOnlyList<string> GetGroupSummaryTexts(GroupResult<TItem> group)
    {
        var texts = new List<string>();
        foreach (var summary in GroupSummaries)
        {
            var key = $"{summary.Function}({summary.Member})";
            var aggregate = group.FindAggregate(key);
            if (aggregate == null) continue;
            var column = FindColumn(summary.Member);
            var label = $"{(column == null || string.IsNullOrEmpty(column.Title) ? summary.Member : column.Title)} {summary.Function}";
            var value = aggregate.Value is IFormattable formattable && column?.FormatString is { Length: > 0 } format
                ? formattable.ToString(format, CultureInfo.CurrentCulture)
                : Convert.ToString(aggregate.Value, CultureInfo.CurrentCulture);
            texts.Add(summary.FormatString == "{0}" ? $"{label}: {value}" : ApplySummaryFormat(summary.FormatString, aggregate.Value, label));
        }
        return texts;
    }

    /// <summary>Composite formats ("Sum {0:C0}") are used as written; a plain specifier ("C0") formats the value and gets the label.</summary>
    private static string ApplySummaryFormat(string format, object? value, string label)
    {
        try
        {
            if (format.Contains('{')) return string.Format(CultureInfo.CurrentCulture, format, value);
            return value is IFormattable formattable ? $"{label}: {formattable.ToString(format, CultureInfo.CurrentCulture)}" : $"{label}: {value}";
        }
        catch (FormatException)
        {
            return $"{label}: {value}";
        }
    }
}
