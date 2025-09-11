using System;
using System.Collections.Generic;
using UnityEngine;
using ZenECS.Core;

namespace ZenECS.Integration
{
    [CreateAssetMenu(menuName="ZenECS/Entity Blueprint")]
    public sealed class EntityBlueprint : ScriptableObject
    {
        [Serializable] public class Entry { public string TypeName; [TextArea(1,16)] public string Json; }
        [SerializeField] List<Entry> _components = new List<Entry>();

        public IReadOnlyList<Entry> Components => _components;

        public static Type ResolveType(string tn) => EcsEntityAuthoring.ResolveType(tn);

        public Entity Instantiate(World world, GameObject view = null)
        {
            var e = world.CreateEntity();
            foreach (var c in _components)
            {
                var t = ResolveType(c.TypeName);
                if (t == null) { Debug.LogWarning($"[ZenECS] Blueprint type not found: {c.TypeName}"); continue; }
                var boxed = Activator.CreateInstance(t);
                try { if (!string.IsNullOrEmpty(c.Json)) JsonUtility.FromJsonOverwrite(c.Json, boxed); }
                catch (Exception ex) { Debug.LogWarning($"[ZenECS] Blueprint json parse failed for {t.Name}: {ex.Message}"); }
                var m = typeof(World).GetMethod("AddOrSet").MakeGenericMethod(t);
                m.Invoke(world, new object[] { e, boxed });
            }
            if (view) world.AddOrSet(e, new ViewComponent{ Instance = view });
            if (!world.GetPool<ViewSyncPolicy>().Has(e)) world.AddOrSet(e, ViewSyncPolicy.Default);
            return e;
        }
    }
}