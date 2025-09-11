using UniRx;
using ZenECS.Core;

namespace ZenECS.Integration.ZenjectRx
{
    public abstract class ReactiveSystemBase : IInitSystem, IRunSystem, IStopSystem
    {
        protected readonly World World;
        protected readonly IEcsMessageBus Bus;
        protected readonly IComponentStreamHub Streams;
        protected readonly CompositeDisposable Disposables = new CompositeDisposable();

        protected ReactiveSystemBase(World w, IEcsMessageBus b, IComponentStreamHub s) { World = w; Bus = b; Streams = s; }
        public virtual void Init(World world)  => OnInit();
        public virtual void Run(World world, float dt) => OnRun(dt);
        public virtual void Stop(World world)  => OnStop();

        protected virtual void OnInit() {}
        protected virtual void OnRun(float dt) {}
        protected virtual void OnStop() { Disposables.Dispose(); }
    }
}