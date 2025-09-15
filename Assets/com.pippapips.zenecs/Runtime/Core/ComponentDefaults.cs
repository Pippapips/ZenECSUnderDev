using System;
using System.Reflection;
using UnityEngine;

namespace ZenECS.Core
{
    public static class ComponentDefaults
    {
        public static object CreateWithDefaults(Type t)
        {
            var o = Activator.CreateInstance(t);
            var attr = t.GetCustomAttribute<EcsDefaultJsonAttribute>();
            if (attr != null && !string.IsNullOrEmpty(attr.Json))
            {
                try { JsonUtility.FromJsonOverwrite(attr.Json, o); } catch { }
            }
            return o;
        }

        public static T CreateWithDefaults<T>() where T : new()
        {
            var o = new T();
            var attr = typeof(T).GetCustomAttribute<EcsDefaultJsonAttribute>();
            if (attr != null && !string.IsNullOrEmpty(attr.Json))
            {
                try { JsonUtility.FromJsonOverwrite(attr.Json, o); } catch { }
            }
            return o;
        }

        public static string GetDefaultJson(Type t)
            => t.GetCustomAttribute<EcsDefaultJsonAttribute>()?.Json;
    }
}