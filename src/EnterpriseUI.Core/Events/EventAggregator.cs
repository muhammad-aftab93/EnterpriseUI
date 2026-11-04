using System.Collections.Concurrent;

namespace EnterpriseUI.Core.Events;

public interface IEventAggregator
{
    void Publish<TEvent>(TEvent eventData);
    IDisposable Subscribe<TEvent>(Action<TEvent> handler);
}

/// <summary>
/// Thread-safe decoupled pub/sub event aggregator for inter-component messaging.
/// </summary>
public sealed class EventAggregator : IEventAggregator
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _subscribers = new();
    private readonly object _lock = new();

    public void Publish<TEvent>(TEvent eventData)
    {
        if (_subscribers.TryGetValue(typeof(TEvent), out var list))
        {
            Delegate[] targets;
            lock (_lock)
            {
                targets = list.ToArray();
            }

            foreach (var target in targets)
            {
                if (target is Action<TEvent> action)
                {
                    action(eventData);
                }
            }
        }
    }

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var type = typeof(TEvent);

        lock (_lock)
        {
            var list = _subscribers.GetOrAdd(type, _ => new List<Delegate>());
            list.Add(handler);
        }

        return new SubscriptionToken(() =>
        {
            lock (_lock)
            {
                if (_subscribers.TryGetValue(type, out var list))
                {
                    list.Remove(handler);
                }
            }
        });
    }

    private sealed class SubscriptionToken : IDisposable
    {
        private Action? _unsubscribe;
        public SubscriptionToken(Action unsubscribe) => _unsubscribe = unsubscribe;
        public void Dispose()
        {
            Interlocked.Exchange(ref _unsubscribe, null)?.Invoke();
        }
    }
}

