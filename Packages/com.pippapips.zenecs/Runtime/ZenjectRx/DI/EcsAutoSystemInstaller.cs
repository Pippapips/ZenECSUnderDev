using System;
using System.Linq;
using Zenject;
using ZenECS.Core;

namespace ZenECS.Integration.ZenjectRx
{
    public sealed class EcsAutoSystemInstaller : MonoInstaller
    {
        public string[] AssemblyNameContains;

        public override void InstallBindings()
        {
            var runT = typeof(IRunSystem);
            var initT = typeof(IInitSystem);
            var stopT = typeof(IStopSystem);
            var ignoreAttr = typeof(ZenECS.Core.EcsAutoBindIgnoreAttribute);

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var an = asm.GetName().Name ?? "";
                // ① Editor 어셈블리 스킵
                if (an.IndexOf("Editor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    an.StartsWith("UnityEditor", StringComparison.OrdinalIgnoreCase))
                    continue;

                // ② 사용자가 필터를 지정했다면 그 안에서만
                if (AssemblyNameContains != null && AssemblyNameContains.Length > 0)
                {
                    var ok = AssemblyNameContains.Any(tag =>
                        an.IndexOf(tag, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (!ok) continue;
                }

                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch
                {
                    continue;
                }

                foreach (var t in types)
                {
                    if (t.IsAbstract || t.IsInterface) continue;

                    // ③ 제외 마커 달린 타입 스킵
                    if (t.GetCustomAttributes(ignoreAttr, inherit: false).Any()) continue;

                    if (runT.IsAssignableFrom(t) || initT.IsAssignableFrom(t) || stopT.IsAssignableFrom(t))
                        Container.BindInterfacesAndSelfTo(t).AsSingle();
                }
            }
        }
    }
}