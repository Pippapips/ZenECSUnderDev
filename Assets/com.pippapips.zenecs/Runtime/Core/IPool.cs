using System;
using System.Collections.Generic;

namespace ZenECS.Core
{
    public interface IPool { Type ComponentType { get; } int Count { get; } }
    public interface IPool<T> : IPool where T: struct, IComponent
    {
        event Action<Entity, T> OnAdded;
        event Action<Entity, T> OnChanged;
        event Action<Entity>    OnRemoved;

        bool Has(Entity e);
        ref T GetRef(Entity e, out bool exists);
        T Get(Entity e);
        void AddOrSet(Entity e, in T value);
        bool Remove(Entity e);

        IEnumerable<KeyValuePair<Entity, T>> All();
        IEnumerable<Entity> AllEntities();
    }
}