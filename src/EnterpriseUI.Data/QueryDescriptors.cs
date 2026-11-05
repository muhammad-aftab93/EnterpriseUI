using System.ComponentModel;

namespace EnterpriseUI.Data;

public enum FilterOperator
{
    IsEqualTo,
    IsNotEqualTo,
    IsLessThan,
    IsLessThanOrEqualTo,
    IsGreaterThan,
    IsGreaterThanOrEqualTo,
    StartsWith,
    EndsWith,
    Contains,
    DoesNotContain,
    IsNull,
    IsNotNull,
    IsEmpty,
    IsNotEmpty,
    /// <summary>Value is an enumerable; matches when the member equals any element.</summary>
    IsContainedIn,
    /// <summary>Value is an enumerable; matches when the member equals no element.</summary>
    IsNotContainedIn,
    /// <summary>Inclusive range between <see cref="FilterDescriptor.Value"/> and <see cref="FilterDescriptor.Value2"/>.</summary>
    IsBetween,
    IsNotBetween
}

public enum FilterLogicalOperator
{
    And,
    Or
}

public interface IFilterDescriptor
{
}

public sealed class FilterDescriptor : IFilterDescriptor
{
    public string Member { get; set; } = string.Empty;
    public FilterOperator Operator { get; set; } = FilterOperator.IsEqualTo;
    public object? Value { get; set; }

    /// <summary>Upper bound for <see cref="FilterOperator.IsBetween"/> and <see cref="FilterOperator.IsNotBetween"/>.</summary>
    public object? Value2 { get; set; }

    /// <summary>When true (default) string comparisons ignore case.</summary>
    public bool IgnoreCase { get; set; } = true;

    public FilterDescriptor() { }

    public FilterDescriptor(string member, FilterOperator @operator, object? value)
    {
        Member = member;
        Operator = @operator;
        Value = value;
    }

    public FilterDescriptor(string member, FilterOperator @operator, object? value, object? value2) : this(member, @operator, value)
    {
        Value2 = value2;
    }
}

/// <summary>Filter whose match logic is an arbitrary predicate over the member value; evaluated in memory only.</summary>
public sealed class CustomFilterDescriptor : IFilterDescriptor
{
    public string Member { get; set; } = string.Empty;

    /// <summary>Name shown in UI and written to saved filters; the predicate itself cannot be serialized.</summary>
    public string OperatorName { get; set; } = "Custom";

    public Func<object?, bool> Predicate { get; set; } = _ => true;

    public CustomFilterDescriptor() { }

    public CustomFilterDescriptor(string member, string operatorName, Func<object?, bool> predicate)
    {
        Member = member;
        OperatorName = operatorName;
        Predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }
}

public sealed class CompositeFilterDescriptor : IFilterDescriptor
{
    public FilterLogicalOperator LogicalOperator { get; set; } = FilterLogicalOperator.And;
    public List<IFilterDescriptor> FilterDescriptors { get; } = new();
}

public sealed class SortDescriptor
{
    public string Member { get; set; } = string.Empty;
    public ListSortDirection SortDirection { get; set; } = ListSortDirection.Ascending;

    /// <summary>Custom ordering over the member value; forces in-memory sorting.</summary>
    public IComparer<object?>? Comparer { get; set; }

    /// <summary>Computes the sort key from the whole row (calculated columns); <see cref="Member"/> then only names the sort. Forces in-memory sorting.</summary>
    public Func<object, object?>? ValueSelector { get; set; }

    public SortDescriptor() { }

    public SortDescriptor(string member, ListSortDirection direction = ListSortDirection.Ascending)
    {
        Member = member;
        SortDirection = direction;
    }
}

public enum AggregateFunction
{
    Sum,
    Min,
    Max,
    Average,
    Count,
    First,
    Last,
    CountDistinct,
    StdDev,
    Variance,
    Median
}

public sealed class AggregateDescriptor
{
    public string Member { get; set; } = string.Empty;
    public AggregateFunction Function { get; set; } = AggregateFunction.Count;

    /// <summary>Overrides <see cref="Function"/>; receives the non-null member values of the aggregated rows.</summary>
    public Func<IReadOnlyList<object?>, object?>? Custom { get; set; }

    /// <summary>Computes the aggregated value from the whole row (calculated columns) instead of reading <see cref="Member"/>.</summary>
    public Func<object, object?>? ValueSelector { get; set; }

    /// <summary>Name used to look the result up; defaults to "Function(Member)".</summary>
    public string? Alias { get; set; }

    public string Key => Alias ?? $"{(Custom != null ? "Custom" : Function.ToString())}({Member})";
}

public sealed class GroupDescriptor
{
    public string Member { get; set; } = string.Empty;
    public ListSortDirection SortDirection { get; set; } = ListSortDirection.Ascending;
    public List<AggregateDescriptor> Aggregates { get; } = new();

    /// <summary>Maps the member value to a group key, e.g. a date to its year or a number to a bucket.</summary>
    public Func<object?, object?>? KeySelector { get; set; }

    /// <summary>Computes the value to group on from the whole row (calculated columns) instead of reading <see cref="Member"/>.</summary>
    public Func<object, object?>? ValueSelector { get; set; }

    /// <summary>Custom ordering of the group keys.</summary>
    public IComparer<object?>? KeyComparer { get; set; }
}

public sealed class AggregateResult
{
    public string Member { get; set; } = string.Empty;
    public AggregateFunction Function { get; set; }
    public string Key { get; set; } = string.Empty;
    public object? Value { get; set; }
}

public sealed class GroupResult<T>
{
    public object? Key { get; set; }
    public string Member { get; set; } = string.Empty;
    public int Level { get; set; }

    /// <summary>Number of leaf items in this group including all subgroups.</summary>
    public int ItemCount { get; set; }

    /// <summary>All rows of this group's subtree, as a view over the sorted result.</summary>
    public IReadOnlyList<T>? Items { get; set; }
    public IReadOnlyList<GroupResult<T>>? Subgroups { get; set; }
    public List<AggregateResult> Aggregates { get; } = new();

    public AggregateResult? FindAggregate(string key) =>
        Aggregates.FirstOrDefault(a => string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase));
}

public sealed class DataSourceRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    /// <summary>Explicit window start; overrides Page/PageSize when set.</summary>
    public int? Skip { get; set; }

    /// <summary>Explicit window length; overrides PageSize when set. Zero or less means unpaged.</summary>
    public int? Take { get; set; }

    public int EffectiveSkip => Math.Max(0, Skip ?? (Page - 1) * PageSize);
    public int EffectiveTake => Take ?? PageSize;

    public List<IFilterDescriptor> Filters { get; } = new();
    public List<SortDescriptor> Sorts { get; } = new();
    public List<GroupDescriptor> Groups { get; } = new();
    public List<AggregateDescriptor> Aggregates { get; } = new();

    public DataSourceRequest Clone()
    {
        var clone = new DataSourceRequest { Page = Page, PageSize = PageSize, Skip = Skip, Take = Take };
        clone.Filters.AddRange(Filters);
        clone.Sorts.AddRange(Sorts);
        clone.Groups.AddRange(Groups);
        clone.Aggregates.AddRange(Aggregates);
        return clone;
    }
}

public sealed class DataSourceResult<T>
{
    public IEnumerable<T>? Data { get; set; }

    /// <summary>Complete group tree over every row matching the filters; populated only when the request has groups.</summary>
    public IEnumerable<GroupResult<T>>? GroupedData { get; set; }

    /// <summary>Rows matching the filters before paging.</summary>
    public int Total { get; set; }
    public List<AggregateResult> AggregateResults { get; } = new();

    public AggregateResult? FindAggregate(string key) =>
        AggregateResults.FirstOrDefault(a => string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase));
}
