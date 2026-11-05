using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EnterpriseUI.Grids;

/// <summary>JSON persistence of column layout profiles (column state, sorts, groups, filters, page size).</summary>
public static class GridLayoutSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true
    };

    public static string ToJson(ColumnLayoutProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return JsonSerializer.Serialize(profile, Options);
    }

    public static ColumnLayoutProfile FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return JsonSerializer.Deserialize<ColumnLayoutProfile>(json, Options) ?? throw new JsonException("The layout is empty.");
    }
}

/// <summary>Named layouts ("column profiles") that users can save, switch between and persist.</summary>
public sealed class GridLayoutStore
{
    private readonly Dictionary<string, ColumnLayoutProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> Names => _profiles.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();

    public void Save<TItem>(DataGrid<TItem> grid, string name)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _profiles[name] = grid.CreateLayoutProfile(name);
    }

    public IReadOnlyList<string> Apply<TItem>(DataGrid<TItem> grid, string name)
    {
        ArgumentNullException.ThrowIfNull(grid);
        return _profiles.TryGetValue(name, out var profile)
            ? grid.ApplyLayoutProfile(profile)
            : throw new KeyNotFoundException($"No layout named '{name}'.");
    }

    public bool Remove(string name) => _profiles.Remove(name);

    public string ExportJson() => JsonSerializer.Serialize(_profiles.Values.ToList(), new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } });

    public void ImportJson(string json)
    {
        var profiles = JsonSerializer.Deserialize<List<ColumnLayoutProfile>>(json, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() }, PropertyNameCaseInsensitive = true }) ?? new();
        foreach (var profile in profiles.Where(p => !string.IsNullOrWhiteSpace(p.ProfileName))) _profiles[profile.ProfileName] = profile;
    }

    /// <summary>Writes all layouts to a file, replacing it only when the write succeeded.</summary>
    public void SaveToFile(string path)
    {
        var temp = path + ".tmp";
        File.WriteAllText(temp, ExportJson());
        File.Move(temp, path, overwrite: true);
    }

    public void LoadFromFile(string path) => ImportJson(File.ReadAllText(path));
}
