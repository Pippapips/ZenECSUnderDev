#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using ZenECS.Core;
using ZenECS.Integration;

namespace ZenECS.Integration.Editor
{
    public sealed class EcsWorldViewerWindow : EditorWindow
    {
        [MenuItem("Window/ZenECS/World Viewer")]
        public static void Open()
        {
            var w = GetWindow<EcsWorldViewerWindow>();
            w.titleContent = new GUIContent("ZenECS World");
            w.Show();
        }

        enum SortMode
        {
            EntityIdAsc,
            EntityIdDesc,
            ComponentCountDesc,
            HasViewFirst
        }

        enum ViewMode
        {
            Entities,   // 엔티티 목록 중심(요구사항 기본)
            Components  // 컴포넌트 타입으로 그룹핑(옵션)
        }

        // ─────────────────────────────────────────────────────────────────────
        // 상태
        Vector2 _scrollLeft, _scrollRight, _scrollGroup;
        string _search = "";
        SortMode _sort = SortMode.EntityIdAsc;
        ViewMode _viewMode = ViewMode.Entities;
        bool _onlyWithView = false;
        bool _autoRefresh = true;

        // 선택 상태
        Entity? _selected;
        Type _selectedComponentForGroup;

        // 캐시
        double _lastRefresh;
        List<int> _cachedEntityIds = new List<int>();
        Dictionary<int, List<Type>> _cachedEntityTypes = new Dictionary<int, List<Type>>();
        Dictionary<Type, List<int>> _cachedTypeToEntities = new Dictionary<Type, List<int>>();

        // 스타일
        static float LINE => EditorGUIUtility.singleLineHeight;
        static float VSP  => EditorGUIUtility.standardVerticalSpacing;

        void OnEnable()
        {
            EditorApplication.update += AutoRepaintLoop;
        }

        void OnDisable()
        {
            EditorApplication.update -= AutoRepaintLoop;
        }

        void AutoRepaintLoop()
        {
            if (_autoRefresh)
            {
                // 5fps 정도로 리프레시
                if (EditorApplication.timeSinceStartup - _lastRefresh > 0.2)
                {
                    Repaint();
                    _lastRefresh = EditorApplication.timeSinceStartup;
                }
            }
        }

        void OnGUI()
        {
            // 상단 툴바
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _viewMode = (ViewMode)EditorGUILayout.EnumPopup(_viewMode, EditorStyles.toolbarPopup, GUILayout.Width(110));

                GUILayout.Space(6);
                GUILayout.Label("Search", GUILayout.Width(48));
                _search = GUILayout.TextField(_search,
                    GUI.skin.FindStyle("ToolbarSeachTextField") ?? EditorStyles.toolbarTextField,
                    GUILayout.MinWidth(160));

                GUILayout.Space(6);
                _sort = (SortMode)EditorGUILayout.EnumPopup(_sort, EditorStyles.toolbarPopup, GUILayout.Width(150));

                GUILayout.Space(6);
                _onlyWithView = GUILayout.Toggle(_onlyWithView, "With View", EditorStyles.toolbarButton, GUILayout.Width(72));

                GUILayout.FlexibleSpace();
                _autoRefresh = GUILayout.Toggle(_autoRefresh, "Auto", EditorStyles.toolbarButton, GUILayout.Width(48));
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))
                    RefreshCaches();
            }

            if (!WorldRuntimeLocator.TryGet(out var world) || world == null)
            {
                EditorGUILayout.HelpBox("World가 아직 준비되지 않았습니다. Play 후 다시 열어주세요.", MessageType.Info);
                return;
            }

            // 캐시 없으면 빌드
            if (_cachedEntityIds.Count == 0 && _cachedEntityTypes.Count == 0)
                RefreshCaches(world);

            var rect = position;
            float leftWidth = Mathf.Clamp(rect.width * 0.38f, 240, 520);

            using (new EditorGUILayout.HorizontalScope())
            {
                // 왼쪽: 엔티티 목록 or 컴포넌트 그룹
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(leftWidth)))
                {
                    if (_viewMode == ViewMode.Entities)
                        DrawEntityList(world);
                    else
                        DrawComponentGroups(world);
                }

                // 분리선
                EditorGUILayout.Separator();

                // 오른쪽: 상세
                using (new EditorGUILayout.VerticalScope())
                {
                    DrawRightDetails(world);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        void DrawEntityList(World world)
        {
            // 필터/정렬
            IEnumerable<int> seq = _cachedEntityIds;

            if (_onlyWithView)
            {
                var vp = GetPoolSafe(world, typeof(ViewComponent));
                if (vp != null)
                    seq = seq.Where(id => PoolHas(vp, new Entity(id)));
                else
                    seq = Enumerable.Empty<int>();
            }

            if (!string.IsNullOrEmpty(_search))
            {
                var s = _search.ToLowerInvariant();
                seq = seq.Where(id =>
                {
                    // id 매칭
                    if (id.ToString().Contains(s)) return true;

                    // 컴포넌트 타입명 매칭
                    if (_cachedEntityTypes.TryGetValue(id, out var types))
                    {
                        foreach (var t in types)
                        {
                            var name = ((t.Namespace ?? "") + "." + t.Name).ToLowerInvariant();
                            if (name.Contains(s)) return true;
                        }
                    }

                    // 뷰 이름 매칭
                    var go = TryGetViewGo(world, new Entity(id));
                    if (go != null && go.name.ToLowerInvariant().Contains(s)) return true;

                    return false;
                });
            }

            seq = _sort switch
            {
                SortMode.EntityIdAsc      => seq.OrderBy(i => i),
                SortMode.EntityIdDesc     => seq.OrderByDescending(i => i),
                SortMode.ComponentCountDesc=> seq.OrderByDescending(i => _cachedEntityTypes.TryGetValue(i, out var t) ? t.Count : 0),
                SortMode.HasViewFirst     => seq.OrderByDescending(i => TryGetViewGo(world, new Entity(i)) != null).ThenBy(i => i),
                _ => seq
            };

            _scrollLeft = EditorGUILayout.BeginScrollView(_scrollLeft);
            foreach (var id in seq)
            {
                using (new EditorGUILayout.HorizontalScope("box"))
                {
                    bool selected = _selected.HasValue && _selected.Value.Id == id;
                    var label = $"#{id}  ({(_cachedEntityTypes.TryGetValue(id, out var list)? list.Count : 0)} comps)";
                    if (GUILayout.Toggle(selected, label, "Button"))
                    {
                        if (!_selected.HasValue || _selected.Value.Id != id)
                        {
                            _selected = new Entity(id);
                            SelectInHierarchy(world, _selected.Value);
                        }
                    }

                    var go = TryGetViewGo(world, new Entity(id));
                    using (new EditorGUI.DisabledScope(go == null))
                    {
                        if (GUILayout.Button("Ping", GUILayout.Width(40)) && go != null)
                            EditorGUIUtility.PingObject(go);
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawComponentGroups(World world)
        {
            // 컴포넌트 타입 목록
            var groups = _cachedTypeToEntities.Keys.ToList();
            groups.Sort((a,b) =>
            {
                int c = string.Compare(a.Namespace ?? "", b.Namespace ?? "", StringComparison.Ordinal);
                if (c != 0) return c;
                return string.Compare(a.Name, b.Name, StringComparison.Ordinal);
            });

            if (!string.IsNullOrEmpty(_search))
            {
                var s = _search.ToLowerInvariant();
                groups = groups.Where(t => (((t.Namespace ?? "") + "." + t.Name).ToLowerInvariant().Contains(s))).ToList();
            }

            _scrollGroup = EditorGUILayout.BeginScrollView(_scrollGroup);
            foreach (var t in groups)
            {
                var list = _cachedTypeToEntities[t];
                if (_onlyWithView)
                {
                    list = list.Where(id => TryGetViewGo(world, new Entity(id)) != null).ToList();
                    if (list.Count == 0) continue;
                }

                using (new EditorGUILayout.VerticalScope("box"))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label($"{NiceName(t)}  ({list.Count})", EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Select Type", GUILayout.Width(96)))
                            _selectedComponentForGroup = t;
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        int lineCount = 0;
                        foreach (var id in list)
                        {
                            if (GUILayout.Button($"#{id}", EditorStyles.miniButton, GUILayout.Width(60)))
                            {
                                _selected = new Entity(id);
                                SelectInHierarchy(world, _selected.Value);
                            }
                            lineCount++;
                            if (lineCount % 8 == 0) GUILayout.FlexibleSpace();
                        }
                        GUILayout.FlexibleSpace();
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawRightDetails(World world)
        {
            if (!_selected.HasValue)
            {
                EditorGUILayout.HelpBox("좌측에서 Entity를 선택하세요.", MessageType.Info);
                return;
            }

            var e = _selected.Value;
            var comps = GetComponentsOf(world, e).ToArray();

            // 상단 바
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label($"Entity #{e.Id}", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                var go = TryGetViewGo(world, e);
                using (new EditorGUI.DisabledScope(go == null))
                {
                    if (GUILayout.Button("Select GO", EditorStyles.toolbarButton, GUILayout.Width(80)) && go != null)
                    {
                        Selection.activeObject = go;
                        EditorGUIUtility.PingObject(go);
                    }
                }

                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))
                    RefreshCaches(world);
            }

            _scrollRight = EditorGUILayout.BeginScrollView(_scrollRight);
            if (comps.Length == 0)
            {
                EditorGUILayout.HelpBox("부착된 컴포넌트가 없습니다.", MessageType.Info);
            }
            else
            {
                foreach (var t in comps)
                {
                    using (new EditorGUILayout.VerticalScope("box"))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Label(NiceName(t), EditorStyles.miniBoldLabel);
                            GUILayout.FlexibleSpace();
                            // 제거
                            if (GUILayout.Button("Remove", GUILayout.Width(72)))
                            {
                                RemoveComponent(world, e, t);
                                RefreshCaches(world);
                                break;
                            }
                        }

                        object inst = GetBoxed(world, e, t);
                        if (inst == null)
                        {
                            EditorGUILayout.LabelField("(null)");
                            continue;
                        }

                        // 간단 AutoForm
                        EditorGUI.BeginChangeCheck();
                        DrawAutoForm(t, inst);
                        if (EditorGUI.EndChangeCheck())
                        {
                            try { SetBoxed(world, e, t, inst); }
                            catch (Exception ex) { Debug.LogWarning($"[ZenECS] Set failed: {t.Name} - {ex.Message}"); }
                        }
                    }
                }
            }
            EditorGUILayout.EndScrollView();

            // 하단: 컴포넌트 추가
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Add Component", GUILayout.Width(140)))
                {
                    var r = GUILayoutUtility.GetLastRect();
                    var screenRect = GUIToScreenRect(r);
                    EditorApplication.delayCall += () =>
                    {
                        ComponentPickerPopup.Open(
                            screenRect,
                            picked =>
                            {
                                if (picked == null) return;
                                if (EcsEntityViewInspector.WorldBridge.EntityHas(world, e, picked))
                                {
                                    EditorUtility.DisplayDialog("중복", $"'{picked.FullName}' 는 이미 부착되어 있습니다.", "OK");
                                    return;
                                }
                                var boxed = Activator.CreateInstance(picked);
                                SetBoxed(world, e, picked, boxed);
                                RefreshCaches(world);
                            },
                            isDisabled: (Type t) => EcsEntityViewInspector.WorldBridge.EntityHas(world, e, t),
                            disabledHint: " (이미 있음)"
                        );
                    };
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        void RefreshCaches(World world = null)
        {
            _cachedEntityIds.Clear();
            _cachedEntityTypes.Clear();
            _cachedTypeToEntities.Clear();

            if (world == null && !WorldRuntimeLocator.TryGet(out world))
                return;

            // 모든 엔티티 수집 (모든 풀의 엔티티 유니온)
            var all = new HashSet<int>();
            foreach (var pool in GetAllPools(world))
                foreach (var id in PoolAllEntities(pool))
                    all.Add(id);

            _cachedEntityIds.AddRange(all);
            _cachedEntityIds.Sort();

            // 엔티티 → 타입들
            foreach (var id in _cachedEntityIds)
            {
                var list = new List<Type>();
                var e = new Entity(id);
                foreach (var pool in GetAllPools(world))
                {
                    var t = pool.GetType().GetGenericArguments()[0];
                    if (PoolHas(pool, e)) list.Add(t);
                }
                _cachedEntityTypes[id] = list;
            }

            // 타입 → 엔티티들
            foreach (var id in _cachedEntityIds)
            {
                if (_cachedEntityTypes.TryGetValue(id, out var types))
                {
                    foreach (var t in types)
                    {
                        if (!_cachedTypeToEntities.TryGetValue(t, out var list))
                            _cachedTypeToEntities[t] = list = new List<int>();
                        list.Add(id);
                    }
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 오른쪽 상세용 폼 드로어(간단형)
        static void DrawAutoForm(Type t, object box)
        {
            var flags = BindingFlags.Public | BindingFlags.Instance;
            foreach (var f in t.GetFields(flags))
            {
                var ft = f.FieldType;
                object val = f.GetValue(box);

                if      (ft == typeof(int))      val = EditorGUILayout.IntField(f.Name, (int)val);
                else if (ft == typeof(float))    val = EditorGUILayout.FloatField(f.Name, (float)val);
                else if (ft == typeof(bool))     val = EditorGUILayout.Toggle(f.Name, (bool)val);
                else if (ft == typeof(string))   val = EditorGUILayout.TextField(f.Name, (string)val ?? "");
                else if (ft == typeof(Vector2))  val = EditorGUILayout.Vector2Field(f.Name, (Vector2)val);
                else if (ft == typeof(Vector3))  val = EditorGUILayout.Vector3Field(f.Name, (Vector3)val);
                else if (ft == typeof(Vector4))  val = EditorGUILayout.Vector4Field(f.Name, (Vector4)val);
                else if (ft == typeof(Quaternion))
                {
                    var q = (Quaternion)val;
                    var eul = EditorGUILayout.Vector3Field(f.Name + " (Euler)", q.eulerAngles);
                    val = Quaternion.Euler(eul);
                }
                else if (ft == typeof(Color))    val = EditorGUILayout.ColorField(f.Name, (Color)val);
                else
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.TextField(f.Name, val != null ? val.ToString() : "null");
                    continue;
                }

                f.SetValue(box, val);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 리플렉션 브리지 & 유틸
        static IEnumerable<object> GetAllPools(World w)
        {
            // World 내부 private: Dictionary<Type, object> _pools
            var fi = typeof(World).GetField("_pools", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fi != null)
            {
                var dict = fi.GetValue(w) as System.Collections.IDictionary;
                if (dict != null)
                {
                    foreach (System.Collections.DictionaryEntry kv in dict)
                        yield return kv.Value;
                    yield break;
                }
            }
            // 대안: GetPools 메서드가 있으면 사용
            var mi = typeof(World).GetMethod("GetAllPools", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi != null)
            {
                var arr = mi.Invoke(w, null) as System.Collections.IEnumerable;
                if (arr != null) foreach (var p in arr) yield return p;
            }
        }

        static IEnumerable<int> PoolAllEntities(object poolObj)
        {
            var mi = poolObj.GetType().GetMethod("AllEntities");
            if (mi == null) yield break;
            var enumerable = mi.Invoke(poolObj, null) as System.Collections.IEnumerable;
            if (enumerable == null) yield break;
            foreach (var e in enumerable)
            {
                if (e is Entity ent) yield return ent.Id;
            }
        }

        static bool PoolHas(object poolObj, Entity e)
        {
            var mi = poolObj.GetType().GetMethod("Has");
            if (mi == null) return false;
            return (bool)mi.Invoke(poolObj, new object[] { e });
        }

        static object GetPoolSafe(World w, Type t)
        {
            var mi = typeof(World).GetMethod("GetPool").MakeGenericMethod(t);
            try { return mi.Invoke(w, null); } catch { return null; }
        }

        static IEnumerable<Type> GetComponentsOf(World w, Entity e)
        {
            foreach (var pool in GetAllPools(w))
            {
                if (PoolHas(pool, e))
                    yield return pool.GetType().GetGenericArguments()[0];
            }
        }

        static object GetBoxed(World w, Entity e, Type t)
        {
            var pool = GetPoolSafe(w, t);
            if (pool == null) return null;
            var mi = pool.GetType().GetMethod("Get");
            return mi.Invoke(pool, new object[] { e });
        }

        static void SetBoxed(World w, Entity e, Type t, object boxed)
        {
            var mi = typeof(World).GetMethod("AddOrSet").MakeGenericMethod(t);
            mi.Invoke(w, new object[] { e, boxed });
        }

        static void RemoveComponent(World w, Entity e, Type t)
        {
            var pool = GetPoolSafe(w, t);
            if (pool == null) return;
            var mi = pool.GetType().GetMethod("Remove");
            mi.Invoke(pool, new object[] { e });
        }

        static GameObject TryGetViewGo(World w, Entity e)
        {
            var pool = GetPoolSafe(w, typeof(ViewComponent));
            if (pool == null) return null;
            if (!PoolHas(pool, e)) return null;
            var miGet = pool.GetType().GetMethod("Get");
            var vc = miGet.Invoke(pool, new object[] { e });
            if (vc == null) return null;
            var fi = vc.GetType().GetField("Instance", BindingFlags.Public | BindingFlags.Instance);
            return fi?.GetValue(vc) as GameObject;
        }

        static void SelectInHierarchy(World w, Entity e)
        {
            var go = TryGetViewGo(w, e);
            if (go != null)
            {
                Selection.activeObject = go;
                EditorGUIUtility.PingObject(go);
            }
        }

        static string NiceName(Type t)
        {
            var ns = string.IsNullOrEmpty(t.Namespace) ? "Global" : t.Namespace;
            return $"{ns}.{t.Name}";
        }

        static Rect GUIToScreenRect(Rect r)
        {
            var p = GUIUtility.GUIToScreenPoint(new Vector2(r.x, r.y));
            return new Rect(p.x, p.y, r.width, r.height);
        }

        // 간단 피커 (추가 버튼 전용)
        sealed class ComponentPickerPopup : EditorWindow
        {
            Action<Type> _onPick;
            string _search = "";
            Vector2 _scroll;
            Func<Type, bool> _isDisabled;
            string _disabledHint = " (이미 있음)";

            public static void Open(Rect screenRect, Action<Type> onPick, Func<Type,bool> isDisabled, string disabledHint)
            {
                var w = CreateInstance<ComponentPickerPopup>();
                w._onPick = onPick;
                w._isDisabled = isDisabled;
                w._disabledHint = disabledHint ?? " (이미 있음)";
                w.wantsMouseMove = true;
                w.ShowAsDropDown(screenRect, new Vector2(Mathf.Max(420, screenRect.width + 200), 480));
                w.Focus();
            }

            void OnGUI()
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    GUILayout.Label("Add Component", GUILayout.Width(100));
                    _search = GUILayout.TextField(_search,
                        GUI.skin.FindStyle("ToolbarSeachTextField") ?? EditorStyles.toolbarTextField,
                        GUILayout.MinWidth(160));
                    GUILayout.FlexibleSpace();
                }

                IEnumerable<Type> seq = ComponentCatalog.All;
                if (!string.IsNullOrEmpty(_search))
                {
                    var s = _search.ToLowerInvariant();
                    seq = seq.Where(t => (((t.Namespace ?? "") + "." + t.Name).ToLowerInvariant().Contains(s)));
                }

                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                foreach (var t in seq)
                {
                    bool disabled = _isDisabled != null && _isDisabled(t);
                    using (new EditorGUI.DisabledScope(disabled))
                    {
                        var label = ((t.Namespace ?? "") + "." + t.Name) + (disabled ? _disabledHint : "");
                        if (GUILayout.Button(label, EditorStyles.miniButton))
                        {
                            if (!disabled) { _onPick?.Invoke(t); Close(); }
                        }
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        static class ComponentCatalog
        {
            static Type Marker = typeof(IComponent);
            static List<Type> _cache;

            public static IEnumerable<Type> All
            {
                get
                {
                    if (_cache != null) return _cache;
                    _cache = new List<Type>(256);
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        Type[] types; try { types = asm.GetTypes(); } catch { continue; }
                        foreach (var t in types)
                            if (t.IsValueType && !t.IsPrimitive && Marker.IsAssignableFrom(t))
                                _cache.Add(t);
                    }
                    _cache.Sort((a, b) =>
                    {
                        var an = string.IsNullOrEmpty(a.Namespace) ? "" : a.Namespace;
                        var bn = string.IsNullOrEmpty(b.Namespace) ? "" : b.Namespace;
                        int c = string.Compare(an, bn, StringComparison.Ordinal);
                        if (c != 0) return c;
                        return string.Compare(a.Name, b.Name, StringComparison.Ordinal);
                    });
                    return _cache;
                }
            }
        }
    }
}
#endif
