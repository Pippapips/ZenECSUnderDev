using ZenECS.Core;

namespace ZenECS.Integration
{
    [EcsDefaultJson("{\"Value\":1.0}")]
    public struct UniformScale : IComponent
    {
        public float Value;
    }
}