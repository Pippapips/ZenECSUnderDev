using UniRx;
using UnityEngine;
using ZenECS.Core;
using ZenECS.Integration;
using ZenECS.Integration.ZenjectRx;

namespace ZenECS.Samples.ZenjectRx
{
    [UpdateInGroup(typeof(SimulationSystemGroup))] [Order(30)]
    public sealed class SampleMovementSystem : ReactiveSystemBase
    {
        readonly IInputService _input; const float Speed = 5f;
        struct Velocity : IComponent { public Vector3 Value; }

        public SampleMovementSystem(World w, IEcsMessageBus b, IComponentStreamHub s, IInputService input) : base(w,b,s) => _input = input;

        protected override void OnInit()
        {
            _input.MoveRx.Subscribe(dir => {
                var pos = World.GetPool<Position>();
                foreach (var (e, _) in pos.All())
                    World.AddOrSet(e, new Velocity { Value = new Vector3(dir.x,0,dir.y)*Speed });
            }).AddTo(Disposables);
        }

        protected override void OnRun(float dt)
        {
            var pos = World.GetPool<Position>();
            var vel = World.GetPool<Velocity>();
            World.EachRef<Position, Velocity>((Entity e, ref Position p, ref Velocity v) => p.Value += v.Value*dt);
        }
    }
}