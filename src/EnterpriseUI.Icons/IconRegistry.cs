namespace EnterpriseUI.Icons;

public static class FluentIconPack
{
    public const string PackName = "Fluent";

    public static readonly SvgIcon Add = new("add", PackName, "M12 4v16m-8-8h16");
    public static readonly SvgIcon Edit = new("edit", PackName, "M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34a.9959.9959 0 00-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z");
    public static readonly SvgIcon Delete = new("delete", PackName, "M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z");
    public static readonly SvgIcon Save = new("save", PackName, "M17 3H5a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2V7l-4-4zm-5 16c-1.66 0-3-1.34-3-3s1.34-3 3-3 3 1.34 3 3-1.34 3-3 3zm3-10H5V5h10v4z");
    public static readonly SvgIcon Search = new("search", PackName, "M15.5 14h-.79l-.28-.27A6.471 6.471 0 0016 9.5 6.5 6.5 0 109.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z");
    public static readonly SvgIcon Filter = new("filter", PackName, "M10 18h4v-2h-4v2zM3 6v2h18V6H3zm3 7h12v-2H6v2z");
    public static readonly SvgIcon Refresh = new("refresh", PackName, "M17.65 6.35A7.958 7.958 0 0012 4c-4.42 0-7.99 3.58-7.99 8s3.57 8 7.99 8c3.73 0 6.84-2.55 7.73-6h-2.08A5.99 5.99 0 0112 18c-3.31 0-6-2.69-6-6s2.69-6 6-6c1.66 0 3.14.69 4.22 1.78L13 11h7V4l-2.35 2.35z");
    public static readonly SvgIcon Grid = new("grid", PackName, "M4 11h5V5H4v6zm0 7h5v-6H4v6zm6 0h5v-6h-5v6zm6 0h5v-6h-5v6zm-6-7h5V5h-5v6zm6-6v6h5V5h-5z");
    public static readonly SvgIcon EnterpriseChart = new("chart", PackName, "M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zM9 17H7v-7h2v7zm4 0h-2V7h2v10zm4 0h-2v-4h2v4z");
    public static readonly SvgIcon Calendar = new("calendar", PackName, "M19 4h-1V2h-2v2H8V2H6v2H5c-1.11 0-1.99.9-1.99 2L3 20a2 2 0 002 2h14c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zm0 16H5V10h14v10zM9 14H7v-2h2v2zm4 0h-2v-2h2v2zm4 0h-2v-2h2v2z");
}

public static class MaterialIconPack
{
    public const string PackName = "Material";

    public static readonly SvgIcon Check = new("check", PackName, "M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z");
    public static readonly SvgIcon Close = new("close", PackName, "M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z");
    public static readonly SvgIcon Menu = new("menu", PackName, "M3 18h18v-2H3v2zm0-5h18v-2H3v2zm0-7v2h18V6H3z");
    public static readonly SvgIcon Settings = new("settings", PackName, "M19.14 12.94c.04-.3.06-.61.06-.94 0-.32-.02-.64-.07-.94l2.03-1.58a.49.49 0 00.12-.61l-1.92-3.32a.488.488 0 00-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94l-.36-2.54a.484.484 0 00-.48-.41h-3.84c-.24 0-.43.17-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96c-.22-.08-.47 0-.59.22L2.74 8.87c-.12.21-.08.47.12.61l2.03 1.58c-.05.3-.09.63-.09.94s.02.64.07.94l-2.03 1.58a.49.49 0 00-.12.61l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.38 1.03.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.56 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32c.12-.22.07-.47-.12-.61l-2.01-1.58zM12 15.6c-1.98 0-3.6-1.62-3.6-3.6s1.62-3.6 3.6-3.6 3.6 1.62 3.6 3.6-1.62 3.6-3.6 3.6z");
    public static readonly SvgIcon Download = new("download", PackName, "M19.35 10.04C18.67 6.59 15.64 4 12 4 9.11 4 6.6 5.64 5.35 8.04 2.34 8.36 0 10.91 0 14c0 3.31 2.69 6 6 6h13c2.76 0 5-2.24 5-5 0-2.64-2.05-4.78-4.65-4.96zM17 13l-5 5-5-5h3V9h4v4h3z");
    public static readonly SvgIcon Upload = new("upload", PackName, "M9 16h6v-6h4l-7-7-7 7h4zm-4 2h14v2H5z");
    public static readonly SvgIcon Print = new("print", PackName, "M19 8H5c-1.66 0-3 1.34-3 3v6h4v4h12v-4h4v-6c0-1.66-1.34-3-3-3zm-3 11H8v-5h8v5zm3-7c-.55 0-1-.45-1-1s.45-1 1-1 1 .45 1 1-.45 1-1 1zm-1-9H6v4h12V3z");
    public static readonly SvgIcon MoreVert = new("more-vert", PackName, "M12 8c1.1 0 2-.9 2-2s-.9-2-2-2-2 .9-2 2 .9 2 2 2zm0 2c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm0 6c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2z");
}

/// <summary>
/// Central registry for custom and built-in icon packs with dynamic lookup.
/// </summary>
public static class IconRegistry
{
    private static readonly Dictionary<string, IIcon> _icons = new(StringComparer.OrdinalIgnoreCase);

    static IconRegistry()
    {
        // Register Fluent Pack
        Register(FluentIconPack.Add);
        Register(FluentIconPack.Edit);
        Register(FluentIconPack.Delete);
        Register(FluentIconPack.Save);
        Register(FluentIconPack.Search);
        Register(FluentIconPack.Filter);
        Register(FluentIconPack.Refresh);
        Register(FluentIconPack.Grid);
        Register(FluentIconPack.EnterpriseChart);
        Register(FluentIconPack.Calendar);

        // Register Material Pack
        Register(MaterialIconPack.Check);
        Register(MaterialIconPack.Close);
        Register(MaterialIconPack.Menu);
        Register(MaterialIconPack.Settings);
        Register(MaterialIconPack.Download);
        Register(MaterialIconPack.Upload);
        Register(MaterialIconPack.Print);
        Register(MaterialIconPack.MoreVert);
    }

    public static void Register(IIcon icon)
    {
        ArgumentNullException.ThrowIfNull(icon);
        var key = $"{icon.Category.ToLowerInvariant()}:{icon.Name.ToLowerInvariant()}";
        _icons[key] = icon;
        _icons[icon.Name.ToLowerInvariant()] = icon; // Shorthand fallback
    }

    public static bool TryGetIcon(string key, out IIcon? icon)
    {
        return _icons.TryGetValue(key.ToLowerInvariant(), out icon);
    }

    public static IIcon GetIcon(string key)
    {
        if (TryGetIcon(key, out var icon) && icon != null)
        {
            return icon;
        }
        // Fallback default
        return FluentIconPack.Search;
    }
}

