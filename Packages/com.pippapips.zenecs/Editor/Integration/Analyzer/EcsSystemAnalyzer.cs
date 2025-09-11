#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using ZenECS.Core;

namespace ZenECS.Integration.Editor
{
    public static class EcsSystemAnalyzer
    {
        public static SystemScheduler.Plan AnalyzeAllAssemblies(bool logToConsole = false)
        {
            var sys = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => {
                            try { return a.GetTypes(); } catch { return Array.Empty<Type>(); }
                        })
                        .Where(t => !t.IsAbstract && !t.IsInterface)
                        .Where(t => typeof(IRunSystem).IsAssignableFrom(t) ||
                                    typeof(IInitSystem).IsAssignableFrom(t) ||
                                    typeof(IStopSystem).IsAssignableFrom(t))
                        .Select(t => Activator.CreateInstance(t, nonPublic:true) ?? new Stub(t))
                        .ToArray();

            var plan = SystemScheduler.Build(sys);
            if (logToConsole)
            {
                foreach (var e in plan.Errors) UnityEngine.Debug.LogError(e);
                foreach (var w in plan.Warnings) UnityEngine.Debug.LogWarning(w);
                foreach (var kv in plan.Ordered)
                    UnityEngine.Debug.Log($"[ZenECS] Group {kv.Key.Name}: {string.Join(", ", kv.Value.Select(v=>v.GetType().Name))}");
            }
            return plan;
        }

        sealed class Stub { readonly Type _t; public Stub(Type t)=>_t=t; public void Run(World w,float dt){} public override string ToString()=>_t.Name; }
    }

    public static class EcsAnalyzerMenu
    {
        const string PREF_LOG = "ZenECS.Analyzer.LogToConsole";

        [MenuItem("ZenECS/Analyzer/Analyze Systems", priority = 10)]
        public static void Analyze()
        {
            var useLog = EditorPrefs.GetBool(PREF_LOG, false);
            EcsSystemAnalyzer.AnalyzeAllAssemblies(useLog);
        }

        [MenuItem("ZenECS/Analyzer/Log To Console", priority = 11)]
        public static void ToggleLog()
        {
            var v = !EditorPrefs.GetBool(PREF_LOG, false);
            EditorPrefs.SetBool(PREF_LOG, v);
            Menu.SetChecked("ZenECS/Analyzer/Log To Console", v);
            UnityEngine.Debug.Log($"[ZenECS] Analyzer LogToConsole = {v}");
        }

        [MenuItem("ZenECS/Analyzer/Log To Console", true)]
        public static bool ToggleLogValidate()
        {
            Menu.SetChecked("ZenECS/Analyzer/Log To Console", EditorPrefs.GetBool("ZenECS.Analyzer.LogToConsole", false));
            return true;
        }
    }
}
#endif
