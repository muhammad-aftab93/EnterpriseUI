using System;
using System.Collections.Generic;
using System.Linq;

namespace EnterpriseUI.WinFormsDemo.Infrastructure;

public sealed class DemoNavigationManager
{
    private static readonly Lazy<DemoNavigationManager> _instance = new(() => new DemoNavigationManager());
    public static DemoNavigationManager Instance => _instance.Value;

    private readonly HashSet<string> _favoriteIds = new();
    private readonly List<string> _recentlyViewedIds = new();

    public event Action? BookmarksChanged;
    public event Action<DemoItem>? DemoSelected;

    public IReadOnlyCollection<string> FavoriteIds => _favoriteIds;
    public IReadOnlyList<string> RecentlyViewedIds => _recentlyViewedIds;

    public void ToggleFavorite(string demoId)
    {
        if (!_favoriteIds.Remove(demoId))
            _favoriteIds.Add(demoId);

        BookmarksChanged?.Invoke();
    }

    public bool IsFavorite(string demoId) => _favoriteIds.Contains(demoId);

    public void RecordVisit(string demoId)
    {
        _recentlyViewedIds.Remove(demoId);
        _recentlyViewedIds.Insert(0, demoId);
        if (_recentlyViewedIds.Count > 15)
        {
            _recentlyViewedIds.RemoveAt(_recentlyViewedIds.Count - 1);
        }
        BookmarksChanged?.Invoke();
    }

    public void NavigateTo(DemoItem item)
    {
        RecordVisit(item.Id);
        DemoSelected?.Invoke(item);
    }
}

public sealed class DemoSearchEngine
{
    public static IReadOnlyList<DemoItem> Search(IEnumerable<DemoItem> source, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return source.ToList();

        var q = query.Trim();
        return source.Where(d =>
            d.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            d.Description.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            d.TargetControlName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            d.Category.ToString().Contains(q, StringComparison.OrdinalIgnoreCase) ||
            d.SubCategory.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            d.Industry.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            d.ApiReference.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            d.Documentation.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            d.BestPractices.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            d.SourceCode.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            d.Features.Any(feature => feature.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
            d.Tags.Any(tag => tag.Contains(q, StringComparison.OrdinalIgnoreCase))
        ).ToList();
    }
}

