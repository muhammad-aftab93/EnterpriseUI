#if !NET5_0_OR_GREATER
// Small BCL shims for APIs that exist on .NET 6+ but not on .NET Framework 4.8.
// Compiled into every net48 project (see Directory.Build.props); internal so nothing leaks into the public API.
using System;
using System.Text;
using System.Threading.Tasks;

namespace System
{
    internal static class Net48Compat
    {
        extension(ValueTask)
        {
            public static ValueTask<T> FromResult<T>(T result) => new ValueTask<T>(result);
        }

        extension(Array)
        {
            public static void Fill<T>(T[] array, T value)
            {
                for (var i = 0; i < array.Length; i++) array[i] = value;
            }
        }

        extension(Environment)
        {
            public static long TickCount64 => (long)(uint)Environment.TickCount;
        }

        extension(double)
        {
            public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        }

        extension(float)
        {
            public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        }

        extension(string value)
        {
            public string Replace(string oldValue, string? newValue, StringComparison comparison)
            {
                if (string.IsNullOrEmpty(oldValue)) throw new ArgumentException("Value cannot be empty.", nameof(oldValue));
                newValue ??= string.Empty;
                var index = value.IndexOf(oldValue, comparison);
                if (index < 0) return value;
                var builder = new StringBuilder(value.Length);
                var start = 0;
                while (index >= 0)
                {
                    builder.Append(value, start, index - start).Append(newValue);
                    start = index + oldValue.Length;
                    index = value.IndexOf(oldValue, start, comparison);
                }
                return builder.Append(value, start, value.Length - start).ToString();
            }
        }
    }
}
#endif
