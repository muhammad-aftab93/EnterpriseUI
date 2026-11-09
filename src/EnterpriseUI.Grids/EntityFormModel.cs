using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace EnterpriseUI.Grids;

public enum EntityEditorKind { Text, MultiLineText, Password, Email, Number, Boolean, Date, Time, Choice }

/// <summary>One editable field of an entity, described by the property and its data annotations.</summary>
public sealed class EntityField
{
    internal EntityField(PropertyInfo property)
    {
        Property = property;
        var display = property.GetCustomAttribute<DisplayAttribute>();
        Label = display?.GetName() ?? SplitWords(property.Name);
        Description = display?.GetDescription();
        Group = display?.GetGroupName();
        Order = display?.GetOrder() ?? int.MaxValue;
        Prompt = display?.GetPrompt();
        IsRequired = property.GetCustomAttribute<RequiredAttribute>() != null;
        IsReadOnly = !property.CanWrite || property.GetCustomAttribute<ReadOnlyAttribute>()?.IsReadOnly == true || property.GetCustomAttribute<EditableAttribute>()?.AllowEdit == false;
        MaxLength = property.GetCustomAttribute<StringLengthAttribute>()?.MaximumLength ?? property.GetCustomAttribute<MaxLengthAttribute>()?.Length;
        Kind = ResolveKind(property);
    }

    public PropertyInfo Property { get; }
    public string Name => Property.Name;
    public Type Type => Nullable.GetUnderlyingType(Property.PropertyType) ?? Property.PropertyType;
    public bool AllowsNull => Nullable.GetUnderlyingType(Property.PropertyType) != null || !Property.PropertyType.IsValueType;
    public string Label { get; }
    public string? Description { get; }
    public string? Group { get; }
    public string? Prompt { get; }
    public int Order { get; }
    public bool IsRequired { get; }
    public bool IsReadOnly { get; }
    public int? MaxLength { get; }
    public EntityEditorKind Kind { get; }

    public IReadOnlyList<string> Choices => Type.IsEnum ? Enum.GetNames(Type) : Array.Empty<string>();

    private static EntityEditorKind ResolveKind(PropertyInfo property)
    {
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        var dataType = property.GetCustomAttribute<DataTypeAttribute>()?.DataType;
        if (type == typeof(bool)) return EntityEditorKind.Boolean;
        if (type.IsEnum) return EntityEditorKind.Choice;
        if (type == typeof(DateTime) || type == typeof(DateOnly)) return dataType == DataType.Time ? EntityEditorKind.Time : EntityEditorKind.Date;
        if (type == typeof(TimeOnly) || type == typeof(TimeSpan)) return EntityEditorKind.Time;
        if (type.IsPrimitive || type == typeof(decimal)) return EntityEditorKind.Number;
        if (dataType == DataType.Password) return EntityEditorKind.Password;
        if (dataType == DataType.EmailAddress || property.GetCustomAttribute<EmailAddressAttribute>() != null) return EntityEditorKind.Email;
        if (dataType == DataType.MultilineText) return EntityEditorKind.MultiLineText;
        return EntityEditorKind.Text;
    }

    private static string SplitWords(string name)
    {
        var chars = new List<char>();
        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1])) chars.Add(' ');
            chars.Add(name[i]);
        }
        return new string(chars.ToArray());
    }
}

/// <summary>
/// Auto-generated entity form model: fields from the public properties, conversion from text, validation with data annotations,
/// pending (uncommitted) values with apply and reset.
/// </summary>
public sealed class EntityFormModel
{
    private readonly Dictionary<string, object?> _pending = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _errors = new(StringComparer.Ordinal);

    public EntityFormModel(object entity)
    {
        Entity = entity ?? throw new ArgumentNullException(nameof(entity));
        Fields = entity.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0 && p.CanRead && p.GetCustomAttribute<BrowsableAttribute>()?.Browsable != false && p.GetCustomAttribute<ScaffoldColumnAttribute>()?.Scaffold != false && IsEditableType(p.PropertyType))
            .Select(p => new EntityField(p))
            .OrderBy(f => f.Order)
            .ToList();
    }

    public object Entity { get; }
    public IReadOnlyList<EntityField> Fields { get; }
    public bool HasChanges => _pending.Count > 0;
    public IReadOnlyDictionary<string, string> Errors => _errors;

    public EntityField? Field(string name) => Fields.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));

    public object? CurrentValue(string name)
    {
        var field = Field(name) ?? throw new ArgumentException($"Unknown field '{name}'.", nameof(name));
        return _pending.TryGetValue(field.Name, out var pending) ? pending : field.Property.GetValue(Entity);
    }

    public string CurrentText(string name)
    {
        var value = CurrentValue(name);
        return value == null ? string.Empty : Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty;
    }

    /// <summary>Converts text to the field type and validates it; the value is kept as pending even when invalid so the user can fix it.</summary>
    public bool SetText(string name, string? text, out string? error)
    {
        var field = Field(name) ?? throw new ArgumentException($"Unknown field '{name}'.", nameof(name));
        if (!TryConvert(field, text, out var value, out error))
        {
            _errors[field.Name] = error!;
            return false;
        }

        return SetValue(name, value, out error);
    }

    public bool SetValue(string name, object? value, out string? error)
    {
        var field = Field(name) ?? throw new ArgumentException($"Unknown field '{name}'.", nameof(name));
        var original = field.Property.GetValue(Entity);
        if (Equals(original, value)) _pending.Remove(field.Name); else _pending[field.Name] = value;

        error = ValidateField(field, value);
        if (error == null) _errors.Remove(field.Name); else _errors[field.Name] = error;
        return error == null;
    }

    public IReadOnlyDictionary<string, string> Validate()
    {
        foreach (var field in Fields.Where(f => !f.IsReadOnly))
        {
            var error = ValidateField(field, CurrentValue(field.Name));
            if (error == null) _errors.Remove(field.Name); else _errors[field.Name] = error;
        }
        return _errors;
    }

    /// <summary>Writes pending values to the entity when every field is valid.</summary>
    public bool Apply()
    {
        Validate();
        if (_errors.Count > 0) return false;
        foreach (var (name, value) in _pending) Field(name)!.Property.SetValue(Entity, value);
        _pending.Clear();
        return true;
    }

    public void Reset()
    {
        _pending.Clear();
        _errors.Clear();
    }

    private string? ValidateField(EntityField field, object? value)
    {
        if (field.IsReadOnly) return null;
        if (value == null && field.IsRequired) return $"{field.Label} is required.";

        var results = new List<ValidationResult>();
        var context = new ValidationContext(Entity) { MemberName = field.Name, DisplayName = field.Label };
        return Validator.TryValidateProperty(value, context, results) ? null : results[0].ErrorMessage;
    }

    private static bool TryConvert(EntityField field, string? text, out object? value, out string? error)
    {
        value = null;
        error = null;
        var type = field.Type;
        if (string.IsNullOrWhiteSpace(text))
        {
            if (type == typeof(string)) { value = field.IsRequired || !field.AllowsNull ? string.Empty : null; return true; }
            if (field.AllowsNull) return true;
            error = $"{field.Label} is required.";
            return false;
        }

        try
        {
            if (type == typeof(string)) value = text;
            else if (type.IsEnum) value = Enum.Parse(type, text, true);
            else if (type == typeof(bool)) value = bool.Parse(text);
            else if (type == typeof(DateTime)) value = DateTime.Parse(text, CultureInfo.CurrentCulture);
            else if (type == typeof(DateOnly)) value = DateOnly.Parse(text, CultureInfo.CurrentCulture);
            else if (type == typeof(TimeOnly)) value = TimeOnly.Parse(text, CultureInfo.CurrentCulture);
            else if (type == typeof(TimeSpan)) value = TimeSpan.Parse(text, CultureInfo.CurrentCulture);
            else if (type == typeof(Guid)) value = Guid.Parse(text);
            else value = Convert.ChangeType(text, type, CultureInfo.CurrentCulture);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException or InvalidCastException)
        {
            error = field.Kind switch
            {
                EntityEditorKind.Number => $"{field.Label} must be a number.",
                EntityEditorKind.Date => $"{field.Label} must be a valid date.",
                EntityEditorKind.Time => $"{field.Label} must be a valid time.",
                EntityEditorKind.Choice => $"{field.Label} must be one of: {string.Join(", ", field.Choices)}.",
                _ => $"{field.Label} is not valid."
            };
            return false;
        }
    }

    private static bool IsEditableType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime) || type == typeof(DateOnly) ||
               type == typeof(TimeOnly) || type == typeof(TimeSpan) || type == typeof(Guid);
    }
}
