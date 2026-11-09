using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace EnterpriseUI.Grids;

public enum PropertyEditorKind { Text, Number, Boolean, Enum, Color, DateTime, Nested, ReadOnly, Choice }

/// <summary>One row of a property inspector: a property of an object with its metadata and conversion rules.</summary>
public sealed class PropertyEntry
{
    private readonly PropertyDescriptor _descriptor;

    internal PropertyEntry(object owner, PropertyDescriptor descriptor, PropertyEntry? parent)
    {
        Owner = owner;
        _descriptor = descriptor;
        Parent = parent;
        Kind = ResolveKind(descriptor);
    }

    public object Owner { get; }
    public PropertyEntry? Parent { get; }
    public string Name => _descriptor.Name;
    public string DisplayName => string.IsNullOrEmpty(_descriptor.DisplayName) ? _descriptor.Name : _descriptor.DisplayName;
    public string Category => string.IsNullOrEmpty(_descriptor.Category) ? "Misc" : _descriptor.Category;
    public string Description => _descriptor.Description;
    public Type PropertyType => _descriptor.PropertyType;
    public PropertyEditorKind Kind { get; }
    public bool IsReadOnly => _descriptor.IsReadOnly || Kind == PropertyEditorKind.ReadOnly;
    public int Depth => Parent == null ? 0 : Parent.Depth + 1;

    /// <summary>True when the value differs from its [DefaultValue].</summary>
    public bool IsModified => _descriptor.CanResetValue(Owner) is { } reset && reset && _descriptor.ShouldSerializeValue(Owner);

    public object? Value => _descriptor.GetValue(Owner);

    public string DisplayText
    {
        get
        {
            var value = Value;
            if (value == null) return string.Empty;
            var converter = _descriptor.Converter;
            try
            {
                return converter.ConvertToString(null, CultureInfo.CurrentCulture, value) ?? string.Empty;
            }
            catch (Exception ex) when (ex is NotSupportedException or ArgumentException or FormatException)
            {
                return Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty;
            }
        }
    }

    /// <summary>Allowed values for enum, boolean and list-converter properties.</summary>
    public IReadOnlyList<string> Choices
    {
        get
        {
            if (Kind == PropertyEditorKind.Boolean) return new[] { "True", "False" };
            if (PropertyType.IsEnum) return Enum.GetNames(PropertyType);
            var values = _descriptor.Converter.GetStandardValues(null);
            return values == null ? Array.Empty<string>() : values.Cast<object?>().Select(v => _descriptor.Converter.ConvertToString(null, CultureInfo.CurrentCulture, v!) ?? string.Empty).ToList();
        }
    }

    public IReadOnlyList<PropertyEntry> Children
    {
        get
        {
            if (Kind != PropertyEditorKind.Nested || Value is not { } child) return Array.Empty<PropertyEntry>();
            return PropertyGridModel.Entries(child, this);
        }
    }

    public bool TrySetText(string? text, out string? error)
    {
        error = null;
        if (IsReadOnly) { error = "This property is read-only."; return false; }

        try
        {
            object? value;
            var type = Nullable.GetUnderlyingType(PropertyType) ?? PropertyType;
            if (string.IsNullOrEmpty(text) && (Nullable.GetUnderlyingType(PropertyType) != null || !PropertyType.IsValueType)) value = null;
            else if (type == typeof(bool)) value = bool.Parse(text ?? string.Empty);
            else value = _descriptor.Converter.ConvertFromString(null, CultureInfo.CurrentCulture, text ?? string.Empty);
            return TrySetValue(value, out error);
        }
        catch (Exception ex) when (ex is NotSupportedException or FormatException or ArgumentException or OverflowException or InvalidCastException)
        {
            error = ex.InnerException?.Message ?? ex.Message;
            return false;
        }
    }

    public bool TrySetValue(object? value, out string? error)
    {
        error = null;
        if (IsReadOnly) { error = "This property is read-only."; return false; }
        try
        {
            _descriptor.SetValue(Owner, value);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidCastException or NotSupportedException or FormatException or OverflowException or InvalidOperationException or System.Reflection.TargetInvocationException)
        {
            error = ex.InnerException?.Message ?? ex.Message;
            return false;
        }
    }

    public void Reset()
    {
        if (_descriptor.CanResetValue(Owner)) _descriptor.ResetValue(Owner);
    }

    private static PropertyEditorKind ResolveKind(PropertyDescriptor descriptor)
    {
        var type = Nullable.GetUnderlyingType(descriptor.PropertyType) ?? descriptor.PropertyType;
        if (type == typeof(bool)) return PropertyEditorKind.Boolean;
        if (type.IsEnum) return PropertyEditorKind.Enum;
        if (type.FullName == "System.Drawing.Color") return PropertyEditorKind.Color;
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset)) return PropertyEditorKind.DateTime;
        if (type == typeof(string)) return descriptor.Converter.GetStandardValuesSupported() ? PropertyEditorKind.Choice : PropertyEditorKind.Text;
        if (type.IsPrimitive || type == typeof(decimal)) return PropertyEditorKind.Number;
        var converter = descriptor.Converter;
        if (converter.GetStandardValuesSupported()) return PropertyEditorKind.Choice;
        if (typeof(IEnumerable).IsAssignableFrom(type)) return PropertyEditorKind.ReadOnly;
        if (converter.GetPropertiesSupported()) return PropertyEditorKind.Nested;
        return converter.CanConvertFrom(typeof(string)) ? PropertyEditorKind.Text : PropertyEditorKind.ReadOnly;
    }
}

public sealed record PropertyCategory(string Name, IReadOnlyList<PropertyEntry> Entries);

/// <summary>Builds the property list of an object (browsable properties, categories, filtering) for an inspector.</summary>
public static class PropertyGridModel
{
    public static IReadOnlyList<PropertyEntry> Entries(object instance, PropertyEntry? parent = null)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return TypeDescriptor.GetProperties(instance, new Attribute[] { BrowsableAttribute.Yes })
            .Cast<PropertyDescriptor>()
            .Select(d => new PropertyEntry(instance, d, parent))
            .ToList();
    }

    /// <summary>Categorized (categories and properties sorted by name) or flat alphabetical listing, optionally filtered by text.</summary>
    public static IReadOnlyList<PropertyCategory> Build(object? instance, bool categorized = true, string? filter = null, bool includeReadOnly = true)
    {
        if (instance == null) return Array.Empty<PropertyCategory>();

        var entries = Entries(instance)
            .Where(e => includeReadOnly || !e.IsReadOnly)
            .Where(e => string.IsNullOrWhiteSpace(filter) || e.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase) || e.Description.Contains(filter, StringComparison.OrdinalIgnoreCase) || e.DisplayText.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!categorized) return new[] { new PropertyCategory(string.Empty, entries.OrderBy(e => e.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList()) };

        return entries.GroupBy(e => e.Category)
            .OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new PropertyCategory(g.Key, g.OrderBy(e => e.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList()))
            .ToList();
    }
}
