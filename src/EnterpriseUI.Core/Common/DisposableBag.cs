namespace EnterpriseUI.Core.Common;

/// <summary>
/// High-performance thread-safe disposable container for managing component lifecycles.
/// </summary>
public sealed class DisposableBag : IDisposable, IAsyncDisposable
{
    private readonly List<IDisposable> _disposables = new();
    private readonly List<IAsyncDisposable> _asyncDisposables = new();
    private readonly object _lock = new();
    private bool _isDisposed;

    public void Add(IDisposable disposable)
    {
        ArgumentNullException.ThrowIfNull(disposable);
        lock (_lock)
        {
            if (_isDisposed)
            {
                disposable.Dispose();
                return;
            }
            _disposables.Add(disposable);
        }
    }

    public void Add(IAsyncDisposable asyncDisposable)
    {
        ArgumentNullException.ThrowIfNull(asyncDisposable);
        lock (_lock)
        {
            if (_isDisposed)
            {
                Task.Run(async () => await asyncDisposable.DisposeAsync().ConfigureAwait(false));
                return;
            }
            _asyncDisposables.Add(asyncDisposable);
        }
    }

    public void Dispose()
    {
        List<IDisposable> toDispose;
        lock (_lock)
        {
            if (_isDisposed) return;
            _isDisposed = true;
            toDispose = new List<IDisposable>(_disposables);
            _disposables.Clear();
        }

        foreach (var item in toDispose)
        {
            try { item.Dispose(); } catch { /* Suppress during teardown */ }
        }
    }

    public async ValueTask DisposeAsync()
    {
        List<IAsyncDisposable> asyncItems;
        List<IDisposable> syncItems;
        lock (_lock)
        {
            if (_isDisposed) return;
            _isDisposed = true;
            asyncItems = new List<IAsyncDisposable>(_asyncDisposables);
            syncItems = new List<IDisposable>(_disposables);
            _asyncDisposables.Clear();
            _disposables.Clear();
        }

        foreach (var item in asyncItems)
        {
            try { await item.DisposeAsync().ConfigureAwait(false); } catch { }
        }

        foreach (var item in syncItems)
        {
            try { item.Dispose(); } catch { }
        }
    }
}
