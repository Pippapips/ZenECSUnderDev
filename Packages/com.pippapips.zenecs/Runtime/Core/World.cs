using System;
using System.Collections.Generic;

namespace ZenECS.Core
{
    public sealed class World
    {
        int _nextId = 1;
        readonly HashSet<int> _alive = new HashSet<int>();
        readonly Dictionary<Type, object> _pools = new Dictionary<Type, object>(64);

        public Entity CreateEntity()
        {
            var id = _nextId++;
            _alive.Add(id);
            return new Entity(id);
        }

        public bool Exists(Entity e) => _alive.Contains(e.Id);

        public void DestroyEntity(Entity e)
        {
            if (!_alive.Remove(e.Id)) return;
            // drop from all pools
            foreach (var obj in _pools.Values)
            {
                var ip = obj as IPool;
                var t = ip.ComponentType;
                var remove = obj.GetType().GetMethod("Remove");
                remove?.Invoke(obj, new object[] { e });
            }
        }

        public IPool<T> GetPool<T>() where T: struct, IComponent
        {
            if (_pools.TryGetValue(typeof(T), out var obj)) return (IPool<T>)obj;
            var p = new Pool<T>();
            _pools[typeof(T)] = p;
            return p;
        }

        public void AddOrSet<T>(Entity e, in T value) where T: struct, IComponent
            => GetPool<T>().AddOrSet(e, value);

        public bool Remove<T>(Entity e) where T: struct, IComponent
            => GetPool<T>().Remove(e);

        public IEnumerable<(Type type, IPool pool)> GetAllPools()
        {
            foreach (var kv in _pools)
                yield return (kv.Key, (IPool)kv.Value);
        }
    }
}