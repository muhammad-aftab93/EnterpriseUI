using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EnterpriseUI.Grids;

public partial class DataGrid<TItem>
{
    private GridPagingMode _pagingMode = GridPagingMode.None;
    private int _pageSize = 50;
    private int _pageIndex;
    private int _infiniteLoaded = 50;
    private IReadOnlyList<TItem>? _clientCache;

    /// <summary>Raised after the page index, page size or paging mode changed.</summary>
    public event EventHandler? PageChanged;

    public IReadOnlyList<int> PageSizeOptions { get; set; } = new[] { 10, 25, 50, 100 };

    public GridPagingMode PagingMode
    {
        get => _pagingMode;
        set
        {
            if (_pagingMode == value) return;
            _pagingMode = value;
            _pageIndex = 0;
            _infiniteLoaded = PageSize;
            _clientCache = null;
            OnPagingChanged();
            ResetAndRefresh();
        }
    }

    public int PageSize
    {
        get => _pageSize;
        set => SetPageSize(value);
    }

    /// <summary>True for the modes that show one page at a time.</summary>
    public bool IsPaged => _pagingMode is GridPagingMode.Client or GridPagingMode.Server;

    public int PageIndex
    {
        get => _hasResult && IsPaged ? Math.Min(_pageIndex, PageCount - 1) : _pageIndex;
        set => GoToPage(value);
    }

    private int PagingTotal => IsMaterialized ? _rows.Length : _totalRecords;

    public int PageCount => IsPaged ? Math.Max(1, (int)Math.Ceiling(PagingTotal / (double)_pageSize)) : 1;

    /// <summary>Rows before the current page, in display rows.</summary>
    private int PageOffset => IsPaged ? _pageIndex * _pageSize : 0;

    private int PageRowCount(int total) => _pagingMode switch
    {
        GridPagingMode.Client or GridPagingMode.Server => Math.Clamp(total - _pageIndex * _pageSize, 0, _pageSize),
        GridPagingMode.Infinite => Math.Min(total, _infiniteLoaded),
        _ => total
    };

    public bool HasPreviousPage => IsPaged && PageIndex > 0;
    public bool HasNextPage => IsPaged && PageIndex < PageCount - 1;

    /// <summary>For infinite paging: whether more rows can be appended.</summary>
    public bool HasMoreRows => _pagingMode == GridPagingMode.Infinite && _infiniteLoaded < PagingTotal;

    /// <summary>1-based index of the first row of the page among all rows, 0 when empty.</summary>
    public int PageFirstRecord => TotalItemsCount == 0 ? 0 : PageOffset + 1;
    public int PageLastRecord => PageOffset + TotalItemsCount;

    public string PagerText => _pagingMode switch
    {
        GridPagingMode.Client or GridPagingMode.Server =>
            PagingTotal == 0 ? "No records" : $"Page {PageIndex + 1} of {PageCount} ({PageFirstRecord:N0}-{PageLastRecord:N0} of {PagingTotal:N0})",
        GridPagingMode.Infinite => $"{TotalItemsCount:N0} of {PagingTotal:N0} loaded",
        _ => $"{_totalRecords:N0} records"
    };

    public bool FirstPage() => GoToPage(0);
    public bool PreviousPage() => GoToPage(PageIndex - 1);
    public bool NextPage() => GoToPage(PageIndex + 1);
    public bool LastPage() => GoToPage(PageCount - 1);

    /// <summary>Navigates to a 0-based page; returns false when paging is off, the page is out of range or already current.</summary>
    public bool GoToPage(int pageIndex)
    {
        if (!IsPaged)
        {
            return false;
        }

        var target = _hasResult ? Math.Clamp(pageIndex, 0, PageCount - 1) : Math.Max(0, pageIndex);
        if (target == _pageIndex)
        {
            return false;
        }

        _pageIndex = target;
        OnPagingChanged();
        RestartViewForPageChange();
        return true;
    }

    public void SetPageSize(int size)
    {
        size = Math.Clamp(size, 1, 100_000);
        if (size == _pageSize) return;

        // Keep the first visible record on the page.
        var firstRecord = PageOffset;
        _pageSize = size;
        _pageIndex = IsPaged ? firstRecord / size : 0;
        _infiniteLoaded = _pagingMode == GridPagingMode.Infinite ? size : Math.Max(_infiniteLoaded, size);
        OnPagingChanged();
        if (_pagingMode != GridPagingMode.None)
        {
            RestartViewForPageChange();
        }
    }

    /// <summary>Appends the next page for <see cref="GridPagingMode.Infinite"/>; returns false when everything is loaded.</summary>
    public async Task<bool> LoadMoreAsync(CancellationToken cancellationToken = default)
    {
        if (_pagingMode != GridPagingMode.Infinite || !HasMoreRows)
        {
            return false;
        }

        _infiniteLoaded += _pageSize;
        OnPagingChanged();
        await LoadAsync(LoadKind.Window, cancellationToken).ConfigureAwait(true);
        return true;
    }

    private void RestartViewForPageChange()
    {
        _wantedFirstRow = 0;
        _verticalOffset = 0;
        _window = Array.Empty<TItem>();
        _windowStart = 0;
        Selection.Clear();
        _detailExpansions.Clear();
        PageChanged?.Invoke(this, EventArgs.Empty);
        QueryChanged?.Invoke(this, EventArgs.Empty);
        Announce($"Page {_pageIndex + 1} of {PageCount}");
        _ = RefreshSafeAsync(LoadKind.Window);
    }

    private void OnPagingChanged()
    {
        OnPropertyChanged(nameof(PageIndex));
        OnPropertyChanged(nameof(PageSize));
        OnPropertyChanged(nameof(PageCount));
        OnPropertyChanged(nameof(PagerText));
        PageChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void Announce(string message, bool assertive = false) =>
        Announcement?.Invoke(this, new GridAnnouncementEventArgs(message, assertive));
}

public static class GridPagingHelper
{
    /// <summary>Page numbers to show in a pager: the window around the current page plus first and last, with -1 as an ellipsis.</summary>
    public static IReadOnlyList<int> GetPageButtons(int pageIndex, int pageCount, int maxButtons = 7)
    {
        if (pageCount <= maxButtons)
        {
            return Enumerable.Range(0, pageCount).ToList();
        }

        var buttons = new List<int> { 0 };
        var inner = maxButtons - 4;
        var start = Math.Clamp(pageIndex - inner / 2, 1, pageCount - 1 - inner);
        if (start > 1) buttons.Add(-1);
        for (var i = start; i < start + inner; i++) buttons.Add(i);
        if (start + inner < pageCount - 1) buttons.Add(-1);
        buttons.Add(pageCount - 1);
        return buttons;
    }
}
