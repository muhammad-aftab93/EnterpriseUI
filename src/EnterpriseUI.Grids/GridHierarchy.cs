using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnterpriseUI.Data;

namespace EnterpriseUI.Grids;

public enum HierarchyMode
{
    /// <summary>Child rows are shown in a detail block below their parent row.</summary>
    MasterDetail,
    /// <summary>Rows of the grid itself form a tree through a parent key (TreeGrid hybrid).</summary>
    SelfReferencing,
    /// <summary>Several relations share one parent row; each is shown as its own labelled block.</summary>
    TabbedDetail
}

/// <summary>
/// Relation between a parent row and its children. Relations nest through <see cref="ChildRelations"/> without a depth limit and
/// can be created and removed at runtime.
/// </summary>
public class GridHierarchyRelation
{
    public string RelationName { get; set; } = string.Empty;

    /// <summary>Parent property holding the key (the primary key for master/detail, the row's own id for self-reference).</summary>
    public string KeyField { get; set; } = string.Empty;

    /// <summary>Child property holding the parent's key (the foreign key, or the parent id for self-reference).</summary>
    public string ForeignField { get; set; } = string.Empty;
    public HierarchyMode Mode { get; set; } = HierarchyMode.MasterDetail;
    public Type? ChildItemType { get; set; }

    /// <summary>Complete child table joined on <see cref="KeyField"/>/<see cref="ForeignField"/>.</summary>
    public IEnumerable? ChildSource { get; set; }

    /// <summary>Resolves children directly from the parent object; takes precedence over <see cref="ChildSource"/>.</summary>
    public Func<object, IEnumerable>? DetailItemsSelector { get; set; }

    /// <summary>Loads children on first expansion (lazy / on-demand loading); results are cached until the grid is refreshed.</summary>
    public Func<object, CancellationToken, Task<IEnumerable>>? LoadChildrenAsync { get; set; }

    /// <summary>Columns of the child table; generated from the child item's public properties when empty.</summary>
    public List<GridColumn> ChildColumns { get; } = new();

    /// <summary>Relations of the child rows, giving a third, fourth, ... hierarchy level.</summary>
    public List<GridHierarchyRelation> ChildRelations { get; } = new();

    public string Title => string.IsNullOrEmpty(RelationName) ? "Details" : RelationName;
}

/// <summary>Resolves and caches child rows for hierarchy relations.</summary>
internal sealed class GridHierarchyResolver
{
    private readonly Dictionary<GridHierarchyRelation, Dictionary<object, List<object>>> _lookups = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<(object Parent, GridHierarchyRelation Relation), IReadOnlyList<object>> _loaded = new(new LoadedKeyComparer());

    private sealed class LoadedKeyComparer : IEqualityComparer<(object Parent, GridHierarchyRelation Relation)>
    {
        public bool Equals((object Parent, GridHierarchyRelation Relation) x, (object Parent, GridHierarchyRelation Relation) y) =>
            ObjectIdentityComparer.Instance.Equals(x.Parent, y.Parent) && ReferenceEquals(x.Relation, y.Relation);

        public int GetHashCode((object Parent, GridHierarchyRelation Relation) obj) =>
            HashCode.Combine(ObjectIdentityComparer.Instance.GetHashCode(obj.Parent), System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Relation));
    }

    private readonly object _gate = new();

    public void Clear()
    {
        lock (_gate)
        {
            _lookups.Clear();
            _loaded.Clear();
        }
    }

    public bool IsLazy(GridHierarchyRelation relation) =>
        relation.DetailItemsSelector == null && relation.ChildSource == null && relation.LoadChildrenAsync != null;

    public bool IsLoaded(object parent, GridHierarchyRelation relation)
    {
        lock (_gate) return !IsLazy(relation) || _loaded.ContainsKey((parent, relation));
    }

    /// <summary>Children of <paramref name="parent"/>; empty for lazy relations that have not been loaded yet.</summary>
    public IReadOnlyList<object> GetChildren(GridHierarchyRelation relation, object parent)
    {
        if (relation.DetailItemsSelector != null)
        {
            return Materialize(relation.DetailItemsSelector(parent));
        }

        if (relation.ChildSource != null)
        {
            if (string.IsNullOrEmpty(relation.KeyField) || string.IsNullOrEmpty(relation.ForeignField))
            {
                return Materialize(relation.ChildSource);
            }

            var key = PropertyPath.GetValue(parent, relation.KeyField);
            if (key == null) return Array.Empty<object>();
            var lookup = GetLookup(relation);
            return lookup.TryGetValue(key, out var list) ? list : Array.Empty<object>();
        }

        lock (_gate) return _loaded.TryGetValue((parent, relation), out var cached) ? cached : Array.Empty<object>();
    }

    public async Task<IReadOnlyList<object>> LoadAsync(GridHierarchyRelation relation, object parent, CancellationToken cancellationToken)
    {
        if (!IsLazy(relation))
        {
            return GetChildren(relation, parent);
        }

        lock (_gate)
        {
            if (_loaded.TryGetValue((parent, relation), out var existing)) return existing;
        }

        var items = Materialize(await relation.LoadChildrenAsync!(parent, cancellationToken).ConfigureAwait(false));
        lock (_gate) _loaded[(parent, relation)] = items;
        return items;
    }

    private Dictionary<object, List<object>> GetLookup(GridHierarchyRelation relation)
    {
        lock (_gate)
        {
            if (_lookups.TryGetValue(relation, out var existing))
            {
                return existing;
            }

            var lookup = new Dictionary<object, List<object>>(GridKeyComparer.Instance);
            foreach (var child in relation.ChildSource!)
            {
                var foreignKey = PropertyPath.GetValue(child, relation.ForeignField);
                if (foreignKey == null) continue;
                if (!lookup.TryGetValue(foreignKey, out var list))
                {
                    lookup[foreignKey] = list = new List<object>();
                }
                list.Add(child!);
            }

            _lookups[relation] = lookup;
            return lookup;
        }
    }

    private static IReadOnlyList<object> Materialize(IEnumerable? source)
    {
        if (source == null) return Array.Empty<object>();
        return source as IReadOnlyList<object> ?? source.Cast<object>().Where(o => o != null).ToList();
    }
}

/// <summary>Key equality that treats equal numbers of different CLR types as equal (an int key matches a long foreign key).</summary>
internal sealed class GridKeyComparer : IEqualityComparer<object>
{
    public static GridKeyComparer Instance { get; } = new();

    public new bool Equals(object? x, object? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x == null || y == null) return false;
        if (NumericValue.TryGet(x, out var nx) && NumericValue.TryGet(y, out var ny)) return nx == ny;
        return x.Equals(y);
    }

    public int GetHashCode(object obj) => NumericValue.TryGet(obj, out var number) ? number.GetHashCode() : obj.GetHashCode();
}
