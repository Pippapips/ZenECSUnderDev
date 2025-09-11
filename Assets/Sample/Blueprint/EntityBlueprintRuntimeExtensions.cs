using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using ZenECS.Core;
using ZenECS.Integration;

namespace ZenECS.Samples
{
    public static class EntityBlueprintRuntimeExtensions
    {
        // Blueprint → Entity 생성(필요 시 View 연결/정책 보정 포함)
        public static Entity CreateEntityFromBlueprint(this EntityBlueprint bp, World world, GameObject viewGo = null, bool ensureDefaultPolicy = true)
        {
            if (bp == null) throw new ArgumentNullException(nameof(bp));
            if (world == null) throw new ArgumentNullException(nameof(world));

            var e = world.CreateEntity();

            // 1) Blueprint의 컴포넌트들 적용
            foreach (var (typeName, json) in EnumerateEntries(bp))
            {
                var t = Resolve(typeName);
                if (t == null) { Debug.LogWarning($"[ZenECS] Blueprint type not found: {typeName}"); continue; }

                object boxed = ComponentDefaults.CreateWithDefaults(t);
                try
                {
                    if (!string.IsNullOrEmpty(json))
                        JsonUtility.FromJsonOverwrite(json, boxed);
                }
                catch (Exception ex) { Debug.LogWarning($"[ZenECS] JSON parse failed for {t.Name}: {ex.Message}"); }

                typeof(World).GetMethod("AddOrSet").MakeGenericMethod(t)
                    .Invoke(world, new object[] { e, boxed });
            }

            // 2) View 연결 (없으면 스킵)
            if (viewGo != null)
            {
                // ViewComponent.Instance 보정
                var vp = world.GetPool<ViewComponent>();
                if (vp.Has(e))
                {
                    var vc = vp.Get(e);
                    if (vc.Instance == null) { vc.Instance = viewGo; world.AddOrSet(e, vc); }
                }
                else
                {
                    world.AddOrSet(e, new ViewComponent { Instance = viewGo });
                }

                // EcsEntityView 컴포넌트에 attach
                var view = viewGo.GetComponent<EcsEntityView>() ?? viewGo.AddComponent<EcsEntityView>();
                view.Attach(e);
            }

            // 3) ViewSyncPolicy 기본값 보정
            if (ensureDefaultPolicy)
            {
                var pol = world.GetPool<ViewSyncPolicy>();
                if (!pol.Has(e)) world.AddOrSet(e, ViewSyncPolicy.Default);
            }

            return e;
        }

        // 내부: Blueprint의 (TypeName, Json)들 열거
        static IEnumerable<(string typeName, string json)> EnumerateEntries(EntityBlueprint bp)
        {
            // 1) 우선 공개 API가 있으면 사용
            var mi = typeof(EntityBlueprint).GetMethod("EnumerateComponents", BindingFlags.Public | BindingFlags.Instance);
            if (mi != null)
            {
                var enumerable = mi.Invoke(bp, null) as IEnumerable;
                if (enumerable != null)
                {
                    foreach (var it in enumerable)
                    {
                        var tn = it.GetType().GetField("TypeName")?.GetValue(it) as string
                              ?? it.GetType().GetProperty("TypeName")?.GetValue(it) as string;
                        var js = it.GetType().GetField("Json")?.GetValue(it) as string
                              ?? it.GetType().GetProperty("Json")?.GetValue(it) as string;
                        yield return (tn, js);
                    }
                    yield break;
                }
            }

            // 2) 백업 경로: 비공개 리스트 필드(_components) 리플렉션
            var fi = typeof(EntityBlueprint).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fi != null)
            {
                var list = fi.GetValue(bp) as IEnumerable;
                if (list != null)
                {
                    foreach (var it in list)
                    {
                        var tn = it.GetType().GetField("TypeName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(it) as string;
                        var js = it.GetType().GetField("Json",      BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(it) as string;
                        yield return (tn, js);
                    }
                }
            }
        }

        static Type Resolve(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;
            var t = Type.GetType(typeName, false);
            if (t != null) return t;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { t = asm.GetType(typeName, false); if (t != null) return t; }
                catch { }
            }
            return null;
        }
    }
}
