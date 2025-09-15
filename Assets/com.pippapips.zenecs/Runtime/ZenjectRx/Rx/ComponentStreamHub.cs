using System;
using UniRx;
using ZenECS.Core;

namespace ZenECS.Integration.ZenjectRx
{
    public interface IComponentStreamHub
    {
        IObservable<(Entity e, T v)> Added<T>() where T: struct, IComponent;
        IObservable<(Entity e, T v)> Changed<T>() where T: struct, IComponent;
        IObservable<Entity> Removed<T>() where T: struct, IComponent;
    }

    public sealed class ComponentStreamHub : IComponentStreamHub
    {
        readonly World _world;
        public ComponentStreamHub(World w) { _world = w; }

        public IObservable<(Entity e, T v)> Added<T>() where T: struct, IComponent => _world.GetPool<T>().OnAddedAsObservable();
        public IObservable<(Entity e, T v)> Changed<T>() where T: struct, IComponent => _world.GetPool<T>().OnChangedAsObservable();
        public IObservable<Entity> Removed<T>() where T: struct, IComponent => _world.GetPool<T>().OnRemovedAsObservable();
    }
}