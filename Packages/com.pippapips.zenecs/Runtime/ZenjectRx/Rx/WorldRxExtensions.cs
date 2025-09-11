using System;
using UniRx;
using ZenECS.Core;

namespace ZenECS.Integration.ZenjectRx
{
    public static class WorldRxExtensions
    {
        public static IObservable<(Entity e, T v)> OnAddedAsObservable<T>(this IPool<T> pool) where T: struct, IComponent
            => Observable.Create<(Entity, T)>(o => { void H(Entity e, T v) => o.OnNext((e,v)); pool.OnAdded += H; return Disposable.Create(()=> pool.OnAdded -= H); });

        public static IObservable<(Entity e, T v)> OnChangedAsObservable<T>(this IPool<T> pool) where T: struct, IComponent
            => Observable.Create<(Entity, T)>(o => { void H(Entity e, T v) => o.OnNext((e,v)); pool.OnChanged += H; return Disposable.Create(()=> pool.OnChanged -= H); });

        public static IObservable<Entity> OnRemovedAsObservable<T>(this IPool<T> pool) where T: struct, IComponent
            => Observable.Create<Entity>(o => { void H(Entity e) => o.OnNext(e); pool.OnRemoved += H; return Disposable.Create(()=> pool.OnRemoved -= H); });
    }
}