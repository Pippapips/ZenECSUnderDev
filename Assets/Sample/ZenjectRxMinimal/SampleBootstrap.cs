using UnityEngine;
using ZenECS.Core;
using ZenECS.Integration;

namespace ZenECS.Samples.ZenjectRx
{
    public sealed class SampleBootstrap : MonoBehaviour
    {
        void Start()
        {
            Debug.Log("Starting ZenjectRxMinimal");
            if (!WorldRuntimeLocator.TryGet(out var world)) return;
            var e = world.CreateEntity();
            world.AddOrSet(e, new ViewComponent { Instance = gameObject });
            world.AddOrSet(e, new Position { Value = transform.position });
            world.AddOrSet(e, ViewSyncPolicy.Default);
        }
    }
}