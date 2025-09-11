using System;

namespace ZenECS.Core
{
    public interface IRunSystem  { void Run(World world, float dt); }
    public interface IInitSystem { void Init(World world); }
    public interface IStopSystem { void Stop(World world); }

    public sealed class SimulationSystemGroup {}
    public sealed class PresentationSystemGroup {}

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class UpdateInGroupAttribute : Attribute
    {
        public Type GroupType { get; }
        public UpdateInGroupAttribute(Type groupType) { GroupType = groupType; }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class OrderAttribute : Attribute
    {
        public int Order { get; }
        public string SubGroup { get; }
        public OrderAttribute(int order = 0, string subGroup = null) { Order = order; SubGroup = subGroup; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class OrderBeforeAttribute : Attribute
    {
        public Type[] Targets { get; }
        public OrderBeforeAttribute(params Type[] targets) { Targets = targets; }
    }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class OrderAfterAttribute : Attribute
    {
        public Type[] Targets { get; }
        public OrderAfterAttribute(params Type[] targets) { Targets = targets; }
    }
}