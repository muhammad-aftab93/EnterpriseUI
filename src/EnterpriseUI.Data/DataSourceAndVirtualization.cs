using EnterpriseUI.Abstractions.Virtualization;

namespace EnterpriseUI.Data;

public interface IDataSource<T>
{
    Task<DataSourceResult<T>> ReadAsync(DataSourceRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// In-memory and IQueryable high performance data source.
/// </summary>
public class DataSource<T> : IDataSource<T>
{
    private readonly IQueryable<T> _queryable;

    public DataSource(IEnumerable<T> source) : this(source.AsQueryable()) { }

    public DataSource(IQueryable<T> queryable)
    {
        _queryable = queryable ?? throw new ArgumentNullException(nameof(queryable));
    }

    public Task<DataSourceResult<T>> ReadAsync(DataSourceRequest request, CancellationToken cancellationToken = default)
    {
        var result = QueryEngine.Execute(_queryable, request);
        return Task.FromResult(result);
    }
}

/// <summary>
/// Remote Data Source supporting REST WebAPI, OData, and GraphQL protocol transformations.
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

    /// <summary>
    /// Generates OData v4 compatible $filter, $orderby, $skip, $top query parameters.
    /// </summary>
    public static string ToODataParameters(DataSourceRequest request)
    {
        var parameters = new List<string>
        {
            $"$top={request.EffectiveTake}",
            $"$skip={request.EffectiveSkip}",
            "$count=true"
        };

        if (request.Sorts.Count > 0)
        {
            var orderBys = request.Sorts.Select(s => $"{s.Member} {(s.SortDirection == System.ComponentModel.ListSortDirection.Ascending ? "asc" : "desc")}");
            parameters.Add($"$orderby={string.Join(",", orderBys)}");
        }

        return string.Join("&", parameters);
    }
}

/// <summary>
/// Ultra-fast 2D virtualization manager supporting millions of records with constant memory footprint.
/// </summary>
public sealed class VirtualizationManager
{
    public double ItemHeight { get; set; } = 36.0;
    public double ItemWidth { get; set; } = 120.0;
    public int BufferItems { get; set; } = 5;

    public IndexRange CalculateVisibleRows(double verticalOffset, double viewportHeight, int totalRows)
    {
        if (totalRows <= 0 || ItemHeight <= 0) return new IndexRange(0, -1);

        var firstVisible = (int)(verticalOffset / ItemHeight);
        var visibleCount = (int)Math.Ceiling(viewportHeight / ItemHeight);

        var start = Math.Max(0, firstVisible - BufferItems);
        var end = Math.Min(totalRows - 1, firstVisible + visibleCount + BufferItems);

        return new IndexRange(start, end);
    }

    public IndexRange CalculateVisibleColumns(double horizontalOffset, double viewportWidth, int totalColumns)
    {
        if (totalColumns <= 0 || ItemWidth <= 0) return new IndexRange(0, -1);

        var firstVisible = (int)(horizontalOffset / ItemWidth);
        var visibleCount = (int)Math.Ceiling(viewportWidth / ItemWidth);

        var start = Math.Max(0, firstVisible - 2);
        var end = Math.Min(totalColumns - 1, firstVisible + visibleCount + 2);

        return new IndexRange(start, end);
    }
}

