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
        if (_favoriteIds.Contains(demoId))
            _favoriteIds.Remove(demoId);
        else
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

        var q = query.Trim().ToLowerInvariant();
        return source.Where(d =>
            d.Title.ToLowerInvariant().Contains(q) ||
            d.Description.ToLowerInvariant().Contains(q) ||
            d.TargetControlName.ToLowerInvariant().Contains(q) ||
            d.Category.ToString().ToLowerInvariant().Contains(q) ||
            d.SubCategory.ToLowerInvariant().Contains(q) ||
            d.Industry.ToLowerInvariant().Contains(q) ||
            d.ApiReference.ToLowerInvariant().Contains(q) ||
            d.Documentation.ToLowerInvariant().Contains(q) ||
            d.BestPractices.ToLowerInvariant().Contains(q) ||
            d.SourceCode.ToLowerInvariant().Contains(q) ||
            d.Features.Any(f => f.ToLowerInvariant().Contains(q)) ||
            d.Tags.Any(t => t.ToLowerInvariant().Contains(q))
        ).ToList();
    }
}

