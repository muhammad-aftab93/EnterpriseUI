using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;

namespace EnterpriseUI.Data;

/// <summary>
/// High-performance expression tree compilation engine for dynamic querying.
/// Superset of Telerik DataSourceRequest and DevExpress CriteriaToExpression converter.
/// </summary>
public static class QueryEngine
{
    public static DataSourceResult<T> Execute<T>(IQueryable<T> source, DataSourceRequest request)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(request);

        // 1. Filtering
        var filtered = ApplyFiltering(source, request.Filters);

        // 2. Count Total
        var total = filtered.Count();

        // 3. Sorting
        var sorted = ApplySorting(filtered, request.Sorts);

        // 4. Paging
        var paged = sorted;
        if (request.EffectiveTake > 0)
        {
            paged = paged.Skip(request.EffectiveSkip).Take(request.EffectiveTake);
        }

        var result = new DataSourceResult<T>
        {
            Data = paged.ToList(),
            Total = total
        };

        // 5. Aggregates calculation
        if (request.Aggregates.Count > 0)
        {
            ComputeAggregates(filtered, request.Aggregates, result.AggregateResults);
        }

        return result;
    }

    public static IQueryable<T> ApplyFiltering<T>(IQueryable<T> query, IEnumerable<IFilterDescriptor> filters)
    {
        var filterList = filters.ToList();
        if (filterList.Count == 0) return query;

        var parameter = Expression.Parameter(typeof(T), "item");
        Expression? combined = null;

        foreach (var filter in filterList)
        {
            var expr = BuildFilterExpression(parameter, filter);
            if (expr != null)
            {
                combined = combined == null ? expr : Expression.AndAlso(combined, expr);
            }
        }

        if (combined == null) return query;

        var lambda = Expression.Lambda<Func<T, bool>>(combined, parameter);
        return query.Where(lambda);
    }

    private static Expression? BuildFilterExpression(ParameterExpression parameter, IFilterDescriptor filter)
    {
        if (filter is FilterDescriptor fd)
        {
            if (string.IsNullOrWhiteSpace(fd.Member)) return null;

            var prop = typeof(ParameterExpression).GetProperty("Type") != null 
                ? GetPropertyPathExpression(parameter, fd.Member)
                : null;

            if (prop == null) return null;

            if (fd.Operator == FilterOperator.IsNull)
            {
                return Nullable.GetUnderlyingType(prop.Type) != null || !prop.Type.IsValueType
                    ? Expression.Equal(prop, Expression.Constant(null, prop.Type))
                    : Expression.Constant(false);
            }

            if (fd.Operator == FilterOperator.IsNotNull)
            {
                return Nullable.GetUnderlyingType(prop.Type) != null || !prop.Type.IsValueType
                    ? Expression.NotEqual(prop, Expression.Constant(null, prop.Type))
                    : Expression.Constant(true);
            }

            var constant = Expression.Constant(ConvertValue(fd.Value, prop.Type), prop.Type);
            var nonNullString = prop.Type == typeof(string)
                ? Expression.NotEqual(prop, Expression.Constant(null, typeof(string)))
                : null;

            return fd.Operator switch
            {
                FilterOperator.IsEqualTo => Expression.Equal(prop, constant),
                FilterOperator.IsNotEqualTo => Expression.NotEqual(prop, constant),
                FilterOperator.IsGreaterThan => Expression.GreaterThan(prop, constant),
                FilterOperator.IsGreaterThanOrEqualTo => Expression.GreaterThanOrEqual(prop, constant),
                FilterOperator.IsLessThan => Expression.LessThan(prop, constant),
                FilterOperator.IsLessThanOrEqualTo => Expression.LessThanOrEqual(prop, constant),
                FilterOperator.StartsWith when prop.Type == typeof(string) =>
                    Expression.AndAlso(nonNullString!, Expression.Call(prop, typeof(string).GetMethod("StartsWith", new[] { typeof(string) })!, constant)),
                FilterOperator.EndsWith when prop.Type == typeof(string) =>
                    Expression.AndAlso(nonNullString!, Expression.Call(prop, typeof(string).GetMethod("EndsWith", new[] { typeof(string) })!, constant)),
                FilterOperator.Contains when prop.Type == typeof(string) =>
                    Expression.AndAlso(nonNullString!, Expression.Call(prop, typeof(string).GetMethod("Contains", new[] { typeof(string) })!, constant)),
                _ => Expression.Equal(prop, constant)
            };
        }
        else if (filter is CompositeFilterDescriptor cfd)
        {
            Expression? nested = null;
            foreach (var child in cfd.FilterDescriptors)
            {
                var childExpr = BuildFilterExpression(parameter, child);
                if (childExpr != null)
                {
                    if (nested == null) nested = childExpr;
                    else
                    {
                        nested = cfd.LogicalOperator == FilterLogicalOperator.And
                            ? Expression.AndAlso(nested, childExpr)
                            : Expression.OrElse(nested, childExpr);
                    }
                }
            }
            return nested;
        }

        return null;
    }

    private static Expression? GetPropertyPathExpression(Expression expression, string propertyPath)
    {
        var parts = propertyPath.Split('.');
        var current = expression;
        foreach (var part in parts)
        {
            var prop = current.Type.GetProperty(part, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            if (prop == null) return null;
            current = Expression.Property(current, prop);
        }
        return current;
    }

    private static object? ConvertValue(object? value, Type targetType)
    {
        if (value == null) return null;
        var nonNullable = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (nonNullable.IsEnum)
        {
            return Enum.Parse(nonNullable, value.ToString()!);
        }
        return Convert.ChangeType(value, nonNullable);
    }

    public static IQueryable<T> ApplySorting<T>(IQueryable<T> query, IEnumerable<SortDescriptor> sorts)
    {
        var sortList = sorts.ToList();
        if (sortList.Count == 0) return query;

        var ordered = (IOrderedQueryable<T>)query;
        var isFirst = true;

        foreach (var sort in sortList)
        {
            if (string.IsNullOrWhiteSpace(sort.Member)) continue;

            var param = Expression.Parameter(typeof(T), "item");
            var propExpr = GetPropertyPathExpression(param, sort.Member);
            if (propExpr == null) continue;

            var lambda = Expression.Lambda(propExpr, param);
            var methodName = isFirst
                ? (sort.SortDirection == ListSortDirection.Ascending ? "OrderBy" : "OrderByDescending")
                : (sort.SortDirection == ListSortDirection.Ascending ? "ThenBy" : "ThenByDescending");

            var method = typeof(Queryable).GetMethods()
                .First(m => m.Name == methodName && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(T), propExpr.Type);

            ordered = (IOrderedQueryable<T>)method.Invoke(null, new object[] { isFirst ? query : ordered, lambda })!;
            isFirst = false;
        }

        return isFirst ? query : ordered;
    }

    private static void ComputeAggregates<T>(IQueryable<T> source, IEnumerable<AggregateDescriptor> aggregates, List<AggregateResult> results)
    {
        foreach (var agg in aggregates)
        {
            if (agg.Function == AggregateFunction.Count)
            {
                results.Add(new AggregateResult
                {
                    Member = agg.Member,
                    Function = agg.Function,
                    Value = source.Count()
                });
            }
        }
    }
}

