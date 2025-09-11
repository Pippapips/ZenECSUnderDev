using System;
using System.Collections.Generic;
using UnityEngine;
using ZenECS.Core;

namespace ZenECS.Integration
{
    // 씬에서 늦게 실행되도록 살짝 뒤로 밀기 (다른 컴포넌트 초기화 후)
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class EcsEntityAuthoring : MonoBehaviour
    {
        [Serializable]
        public class Draft
        {
            public string TypeName;
            [TextArea(1, 16)] public string Json;
        }

        [Header("Entity")] [SerializeField] int _entityId;
        [SerializeField] bool _applyOnEnable = true;

        [Tooltip("Play 시 World 준비되면 EcsEntityView로 치환(부착)")] [SerializeField]
        bool _autoReplaceWithEntityView = true;

        [Tooltip("치환 시 Authoring 컴포넌트를 제거(Play 모드 한정)")] [SerializeField]
        bool _destroyAuthoringOnPlay = true;

        [Header("Draft Components")] [SerializeField]
        List<Draft> _drafts = new List<Draft>();

        bool _applied;
        bool _subscribed;

        void OnEnable()
        {
            if (!_applyOnEnable) return;

            // 이미 World가 있으면 즉시 처리
            if (WorldRuntimeLocator.TryGet(out var world))
            {
                ApplyNow(world);
            }
            else
            {
                // 아직 World가 없다면 준비될 때까지 대기
                WorldRuntimeLocator.WorldSet += OnWorldReady;
                _subscribed = true;
            }
        }

        void OnDisable()
        {
            if (_subscribed)
            {
                WorldRuntimeLocator.WorldSet -= OnWorldReady;
                _subscribed = false;
            }
        }

        void OnWorldReady(World world)
        {
            if (_applied) return;
            WorldRuntimeLocator.WorldSet -= OnWorldReady;
            _subscribed = false;
            ApplyNow(world);
        }

        void ApplyNow(World world)
        {
            var e = EnsureEntity(world);
            ApplyDraftsToWorld(world, e);
            if (_autoReplaceWithEntityView)
            {
                ReplaceWithEntityView(e);
                if (Application.isPlaying && _destroyAuthoringOnPlay)
                    Destroy(this); // ★ 실제 "치환" 느낌: 자기 자신 제거
            }

            _applied = true;
        }

        Entity EnsureEntity(World world)
        {
            if (_entityId <= 0) _entityId = world.CreateEntity().Id;
            return new Entity(_entityId);
        }

        void ReplaceWithEntityView(Entity e)
        {
            var view = gameObject.GetComponent<EcsEntityView>() ?? gameObject.AddComponent<EcsEntityView>();
            view.Attach(e);
        }

        public static Type ResolveType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;
            var t = Type.GetType(typeName, false);
            if (t != null) return t;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    t = asm.GetType(typeName, false);
                    if (t != null) return t;
                }
                catch
                {
                }
            }

            return null;
        }

// EcsEntityAuthoring.cs 안
        public void ApplyDraftsToWorld(World world, Entity entity)
        {
            // 1) 사용자 Draft 먼저 적용
            foreach (var d in _drafts)
            {
                var t = ResolveType(d.TypeName);
                if (t == null)
                {
                    Debug.LogWarning($"[ZenECS] Draft type not found: {d.TypeName}");
                    continue;
                }

                object boxed = Activator.CreateInstance(t);
                // try
                // {
                //     if (!string.IsNullOrEmpty(d.Json)) JsonUtility.FromJsonOverwrite(d.Json, boxed);
                // }
                try
                {
                    if (!string.IsNullOrEmpty(d.Json))
                        JsonUtility.FromJsonOverwrite(d.Json, boxed);
                    else
                    {
                        var def = ComponentDefaults.GetDefaultJson(t);
                        if (!string.IsNullOrEmpty(def))
                            JsonUtility.FromJsonOverwrite(def, boxed);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ZenECS] Draft json parse failed for {t.Name}: {ex.Message}");
                }

                var m = typeof(World).GetMethod("AddOrSet").MakeGenericMethod(t);
                m.Invoke(world, new object[] { entity, boxed });
            }

            // 2) ViewComponent 보정: 있더라도 Instance가 null이면 현재 GO로 채움
            var vp = world.GetPool<ViewComponent>();
            if (vp.Has(entity))
            {
                var vc = vp.Get(entity);
                if (vc.Instance == null)
                {
                    vc.Instance = gameObject;
                    world.AddOrSet(entity, vc); // 덮어쓰기 아님, 필드 보정
                }
            }
            else
            {
                world.AddOrSet(entity, new ViewComponent { Instance = gameObject });
            }

            // 3) 정책 기본값 보정(있으면 건드리지 않음)
            var pol = world.GetPool<ViewSyncPolicy>();
            if (!pol.Has(entity)) world.AddOrSet(entity, ViewSyncPolicy.Default);
        }

        public List<Draft> Drafts => _drafts;
    }
}