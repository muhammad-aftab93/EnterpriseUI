namespace EnterpriseUI.TemplateStudio;

public enum TemplateType
{
    DataGridCrudView,
    ExecutiveDashboard,
    SchedulerBookingApp,
    DocumentPortal
}

/// <summary>
/// Scaffolding and project template generation engine.
/// </summary>
public static class TemplateStudioEngine
{
    public static string GenerateTemplateCode(TemplateType type, string entityName)
    {
        return type switch
        {
            TemplateType.DataGridCrudView => $@"// Generated DataGrid CRUD View for {entityName}
public class {entityName}ViewModel : ObservableObject
{{
    public DataGrid<{entityName}> Grid {{ get; }} = new();
    public {entityName}ViewModel()
    {{
        Grid.EnableFiltering = true;
        Grid.EnableSorting = true;
        Grid.EnableGrouping = true;
        Grid.EnableVirtualization = true;
    }}
}}",
            _ => $"// Template for {type}"
        };
    }
}

