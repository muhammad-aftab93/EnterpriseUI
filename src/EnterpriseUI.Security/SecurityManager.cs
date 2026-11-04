using System.Security.Claims;
using EnterpriseUI.Abstractions.Controls;

namespace EnterpriseUI.Security;

public interface IAuthorizedControl : IUIComponent
{
    string? RequiredPermission { get; set; }
    string? RequiredRole { get; set; }
    bool IsAuthorized { get; }
}

/// <summary>
/// Enterprise UI Security and Authorization Manager for RBAC and field-level permission gating.
/// </summary>
public sealed class SecurityManager
{
    private static readonly Lazy<SecurityManager> _instance = new(() => new SecurityManager());
    public static SecurityManager Instance => _instance.Value;

    private ClaimsPrincipal? _currentPrincipal;
    private readonly HashSet<string> _grantedPermissions = new(StringComparer.OrdinalIgnoreCase);

    public ClaimsPrincipal? CurrentPrincipal => _currentPrincipal;

    public void SetUser(ClaimsPrincipal principal, IEnumerable<string>? permissions = null)
    {
        _currentPrincipal = principal;
        _grantedPermissions.Clear();
        if (permissions != null)
        {
            foreach (var perm in permissions)
            {
                _grantedPermissions.Add(perm);
            }
        }
    }

    public bool IsInRole(string? role)
    {
        if (string.IsNullOrEmpty(role)) return true;
        return _currentPrincipal?.IsInRole(role) ?? false;
    }

    public bool HasPermission(string? permission)
    {
        if (string.IsNullOrEmpty(permission)) return true;
        return _grantedPermissions.Contains(permission);
    }

    public bool Authorize(string? role, string? permission)
    {
        return IsInRole(role) && HasPermission(permission);
    }
}
