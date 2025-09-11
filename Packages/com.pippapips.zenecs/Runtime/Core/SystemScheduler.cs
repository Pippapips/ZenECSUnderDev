using System;
using System.Collections.Generic;
using System.Linq;

namespace ZenECS.Core
{
    public static class SystemScheduler
    {
        public sealed class Plan
        {
            public readonly Dictionary<Type, List<object>> Ordered = new();
            public readonly List<string> Errors = new();
            public readonly List<string> Warnings = new();
        }

        public static Plan Build(IEnumerable<object> systems)
        {
            var plan = new Plan();
            var byGroup = new Dictionary<Type, List<Type>>();
            var typeToObj = new Dictionary<Type, object>();

            foreach (var s in systems)
            {
                var t = s.GetType();
                typeToObj[t] = s;
                var g = t.GetCustomAttributes(typeof(UpdateInGroupAttribute), false)
                         .Cast<UpdateInGroupAttribute>().FirstOrDefault()?.GroupType
                         ?? typeof(SimulationSystemGroup);
                if (!byGroup.TryGetValue(g, out var list)) byGroup[g] = list = new List<Type>();
                list.Add(t);
            }

            foreach (var group in byGroup.Keys.ToArray())
            {
                var types = byGroup[group];
                var edges = new List<(Type from, Type to)>();
                var score = new Dictionary<Type, int>(); // Order + SubGroup hash
                foreach (var t in types)
                {
                    var ord = t.GetCustomAttributes(typeof(OrderAttribute), false).Cast<OrderAttribute>().FirstOrDefault();
                    var o = ord?.Order ?? 0;
                    if (ord?.SubGroup != null) o = unchecked(o * 397 + ord.SubGroup.GetHashCode());
                    score[t] = o;

                    foreach (var bef in t.GetCustomAttributes(typeof(OrderBeforeAttribute), false).Cast<OrderBeforeAttribute>())
                    foreach (var target in bef.Targets ?? Array.Empty<Type>())
                    {
                        CheckCrossGroup(t, target, group, byGroup, plan);
                        if (types.Contains(target)) edges.Add((t, target));
                    }
                    foreach (var aft in t.GetCustomAttributes(typeof(OrderAfterAttribute), false).Cast<OrderAfterAttribute>())
                    foreach (var target in aft.Targets ?? Array.Empty<Type>())
                    {
                        CheckCrossGroup(t, target, group, byGroup, plan);
                        if (types.Contains(target)) edges.Add((target, t));
                    }
                }

                var sorted = TopoSort(types, edges, score, plan);
                plan.Ordered[group] = sorted.Select(tt => typeToObj[tt]).ToList();
            }

            return plan;
        }

        static void CheckCrossGroup(Type src, Type dst, Type thisGroup,
                                    Dictionary<Type,List<Type>> groups, Plan plan)
        {
            foreach (var kv in groups)
            {
                if (kv.Value.Contains(dst) && kv.Key != thisGroup)
                {
                    plan.Errors.Add($"[ZenECS] Cross-group dependency forbidden: {src.Name} -> {dst.Name} ({thisGroup.Name} → {kv.Key.Name})");
                }
            }
        }

        static List<Type> TopoSort(List<Type> nodes, List<(Type from, Type to)> edges,
                                   Dictionary<Type,int> score, Plan plan)
        {
            var indeg = nodes.ToDictionary(n => n, _ => 0);
            foreach (var e in edges) indeg[e.to]++;

            var q = new List<Type>(nodes.Where(n => indeg[n]==0).OrderBy(n => score.TryGetValue(n,out var o)?o:0));
            var outEdges = nodes.ToDictionary(n => n, _ => new List<Type>());
            foreach (var e in edges) outEdges[e.from].Add(e.to);

            var result = new List<Type>();
            while (q.Count>0)
            {
                var n = q[0]; q.RemoveAt(0);
                result.Add(n);
                foreach (var m in outEdges[n])
                {
                    indeg[m]--;
                    if (indeg[m]==0) q.Add(m);
                }
                q.Sort((a,b) => (score[a]).CompareTo(score[b]));
            }

            if (result.Count != nodes.Count)
            {
                var cyc = string.Join(" ", nodes.Where(n => indeg[n]>0).Select(n=>$"{n.Name}(indeg={indeg[n]})"));
                plan.Errors.Add($"[ZenECS] Cycle detected in group: {cyc}");
                // fallback: stable by score
                return nodes.OrderBy(n => score[n]).ToList();
            }
            return result;
        }
    }
}
