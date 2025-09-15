#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZenECS.EditorTools
{
    [InitializeOnLoad]
    public static class AutoDefines
    {
        const string MENU = "ZenECS/Defines/Auto Refresh";
        const string PREF = "ZenECS.AutoDefines.Enabled";

        static readonly string[] Targets = {
            "ZENECS_UNIRX",
            "ZENECS_ZENJECT"
        };

        static AutoDefines()
        {
            if (!EditorPrefs.HasKey(PREF)) EditorPrefs.SetBool(PREF, true);
            if (EditorPrefs.GetBool(PREF, true))
                EditorApplication.update += OneShot;
        }

        static void OneShot() { EditorApplication.update -= OneShot; Refresh(); }

        [MenuItem(MENU)]
        static void Toggle()
        {
            var v = !EditorPrefs.GetBool(PREF, true);
            EditorPrefs.SetBool(PREF, v);
            Menu.SetChecked(MENU, v);
            if (v) Refresh();
        }

        [MenuItem(MENU, true)]
        static bool Validate() { Menu.SetChecked(MENU, EditorPrefs.GetBool(PREF, true)); return true; }

        [MenuItem("ZenECS/Defines/Refresh Now")]
        public static void Refresh()
        {
            bool hasUniRx  = HasType("UniRx.Unit") || HasType("UniRx.Subject`1");
            bool hasZenject= HasType("Zenject.DiContainer") || HasType("Zenject.MonoInstaller");

#if UNITY_2021_2_OR_NEWER
            var nbtList = new[] {
                UnityEditor.Build.NamedBuildTarget.Standalone,
                UnityEditor.Build.NamedBuildTarget.Android,
                UnityEditor.Build.NamedBuildTarget.iOS,
                UnityEditor.Build.NamedBuildTarget.WebGL
            };
            foreach (var nbt in nbtList)
            {
                var defs = PlayerSettings.GetScriptingDefineSymbols(nbt).Split(';').Where(s=>!string.IsNullOrWhiteSpace(s)).ToHashSet();
                Set(defs, "ZENECS_UNIRX",   hasUniRx);
                Set(defs, "ZENECS_ZENJECT", hasZenject);
                PlayerSettings.SetScriptingDefineSymbols(nbt, string.Join(";", defs));
            }
#else
            var groups = new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android, BuildTargetGroup.iOS, BuildTargetGroup.WebGL };
            foreach (var g in groups)
            {
                var defs = PlayerSettings.GetScriptingDefineSymbolsForGroup(g).Split(';').Where(s=>!string.IsNullOrWhiteSpace(s)).ToHashSet();
                Set(defs, "ZENECS_UNIRX",   hasUniRx);
                Set(defs, "ZENECS_ZENJECT", hasZenject);
                PlayerSettings.SetScriptingDefineSymbolsForGroup(g, string.Join(";", defs));
            }
#endif
            //Debug.Log($"[ZenECS] UniRx={hasUniRx}, Zenject={hasZenject} → Scripting Defines updated.");
        }

        static void Set(System.Collections.Generic.HashSet<string> set, string sym, bool on)
        {
            if (on) set.Add(sym); else set.Remove(sym);
        }

        static bool HasType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                try { if (asm.GetType(fullName, false) != null) return true; } catch {}
            return false;
        }
    }
}
#endif
