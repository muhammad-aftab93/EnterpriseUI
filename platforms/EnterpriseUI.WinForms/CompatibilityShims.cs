using System.Collections;
using System.Runtime.CompilerServices;

namespace EnterpriseUI.WinForms
{
    /// <summary>Reference-identity comparer usable on every supported target framework.</summary>
    internal sealed class ObjectReferenceComparer : IEqualityComparer<object?>, IEqualityComparer
    {
        public static readonly ObjectReferenceComparer Instance = new();

        private ObjectReferenceComparer() { }

        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

        public int GetHashCode(object? obj) => RuntimeHelpers.GetHashCode(obj!);
    }
}

#if NETFRAMEWORK
namespace System.Globalization
{
    /// <summary>ISO 8601 week helpers that .NET Framework 4.8 lacks.</summary>
    internal static class ISOWeek
    {
        public static int GetWeekOfYear(DateTime date)
        {
            var day = (int)CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(date);
            if (day >= (int)DayOfWeek.Monday && day <= (int)DayOfWeek.Wednesday) date = date.AddDays(3);
            return CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        }
    }
}

namespace System.Windows.Forms
{
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    /// <summary>Cue-banner based <c>PlaceholderText</c> for text boxes on .NET Framework 4.8.</summary>
    internal static class TextBoxPlaceholderExtensions
    {
        private const int EmSetCueBanner = 0x1501;
        private static readonly ConditionalWeakTable<TextBox, string> Texts = new();
        private static readonly ConditionalWeakTable<TextBox, object> Hooked = new();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        extension(TextBox textBox)
        {
            public string PlaceholderText
            {
                get => Texts.TryGetValue(textBox, out var text) ? text : string.Empty;
                set
                {
                    Texts.Remove(textBox);
                    Texts.Add(textBox, value ?? string.Empty);
                    if (textBox.IsHandleCreated) Apply(textBox);
                    else if (!Hooked.TryGetValue(textBox, out _))
                    {
                        Hooked.Add(textBox, new object());
                        textBox.HandleCreated += (_, _) => Apply(textBox);
                    }
                }
            }
        }

        private static void Apply(TextBox textBox) =>
            SendMessage(textBox.Handle, EmSetCueBanner, new IntPtr(1), Texts.TryGetValue(textBox, out var text) ? text : string.Empty);
    }
}
#endif
