using System;
using UniRx;
using UnityEngine;
using Zenject;

namespace ZenECS.Integration.ZenjectRx
{
    public interface IInputService { IObservable<Vector2> MoveRx { get; } IObservable<bool> FireRx { get; } }

    public sealed class InputService : IInputService, ITickable
    {
        readonly Subject<Vector2> _move = new Subject<Vector2>();
        readonly Subject<bool> _fire = new Subject<bool>();
        public IObservable<Vector2> MoveRx => _move;
        public IObservable<bool> FireRx => _fire;
        public void Tick()
        {
            var v = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (v.sqrMagnitude > 0f) _move.OnNext(v.normalized);
            if (Input.GetKeyDown(KeyCode.Space)) _fire.OnNext(true);
        }
    }
}