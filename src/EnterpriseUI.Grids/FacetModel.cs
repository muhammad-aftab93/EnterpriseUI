using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EnterpriseUI.Data;

namespace EnterpriseUI.Grids;

public enum FacetKind { List, Range }

public sealed record FacetValue(object? Value, string Text, int Count);

/// <summary>One filterable dimension: a checklist of distinct values with counts, or a numeric/date range.</summary>
public sealed class FacetField
{
    public FacetField(string field, string title, FacetKind kind)
    {
        Field = field;
        Title = title;
        Kind = kind;
    }

    public string Field { get; }
    public string Title { get; set; }
    public FacetKind Kind { get; }
    public int MaxValues { get; set; } = 50;
    public IReadOnlyList<FacetValue> Values { get; internal set; } = Array.Empty<FacetValue>();
    public HashSet<object?> Selected { get; } = new(FacetValueComparer.Instance);

    /// <summary>Bounds found in the data, for a range facet.</summary>
    public IComparable? Minimum { get; internal set; }
    public IComparable? Maximum { get; internal set; }
    public IComparable? From { get; set; }
    public IComparable? To { get; set; }

    public bool IsActive => Kind == FacetKind.List ? Selected.Count > 0 : From != null || To != null;

    public void Clear()
    {
        Selected.Clear();
        From = null;
        To = null;
    }
}

internal sealed class FacetValueComparer : IEqualityComparer<object?>
{
    public static FacetValueComparer Instance { get; } = new();
    public new bool Equals(object? x, object? y) => x == null || y == null ? x == y : Convert.ToString(x, CultureInfo.InvariantCulture) == Convert.ToString(y, CultureInfo.InvariantCulture);
    public int GetHashCode(object? obj) => obj == null ? 0 : (Convert.ToString(obj, CultureInfo.InvariantCulture) ?? string.Empty).GetHashCode();
}

/// <summary>
/// Faceted filtering over a collection: build the facets from the data, let the user select values or ranges, then read the selection as
/// filter descriptors (values of a facet are OR-ed, facets are AND-ed) or apply it directly.
/// </summary>
public sealed class FacetModel<TItem>
{
    private IReadOnlyList<TItem> _items = Array.Empty<TItem>();

    public List<FacetField> Fields { get; } = new();

    public event EventHandler? Changed;

    public FacetField AddList(string field, string? title = null, int maxValues = 50)
    {
        var facet = new FacetField(field, title ?? field, FacetKind.List) { MaxValues = maxValues };
        Fields.Add(facet);
        return facet;
    }

    public FacetField AddRange(string field, string? title = null)
    {
        var facet = new FacetField(field, title ?? field, FacetKind.Range);
        Fields.Add(facet);
        return facet;
    }

    /// <summary>Loads the data and computes distinct values with counts and range bounds.</summary>
    public void Load(IEnumerable<TItem> items)
    {
        _items = items.ToList();
        foreach (var facet in Fields)
        {
            var values = _items.Select(i => PropertyPath.GetValue(i, facet.Field)).ToList();
            if (facet.Kind == FacetKind.List)
            {
                facet.Values = values.GroupBy(v => v, FacetValueComparer.Instance)
                    .OrderBy(g => g.Key, ValueComparer.Instance)
                    .Take(facet.MaxValues)
                    .Select(g => new FacetValue(g.Key, g.Key == null ? "(blank)" : Convert.ToString(g.Key, CultureInfo.CurrentCulture) ?? string.Empty, g.Count()))
                    .ToList();
            }
            else
            {
                var comparables = values.OfType<IComparable>().ToList();
                facet.Minimum = comparables.Count == 0 ? null : comparables.OrderBy(v => v, ValueComparer.Instance).First();
                facet.Maximum = comparables.Count == 0 ? null : comparables.OrderBy(v => v, ValueComparer.Instance).Last();
            }
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Toggle(string field, object? value, bool selected)
    {
        var facet = Fields.First(f => string.Equals(f.Field, field, StringComparison.OrdinalIgnoreCase));
        if (selected) facet.Selected.Add(value); else facet.Selected.Remove(value);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetRange(string field, IComparable? from, IComparable? to)
    {
        var facet = Fields.First(f => string.Equals(f.Field, field, StringComparison.OrdinalIgnoreCase));
        facet.From = from;
        facet.To = to;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ClearAll()
    {
        foreach (var facet in Fields) facet.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool HasSelection => Fields.Any(f => f.IsActive);

    public IReadOnlyList<IFilterDescriptor> ToDescriptors()
    {
        var result = new List<IFilterDescriptor>();
        foreach (var facet in Fields.Where(f => f.IsActive))
        {
            if (facet.Kind == FacetKind.List)
            {
                var selected = facet.Selected.ToArray();
                result.Add(selected.Length == 1
                    ? new FilterDescriptor(facet.Field, selected[0] == null ? FilterOperator.IsNull : FilterOperator.IsEqualTo, selected[0])
                    : new FilterDescriptor(facet.Field, FilterOperator.IsContainedIn, selected));
            }
            else if (facet.From != null && facet.To != null)
            {
                result.Add(new FilterDescriptor(facet.Field, FilterOperator.IsBetween, facet.From, facet.To));
            }
            else if (facet.From != null)
            {
                result.Add(new FilterDescriptor(facet.Field, FilterOperator.IsGreaterThanOrEqualTo, facet.From));
            }
            else
            {
                result.Add(new FilterDescriptor(facet.Field, FilterOperator.IsLessThanOrEqualTo, facet.To));
            }
        }
        return result;
    }

    public bool Matches(TItem item)
    {
        foreach (var facet in Fields.Where(f => f.IsActive))
        {
            var value = PropertyPath.GetValue(item, facet.Field);
            if (facet.Kind == FacetKind.List)
            {
                if (!facet.Selected.Contains(value)) return false;
            }
            else
            {
                if (facet.From != null && !FilterEvaluator.Matches(value, FilterOperator.IsGreaterThanOrEqualTo, facet.From)) return false;
                if (facet.To != null && !FilterEvaluator.Matches(value, FilterOperator.IsLessThanOrEqualTo, facet.To)) return false;
            }
        }
        return true;
    }

    public IEnumerable<TItem> Apply(IEnumerable<TItem> items) => items.Where(Matches);

    /// <summary>Items matching every other facet, so counts next to values show what selecting them would leave.</summary>
    public int CountWith(FacetField facet, object? value)
    {
        return _items.Count(item =>
        {
            foreach (var other in Fields.Where(f => f.IsActive && !ReferenceEquals(f, facet)))
            {
                var v = PropertyPath.GetValue(item, other.Field);
                if (other.Kind == FacetKind.List ? !other.Selected.Contains(v) : (other.From != null && !FilterEvaluator.Matches(v, FilterOperator.IsGreaterThanOrEqualTo, other.From)) || (other.To != null && !FilterEvaluator.Matches(v, FilterOperator.IsLessThanOrEqualTo, other.To))) return false;
            }
            return FacetValueComparer.Instance.Equals(PropertyPath.GetValue(item, facet.Field), value);
        });
    }
}
