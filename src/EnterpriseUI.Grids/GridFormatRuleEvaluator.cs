using System;
using System.Globalization;

namespace EnterpriseUI.Grids;

public static class GridFormatRuleEvaluator
{
    public static bool Matches(GridFormatRule rule, object? value)
    {
        ArgumentNullException.ThrowIfNull(rule);

        if (!rule.Enabled || value == null || rule.Operator is FormatConditionOperator.TopN or FormatConditionOperator.BottomN)
        {
            return false;
        }

        if (!TryCompare(value, rule.Value1, out var firstComparison))
        {
            return false;
        }

        return rule.Operator switch
        {
            FormatConditionOperator.Equal => firstComparison == 0,
            FormatConditionOperator.NotEqual => firstComparison != 0,
            FormatConditionOperator.GreaterThan => firstComparison > 0,
            FormatConditionOperator.GreaterThanOrEqual => firstComparison >= 0,
            FormatConditionOperator.LessThan => firstComparison < 0,
            FormatConditionOperator.LessThanOrEqual => firstComparison <= 0,
            FormatConditionOperator.Between =>
                TryCompare(value, rule.Value2, out var secondComparison) && firstComparison >= 0 && secondComparison <= 0,
            _ => false
        };
    }

    public static bool TryGetNormalizedValue(object? value, object? minimum, object? maximum, out double normalizedValue)
    {
        normalizedValue = 0;
        if (!TryConvertNumber(value, out var numericValue) ||
            !TryConvertNumber(minimum, out var numericMinimum) ||
            !TryConvertNumber(maximum, out var numericMaximum) ||
            numericMaximum <= numericMinimum)
        {
            return false;
        }

        normalizedValue = (double)((numericValue - numericMinimum) / (numericMaximum - numericMinimum));
        normalizedValue = Math.Clamp(normalizedValue, 0, 1);
        return true;
    }

    private static bool TryCompare(object? value, object? expected, out int comparison)
    {
        comparison = 0;
        if (value == null || expected == null)
        {
            return false;
        }

        if (TryConvertNumber(value, out var numericValue) && TryConvertNumber(expected, out var numericExpected))
        {
            comparison = numericValue.CompareTo(numericExpected);
            return true;
        }

        if (value is string text && expected is string expectedText)
        {
            comparison = string.Compare(text, expectedText, StringComparison.OrdinalIgnoreCase);
            return true;
        }

        if (value.GetType() == expected.GetType() && value is IComparable comparable)
        {
            comparison = comparable.CompareTo(expected);
            return true;
        }

        return false;
    }

    private static bool TryConvertNumber(object? value, out decimal number)
    {
        number = 0;
        if (value == null)
        {
            return false;
        }

        var typeCode = Type.GetTypeCode(value.GetType());
        if (typeCode is not (TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 or
            TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or
            TypeCode.Double or TypeCode.Decimal))
        {
            return false;
        }

        try
        {
            number = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }
}