using System;
using System.Collections.Generic;
using System.Linq;

namespace ZenECS.Core
{
    public static class QueryHelpers
    {
        // 교차 집합 (안전 캐스팅, 성능 고려)
        public static IEnumerable<Entity> Query<TA, TB>(this World w)
            where TA : struct, IComponent
            where TB : struct, IComponent
        {
            var a = w.GetPool<TA>();
            var b = w.GetPool<TB>();
            bool useA = a.Count <= b.Count;
            var iter = useA ? a.AllEntities() : b.AllEntities();
            foreach (var e in iter)
                if (useA ? b.Has(e) : a.Has(e))
                    yield return e;
        }

        // 필요 시: 1컴포넌트 버전
        public static IEnumerable<Entity> Query<T>(this World w) where T: struct, IComponent
            => w.GetPool<T>().AllEntities();

        // ref 델리게이트 시그니처 (Action<T>는 ref/in 불가)
        public delegate void RefAction<TA, TB>(TA a, ref TB b);
        public delegate void RefAction<TA, TB, TC>(TA a, ref TB b, ref TC c);

        public static void EachRef<T>(this World w, RefAction<Entity, T> fn)
            where T : struct, IComponent
        {
            var p = w.GetPool<T>();
            foreach (var e in p.AllEntities())
            {
                ref var r = ref p.GetRef(e, out _);
                fn(e, ref r);
            }
        }

        public static void EachRef<TA, TB>(this World w, RefAction<Entity, TA, TB> fn)
            where TA : struct, IComponent
            where TB : struct, IComponent
        {
            var a = w.GetPool<TA>();
            var b = w.GetPool<TB>();
            var ents = w.Query<TA, TB>().ToList(); // 스냅샷
            foreach (var e in ents)
            {
                ref var ra = ref a.GetRef(e, out _);
                ref var rb = ref b.GetRef(e, out _);
                fn(e, ref ra, ref rb);
            }
        }
    }
}