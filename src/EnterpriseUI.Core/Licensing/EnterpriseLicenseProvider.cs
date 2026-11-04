namespace EnterpriseUI.Core.Licensing;

/// <summary>
/// Open source licensing tier indicator. EnterpriseUI is 100% free and open-source under the Apache-2.0 license.
/// </summary>
public enum LicenseType
{
    OpenSource,
    Community,
    Enterprise,
    UnlimitedOEM
}

/// <summary>
/// Immutable licensing descriptor certifying zero-activation open source terms.
/// </summary>
public sealed record LicenseInfo(
    string Licensee,
    LicenseType Type,
    DateTime ValidUntil,
    bool IsActive,
    string LicenseNotice);

/// <summary>
/// Zero-activation, open-source license provider for EnterpriseUI.
/// Requires NO license keys, NO activation, NO telemetry, and NO phone-home validation.
/// Completely free for commercial, private, SaaS, and enterprise deployment.
/// </summary>
public static class EnterpriseLicenseProvider
{
    private static readonly LicenseInfo _openSourceLicense = new(
        Licensee: "Open Source Community & Enterprise Users",
        Type: LicenseType.OpenSource,
        ValidUntil: DateTime.MaxValue,
        IsActive: true,
        LicenseNotice: "Licensed under the Apache License, Version 2.0. Commercial, personal, and enterprise use permitted without fees or keys.");

    /// <summary>
    /// Current license information indicating permanent open-source validity.
    /// </summary>
    public static LicenseInfo CurrentLicense => _openSourceLicense;

    /// <summary>
    /// Backward-compatibility stub. No license key is required for EnterpriseUI.
    /// </summary>
    /// <param name="licenseKey">Ignored. Provided for seamless migration from commercial suites.</param>
    public static void RegisterLicense(string? licenseKey = null)
    {
        // No-op. EnterpriseUI is 100% Open Source under Apache 2.0.
        // No activation or validation is required.
    }
}
