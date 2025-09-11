using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;
using ZenECS.Core;

namespace ZenECS.Integration.ZenjectRx
{
    public sealed class EcsRunnerZenject : IInitializable, ITickable, IDisposable
    {
        readonly World _world;
        readonly List<IRunSystem> _runs;
        readonly List<IInitSystem> _inits;
        readonly List<IStopSystem> _stops;
        SystemScheduler.Plan _plan;

        public EcsRunnerZenject(
            World world,
            [Inject(Optional = true)] List<IRunSystem> runs,
            [Inject(Optional = true)] List<IInitSystem> inits,
            [Inject(Optional = true)] List<IStopSystem> stops)
        {
            _world = world;
            _runs  = runs  ?? new List<IRunSystem>();
            _inits = inits ?? new List<IInitSystem>();
            _stops = stops ?? new List<IStopSystem>();
        }

        public void Initialize()
        {
            WorldRuntimeLocator.Set(_world);
            var all = new HashSet<object>(); foreach (var s in _runs) all.Add(s); foreach (var s in _inits) all.Add(s); foreach (var s in _stops) all.Add(s);
            _plan = SystemScheduler.Build(all);
            foreach (var e in _plan.Errors) Debug.LogError(e);
            foreach (var w in _plan.Warnings) Debug.LogWarning(w);
            foreach (var i in _inits) Safe(()=> i.Init(_world), i);
        }

        public void Tick()
        {
            if (_plan == null) return;
            RunGroup<SimulationSystemGroup>(Time.deltaTime);
            RunGroup<PresentationSystemGroup>(Time.deltaTime);
        }

        public void Dispose()
        {
            foreach (var s in _stops) Safe(()=> s.Stop(_world), s);
        }

        void RunGroup<T>(float dt)
        {
            if (!_plan.Ordered.TryGetValue(typeof(T), out var list)) return;
            foreach (var sys in list.OfType<IRunSystem>()) Safe(()=> sys.Run(_world, dt), sys);
        }

        static void Safe(Action a, object owner)
        {
            try { a(); } catch (Exception ex) { Debug.LogError($"[ZenECS] {owner.GetType().Name} threw: {ex}"); }
        }
    }
}
