using EnterpriseUI.Abstractions.Accessibility;

namespace EnterpriseUI.Accessibility;

public enum LiveRegionPoliteness
{
    Off,
    Polite,
    Assertive
}

public interface IAccessibilityAnnouncer
{
    void Announce(string message, LiveRegionPoliteness politeness = LiveRegionPoliteness.Polite);
}

/// <summary>
/// WCAG 2.2 AA and Section 508 Accessibility Engine with screen reader live region support.
/// </summary>
public sealed class AccessibilityManager : IAccessibilityAnnouncer
{
    private static readonly Lazy<AccessibilityManager> _instance = new(() => new AccessibilityManager());
    public static AccessibilityManager Instance => _instance.Value;

    public event Action<string, LiveRegionPoliteness>? AnnouncementRequested;

    public bool IsHighContrastModeActive { get; private set; }
    public bool ScreenReaderDetected { get; set; }

    public void SetHighContrastMode(bool active)
    {
        IsHighContrastModeActive = active;
    }

    public void Announce(string message, LiveRegionPoliteness politeness = LiveRegionPoliteness.Polite)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        AnnouncementRequested?.Invoke(message, politeness);
    }

    public static string GenerateAriaAttributes(IAccessibleControl control)
    {
        var attrs = new List<string>();
        if (control.Role != AriaRole.None)
        {
            attrs.Add($"role=\"{control.Role.ToString().ToLowerInvariant()}\"");
        }
        if (!string.IsNullOrEmpty(control.AriaLabel))
        {
            attrs.Add($"aria-label=\"{control.AriaLabel}\"");
        }
        if (!string.IsNullOrEmpty(control.AriaDescription))
        {
            attrs.Add($"aria-description=\"{control.AriaDescription}\"");
        }
        if (control.AriaExpanded)
        {
            attrs.Add("aria-expanded=\"true\"");
        }
        if (control.AriaDisabled)
        {
            attrs.Add("aria-disabled=\"true\"");
        }
        if (control.AriaRequired)
        {
            attrs.Add("aria-required=\"true\"");
        }
        if (control.AriaInvalid)
        {
            attrs.Add("aria-invalid=\"true\"");
        }
        if (control.AriaHidden)
        {
            attrs.Add("aria-hidden=\"true\"");
        }

        return string.Join(" ", attrs);
    }
}

