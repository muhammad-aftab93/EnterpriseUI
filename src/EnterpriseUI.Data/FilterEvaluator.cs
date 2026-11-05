using System.Collections;
using System.Globalization;

namespace EnterpriseUI.Data;

/// <summary>Evaluates filter operators against a single runtime value; used for calculated columns that have no property to query.</summary>
public static class FilterEvaluator
{
    public static bool Matches(object? value, FilterOperator op, object? operand, object? operand2 = null, bool ignoreCase = true)
    {
        var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        switch (op)
        {
            case FilterOperator.IsNull:
                return value == null;
            case FilterOperator.IsNotNull:
                return value != null;
            case FilterOperator.IsEmpty:
                return value == null || value is string { Length: 0 };
            case FilterOperator.IsNotEmpty:
                return !(value == null || value is string { Length: 0 });

            case FilterOperator.Contains:
            case FilterOperator.DoesNotContain:
            case FilterOperator.StartsWith:
            case FilterOperator.EndsWith:
            {
                var needle = QueryEngine.AsText(operand) ?? string.Empty;
                if (needle.Length == 0) return true;
                var text = QueryEngine.AsText(value);
                var result = text != null && op switch
                {
                    FilterOperator.StartsWith => text.StartsWith(needle, comparison),
                    FilterOperator.EndsWith => text.EndsWith(needle, comparison),
                    _ => text.Contains(needle, comparison)
                };
                return op == FilterOperator.DoesNotContain ? !result : result;
            }

            case FilterOperator.IsContainedIn:
            case FilterOperator.IsNotContainedIn:
            {
                var contained = operand is IEnumerable items and not string && items.Cast<object?>().Any(i => AreEqual(value, i, comparison));
                return op == FilterOperator.IsContainedIn ? contained : !contained;
            }

            case FilterOperator.IsBetween:
            case FilterOperator.IsNotBetween:
            {
                var between = Compare(value, operand, comparison) is { } low && low >= 0 && Compare(value, operand2, comparison) is { } high && high <= 0;
                return op == FilterOperator.IsBetween ? between : !between;
            }

            case FilterOperator.IsEqualTo:
                return AreEqual(value, operand, comparison);
            case FilterOperator.IsNotEqualTo:
                return !AreEqual(value, operand, comparison);

            default:
            {
                var order = Compare(value, operand, comparison);
                if (order == null) return false;
                return op switch
                {
                    FilterOperator.IsLessThan => order < 0,
                    FilterOperator.IsLessThanOrEqualTo => order <= 0,
                    FilterOperator.IsGreaterThan => order > 0,
                    FilterOperator.IsGreaterThanOrEqualTo => order >= 0,
                    _ => false
                };
            }
        }
    }

    private static bool AreEqual(object? value, object? operand, StringComparison comparison)
    {
        if (value == null || operand == null) return value == null && operand == null;
        return Compare(value, operand, comparison) == 0;
    }

    private static int? Compare(object? value, object? operand, StringComparison comparison)
    {
        if (value == null || operand == null) return null;
        if (value is string text)
        {
            return string.Compare(text, Convert.ToString(operand, CultureInfo.InvariantCulture), comparison);
        }

        return QueryEngine.TryConvertValue(operand, value.GetType(), out var converted) && converted != null
            ? ValueComparer.Instance.Compare(value, converted)
            : null;
    }
}
