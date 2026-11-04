using System.Globalization;

namespace EnterpriseUI.Localization;

public interface IStringLocalizer
{
    string this[string key] { get; }
    string GetString(string key, params object[] arguments);
}

/// <summary>
/// Enterprise Localization engine with RTL (Right-to-Left) language awareness and dynamic culture switching.
/// </summary>
public sealed class LocalizationManager : IStringLocalizer
{
    private static readonly Lazy<LocalizationManager> _instance = new(() => new LocalizationManager());
    public static LocalizationManager Instance => _instance.Value;

    private CultureInfo _currentCulture = CultureInfo.CurrentUICulture;
    private readonly Dictionary<string, Dictionary<string, string>> _translations = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler<CultureInfo>? CultureChanged;

    public CultureInfo CurrentCulture => _currentCulture;
    public bool IsRightToLeft => _currentCulture.TextInfo.IsRightToLeft;

    public LocalizationManager()
    {
        // Register default translations
        RegisterCultureStrings("en-US", new Dictionary<string, string>
        {
            ["Grid_Filter"] = "Filter",
            ["Grid_Clear"] = "Clear",
            ["Grid_Apply"] = "Apply",
            ["Grid_SortAsc"] = "Sort Ascending",
            ["Grid_SortDesc"] = "Sort Descending",
            ["Grid_GroupBy"] = "Group by this column",
            ["Grid_NoRecords"] = "No records available.",
            ["Common_Save"] = "Save",
            ["Common_Cancel"] = "Cancel",
            ["Common_Delete"] = "Delete",
            ["Common_Search"] = "Search..."
        });

        RegisterCultureStrings("ar-SA", new Dictionary<string, string>
        {
            ["Grid_Filter"] = "تصفية",
            ["Grid_Clear"] = "مسح",
            ["Grid_Apply"] = "تطبيق",
            ["Grid_SortAsc"] = "ترتيب تصاعدي",
            ["Grid_SortDesc"] = "ترتيب تنازلي",
            ["Grid_GroupBy"] = "تجميع حسب هذا العمود",
            ["Grid_NoRecords"] = "لا توجد سجلات متاحة.",
            ["Common_Save"] = "حفظ",
            ["Common_Cancel"] = "إلغاء",
            ["Common_Delete"] = "حذف",
            ["Common_Search"] = "بحث..."
        });
    }

    public void RegisterCultureStrings(string cultureName, IDictionary<string, string> strings)
    {
        if (!_translations.TryGetValue(cultureName, out var dict))
        {
            dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _translations[cultureName] = dict;
        }

        foreach (var (k, v) in strings)
        {
            dict[k] = v;
        }
    }

    public void SetCulture(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        _currentCulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureChanged?.Invoke(this, culture);
    }

    public string this[string key] => GetString(key);

    public string GetString(string key, params object[] arguments)
    {
        var cultureName = _currentCulture.Name;
        if (_translations.TryGetValue(cultureName, out var dict) && dict.TryGetValue(key, out var val))
        {
            return arguments.Length > 0 ? string.Format(_currentCulture, val, arguments) : val;
        }

        // Fallback to en-US
        if (_translations.TryGetValue("en-US", out var fallbackDict) && fallbackDict.TryGetValue(key, out var fallbackVal))
        {
            return arguments.Length > 0 ? string.Format(_currentCulture, fallbackVal, arguments) : fallbackVal;
        }

        return key;
    }
}

