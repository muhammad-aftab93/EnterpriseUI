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
    IsNotEmpty
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

    public FilterDescriptor() { }

    public FilterDescriptor(string member, FilterOperator @operator, object? value)
    {
        Member = member;
        Operator = @operator;
        Value = value;
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
    Count
}

public sealed class AggregateDescriptor
{
    public string Member { get; set; } = string.Empty;
    public AggregateFunction Function { get; set; } = AggregateFunction.Count;
}

public sealed class GroupDescriptor
{
    public string Member { get; set; } = string.Empty;
    public ListSortDirection SortDirection { get; set; } = ListSortDirection.Ascending;
    public List<AggregateDescriptor> Aggregates { get; } = new();
}

public sealed class AggregateResult
{
    public string Member { get; set; } = string.Empty;
    public AggregateFunction Function { get; set; }
    public object? Value { get; set; }
}

public sealed class GroupResult<T>
{
    public object? Key { get; set; }
    public int ItemCount { get; set; }
    public IEnumerable<T>? Items { get; set; }
    public IEnumerable<GroupResult<T>>? Subgroups { get; set; }
    public List<AggregateResult> Aggregates { get; } = new();
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
    public IEnumerable<GroupResult<T>>? GroupedData { get; set; }
    public int Total { get; set; }
    public List<AggregateResult> AggregateResults { get; } = new();
}

