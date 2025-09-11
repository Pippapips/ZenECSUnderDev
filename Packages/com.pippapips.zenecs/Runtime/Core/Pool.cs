using System;
using System.Collections.Generic;

namespace ZenECS.Core
{
    public sealed class Pool<T> : IPool<T> where T : struct, IComponent
    {
        // sparse: EntityId -> (index+1), 0 = none
        private readonly Dictionary<int, int> _sparse = new Dictionary<int, int>(256);

        // dense arrays
        private int[] _entities = new int[256];
        private T[]   _values   = new T[256];
        private int   _count;

        public Type ComponentType => typeof(T);
        public int Count => _count;

        public event Action<Entity, T> OnAdded;
        public event Action<Entity, T> OnChanged;
        public event Action<Entity>    OnRemoved;

        public bool Has(Entity e)
            => _sparse.TryGetValue(e.Id, out var ip1) && ip1 != 0;

        public ref T GetRef(Entity e, out bool exists)
        {
            if (_sparse.TryGetValue(e.Id, out var ip1) && ip1 != 0)
            {
                exists = true;
                int i = ip1 - 1;
                return ref _values[i]; // ✅ 배열 요소는 ref 반환 가능
            }
            exists = false;
            throw new KeyNotFoundException($"[{typeof(T).Name}] {e} not found");
        }

        public T Get(Entity e)
        {
            if (!_sparse.TryGetValue(e.Id, out var ip1) || ip1 == 0)
                throw new KeyNotFoundException($"[{typeof(T).Name}] {e} not found");
            return _values[ip1 - 1];
        }

        public void AddOrSet(Entity e, in T value)
        {
            if (_sparse.TryGetValue(e.Id, out var ip1) && ip1 != 0)
            {
                int i = ip1 - 1;
                _values[i] = value;
                OnChanged?.Invoke(e, value);
            }
            else
            {
                EnsureCapacity(_count + 1);
                int i = _count++;
                _entities[i] = e.Id;
                _values[i]   = value;
                _sparse[e.Id] = i + 1;
                OnAdded?.Invoke(e, value);
            }
        }

        public bool Remove(Entity e)
        {
            if (!_sparse.TryGetValue(e.Id, out var ip1) || ip1 == 0)
                return false;

            int i    = ip1 - 1;
            int last = _count - 1;

            // swap-back 제거
            if (i != last)
            {
                int lastEntId = _entities[last];
                _entities[i] = lastEntId;
                _values[i]   = _values[last];
                _sparse[lastEntId] = i + 1;
            }

            _count--;
            _sparse[e.Id] = 0; // 또는 _sparse.Remove(e.Id);

            OnRemoved?.Invoke(e);
            return true;
        }

        public IEnumerable<KeyValuePair<Entity, T>> All()
        {
            for (int i = 0; i < _count; i++)
                yield return new KeyValuePair<Entity, T>(new Entity(_entities[i]), _values[i]);
        }

        public IEnumerable<Entity> AllEntities()
        {
            for (int i = 0; i < _count; i++)
                yield return new Entity(_entities[i]);
        }

        private void EnsureCapacity(int min)
        {
            if (_values.Length >= min) return;
            int newCap = Math.Max(_values.Length * 2, min);
            Array.Resize(ref _values, newCap);
            Array.Resize(ref _entities, newCap);
        }
    }
}
