using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using VideoDownloader.Domain.Logging;
using VideoDownloader.Domain.Messenger;

namespace VideoDownloader.Application.Messenger;

public class EventMessenger : IEventMessenger
{
    private readonly ConcurrentDictionary<Type, List<Action<object>>> _subscribers = new();
    private readonly ILogger _logger;

    public EventMessenger(ILogger logger)
    {
        _logger = logger;
    }

    public void Send<TMessage>(TMessage message) where TMessage : class
    {
        if (!_subscribers.TryGetValue(typeof(TMessage), out var handlers))
            return;

        Action<object>[] snapshot;
        lock (handlers)
        {
            snapshot = handlers.ToArray();
        }
        foreach (var handler in snapshot)
        {
            try { handler(message); }
            catch (Exception ex)
            {
                _logger.LogException("Messenger", $"事件订阅处理器异常 ({typeof(TMessage).Name})", ex);
            }
        }
    }

    public IDisposable Subscribe<TMessage>(Action<TMessage> handler) where TMessage : class
    {
        Action<object> wrapper = o => handler((TMessage)o);
        var handlers = _subscribers.GetOrAdd(typeof(TMessage), _ => new List<Action<object>>());
        lock (handlers)
        {
            handlers.Add(wrapper);
        }
        return new SubscriptionToken(handlers, wrapper);
    }

    private class SubscriptionToken : IDisposable
    {
        private readonly List<Action<object>> _handlers;
        private readonly Action<object> _wrapper;
        private bool _disposed;

        public SubscriptionToken(List<Action<object>> handlers, Action<object> wrapper)
        {
            _handlers = handlers;
            _wrapper = wrapper;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            lock (_handlers)
            {
                _handlers.Remove(_wrapper);
            }
        }
    }
}