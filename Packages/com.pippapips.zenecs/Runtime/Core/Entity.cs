using System;

namespace ZenECS.Core
{
    public readonly struct Entity : IEquatable<Entity>
    {
        public readonly int Id;
        public Entity(int id) => Id = id;
        public bool Equals(Entity other) => Id == other.Id;
        public override bool Equals(object obj) => obj is Entity e && Equals(e);
        public override int GetHashCode() => Id;
        public override string ToString() => $"Entity({Id})";
        public static implicit operator int(Entity e) => e.Id;
    }

    public interface IComponent { }
}