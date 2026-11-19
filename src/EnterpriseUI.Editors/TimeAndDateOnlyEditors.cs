using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using EnterpriseUI.Abstractions.Accessibility;
using EnterpriseUI.Abstractions.Controls;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Core.Mvvm;

namespace EnterpriseUI.Editors;

/// <summary>Shared interaction, accessibility and theming members of the date and time editor models.</summary>
public abstract class EnterpriseEditorModel : ObservableObject, IInteractiveControl, IThemeableControl, IAccessibleControl
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public bool IsFocused { get; private set; }
    public bool IsReadOnly { get; set; }
    public int TabIndex { get; set; }

    public AriaRole Role { get; set; } = AriaRole.ComboBox;
    public string? AriaLabel { get; set; }
    public string? AriaDescription { get; set; }
    public string? AriaLabelledBy { get; set; }
    public string? AriaDescribedBy { get; set; }
    public bool AriaHidden { get; set; }
    public bool AriaExpanded { get; set; }
    public bool AriaDisabled { get; set; }
    public bool AriaRequired { get; set; }
    public bool AriaInvalid { get; set; }
    public string? ThemeOverride { get; set; }

    public ValueTask<bool> FocusAsync() => ValueTask.FromResult(true);
    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void OnThemeChanged(ITheme newTheme) { }
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>Parsing and formatting of times of day shared by the time editors.</summary>
public static class TimeText
{
    /// <summary>Accepts "3:45 PM", "15:45:30", "3 pm" and compact "1545" forms.</summary>
    public static bool TryParse(string? text, IFormatProvider? provider, out TimeOnly time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var trimmed = text.Trim();
        if (TimeOnly.TryParse(trimmed, provider, DateTimeStyles.AllowWhiteSpaces, out time)) return true;
        if (trimmed.Length is 3 or 4 && int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var compact))
        {
            var hours = compact / 100;
            var minutes = compact % 100;
            if (hours < 24 && minutes < 60) { time = new TimeOnly(hours, minutes); return true; }
        }
        return false;
    }

    public static string Format(TimeOnly time, bool is24Hour, bool showSeconds, IFormatProvider? provider = null)
    {
        var pattern = (is24Hour, showSeconds) switch
        {
            (true, true) => "HH:mm:ss",
            (true, false) => "HH:mm",
            (false, true) => "h:mm:ss tt",
            _ => "h:mm tt"
        };
        return time.ToString(pattern, provider ?? CultureInfo.CurrentCulture);
    }
}

/// <summary>Edits the time-of-day part of a <see cref="DateTime"/>; the date part is preserved.</summary>
public class EnterpriseTimePicker : EnterpriseEditorModel
{
    private DateTime? _value;
    private int _minuteStep = 1;

    public EnterpriseTimePicker() => AriaLabel = "Time Picker";

    public DateTime? Value
    {
        get => _value;
        set => SetProperty(ref _value, value, nameof(Value));
    }

    public bool Is24Hour { get; set; }
    public bool ShowSeconds { get; set; }
    public TimeSpan Minimum { get; set; } = TimeSpan.Zero;
    public TimeSpan Maximum { get; set; } = new(23, 59, 59);

    /// <summary>Granularity of the minute column and of <see cref="Increment"/>.</summary>
    public int MinuteStep
    {
        get => _minuteStep;
        set => _minuteStep = value is >= 1 and <= 30 && 60 % value == 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), "The step must divide 60 and be between 1 and 30.");
    }

    public TimeSpan? TimeOfDay => _value?.TimeOfDay;

    public string DisplayText(IFormatProvider? provider = null) =>
        _value is { } v ? TimeText.Format(TimeOnly.FromDateTime(v), Is24Hour, ShowSeconds, provider) : string.Empty;

    /// <summary>Sets the time, keeping the current date (or today when empty); returns false when the text is invalid or out of range.</summary>
    public bool TrySetText(string? text, IFormatProvider? provider = null)
    {
        if (string.IsNullOrWhiteSpace(text)) { Value = null; return true; }
        if (!TimeText.TryParse(text, provider, out var time)) return false;
        return TrySetTime(time.ToTimeSpan());
    }

    public bool TrySetTime(TimeSpan time)
    {
        if (time < Minimum || time > Maximum || time < TimeSpan.Zero || time >= TimeSpan.FromDays(1)) return false;
        Value = (_value?.Date ?? DateTime.Today) + time;
        return true;
    }

    /// <summary>Moves by whole steps, clamped to the allowed range; an empty value starts from the minimum.</summary>
    public void Increment(int steps)
    {
        var current = TimeOfDay ?? Minimum;
        var next = current + TimeSpan.FromMinutes((long)steps * _minuteStep);
        var clamped = TimeSpan.FromTicks(Math.Clamp(next.Ticks, Minimum.Ticks, Maximum.Ticks));
        TrySetTime(clamped);
    }
}

/// <summary>Edits a <see cref="TimeOnly"/> with a selectable precision.</summary>
public class EnterpriseTimeOnlyPicker : EnterpriseEditorModel
{
    private TimeOnly? _value;

    public EnterpriseTimeOnlyPicker() => AriaLabel = "Time Picker";

    public TimeOnly? Value
    {
        get => _value;
        set => SetProperty(ref _value, value is { } v ? Truncate(v) : null, nameof(Value));
    }

    public TimeOnlyPrecision Precision { get; set; } = TimeOnlyPrecision.Minutes;

    /// <summary>Granularity of the minute column in drop-down editors.</summary>
    public int MinuteStep { get; set; } = 1;
    public bool Is24Hour { get; set; }
    public TimeOnly Minimum { get; set; } = TimeOnly.MinValue;
    public TimeOnly Maximum { get; set; } = TimeOnly.MaxValue;

    public string DisplayText(IFormatProvider? provider = null) =>
        _value is { } v ? TimeText.Format(v, Is24Hour, Precision >= TimeOnlyPrecision.Seconds, provider) : string.Empty;

    public bool TrySetText(string? text, IFormatProvider? provider = null)
    {
        if (string.IsNullOrWhiteSpace(text)) { Value = null; return true; }
        return TimeText.TryParse(text, provider, out var time) && TrySetValue(time);
    }

    public bool TrySetValue(TimeOnly time)
    {
        var truncated = Truncate(time);
        if (truncated < Minimum || truncated > Maximum) return false;
        Value = truncated;
        return true;
    }

    /// <summary>Adds one unit of the current precision per step, wrapping around midnight only when the range is the full day.</summary>
    public void Increment(int steps)
    {
        var unit = Precision switch { TimeOnlyPrecision.Minutes => TimeSpan.FromMinutes(1), TimeOnlyPrecision.Seconds => TimeSpan.FromSeconds(1), _ => TimeSpan.FromMilliseconds(1) };
        var current = _value ?? Minimum;
        var ticks = current.Ticks + unit.Ticks * steps;
        var fullDay = Minimum == TimeOnly.MinValue && Maximum == TimeOnly.MaxValue;
        if (fullDay) ticks = ((ticks % TimeSpan.TicksPerDay) + TimeSpan.TicksPerDay) % TimeSpan.TicksPerDay;
        else ticks = Math.Clamp(ticks, Minimum.Ticks, Maximum.Ticks);
        TrySetValue(new TimeOnly(ticks));
    }

    private TimeOnly Truncate(TimeOnly time)
    {
        var unit = Precision switch { TimeOnlyPrecision.Minutes => TimeSpan.TicksPerMinute, TimeOnlyPrecision.Seconds => TimeSpan.TicksPerSecond, _ => TimeSpan.TicksPerMillisecond };
        return new TimeOnly(time.Ticks - time.Ticks % unit);
    }
}

public enum TimeOnlyPrecision { Minutes, Seconds, Milliseconds }

/// <summary>Edits a <see cref="DateOnly"/> without any time or time zone component.</summary>
public class EnterpriseDateOnlyPicker : EnterpriseEditorModel
{
    private DateOnly? _value;

    public EnterpriseDateOnlyPicker() => AriaLabel = "Date Picker";

    public DateOnly? Value
    {
        get => _value;
        set => SetProperty(ref _value, value, nameof(Value));
    }

    public DateOnly MinDate { get; set; } = DateOnly.MinValue;
    public DateOnly MaxDate { get; set; } = DateOnly.MaxValue;
    public DayOfWeek FirstDayOfWeek { get; set; } = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
    public bool ShowWeekNumbers { get; set; }
    public string Format { get; set; } = "d";

    public string DisplayText(IFormatProvider? provider = null) =>
        _value is { } v ? v.ToString(Format, provider ?? CultureInfo.CurrentCulture) : string.Empty;

    public bool TrySetText(string? text, IFormatProvider? provider = null)
    {
        if (string.IsNullOrWhiteSpace(text)) { Value = null; return true; }
        return DateOnly.TryParse(text.Trim(), provider ?? CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out var date) && TrySetValue(date);
    }

    public bool TrySetValue(DateOnly date)
    {
        if (date < MinDate || date > MaxDate) return false;
        Value = date;
        return true;
    }

    /// <summary>Moves by days and clamps to the allowed range; an empty value starts from today.</summary>
    public void AddDays(int days)
    {
        var current = _value ?? DateOnly.FromDateTime(DateTime.Today);
        var target = days >= 0
            ? (current.DayNumber + (long)days > MaxDate.DayNumber ? MaxDate : current.AddDays(days))
            : (current.DayNumber + (long)days < MinDate.DayNumber ? MinDate : current.AddDays(days));
        TrySetValue(target < MinDate ? MinDate : target > MaxDate ? MaxDate : target);
    }

    public void AddMonths(int months)
    {
        var current = _value ?? DateOnly.FromDateTime(DateTime.Today);
        DateOnly target;
        try { target = current.AddMonths(months); }
        catch (ArgumentOutOfRangeException) { target = months < 0 ? MinDate : MaxDate; }
        TrySetValue(target < MinDate ? MinDate : target > MaxDate ? MaxDate : target);
    }
}
