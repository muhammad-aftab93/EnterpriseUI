using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace EnterpriseUI.Core.Common;

/// <summary>
/// Defensive programming assertions and boundary validation.
/// </summary>
public static class Guard
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T NotNull<T>([NotNull] T? value, [CallerArgumentExpression(nameof(value))] string? parameterName = null)
    {
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string NotNullOrWhiteSpace([NotNull] string? value, [CallerArgumentExpression(nameof(value))] string? parameterName = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be null or empty/whitespace.", parameterName);
        }
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void InRange(int value, int min, int max, [CallerArgumentExpression(nameof(value))] string? parameterName = null)
    {
        if (value < min || value > max)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"Value {value} must be between {min} and {max}.");
        }
    }
}

