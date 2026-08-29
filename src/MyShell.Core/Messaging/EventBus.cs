using System.Collections.Concurrent;
using System.Windows.Threading;

namespace MyShell.Core.Messaging;

public interface IEventBus
{
    IDisposable Subscribe<TEvent>(Action<TEvent> handler);
    void Publish<TEvent>(TEvent message);
}

public sealed class EventBus(Dispatcher dispatcher) : IEventBus
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
    {
        var handlers = _handlers.GetOrAdd(typeof(TEvent), _ => []);
        lock (handlers)
            handlers.Add(handler);

        return new Subscription(() =>
        {
            lock (handlers)
                handlers.Remove(handler);
        });
    }

    public void Publish<TEvent>(TEvent message)
    {
        if (!_handlers.TryGetValue(typeof(TEvent), out var handlers))
            return;

        Delegate[] snapshot;
        lock (handlers)
            snapshot = handlers.ToArray();

        foreach (var handler in snapshot)
            dispatcher.BeginInvoke(() => ((Action<TEvent>)handler)(message));
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}

public readonly record struct WidgetsChangedEvent;
