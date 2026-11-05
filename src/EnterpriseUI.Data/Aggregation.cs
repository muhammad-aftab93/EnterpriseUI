using System.Collections;
using System.Globalization;

namespace EnterpriseUI.Data;

/// <summary>Total ordering over arbitrary boxed values: nulls first, numbers by magnitude, same-type comparables natively, anything else by text.</summary>
public sealed class ValueComparer : IComparer<object?>, IComparer
{
    public static ValueComparer Instance { get; } = new();

    public int Compare(object? x, object? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x == null) return -1;
        if (y == null) return 1;

        if (x is string sx && y is string sy)
        {
            return string.Compare(sx, sy, StringComparison.CurrentCulture);
        }

        if (NumericValue.TryGet(x, out var nx) && NumericValue.TryGet(y, out var ny))
        {
            return nx.CompareTo(ny);
        }

        if (x.GetType() == y.GetType() && x is IComparable comparable)
        {
            return comparable.CompareTo(y);
        }

        return string.Compare(Convert.ToString(x, CultureInfo.CurrentCulture), Convert.ToString(y, CultureInfo.CurrentCulture), StringComparison.CurrentCulture);
    }

    int IComparer.Compare(object? x, object? y) => Compare(x, y);
}

/// <summary>Converts boxed numeric values of any CLR numeric type to decimal for comparison and arithmetic.</summary>
public static class NumericValue
{
    public static bool TryGet(object? value, out decimal number)
    {
        number = 0;
        switch (value)
        {
            case null: return false;
            case decimal d: number = d; return true;
            case int i: number = i; return true;
            case long l: number = l; return true;
            case short s: number = s; return true;
            case byte b: number = b; return true;
            case sbyte sb: number = sb; return true;
            case ushort us: number = us; return true;
            case uint ui: number = ui; return true;
            case ulong ul: number = ul; return true;
            case double dbl when !double.IsNaN(dbl) && !double.IsInfinity(dbl) && Math.Abs(dbl) < 7.9e28: number = (decimal)dbl; return true;
            case float f when !float.IsNaN(f) && !float.IsInfinity(f) && Math.Abs(f) < 7.9e28f: number = (decimal)f; return true;
            default: return false;
        }
    }
}

/// <summary>Zero-copy window over a sorted result so subgroups can expose their items without allocating.</summary>
internal sealed class ListSlice<T> : IReadOnlyList<T>
{
    private readonly IReadOnlyList<T> _source;
    private readonly int _start;

    public ListSlice(IReadOnlyList<T> source, int start, int count)
    {
        _source = source;
        _start = start;
        Count = count;
    }

    public int Count { get; }

    public T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
            return _source[_start + index];
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (var i = 0; i < Count; i++)
        {
            yield return _source[_start + i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>In-memory aggregate evaluation shared by footers, group summaries and exports.</summary>
public static class AggregateCalculator
{
    public static object? Compute<T>(IReadOnlyList<T> items, AggregateDescriptor aggregate)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(aggregate);

        if (aggregate.Custom == null && aggregate.Function == AggregateFunction.Count)
        {
            return items.Count;
        }

        var getter = aggregate.ValueSelector == null ? PropertyPath.GetGetter(typeof(T), aggregate.Member) : null;
        var first = true;
        var values = new List<object?>(items.Count);
        object? firstValue = null, lastValue = null;
        foreach (var item in items)
        {
            if (item == null) continue;
            var value = aggregate.ValueSelector != null ? aggregate.ValueSelector(item) : getter != null ? getter(item) : PropertyPath.GetValue(item, aggregate.Member);
            if (first)
            {
                firstValue = value;
                first = false;
            }
            lastValue = value;
            if (value != null) values.Add(value);
        }

        if (aggregate.Custom != null)
        {
            return aggregate.Custom(values);
        }

        return aggregate.Function switch
        {
            AggregateFunction.First => firstValue,
            AggregateFunction.Last => lastValue,
            AggregateFunction.CountDistinct => values.Distinct().Count(),
            AggregateFunction.Sum => Sum(values, aggregate.ValueSelector != null ? null : PropertyPath.GetPathType(typeof(T), aggregate.Member)),
            AggregateFunction.Average => Average(values),
            AggregateFunction.Min => Extreme(values, min: true),
            AggregateFunction.Max => Extreme(values, min: false),
            AggregateFunction.Median => Median(values),
            AggregateFunction.StdDev => Variance(values, sqrt: true),
            AggregateFunction.Variance => Variance(values, sqrt: false),
            _ => null
        };
    }

    public static List<AggregateResult> ComputeAll<T>(IReadOnlyList<T> items, IEnumerable<AggregateDescriptor> aggregates)
    {
        var results = new List<AggregateResult>();
        foreach (var aggregate in aggregates)
        {
            results.Add(new AggregateResult
            {
                Member = aggregate.Member,
                Function = aggregate.Function,
                Key = aggregate.Key,
                Value = Compute(items, aggregate)
            });
        }
        return results;
    }

    private static object? Sum(List<object?> values, Type? declaredType)
    {
        var type = Nullable.GetUnderlyingType(declaredType ?? typeof(object)) ?? declaredType ?? typeof(object);
        if (values.Count == 0)
        {
            if (type == typeof(decimal)) return 0m;
            if (type == typeof(double) || type == typeof(float)) return 0d;
            return type.IsPrimitive && type != typeof(bool) && type != typeof(char) ? 0L : null;
        }

        if (values[0] is double or float)
        {
            double total = 0;
            foreach (var value in values)
            {
                if (!TryDouble(value, out var d)) return null;
                total += d;
            }
            return total;
        }

        if (values[0] is decimal)
        {
            decimal total = 0;
            foreach (var value in values)
            {
                if (!NumericValue.TryGet(value, out var d)) return null;
                total += d;
            }
            return total;
        }

        long integral = 0;
        try
        {
            foreach (var value in values)
            {
                if (!NumericValue.TryGet(value, out var d) || d != decimal.Truncate(d)) return SumAsDecimal(values);
                integral = checked(integral + (long)d);
            }
            return integral;
        }
        catch (OverflowException)
        {
            return SumAsDecimal(values);
        }
    }

    private static object? SumAsDecimal(List<object?> values)
    {
        decimal total = 0;
        foreach (var value in values)
        {
            if (!NumericValue.TryGet(value, out var d)) return null;
            total += d;
        }
        return total;
    }

    private static object? Average(List<object?> values)
    {
        if (values.Count == 0) return null;
        if (values[0] is decimal)
        {
            decimal total = 0;
            foreach (var value in values)
            {
                if (!NumericValue.TryGet(value, out var d)) return null;
                total += d;
            }
            return total / values.Count;
        }

        double sum = 0;
        foreach (var value in values)
        {
            if (!TryDouble(value, out var d)) return null;
            sum += d;
        }
        return sum / values.Count;
    }

    private static object? Extreme(List<object?> values, bool min)
    {
        if (values.Count == 0) return null;
        var best = values[0];
        for (var i = 1; i < values.Count; i++)
        {
            var comparison = ValueComparer.Instance.Compare(values[i], best);
            if (min ? comparison < 0 : comparison > 0) best = values[i];
        }
        return best;
    }

    private static object? Median(List<object?> values)
    {
        if (values.Count == 0) return null;
        var ordered = values.OrderBy(v => v, ValueComparer.Instance).ToList();
        var mid = ordered.Count / 2;
        if (ordered.Count % 2 == 1) return ordered[mid];

        if (NumericValue.TryGet(ordered[mid - 1], out var a) && NumericValue.TryGet(ordered[mid], out var b))
        {
            return ordered[mid] is decimal ? (a + b) / 2 : (double)((a + b) / 2);
        }
        return ordered[mid - 1];
    }

    private static object? Variance(List<object?> values, bool sqrt)
    {
        if (values.Count < 2) return null;
        var numbers = new List<double>(values.Count);
        foreach (var value in values)
        {
            if (!TryDouble(value, out var d)) return null;
            numbers.Add(d);
        }

        var mean = numbers.Average();
        var variance = numbers.Sum(n => (n - mean) * (n - mean)) / (numbers.Count - 1);
        return sqrt ? Math.Sqrt(variance) : variance;
    }

    private static bool TryDouble(object? value, out double result)
    {
        switch (value)
        {
            case double d: result = d; return true;
            case float f: result = f; return true;
            default:
                if (NumericValue.TryGet(value, out var number))
                {
                    result = (double)number;
                    return true;
                }
                result = 0;
                return false;
        }
    }
}
