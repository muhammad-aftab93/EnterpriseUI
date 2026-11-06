using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace EnterpriseUI.Data;

/// <summary>Filtered and sorted rows of one request before paging, so a cache can serve any window of it.</summary>
public sealed class QueryMaterialization<T>
{
    public QueryMaterialization(T[] rows, IReadOnlyList<GroupResult<T>>? groups, List<AggregateResult> aggregates)
    {
        Rows = rows;
        Groups = groups;
        Aggregates = aggregates;
    }

    public T[] Rows { get; }
    public int Total => Rows.Length;
    public IReadOnlyList<GroupResult<T>>? Groups { get; }
    public List<AggregateResult> Aggregates { get; }
}

/// <summary>
/// Expression tree compilation engine for dynamic filtering, sorting, grouping and aggregation.
/// Unknown members are ignored, unconvertible filter values match nothing and never throw.
/// </summary>
public static class QueryEngine
{
    private static readonly MethodInfo AsTextMethod = typeof(QueryEngine).GetMethod(nameof(AsText), BindingFlags.Public | BindingFlags.Static)!;
    private static readonly MethodInfo CompareValuesMethod = typeof(QueryEngine).GetMethod(nameof(CompareValues), BindingFlags.Public | BindingFlags.Static)!;
    private static readonly MethodInfo InvokePredicateMethod = typeof(QueryEngine).GetMethod(nameof(InvokePredicate), BindingFlags.Public | BindingFlags.Static)!;
    private static readonly MethodInfo StringEquals = typeof(string).GetMethod(nameof(string.Equals), new[] { typeof(string), typeof(string), typeof(StringComparison) })!;
    private static readonly MethodInfo StringCompare = typeof(string).GetMethod(nameof(string.Compare), new[] { typeof(string), typeof(string), typeof(StringComparison) })!;

    public static DataSourceResult<T> Execute<T>(IQueryable<T> source, DataSourceRequest request)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(request);

        if (request.Groups.Count > 0)
        {
            return Page(Materialize(source, request), request);
        }

        var filtered = ApplyFiltering(source, request.Filters);
        var total = filtered.Count();
        IQueryable<T> paged = ApplySorting(filtered, request.Sorts);
        if (request.EffectiveTake > 0)
        {
            paged = paged.Skip(request.EffectiveSkip).Take(request.EffectiveTake);
        }

        var result = new DataSourceResult<T> { Data = paged.ToList(), Total = total };
        if (request.Aggregates.Count > 0)
        {
            result.AggregateResults.AddRange(ComputeAggregates(filtered, request.Aggregates, total));
        }

        return result;
    }

    /// <summary>Applies filters, sorts and groups and materializes every matching row, without paging.</summary>
    public static QueryMaterialization<T> Materialize<T>(IQueryable<T> source, DataSourceRequest request)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(request);

        var filtered = ApplyFiltering(source, request.Filters);
        var sorted = ApplySorting(filtered, BuildEffectiveSorts(request));
        var rows = sorted.ToArray();

        IReadOnlyList<GroupResult<T>>? groups = request.Groups.Count > 0 ? GroupBuilder.Build(rows, request.Groups) : null;
        var aggregates = request.Aggregates.Count > 0
            ? AggregateCalculator.ComputeAll(rows, request.Aggregates)
            : new List<AggregateResult>();

        return new QueryMaterialization<T>(rows, groups, aggregates);
    }

    /// <summary>Slices a materialized query to the request's window.</summary>
    public static DataSourceResult<T> Page<T>(QueryMaterialization<T> materialized, DataSourceRequest request)
    {
        var rows = materialized.Rows;
        var skip = Math.Min(request.EffectiveSkip, rows.Length);
        var take = request.EffectiveTake > 0 ? Math.Min(request.EffectiveTake, rows.Length - skip) : rows.Length - skip;

        var data = new T[take];
        Array.Copy(rows, skip, data, 0, take);

        var result = new DataSourceResult<T> { Data = data, Total = rows.Length, GroupedData = materialized.Groups };
        result.AggregateResults.AddRange(materialized.Aggregates);
        return result;
    }

    private static List<SortDescriptor> BuildEffectiveSorts(DataSourceRequest request)
    {
        var sorts = new List<SortDescriptor>();
        foreach (var group in request.Groups)
        {
            var keyComparer = group.KeyComparer;
            var selector = group.KeySelector;
            sorts.Add(new SortDescriptor(group.Member, group.SortDirection)
            {
                ValueSelector = group.ValueSelector,
                Comparer = selector == null && keyComparer == null ? null : new KeyedComparer(selector, keyComparer ?? ValueComparer.Instance)
            });
        }

        foreach (var sort in request.Sorts)
        {
            if (!request.Groups.Any(g => string.Equals(g.Member, sort.Member, StringComparison.OrdinalIgnoreCase)))
            {
                sorts.Add(sort);
            }
        }

        return sorts;
    }

    private sealed class KeyedComparer : IComparer<object?>
    {
        private readonly Func<object?, object?>? _selector;
        private readonly IComparer<object?> _inner;

        public KeyedComparer(Func<object?, object?>? selector, IComparer<object?> inner)
        {
            _selector = selector;
            _inner = inner;
        }

        public int Compare(object? x, object? y) =>
            _inner.Compare(_selector != null ? _selector(x) : x, _selector != null ? _selector(y) : y);
    }

    // ------------------------------------------------------------------
    // Filtering
    // ------------------------------------------------------------------

    public static IQueryable<T> ApplyFiltering<T>(IQueryable<T> query, IEnumerable<IFilterDescriptor> filters)
    {
        var filterList = filters.ToList();
        if (filterList.Count == 0) return query;

        var parameter = Expression.Parameter(typeof(T), "item");
        Expression? combined = null;

        foreach (var filter in filterList)
        {
            var expression = BuildFilterExpression(parameter, filter);
            if (expression != null)
            {
                combined = combined == null ? expression : Expression.AndAlso(combined, expression);
            }
        }

        if (combined == null) return query;

        return query.Where(Expression.Lambda<Func<T, bool>>(combined, parameter));
    }

    /// <summary>Compiles filters into a predicate for in-memory evaluation; always true when there are no usable filters.</summary>
    public static Func<T, bool> CompileFilter<T>(IEnumerable<IFilterDescriptor> filters)
    {
        var parameter = Expression.Parameter(typeof(T), "item");
        Expression? combined = null;
        foreach (var filter in filters)
        {
            var expression = BuildFilterExpression(parameter, filter);
            if (expression != null)
            {
                combined = combined == null ? expression : Expression.AndAlso(combined, expression);
            }
        }

        return combined == null ? static _ => true : Expression.Lambda<Func<T, bool>>(combined, parameter).Compile();
    }

    private static Expression? BuildFilterExpression(ParameterExpression parameter, IFilterDescriptor filter)
    {
        switch (filter)
        {
            case FilterDescriptor fd:
            {
                if (string.IsNullOrWhiteSpace(fd.Member)) return null;
                var member = BuildPropertyPath(parameter, fd.Member);
                return member == null ? null : BuildMemberFilter(member, fd);
            }

            case CustomFilterDescriptor custom:
            {
                Expression target = string.IsNullOrWhiteSpace(custom.Member) ? parameter : (BuildPropertyPath(parameter, custom.Member) ?? Expression.Constant(null));
                return Expression.Call(InvokePredicateMethod, Expression.Constant(custom.Predicate), Expression.Convert(target, typeof(object)));
            }

            case CompositeFilterDescriptor composite:
            {
                Expression? nested = null;
                foreach (var child in composite.FilterDescriptors)
                {
                    var childExpression = BuildFilterExpression(parameter, child);
                    if (childExpression == null) continue;
                    nested = nested == null
                        ? childExpression
                        : composite.LogicalOperator == FilterLogicalOperator.And
                            ? Expression.AndAlso(nested, childExpression)
                            : Expression.OrElse(nested, childExpression);
                }
                return nested;
            }

            default:
                return null;
        }
    }

    private static Expression BuildMemberFilter(Expression member, FilterDescriptor fd)
    {
        var memberType = member.Type;
        var underlying = Nullable.GetUnderlyingType(memberType) ?? memberType;
        var canBeNull = !memberType.IsValueType || Nullable.GetUnderlyingType(memberType) != null;
        var comparison = fd.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var isNull = canBeNull ? Expression.Equal(member, Expression.Constant(null, memberType)) : (Expression)Expression.Constant(false);
        var notNull = canBeNull ? Expression.NotEqual(member, Expression.Constant(null, memberType)) : (Expression)Expression.Constant(true);

        switch (fd.Operator)
        {
            case FilterOperator.IsNull:
                return isNull;
            case FilterOperator.IsNotNull:
                return notNull;
            case FilterOperator.IsEmpty:
                return underlying == typeof(string)
                    ? Expression.OrElse(isNull, Expression.Equal(Expression.Property(member, nameof(string.Length)), Expression.Constant(0)))
                    : isNull;
            case FilterOperator.IsNotEmpty:
                return underlying == typeof(string)
                    ? Expression.AndAlso(notNull, Expression.GreaterThan(Expression.Property(member, nameof(string.Length)), Expression.Constant(0)))
                    : notNull;

            case FilterOperator.Contains:
            case FilterOperator.DoesNotContain:
            case FilterOperator.StartsWith:
            case FilterOperator.EndsWith:
                return BuildTextFilter(member, fd, comparison);

            case FilterOperator.IsContainedIn:
            case FilterOperator.IsNotContainedIn:
            {
                var contains = BuildContainedIn(member, fd, underlying, canBeNull);
                return fd.Operator == FilterOperator.IsContainedIn ? contains : Expression.Not(contains);
            }

            case FilterOperator.IsBetween:
            case FilterOperator.IsNotBetween:
            {
                var lower = BuildRelational(member, underlying, fd.Value, FilterOperator.IsGreaterThanOrEqualTo, comparison);
                var upper = BuildRelational(member, underlying, fd.Value2, FilterOperator.IsLessThanOrEqualTo, comparison);
                var between = Expression.AndAlso(lower, upper);
                return fd.Operator == FilterOperator.IsBetween ? between : Expression.Not(between);
            }

            default:
                if (fd.Value == null)
                {
                    return fd.Operator switch
                    {
                        FilterOperator.IsEqualTo => isNull,
                        FilterOperator.IsNotEqualTo => notNull,
                        _ => Expression.Constant(false)
                    };
                }
                return BuildRelational(member, underlying, fd.Value, fd.Operator, comparison);
        }
    }

    private static Expression BuildTextFilter(Expression member, FilterDescriptor fd, StringComparison comparison)
    {
        var needle = Convert.ToString(fd.Value, CultureInfo.InvariantCulture) ?? string.Empty;
        if (needle.Length == 0)
        {
            return Expression.Constant(true);
        }

        var text = member.Type == typeof(string)
            ? member
            : Expression.Call(AsTextMethod, Expression.Convert(member, typeof(object)));

        var methodName = fd.Operator switch
        {
            FilterOperator.StartsWith => nameof(string.StartsWith),
            FilterOperator.EndsWith => nameof(string.EndsWith),
            _ => nameof(string.Contains)
        };
        var method = typeof(string).GetMethod(methodName, new[] { typeof(string), typeof(StringComparison) })!;

        Expression Body(Expression value)
        {
            var call = Expression.Call(value, method, Expression.Constant(needle), Expression.Constant(comparison));
            var valueIsNull = Expression.Equal(value, Expression.Constant(null, typeof(string)));
            return fd.Operator == FilterOperator.DoesNotContain
                ? Expression.OrElse(valueIsNull, Expression.Not(call))
                : Expression.AndAlso(Expression.Not(valueIsNull), call);
        }

        if (text == member)
        {
            return Body(text);
        }

        var temp = Expression.Variable(typeof(string), "text");
        return Expression.Block(typeof(bool), new[] { temp }, Expression.Assign(temp, text), Body(temp));
    }

    private static Expression BuildContainedIn(Expression member, FilterDescriptor fd, Type underlying, bool canBeNull)
    {
        var setType = typeof(HashSet<>).MakeGenericType(underlying);
        var set = underlying == typeof(string)
            ? Activator.CreateInstance(setType, fd.IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)!
            : Activator.CreateInstance(setType)!;
        var add = setType.GetMethod("Add")!;

        if (fd.Value is IEnumerable values and not string)
        {
            foreach (var value in values)
            {
                if (value != null && TryConvertValue(value, underlying, out var converted) && converted != null)
                {
                    add.Invoke(set, new[] { converted });
                }
            }
        }
        else if (fd.Value != null && TryConvertValue(fd.Value, underlying, out var single) && single != null)
        {
            add.Invoke(set, new[] { single });
        }

        var containsMethod = setType.GetMethod("Contains")!;
        var setConstant = Expression.Constant(set, setType);

        if (Nullable.GetUnderlyingType(member.Type) != null)
        {
            return Expression.AndAlso(
                Expression.Property(member, "HasValue"),
                Expression.Call(setConstant, containsMethod, Expression.Property(member, "Value")));
        }

        if (!member.Type.IsValueType && underlying != typeof(string))
        {
            return Expression.AndAlso(
                Expression.NotEqual(member, Expression.Constant(null, member.Type)),
                Expression.Call(setConstant, containsMethod, member));
        }

        return Expression.Call(setConstant, containsMethod, member);
    }

    private static Expression BuildRelational(Expression member, Type underlying, object? value, FilterOperator op, StringComparison comparison)
    {
        var negate = op == FilterOperator.IsNotEqualTo;
        if (value == null || !TryConvertValue(value, underlying, out var converted) || converted == null)
        {
            return Expression.Constant(negate);
        }

        var constant = Expression.Constant(converted, member.Type);
        Expression result;

        if (underlying == typeof(string))
        {
            if (op is FilterOperator.IsEqualTo or FilterOperator.IsNotEqualTo)
            {
                result = Expression.Call(StringEquals, member, constant, Expression.Constant(comparison));
            }
            else
            {
                result = CompareToZero(op, Expression.Call(StringCompare, member, constant, Expression.Constant(comparison)));
            }
        }
        else
        {
            try
            {
                result = op switch
                {
                    FilterOperator.IsEqualTo or FilterOperator.IsNotEqualTo => Expression.Equal(member, constant),
                    FilterOperator.IsLessThan => Expression.LessThan(member, constant),
                    FilterOperator.IsLessThanOrEqualTo => Expression.LessThanOrEqual(member, constant),
                    FilterOperator.IsGreaterThan => Expression.GreaterThan(member, constant),
                    FilterOperator.IsGreaterThanOrEqualTo => Expression.GreaterThanOrEqual(member, constant),
                    _ => Expression.Constant(false)
                };
            }
            catch (InvalidOperationException)
            {
                var compare = Expression.Call(CompareValuesMethod, Expression.Convert(member, typeof(object)), Expression.Constant(converted, typeof(object)));
                result = op is FilterOperator.IsEqualTo or FilterOperator.IsNotEqualTo
                    ? Expression.Equal(compare, Expression.Constant(0))
                    : CompareToZero(op, compare);
            }
        }

        return negate ? Expression.Not(result) : result;
    }

    private static BinaryExpression CompareToZero(FilterOperator op, Expression comparison)
    {
        var zero = Expression.Constant(0);
        return op switch
        {
            FilterOperator.IsLessThan => Expression.LessThan(comparison, zero),
            FilterOperator.IsLessThanOrEqualTo => Expression.LessThanOrEqual(comparison, zero),
            FilterOperator.IsGreaterThan => Expression.GreaterThan(comparison, zero),
            FilterOperator.IsGreaterThanOrEqualTo => Expression.GreaterThanOrEqual(comparison, zero),
            _ => Expression.Equal(comparison, zero)
        };
    }

    public static string? AsText(object? value) => value switch
    {
        null => null,
        string text => text,
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };

    public static int CompareValues(object? left, object? right) => ValueComparer.Instance.Compare(left, right);

    public static bool InvokePredicate(Func<object?, bool> predicate, object? value)
    {
        try
        {
            return predicate(value);
        }
        catch (Exception ex) when (ex is InvalidCastException or NullReferenceException or FormatException)
        {
            return false;
        }
    }

    /// <summary>Converts filter text or a boxed value to the member's underlying type; false if it cannot be represented.</summary>
    public static bool TryConvertValue(object? value, Type targetType, out object? result)
    {
        result = null;
        if (value == null) return false;

        var target = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (target.IsInstanceOfType(value))
        {
            result = value;
            return true;
        }

        try
        {
            if (target.IsEnum)
            {
                if (value is string name)
                {
                    if (Enum.TryParse(target, name.Trim(), ignoreCase: true, out var parsed) && Enum.IsDefined(target, parsed!))
                    {
                        result = parsed;
                        return true;
                    }
                    return false;
                }

                if (NumericValue.TryGet(value, out var number) && number == decimal.Truncate(number))
                {
                    result = Enum.ToObject(target, (long)number);
                    return true;
                }
                return false;
            }

            var text = value as string;
            if (target == typeof(Guid)) return Guid.TryParse(text, out var guid) && Assign(guid, out result);
            if (target == typeof(DateTime))
            {
                if (value is DateTimeOffset offset) return Assign(offset.DateTime, out result);
                return text != null && (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) || DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out dt)) && Assign(dt, out result);
            }
            if (target == typeof(DateTimeOffset)) return (value is DateTime d ? Assign(new DateTimeOffset(d), out result) : text != null && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto) && Assign(dto, out result));
            if (target == typeof(DateOnly)) return text != null && DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly) && Assign(dateOnly, out result);
            if (target == typeof(TimeOnly)) return text != null && TimeOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timeOnly) && Assign(timeOnly, out result);
            if (target == typeof(TimeSpan)) return text != null && TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var span) && Assign(span, out result);
            if (target == typeof(bool)) return text != null && bool.TryParse(text.Trim(), out var flag) && Assign(flag, out result);
            if (target == typeof(string)) return Assign(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty, out result);
            if (target == typeof(char)) return text is { Length: 1 } && Assign(text[0], out result);

            if (value is string numeric)
            {
                numeric = numeric.Trim();
                if (numeric.Length == 0) return false;
                result = Convert.ChangeType(numeric, target, CultureInfo.InvariantCulture);
                return true;
            }

            if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(target))
            {
                result = Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
                return true;
            }
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException or ArgumentException)
        {
            result = null;
        }

        return false;
    }

    private static bool Assign(object value, out object? result)
    {
        result = value;
        return true;
    }

    // ------------------------------------------------------------------
    // Sorting
    // ------------------------------------------------------------------

    public static IQueryable<T> ApplySorting<T>(IQueryable<T> query, IEnumerable<SortDescriptor> sorts)
    {
        var sortList = sorts.Where(s => !string.IsNullOrWhiteSpace(s.Member) || s.ValueSelector != null).ToList();
        if (sortList.Count == 0) return query;

        if (sortList.Any(s => s.Comparer != null || s.ValueSelector != null))
        {
            return ApplyComparerSorting(query, sortList);
        }

        IQueryable<T>? ordered = null;
        foreach (var sort in sortList)
        {
            var parameter = Expression.Parameter(typeof(T), "item");
            var propertyExpression = BuildPropertyPath(parameter, sort.Member);
            if (propertyExpression == null) continue;

            var descending = sort.SortDirection == ListSortDirection.Descending;
            var methodName = ordered == null
                ? (descending ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy))
                : (descending ? nameof(Queryable.ThenByDescending) : nameof(Queryable.ThenBy));

            var method = typeof(Queryable).GetMethods()
                .First(m => m.Name == methodName && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(T), propertyExpression.Type);

            ordered = (IQueryable<T>)method.Invoke(null, new object[] { ordered ?? query, Expression.Lambda(propertyExpression, parameter) })!;
        }

        return ordered ?? query;
    }

    private static IQueryable<T> ApplyComparerSorting<T>(IQueryable<T> query, List<SortDescriptor> sorts)
    {
        IOrderedEnumerable<T>? ordered = null;
        foreach (var sort in sorts)
        {
            var member = sort.Member;
            var selector = sort.ValueSelector;
            var getter = selector == null ? PropertyPath.GetGetter(typeof(T), member) : null;
            Func<T, object?> key = item => item == null ? null : selector != null ? selector(item) : getter != null ? getter(item) : PropertyPath.GetValue(item, member);
            var comparer = sort.Comparer ?? ValueComparer.Instance;
            var descending = sort.SortDirection == ListSortDirection.Descending;

            ordered = ordered == null
                ? (descending ? query.OrderByDescending(key, comparer) : query.OrderBy(key, comparer))
                : (descending ? ordered.ThenByDescending(key, comparer) : ordered.ThenBy(key, comparer));
        }

        return ordered == null ? query : ordered.AsQueryable();
    }

    /// <summary>Builds a null-safe member access; returns null when any segment is unknown. Value types reached through reference segments become nullable.</summary>
    private static Expression? BuildPropertyPath(Expression root, string path)
    {
        var segments = path.Split('.');
        var guards = new List<Expression>();
        var current = root;

        for (var i = 0; i < segments.Length; i++)
        {
            var property = PropertyPath.FindProperty(current.Type, segments[i].Trim());
            if (property is not { CanRead: true }) return null;

            current = Expression.Property(current, property);
            if (i == segments.Length - 1) break;

            if (!current.Type.IsValueType)
            {
                guards.Add(current);
            }
            else if (Nullable.GetUnderlyingType(current.Type) != null)
            {
                guards.Add(current);
                current = Expression.Property(current, "Value");
            }
        }

        if (guards.Count == 0) return current;

        var resultType = current.Type.IsValueType && Nullable.GetUnderlyingType(current.Type) == null
            ? typeof(Nullable<>).MakeGenericType(current.Type)
            : current.Type;
        var value = resultType == current.Type ? current : Expression.Convert(current, resultType);
        var anyNull = guards
            .Select(g => (Expression)Expression.Equal(g, Expression.Constant(null, g.Type)))
            .Aggregate(Expression.OrElse);

        return Expression.Condition(anyNull, Expression.Default(resultType), value);
    }

    // ------------------------------------------------------------------
    // Aggregates
    // ------------------------------------------------------------------

    private static List<AggregateResult> ComputeAggregates<T>(IQueryable<T> filtered, List<AggregateDescriptor> aggregates, int total)
    {
        if (aggregates.All(a => a.Custom == null && a.Function == AggregateFunction.Count))
        {
            return aggregates.Select(a => new AggregateResult { Member = a.Member, Function = a.Function, Key = a.Key, Value = total }).ToList();
        }

        return AggregateCalculator.ComputeAll(filtered.ToArray(), aggregates);
    }
}

/// <summary>Builds nested group trees over rows already sorted by their group keys.</summary>
internal static class GroupBuilder
{
    public static IReadOnlyList<GroupResult<T>> Build<T>(T[] rows, IReadOnlyList<GroupDescriptor> descriptors) =>
        BuildLevel(rows, descriptors, 0, 0, rows.Length);

    private static List<GroupResult<T>> BuildLevel<T>(T[] rows, IReadOnlyList<GroupDescriptor> descriptors, int level, int start, int end)
    {
        var descriptor = descriptors[level];
        var getter = descriptor.ValueSelector == null ? PropertyPath.GetGetter(typeof(T), descriptor.Member) : null;
        var groups = new List<GroupResult<T>>();

        object? KeyOf(T item)
        {
            if (item == null) return null;
            var value = descriptor.ValueSelector != null ? descriptor.ValueSelector(item) : getter != null ? getter(item) : PropertyPath.GetValue(item, descriptor.Member);
            return descriptor.KeySelector != null ? descriptor.KeySelector(value) : value;
        }

        var runStart = start;
        var runKey = start < end ? KeyOf(rows[start]) : null;
        for (var i = start + 1; i <= end; i++)
        {
            if (i < end)
            {
                var key = KeyOf(rows[i]);
                if (ValueComparer.Instance.Compare(key, runKey) == 0)
                {
                    continue;
                }

                groups.Add(CreateGroup(rows, descriptors, level, runStart, i, runKey));
                runStart = i;
                runKey = key;
            }
            else if (end > start)
            {
                groups.Add(CreateGroup(rows, descriptors, level, runStart, end, runKey));
            }
        }

        return groups;
    }

    private static GroupResult<T> CreateGroup<T>(T[] rows, IReadOnlyList<GroupDescriptor> descriptors, int level, int start, int end, object? key)
    {
        var descriptor = descriptors[level];
        var items = new ListSlice<T>(rows, start, end - start);
        var group = new GroupResult<T>
        {
            Key = key,
            Member = descriptor.Member,
            Level = level,
            ItemCount = end - start,
            Items = items,
            Subgroups = level + 1 < descriptors.Count ? BuildLevel(rows, descriptors, level + 1, start, end) : null
        };

        if (descriptor.Aggregates.Count > 0)
        {
            group.Aggregates.AddRange(AggregateCalculator.ComputeAll(items, descriptor.Aggregates));
        }

        return group;
    }
}
