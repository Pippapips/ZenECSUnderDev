using UnityEngine;

namespace ZenECS.Core
{
    [EcsDefaultJson("{\"Value\":1.0}")]
    public struct UniformScale : IComponent
    {
        public float Value;
    }

    // 참고: 비균일 스케일 기본 (1,1,1)도 함께 지정 가능
    [EcsDefaultJson("{\"Value\":{\"x\":1.0,\"y\":1.0,\"z\":1.0}}")]
    public struct NonUniformScale : IComponent
    {
        public Vector3 Value;
    }
}