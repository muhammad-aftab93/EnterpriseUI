using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace EnterpriseUI.Grids;

public enum GridValidationSeverity
{
    Error,
    Warning
}

public sealed record GridValidationError(object Item, string Field, string Message, GridValidationSeverity Severity = GridValidationSeverity.Error);

/// <summary>What a rule sees when a cell edit is being validated.</summary>
public sealed class GridValidationContext
{
    public GridValidationContext(object item, GridColumn? column, string field, object? oldValue, object? newValue)
    {
        Item = item;
        Column = column;
        Field = field;
        OldValue = oldValue;
        NewValue = newValue;
    }

    public object Item { get; }
    public GridColumn? Column { get; }
    public string Field { get; }
    public object? OldValue { get; }

    /// <summary>The converted value that would be written to the property.</summary>
    public object? NewValue { get; }

    /// <summary>Reads another property of the edited row, e.g. to validate a field against its neighbours.</summary>
    public object? GetSibling(string field) => Data.PropertyPath.GetValue(Item, field);
}

/// <summary>Validates one cell. Return null when valid, otherwise the message to show.</summary>
public abstract class GridValidationRule
{
    public string? Message { get; set; }
    public GridValidationSeverity Severity { get; set; } = GridValidationSeverity.Error;

    /// <summary>Asynchronous rules (e.g. server lookups) only run through the async edit APIs.</summary>
    public virtual bool IsAsync => false;

    public abstract string? Validate(GridValidationContext context);

    public virtual Task<string?> ValidateAsync(GridValidationContext context, CancellationToken cancellationToken) =>
        Task.FromResult(Validate(context));

    protected string Fail(string fallback) => string.IsNullOrEmpty(Message) ? fallback : Message!;
}

public sealed class RequiredRule : GridValidationRule
{
    public override string? Validate(GridValidationContext context) =>
        context.NewValue is null or "" || (context.NewValue is string s && string.IsNullOrWhiteSpace(s))
            ? Fail($"{context.Column?.Title ?? context.Field} is required.")
            : null;
}

public sealed class RangeRule : GridValidationRule
{
    public RangeRule(IComparable? minimum, IComparable? maximum)
    {
        Minimum = minimum;
        Maximum = maximum;
    }

    public IComparable? Minimum { get; }
    public IComparable? Maximum { get; }

    public override string? Validate(GridValidationContext context)
    {
        if (context.NewValue == null) return null;
        var value = context.NewValue;
        if (Minimum != null && Compare(value, Minimum) < 0 || Maximum != null && Compare(value, Maximum) > 0)
        {
            return Fail($"{context.Column?.Title ?? context.Field} must be between {Minimum} and {Maximum}.");
        }
        return null;
    }

    private static int Compare(object value, IComparable bound) =>
        EnterpriseUI.Data.ValueComparer.Instance.Compare(value, bound);
}

public sealed class LengthRule : GridValidationRule
{
    public LengthRule(int minimum, int maximum)
    {
        Minimum = minimum;
        Maximum = maximum;
    }

    public int Minimum { get; }
    public int Maximum { get; }

    public override string? Validate(GridValidationContext context)
    {
        var length = Convert.ToString(context.NewValue, CultureInfo.CurrentCulture)?.Length ?? 0;
        return length < Minimum || length > Maximum
            ? Fail($"{context.Column?.Title ?? context.Field} must be {Minimum}-{Maximum} characters.")
            : null;
    }
}

public sealed class RegexRule : GridValidationRule
{
    private readonly Regex _regex;

    /// <summary>The match timeout guards against catastrophic backtracking in user supplied patterns.</summary>
    public RegexRule(string pattern, TimeSpan? timeout = null)
    {
        _regex = new Regex(pattern, RegexOptions.CultureInvariant, timeout ?? TimeSpan.FromMilliseconds(250));
    }

    public override string? Validate(GridValidationContext context)
    {
        var text = Convert.ToString(context.NewValue, CultureInfo.CurrentCulture);
        if (string.IsNullOrEmpty(text)) return null;

        try
        {
            return _regex.IsMatch(text) ? null : Fail($"{context.Column?.Title ?? context.Field} has an invalid format.");
        }
        catch (RegexMatchTimeoutException)
        {
            return Fail($"{context.Column?.Title ?? context.Field} could not be validated.");
        }
    }
}

/// <summary>Business rule from a delegate returning an error message, or null when valid.</summary>
public sealed class DelegateRule : GridValidationRule
{
    private readonly Func<GridValidationContext, string?> _validate;

    public DelegateRule(Func<GridValidationContext, string?> validate) => _validate = validate ?? throw new ArgumentNullException(nameof(validate));

    public override string? Validate(GridValidationContext context) => _validate(context);
}

/// <summary>Asynchronous business rule such as uniqueness checks against a service.</summary>
public sealed class AsyncDelegateRule : GridValidationRule
{
    private readonly Func<GridValidationContext, CancellationToken, Task<string?>> _validate;

    public AsyncDelegateRule(Func<GridValidationContext, CancellationToken, Task<string?>> validate) => _validate = validate ?? throw new ArgumentNullException(nameof(validate));

    public override bool IsAsync => true;

    public override string? Validate(GridValidationContext context) => null;

    public override Task<string?> ValidateAsync(GridValidationContext context, CancellationToken cancellationToken) => _validate(context, cancellationToken);
}

public sealed class GridEditResult
{
    public GridEditResult(bool success, object? oldValue, object? newValue, IReadOnlyList<GridValidationError> errors)
    {
        Success = success;
        OldValue = oldValue;
        NewValue = newValue;
        Errors = errors;
    }

    /// <summary>True when the value was written (it may still carry warnings or, with <c>RejectInvalidEdits</c> off, errors).</summary>
    public bool Success { get; }
    public object? OldValue { get; }
    public object? NewValue { get; }
    public IReadOnlyList<GridValidationError> Errors { get; }
    public bool Changed => Success && !Equals(OldValue, NewValue);
}

public sealed class GridPendingChange
{
    public GridPendingChange(object item, string field, object? originalValue, object? currentValue)
    {
        Item = item;
        Field = field;
        OriginalValue = originalValue;
        CurrentValue = currentValue;
    }

    public object Item { get; }
    public string Field { get; }

    /// <summary>The value before the first uncommitted edit.</summary>
    public object? OriginalValue { get; }
    public object? CurrentValue { get; internal set; }
}

public sealed class GridCellEditedEventArgs : EventArgs
{
    public GridCellEditedEventArgs(object item, string field, object? oldValue, object? newValue)
    {
        Item = item;
        Field = field;
        OldValue = oldValue;
        NewValue = newValue;
    }

    public object Item { get; }
    public string Field { get; }
    public object? OldValue { get; }
    public object? NewValue { get; }
}

public sealed record GridCommitResult(bool Success, IReadOnlyList<GridPendingChange> Changes, IReadOnlyList<GridValidationError> Errors);

public sealed class GridChangesEventArgs : EventArgs
{
    public GridChangesEventArgs(IReadOnlyList<GridPendingChange> changes) => Changes = changes;
    public IReadOnlyList<GridPendingChange> Changes { get; }
}

public sealed class GridValidationEventArgs : EventArgs
{
    public GridValidationEventArgs(IReadOnlyList<GridValidationError> errors) => Errors = errors;
    public IReadOnlyList<GridValidationError> Errors { get; }
}

public sealed class GridDataLoadFailedEventArgs : EventArgs
{
    public GridDataLoadFailedEventArgs(Exception exception) => Exception = exception;
    public Exception Exception { get; }
}

public sealed class GridAnnouncementEventArgs : EventArgs
{
    public GridAnnouncementEventArgs(string message, bool assertive = false)
    {
        Message = message;
        Assertive = assertive;
    }

    public string Message { get; }
    public bool Assertive { get; }
}

/// <summary>Reference identity for reference types, value equality for value types; used where row objects are keys.</summary>
internal sealed class IdentityComparer<T> : IEqualityComparer<T>
{
    public static IdentityComparer<T> Instance { get; } = new();

    public bool Equals(T? x, T? y) => typeof(T).IsValueType ? EqualityComparer<T>.Default.Equals(x, y) : ReferenceEquals(x, y);

    public int GetHashCode(T obj) => typeof(T).IsValueType
        ? EqualityComparer<T>.Default.GetHashCode(obj!)
        : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
}

internal sealed class ObjectIdentityComparer : IEqualityComparer<object>
{
    public static ObjectIdentityComparer Instance { get; } = new();

    public new bool Equals(object? x, object? y) => x != null && y != null && (x.GetType().IsValueType ? x.Equals(y) : ReferenceEquals(x, y));

    public int GetHashCode(object obj) => obj.GetType().IsValueType ? obj.GetHashCode() : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
}
