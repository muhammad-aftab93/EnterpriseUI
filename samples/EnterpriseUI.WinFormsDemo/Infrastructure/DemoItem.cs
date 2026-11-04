using System;
using System.Collections.Generic;

namespace EnterpriseUI.WinFormsDemo.Infrastructure;

public enum DemoCategoryType
{
    Overview,
    GridsAndData,
    ChartsAndGauges,
    EditorsAndForms,
    NavigationAndLayout,
    ReportingAndDashboard,
    DocumentsAndMedia,
    DiagramsAndWorkflow,
    AIAndIntelligence,
    SecurityAndGovernance,
    ThemingAndStyling,
    BusinessScenarios,
    PerformanceLab,
    AccessibilityCenter,
    LearningCenter,
    QAValidation
}

public sealed class DemoItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DemoCategoryType Category { get; init; }
    public string SubCategory { get; init; } = "General";
    public string TargetControlName { get; init; } = string.Empty;
    public IReadOnlyList<string> Features { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public string Industry { get; init; } = "Cross-Industry";
    public string Technology { get; init; } = "WinForms / .NET 8";
    public bool IsNew { get; init; }
    public bool IsFeatured { get; init; }
    public bool IsPopular { get; init; }
    public Func<Control> ViewFactory { get; init; } = () => new Panel();
    public string SourceCode { get; init; } = string.Empty;
    public string Documentation { get; init; } = string.Empty;
    public string BasicUsage { get; init; } = string.Empty;
    public string AdvancedUsage { get; init; } = string.Empty;
    public string ArchitectureNotes { get; init; } = string.Empty;
    public string PerformanceNotes { get; init; } = string.Empty;
    public string AccessibilityNotes { get; init; } = string.Empty;
    public string LocalizationNotes { get; init; } = string.Empty;
    public string RtlNotes { get; init; } = string.Empty;
    public string CommonMistakes { get; init; } = string.Empty;
    public string RelatedControls { get; init; } = string.Empty;
    public string BestPractices { get; init; } = string.Empty;
    public string Troubleshooting { get; init; } = string.Empty;
    public string ApiReference { get; init; } = string.Empty;
}

