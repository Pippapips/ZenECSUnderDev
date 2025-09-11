using System;
using System.Linq;
using UnityEngine;
using Zenject;

namespace ZenECS.Integration.ZenjectRx
{
    public sealed class EcsFeatureInstaller : MonoInstaller
    {
        [Serializable]
        public struct SystemTypeRef
        {
            [SerializeField] string _typeName;
            public string TypeName => _typeName;
            public Type Resolve()
            {
                if (string.IsNullOrEmpty(_typeName)) return null;
                var t = Type.GetType(_typeName, false);
                if (t != null) return t;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try { t = asm.GetType(_typeName, false); if (t != null) return t; } catch {}
                }
                return null;
            }
            public void Set(Type t) => _typeName = t?.AssemblyQualifiedName;
        }

        public SystemTypeRef[] Systems;
        public ScriptableObject[] Configs;

        public override void InstallBindings()
        {
            if (Systems != null)
                foreach (var r in Systems)
                {
                    var t = r.Resolve();
                    if (t != null) Container.BindInterfacesAndSelfTo(t).AsSingle();
                }
            if (Configs != null)
                foreach (var cfg in Configs.Where(c=>c))
                    Container.Bind(cfg.GetType()).FromInstance(cfg).AsSingle();
        }
    }
}