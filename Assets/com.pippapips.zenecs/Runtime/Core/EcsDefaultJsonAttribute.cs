using System;

namespace ZenECS.Core
{
    // 컴포넌트 타입에 붙여 기본 초기값을 JSON으로 지정
    [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class EcsDefaultJsonAttribute : Attribute
    {
        public string Json { get; }
        public EcsDefaultJsonAttribute(string json) => Json = json;
    }
}