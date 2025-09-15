// EcsEntityView.cs
using UnityEngine;
using ZenECS.Core;

namespace ZenECS.Integration
{
    [DisallowMultipleComponent]
    public sealed class EcsEntityView : MonoBehaviour
    {
        [SerializeField] int _entityId;
        public int EntityId => _entityId;
        public bool ShowHierarchyBadge = true;
        public Entity Entity => new Entity(_entityId);

        public void Attach(Entity e)
        {
            _entityId = e.Id;

            if (WorldRuntimeLocator.TryGet(out var world))
            {
                var vp = world.GetPool<ViewComponent>();
                if (vp.Has(e))
                {
                    var vc = vp.Get(e);
                    if (vc.Instance == null || vc.Instance != gameObject)
                    {
                        vc.Instance = gameObject;
                        world.AddOrSet(e, vc);
                    }
                }
                else
                {
                    world.AddOrSet(e, new ViewComponent { Instance = gameObject });
                }
            }
        }

        void OnEnable()
        {
            // 씬에 붙은 View가 사전에 EntityId를 갖고 있으면 자동 보정
            if (_entityId > 0)
                Attach(new Entity(_entityId));
        }
    }
}