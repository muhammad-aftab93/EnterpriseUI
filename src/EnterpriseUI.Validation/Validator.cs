using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace EnterpriseUI.Validation;

public sealed record ValidationError(string PropertyName, string ErrorMessage);

public sealed class ValidationResult
{
    public static readonly ValidationResult Success = new(true, Array.Empty<ValidationError>());

    public bool IsValid { get; }
    public IReadOnlyList<ValidationError> Errors { get; }

    public ValidationResult(bool isValid, IEnumerable<ValidationError> errors)
    {
        IsValid = isValid;
        Errors = errors.ToList();
    }
}

/// <summary>
/// Fluent enterprise validator inspired by FluentValidation with full async support.
/// </summary>
public class Validator<T>
{
    private readonly List<Func<T, CancellationToken, Task<ValidationError?>>> _rules = new();

    public Validator<T> RuleFor<TProp>(Expression<Func<T, TProp>> propertyExpression, Func<TProp, bool> predicate, string errorMessage)
    {
        var memberName = GetMemberName(propertyExpression);
        var compiled = propertyExpression.Compile();

        _rules.Add((instance, _) =>
        {
            var value = compiled(instance);
            if (!predicate(value))
            {
                return Task.FromResult<ValidationError?>(new ValidationError(memberName, errorMessage));
            }
            return Task.FromResult<ValidationError?>(null);
        });

        return this;
    }

    public Validator<T> RuleForAsync<TProp>(Expression<Func<T, TProp>> propertyExpression, Func<TProp, CancellationToken, Task<bool>> asyncPredicate, string errorMessage)
    {
        var memberName = GetMemberName(propertyExpression);
        var compiled = propertyExpression.Compile();

        _rules.Add(async (instance, ct) =>
        {
            var value = compiled(instance);
            var ok = await asyncPredicate(value, ct);
            if (!ok)
            {
                return new ValidationError(memberName, errorMessage);
            }
            return null;
        });

        return this;
    }

    public async Task<ValidationResult> ValidateAsync(T instance, CancellationToken cancellationToken = default)
    {
        if (instance == null) return new ValidationResult(false, new[] { new ValidationError("", "Instance is null.") });

        var errors = new List<ValidationError>();
        foreach (var rule in _rules)
        {
            var error = await rule(instance, cancellationToken);
            if (error != null)
            {
                errors.Add(error);
            }
        }

        return new ValidationResult(errors.Count == 0, errors);
    }

    private static string GetMemberName<TProp>(Expression<Func<T, TProp>> expression)
    {
        if (expression.Body is MemberExpression memberExpr)
        {
            return memberExpr.Member.Name;
        }
        return string.Empty;
    }
}

