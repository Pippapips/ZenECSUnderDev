using UniRx;
using UnityEngine;
using Zenject;

namespace ZenECS.Integration.ZenjectRx
{
    public interface ITimeService { float DeltaTime { get; } IReadOnlyReactiveProperty<float> DeltaTimeRx { get; } }

    public sealed class TimeService : ITimeService, ITickable
    {
        readonly ReactiveProperty<float> _dt = new ReactiveProperty<float>(0f);
        public float DeltaTime => _dt.Value;
        public IReadOnlyReactiveProperty<float> DeltaTimeRx => _dt;
        public void Tick() => _dt.Value = Time.deltaTime;
    }
}