using System;
using System.Collections.Concurrent;
using UniRx;

namespace ZenECS.Integration.ZenjectRx
{
    public interface IEcsMessageBus { void Publish<T>(T message); IObservable<T> On<T>(); }

    public sealed class EcsMessageBus : IEcsMessageBus, IDisposable
    {
        readonly ConcurrentDictionary<Type, object> _subjects = new();
        public void Publish<T>(T message) { if (_subjects.TryGetValue(typeof(T), out var s)) ((ISubject<T>)s).OnNext(message); }
        public IObservable<T> On<T>() => (ISubject<T>)_subjects.GetOrAdd(typeof(T), _ => new Subject<T>());
        public void Dispose() { foreach (var s in _subjects.Values) (s as IDisposable)?.Dispose(); _subjects.Clear(); }
    }
}