using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using EnterpriseUI.Abstractions.Virtualization;

namespace EnterpriseUI.Data;

public interface IDataSource<T>
{
    Task<DataSourceResult<T>> ReadAsync(DataSourceRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Implemented by sources that cache query results and need to be told when the underlying data changed.</summary>
public interface IInvalidatableDataSource
{
    void Invalidate();
}

/// <summary>
/// In-memory and IQueryable data source. Large in-memory collections run on the thread pool and cache the
/// filtered, sorted result so scrolling and paging do not re-sort the data; call <see cref="Invalidate"/> after mutating items.
/// </summary>
public class DataSource<T> : IDataSource<T>, IInvalidatableDataSource
{
    private readonly IQueryable<T> _queryable;
    private readonly ICollection<T>? _collection;
    private readonly object _gate = new();
    private CacheEntry? _cache;

    private sealed record CacheEntry(string Signature, QueryMaterialization<T> Data);

    public DataSource(IEnumerable<T> source) : this(source.AsQueryable())
    {
        _collection = source as ICollection<T>;
        if (source is INotifyCollectionChanged notify)
        {
            notify.CollectionChanged += (_, _) => Invalidate();
        }
    }

    public DataSource(IQueryable<T> queryable)
    {
        _queryable = queryable ?? throw new ArgumentNullException(nameof(queryable));
    }

    /// <summary>Collections at least this large are queried on the thread pool so the UI thread stays responsive.</summary>
    public int OffloadThreshold { get; set; } = 50_000;

    /// <summary>Collections at least this large cache their sorted result between reads.</summary>
    public int CacheThreshold { get; set; } = 20_000;

    public void Invalidate()
    {
        lock (_gate)
        {
            _cache = null;
        }
    }

    public Task<DataSourceResult<T>> ReadAsync(DataSourceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var size = _collection?.Count ?? -1;
            if (size >= OffloadThreshold)
            {
                return Task.Run(() => Read(request, size, cancellationToken), cancellationToken);
            }

            return Task.FromResult(Read(request, size, cancellationToken));
        }
        catch (Exception ex)
        {
            return Task.FromException<DataSourceResult<T>>(ex);
        }
    }

    private DataSourceResult<T> Read(DataSourceRequest request, int size, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (size < CacheThreshold)
        {
            return QueryEngine.Execute(_queryable, request);
        }

        var signature = $"{size}|{QuerySignature.Compute(request)}";
        QueryMaterialization<T>? materialized;
        lock (_gate)
        {
            materialized = _cache != null && _cache.Signature == signature ? _cache.Data : null;
        }

        if (materialized == null)
        {
            materialized = QueryEngine.Materialize(_queryable, request);
            lock (_gate)
            {
                _cache = new CacheEntry(signature, materialized);
            }
        }

        return QueryEngine.Page(materialized, request);
    }
}

/// <summary>Stable text identity of the paging-independent part of a request, used as a cache key.</summary>
public static class QuerySignature
{
    public static string Compute(DataSourceRequest request)
    {
        var text = new StringBuilder();
        foreach (var filter in request.Filters) Append(text, filter);
        text.Append('#');
        foreach (var sort in request.Sorts)
        {
            text.Append(sort.Member).Append(sort.SortDirection == ListSortDirection.Ascending ? '+' : '-').Append(Hash(sort.Comparer)).Append(Hash(sort.ValueSelector)).Append(';');
        }
        text.Append('#');
        foreach (var group in request.Groups)
        {
            text.Append(group.Member).Append(group.SortDirection == ListSortDirection.Ascending ? '+' : '-')
                .Append(Hash(group.KeySelector)).Append(Hash(group.KeyComparer)).Append(Hash(group.ValueSelector));
            foreach (var aggregate in group.Aggregates) text.Append(aggregate.Key).Append(Hash(aggregate.Custom)).Append(Hash(aggregate.ValueSelector));
            text.Append(';');
        }
        text.Append('#');
        foreach (var aggregate in request.Aggregates) text.Append(aggregate.Key).Append(Hash(aggregate.Custom)).Append(Hash(aggregate.ValueSelector)).Append(';');
        return text.ToString();
    }

    private static string Hash(object? instance) => instance == null ? string.Empty : "@" + RuntimeHelpers.GetHashCode(instance);

    private static void Append(StringBuilder text, IFilterDescriptor filter)
    {
        switch (filter)
        {
            case FilterDescriptor fd:
                text.Append(fd.Member).Append('|').Append((int)fd.Operator).Append('|').Append(fd.IgnoreCase ? 'i' : 's').Append('|');
                AppendValue(text, fd.Value);
                text.Append('|');
                AppendValue(text, fd.Value2);
                text.Append(';');
                break;
            case CustomFilterDescriptor custom:
                text.Append("custom|").Append(custom.Member).Append('|').Append(custom.OperatorName).Append(Hash(custom.Predicate)).Append(';');
                break;
            case CompositeFilterDescriptor composite:
                text.Append(composite.LogicalOperator == FilterLogicalOperator.And ? "(&" : "(|");
                foreach (var child in composite.FilterDescriptors) Append(text, child);
                text.Append(')');
                break;
        }
    }

    private static void AppendValue(StringBuilder text, object? value)
    {
        switch (value)
        {
            case null:
                text.Append('~');
                break;
            case string s:
                text.Append('"').Append(s).Append('"');
                break;
            case IEnumerable items:
                text.Append('[');
                foreach (var item in items) AppendValue(text, item);
                text.Append(']');
                break;
            default:
                text.Append(value.GetType().Name).Append(':').Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
        }
    }
}

/// <summary>
/// Remote data source delegating to a fetch callback (REST, OData, GraphQL). The callback receives
/// the filters, sorts, groups and the explicit Skip/Take window and returns the rows plus the total count.
/// </summary>
public class RemoteDataSource<T> : IDataSource<T>
{
    private readonly Func<DataSourceRequest, CancellationToken, Task<DataSourceResult<T>>> _remoteFetcher;

    public RemoteDataSource(Func<DataSourceRequest, CancellationToken, Task<DataSourceResult<T>>> remoteFetcher)
    {
        _remoteFetcher = remoteFetcher ?? throw new ArgumentNullException(nameof(remoteFetcher));
    }

    public Task<DataSourceResult<T>> ReadAsync(DataSourceRequest request, CancellationToken cancellationToken = default)
    {
        return _remoteFetcher(request, cancellationToken);
    }

    /// <summary>OData v4 query string: $filter, $orderby, $top, $skip, $count. Custom filters and groups have no OData equivalent and are omitted.</summary>
    public static string ToODataParameters(DataSourceRequest request) => ODataQueryBuilder.Build(request);
}

/// <summary>Translates a <see cref="DataSourceRequest"/> to an OData v4 query string.</summary>
public static class ODataQueryBuilder
{
    public static string Build(DataSourceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var parameters = new List<string>();

        var filter = BuildFilter(request.Filters, FilterLogicalOperator.And);
        if (!string.IsNullOrEmpty(filter))
        {
            parameters.Add($"$filter={Uri.EscapeDataString(filter)}");
        }

        if (request.Sorts.Count > 0)
        {
            var orderBys = request.Sorts.Select(s => $"{s.Member.Replace('.', '/')} {(s.SortDirection == ListSortDirection.Ascending ? "asc" : "desc")}");
            parameters.Add($"$orderby={Uri.EscapeDataString(string.Join(",", orderBys))}");
        }

        if (request.EffectiveTake > 0)
        {
            parameters.Add($"$top={request.EffectiveTake}");
        }

        if (request.EffectiveSkip > 0)
        {
            parameters.Add($"$skip={request.EffectiveSkip}");
        }

        parameters.Add("$count=true");
        return string.Join("&", parameters);
    }

    public static string BuildFilter(IEnumerable<IFilterDescriptor> filters, FilterLogicalOperator join)
    {
        var parts = filters.Select(BuildFilter).Where(p => p.Length > 0).ToList();
        return parts.Count switch
        {
            0 => string.Empty,
            1 => parts[0],
            _ => "(" + string.Join(join == FilterLogicalOperator.And ? " and " : " or ", parts) + ")"
        };
    }

    private static string BuildFilter(IFilterDescriptor filter)
    {
        switch (filter)
        {
            case CompositeFilterDescriptor composite:
                return BuildFilter(composite.FilterDescriptors, composite.LogicalOperator);
            case FilterDescriptor fd when !string.IsNullOrWhiteSpace(fd.Member):
                return BuildMember(fd);
            default:
                return string.Empty;
        }
    }

    private static string BuildMember(FilterDescriptor fd)
    {
        var member = fd.Member.Replace('.', '/');
        var text = fd.Value is string && fd.IgnoreCase ? $"tolower({member})" : member;
        string Literal(object? value) => fd.IgnoreCase && value is string s ? ToLiteral(s.ToLowerInvariant()) : ToLiteral(value);

        return fd.Operator switch
        {
            FilterOperator.IsEqualTo => $"{text} eq {Literal(fd.Value)}",
            FilterOperator.IsNotEqualTo => $"{text} ne {Literal(fd.Value)}",
            FilterOperator.IsLessThan => $"{text} lt {Literal(fd.Value)}",
            FilterOperator.IsLessThanOrEqualTo => $"{text} le {Literal(fd.Value)}",
            FilterOperator.IsGreaterThan => $"{text} gt {Literal(fd.Value)}",
            FilterOperator.IsGreaterThanOrEqualTo => $"{text} ge {Literal(fd.Value)}",
            FilterOperator.Contains => $"contains(tolower({member}),{ToLiteral(Convert.ToString(fd.Value, CultureInfo.InvariantCulture)?.ToLowerInvariant())})",
            FilterOperator.DoesNotContain => $"not contains(tolower({member}),{ToLiteral(Convert.ToString(fd.Value, CultureInfo.InvariantCulture)?.ToLowerInvariant())})",
            FilterOperator.StartsWith => $"startswith(tolower({member}),{ToLiteral(Convert.ToString(fd.Value, CultureInfo.InvariantCulture)?.ToLowerInvariant())})",
            FilterOperator.EndsWith => $"endswith(tolower({member}),{ToLiteral(Convert.ToString(fd.Value, CultureInfo.InvariantCulture)?.ToLowerInvariant())})",
            FilterOperator.IsNull => $"{member} eq null",
            FilterOperator.IsNotNull => $"{member} ne null",
            FilterOperator.IsEmpty => $"({member} eq null or {member} eq '')",
            FilterOperator.IsNotEmpty => $"({member} ne null and {member} ne '')",
            FilterOperator.IsContainedIn => InList(member, fd.Value, "eq", "or"),
            FilterOperator.IsNotContainedIn => InList(member, fd.Value, "ne", "and"),
            FilterOperator.IsBetween => $"({member} ge {ToLiteral(fd.Value)} and {member} le {ToLiteral(fd.Value2)})",
            FilterOperator.IsNotBetween => $"({member} lt {ToLiteral(fd.Value)} or {member} gt {ToLiteral(fd.Value2)})",
            _ => string.Empty
        };
    }

    private static string InList(string member, object? value, string comparison, string join)
    {
        var items = (value as IEnumerable ?? Array.Empty<object>()).Cast<object?>().Select(v => $"{member} {comparison} {ToLiteral(v)}").ToList();
        return items.Count == 0 ? string.Empty : "(" + string.Join($" {join} ", items) + ")";
    }

    public static string ToLiteral(object? value) => value switch
    {
        null => "null",
        string s => "'" + s.Replace("'", "''") + "'",
        bool b => b ? "true" : "false",
        DateTime dt => (dt.Kind == DateTimeKind.Local ? dt.ToUniversalTime() : dt).ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture),
        DateTimeOffset dto => dto.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        Guid g => g.ToString(),
        Enum e => "'" + e + "'",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => "'" + Convert.ToString(value, CultureInfo.InvariantCulture)?.Replace("'", "''") + "'"
    };
}

/// <summary>
/// 2D virtualization math: maps scroll offsets to the row and column index ranges that need to be realized.
/// </summary>
public sealed class VirtualizationManager
{
    public double ItemHeight { get; set; } = 36.0;
    public double ItemWidth { get; set; } = 120.0;
    public int BufferItems { get; set; } = 5;

    public IndexRange CalculateVisibleRows(double verticalOffset, double viewportHeight, int totalRows)
    {
        if (totalRows <= 0 || ItemHeight <= 0) return new IndexRange(0, -1);

        var firstVisible = Math.Clamp((int)(Math.Max(0, verticalOffset) / ItemHeight), 0, totalRows - 1);
        var visibleCount = (int)Math.Ceiling(Math.Max(0, viewportHeight) / ItemHeight);

        var start = Math.Max(0, firstVisible - BufferItems);
        var end = Math.Min(totalRows - 1, firstVisible + visibleCount + BufferItems);

        return new IndexRange(start, end);
    }

    public IndexRange CalculateVisibleColumns(double horizontalOffset, double viewportWidth, int totalColumns)
    {
        if (totalColumns <= 0 || ItemWidth <= 0) return new IndexRange(0, -1);

        var firstVisible = Math.Clamp((int)(Math.Max(0, horizontalOffset) / ItemWidth), 0, totalColumns - 1);
        var visibleCount = (int)Math.Ceiling(Math.Max(0, viewportWidth) / ItemWidth);

        var start = Math.Max(0, firstVisible - 2);
        var end = Math.Min(totalColumns - 1, firstVisible + visibleCount + 2);

        return new IndexRange(start, end);
    }
}
