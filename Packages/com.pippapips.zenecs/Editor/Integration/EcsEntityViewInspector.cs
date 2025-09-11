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
    [CustomEditor(typeof(EcsEntityView))]
    public sealed class EcsEntityViewInspector : UnityEditor.Editor
    {
        const string PREF_GROUP_FILTER = "ZenECS.EntityView.GroupFilter";
        const string PREF_DISALLOW_DUP = "ZenECS.EntityView.DisallowDuplicates";

        string _groupFilter;
        bool _disallowDup;

        Vector2 _scroll;
        FavoritesStore _favs;
        RecentsStore _recents;

        void OnEnable()
        {
            _groupFilter = EditorPrefs.GetString(PREF_GROUP_FILTER, "(All)");
            _disallowDup = EditorPrefs.GetBool(PREF_DISALLOW_DUP, true);
            _favs = FavoritesStore.Load();
            _recents = RecentsStore.Load();

            // 실시간 갱신
            EditorApplication.update += Repaint;
        }

        void OnDisable()
        {
            EditorApplication.update -= Repaint;
        }

        public override void OnInspectorGUI()
        {
            var view = (EcsEntityView)target;

            if (!WorldRuntimeLocator.TryGet(out var world) || world == null)
            {
                EditorGUILayout.HelpBox("World가 아직 준비되지 않았습니다. (Play 시작 후 자동 연결)", MessageType.Info);
                return;
            }

            if (!TryGetEntity(view, out var e))
            {
                EditorGUILayout.HelpBox("이 View에 연결된 Entity를 찾지 못했습니다. (Attach 호출 여부 확인)", MessageType.Warning);
                return;
            }

            // 헤더
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label($"Entity #{e.Id}", EditorStyles.boldLabel);

                GUILayout.Space(8);
                var groups = ComponentCatalog.GroupsCached;
                int sel = Mathf.Max(0, Array.IndexOf(groups, _groupFilter));
                GUILayout.Label("그룹", GUILayout.Width(32));
                int newSel = EditorGUILayout.Popup(sel < 0 ? 0 : sel, groups, GUILayout.MaxWidth(180));
                if (newSel != sel)
                {
                    _groupFilter = groups[newSel];
                    EditorPrefs.SetString(PREF_GROUP_FILTER, _groupFilter);
                }

                GUILayout.Space(8);
                var dupNew = GUILayout.Toggle(_disallowDup, new GUIContent("중복 금지", "같은 컴포넌트를 중복 추가하지 않음"),
                    EditorStyles.toolbarButton, GUILayout.Width(78));
                if (dupNew != _disallowDup)
                {
                    _disallowDup = dupNew;
                    EditorPrefs.SetBool(PREF_DISALLOW_DUP, _disallowDup);
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("추가", EditorStyles.toolbarButton, GUILayout.Width(56)))
                {
                    var btnRect = GUILayoutUtility.GetLastRect();
                    var screenRect = GUIToScreenRect(btnRect);
                    EditorApplication.delayCall += () =>
                    {
                        ComponentPickerPopupEV.Open(
                            screenRect,
                            onPick: picked =>
                            {
                                if (picked == null) return;
                                if (_disallowDup && WorldBridge.EntityHas(world, e, picked))
                                {
                                    EditorUtility.DisplayDialog("중복", $"'{picked.FullName}' 는 이미 추가되어 있습니다.", "OK");
                                    return;
                                }

                                //var boxed = Activator.CreateInstance(picked);
                                var boxed = ComponentDefaults.CreateWithDefaults(picked);
                                WorldBridge.SetBoxed(world, e, picked, boxed);
                                _recents.Push(picked);
                                _recents.Save();
                            },
                            filterSearch: "",
                            groupFilter: _groupFilter,
                            favoritesOnly: false,
                            recentsOnly: false,
                            favs: _favs,
                            recents: _recents,
                            isDisabled: (Type t) => _disallowDup && WorldBridge.EntityHas(world, e, t),
                            disabledHint: " (이미 추가됨)"
                        );
                    };
                }
            }

            // 현재 부착된 컴포넌트 나열
            var attached = GetAttachedComponentTypes(world, e, _groupFilter).ToArray();
            if (attached.Length == 0)
            {
                EditorGUILayout.HelpBox("부착된 컴포넌트가 없습니다. 상단의 [추가]로 컴포넌트를 붙여보세요.", MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var t in attached)
            {
                DrawComponentCard(world, e, t);
            }

            EditorGUILayout.EndScrollView();
        }

        void DrawComponentCard(World world, Entity e, Type t)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(NiceName(t), EditorStyles.miniBoldLabel);
                    GUILayout.FlexibleSpace();
                    var fav = _favs.Contains(t);
                    var nfav = GUILayout.Toggle(fav, new GUIContent("★", "즐겨찾기 전환"), EditorStyles.miniButton,
                        GUILayout.Width(24));
                    if (nfav != fav)
                    {
                        if (nfav) _favs.Add(t);
                        else _favs.Remove(t);
                        _favs.Save();
                    }

                    if (GUILayout.Button("제거", GUILayout.Width(56)))
                    {
                        if (EditorUtility.DisplayDialog("제거", $"{t.FullName}을(를) 제거할까요?", "Remove", "Cancel"))
                        {
                            WorldBridge.Remove(world, e, t);
                            GUI.FocusControl(null);
                            Repaint();
                            return;
                        }
                    }
                }

                // 값 편집 (AutoForm)
                object instance = WorldBridge.GetBoxed(world, e, t);
                EditorGUI.BeginChangeCheck();
                DrawAutoForm(t, instance);
                if (EditorGUI.EndChangeCheck())
                {
                    try
                    {
                        WorldBridge.SetBoxed(world, e, t, instance);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[ZenECS] Set failed: {t.Name} - {ex.Message}");
                    }
                }
            }
        }

        static IEnumerable<Type> GetAttachedComponentTypes(World w, Entity e, string groupFilter)
        {
            IEnumerable<Type> seq = ComponentCatalog.All;
            if (!string.IsNullOrEmpty(groupFilter) && groupFilter != "(All)")
            {
                seq = seq.Where(t =>
                {
                    var ns = string.IsNullOrEmpty(t.Namespace) ? "Global" : t.Namespace;
                    var grp = ns.Split('.').Last();
                    return grp == groupFilter;
                });
            }

            foreach (var t in seq)
                if (WorldBridge.EntityHas(w, e, t))
                    yield return t;
        }

        // ────────────── Helpers ──────────────
        static Rect GUIToScreenRect(Rect r)
        {
            // 인스펙터 로컬 → 스크린
            var p = GUIUtility.GUIToScreenPoint(new Vector2(r.x, r.y));
            return new Rect(p.x, p.y, r.width, r.height);
        }

        static bool TryGetEntity(EcsEntityView view, out Entity e)
        {
            // 1) public prop Entity
            var pi = typeof(EcsEntityView).GetProperty("Entity", BindingFlags.Public | BindingFlags.Instance);
            if (pi != null && pi.PropertyType == typeof(Entity))
            {
                e = (Entity)pi.GetValue(view);
                return e.Id > 0;
            }

            // 2) private field _entity
            var fi = typeof(EcsEntityView).GetField("_entity", BindingFlags.NonPublic | BindingFlags.Instance);
            if (fi != null && fi.FieldType == typeof(Entity))
            {
                e = (Entity)fi.GetValue(view);
                return e.Id > 0;
            }

            // 3) private field _entityId
            var fiId = typeof(EcsEntityView).GetField("_entityId", BindingFlags.NonPublic | BindingFlags.Instance);
            if (fiId != null && fiId.FieldType == typeof(int))
            {
                var id = (int)fiId.GetValue(view);
                e = new Entity(id);
                return id > 0;
            }

            e = default;
            return false;
        }

        static string NiceName(Type t)
        {
            var ns = string.IsNullOrEmpty(t.Namespace) ? "Global" : t.Namespace;
            return $"{ns}.{t.Name}";
        }

        static void DrawAutoForm(Type t, object box)
        {
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var ft = f.FieldType;
                object val = f.GetValue(box);

                if (ft == typeof(int)) val = EditorGUILayout.IntField(f.Name, (int)val);
                else if (ft == typeof(float)) val = EditorGUILayout.FloatField(f.Name, (float)val);
                else if (ft == typeof(bool)) val = EditorGUILayout.Toggle(f.Name, (bool)val);
                else if (ft == typeof(string)) val = EditorGUILayout.TextField(f.Name, (string)val ?? "");
                else if (ft == typeof(Vector2)) val = EditorGUILayout.Vector2Field(f.Name, (Vector2)val);
                else if (ft == typeof(Vector3)) val = EditorGUILayout.Vector3Field(f.Name, (Vector3)val);
                else if (ft == typeof(Vector4)) val = EditorGUILayout.Vector4Field(f.Name, (Vector4)val);
                else if (ft == typeof(Quaternion))
                {
                    var q = (Quaternion)val;
                    var eul = EditorGUILayout.Vector3Field(f.Name + " (Euler)", q.eulerAngles);
                    val = Quaternion.Euler(eul);
                }
                else if (ft == typeof(Color)) val = EditorGUILayout.ColorField(f.Name, (Color)val);
                else
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.TextField(f.Name, val != null ? val.ToString() : "null");
                    continue;
                }

                f.SetValue(box, val);
            }
        }

        // ────────────── Reflection Bridge ──────────────
        public static class WorldBridge
        {
            static readonly MethodInfo MI_GetPool = typeof(World).GetMethod("GetPool");
            static readonly MethodInfo MI_AddOrSet = typeof(World).GetMethod("AddOrSet");

            static object GetPool(World w, Type t)
                => MI_GetPool.MakeGenericMethod(t).Invoke(w, null);

            public static bool EntityHas(World w, Entity e, Type t)
            {
                var pool = GetPool(w, t);
                var miHas = pool.GetType().GetMethod("Has");
                return (bool)miHas.Invoke(pool, new object[] { e });
            }

            public static object GetBoxed(World w, Entity e, Type t)
            {
                var pool = GetPool(w, t);
                var miGet = pool.GetType().GetMethod("Get");
                return miGet.Invoke(pool, new object[] { e });
            }

            public static void SetBoxed(World w, Entity e, Type t, object boxed)
            {
                MI_AddOrSet.MakeGenericMethod(t).Invoke(w, new object[] { e, boxed });
            }

            public static bool Remove(World w, Entity e, Type t)
            {
                var pool = GetPool(w, t);
                var mi = pool.GetType().GetMethod("Remove");
                return (bool)mi.Invoke(pool, new object[] { e });
            }
        }

        // ────────────── 타입 카탈로그 / 즐겨찾기 / 최근 / 피커 ──────────────
        static class ComponentCatalog
        {
            static Type Marker = typeof(IComponent);
            static List<Type> _cache;
            static string[] _groups;

            public static IEnumerable<Type> All
            {
                get
                {
                    if (_cache != null) return _cache;
                    _cache = new List<Type>(256);
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
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

            public static string[] GroupsCached
            {
                get
                {
                    if (_groups != null) return _groups;
                    var set = new HashSet<string> { "(All)" };
                    foreach (var t in All)
                    {
                        var ns = string.IsNullOrEmpty(t.Namespace) ? "Global" : t.Namespace;
                        var grp = ns.Split('.').Last();
                        set.Add(grp);
                    }

                    _groups = set.ToArray();
                    Array.Sort(_groups, StringComparer.Ordinal);
                    return _groups;
                }
            }
        }

        sealed class FavoritesStore
        {
            const string PREF = "ZenECS.EntityView.Favorites";
            readonly HashSet<string> _set = new HashSet<string>();

            public bool Contains(Type t) => t != null && _set.Contains(t.AssemblyQualifiedName);

            public void Add(Type t)
            {
                if (t != null) _set.Add(t.AssemblyQualifiedName);
            }

            public void Remove(Type t)
            {
                if (t != null) _set.Remove(t.AssemblyQualifiedName);
            }

            public void Save() => EditorPrefs.SetString(PREF, string.Join("|", _set));

            public static FavoritesStore Load()
            {
                var s = new FavoritesStore();
                var raw = EditorPrefs.GetString(PREF, "");
                foreach (var token in raw.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                    s._set.Add(token);
                return s;
            }

            // ★ 추가 1: 즐겨찾기 목록을 Type 열거로 반환
            public IEnumerable<Type> Types()
            {
                foreach (var aqn in _set)
                {
                    var t = Resolve(aqn);
                    if (t != null) yield return t;
                }
            }

            // ★ 추가 2: AQN → Type 복원 헬퍼
            static Type Resolve(string typeName)
            {
                if (string.IsNullOrEmpty(typeName)) return null;
                var t = Type.GetType(typeName, false);
                if (t != null) return t;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        t = asm.GetType(typeName, false);
                        if (t != null) return t;
                    }
                    catch
                    {
                    }
                }

                return null;
            }
        }

        sealed class RecentsStore
        {
            const string PREF = "ZenECS.EntityView.Recents";
            const int MAX = 20;
            readonly LinkedList<string> _list = new LinkedList<string>();

            public void Push(Type t)
            {
                if (t == null) return;
                var key = t.AssemblyQualifiedName;
                var node = _list.Find(key);
                if (node != null) _list.Remove(node);
                _list.AddFirst(key);
                while (_list.Count > MAX) _list.RemoveLast();
            }

            public void Save() => EditorPrefs.SetString(PREF, string.Join("|", _list));
            public static RecentsStore Load()
            {
                var s = new RecentsStore();
                var raw = EditorPrefs.GetString(PREF, "");
                foreach (var token in raw.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                    s._list.AddLast(token);
                return s;
            }

            // ★ 추가: 최근 목록을 Type 시퀀스로 반환 (yield는 try/catch 밖에서만)
            public IEnumerable<Type> Types()
            {
                foreach (var aqn in _list)
                {
                    var t = ResolveTypeSafe(aqn);
                    if (t != null) yield return t;
                }
            }

            static Type ResolveTypeSafe(string typeName)
            {
                if (string.IsNullOrEmpty(typeName)) return null;

                // 1) 직접
                var t = Type.GetType(typeName, false);
                if (t != null) return t;

                // 2) 어셈블리 순회 (try는 여기서만, yield 없음)
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        t = asm.GetType(typeName, throwOnError: false);
                        if (t != null) return t;
                    }
                    catch { /* ignore */ }
                }
                return null;
            }
        }

        sealed class ComponentPickerPopupEV : EditorWindow
        {
            Action<Type> _onPick;
            string _search;
            string _group;
            bool _favOnly;
            bool _recOnly;
            FavoritesStore _favs;
            RecentsStore _recents;
            Vector2 _scroll;
            Func<Type, bool> _isDisabled;
            string _disabledHint = " (이미 추가됨)";

            public static void Open(Rect screenRect,
                Action<Type> onPick,
                string filterSearch,
                string groupFilter,
                bool favoritesOnly,
                bool recentsOnly,
                FavoritesStore favs,
                RecentsStore recents,
                Func<Type, bool> isDisabled = null,
                string disabledHint = " (이미 추가됨)")
            {
                var w = CreateInstance<ComponentPickerPopupEV>();
                w._onPick = onPick;
                w._search = filterSearch ?? "";
                w._group = groupFilter ?? "(All)";
                w._favOnly = favoritesOnly;
                w._recOnly = recentsOnly;
                w._favs = favs;
                w._recents = recents;
                w._isDisabled = isDisabled;
                w._disabledHint = disabledHint ?? " (이미 추가됨)";
                w.wantsMouseMove = true;
                w.ShowAsDropDown(screenRect, new Vector2(Mathf.Max(380, screenRect.width + 160), 420));
                w.Focus();
            }

            void OnGUI()
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    _search = GUILayout.TextField(_search,
                        GUI.skin.FindStyle("ToolbarSeachTextField") ?? EditorStyles.toolbarTextField,
                        GUILayout.MinWidth(140));
                    GUILayout.FlexibleSpace();
                    _favOnly = GUILayout.Toggle(_favOnly, "즐겨찾기", EditorStyles.toolbarButton, GUILayout.Width(72));
                    _recOnly = GUILayout.Toggle(_recOnly, "최근", EditorStyles.toolbarButton, GUILayout.Width(56));
                }

                IEnumerable<Type> seq;
                if (_favOnly) seq = _favs.Types();
                else if (_recOnly) seq = _recents.Types();
                else seq = ComponentCatalog.All;

                if (!string.IsNullOrEmpty(_group) && _group != "(All)")
                {
                    seq = seq.Where(t =>
                    {
                        var ns = string.IsNullOrEmpty(t.Namespace) ? "Global" : t.Namespace;
                        var grp = ns.Split('.').Last();
                        return grp == _group;
                    });
                }

                if (!string.IsNullOrEmpty(_search))
                {
                    var s = _search.ToLowerInvariant();
                    seq = seq.Where(t =>
                    {
                        var full = (t.Namespace ?? "") + "." + t.Name;
                        return full.ToLowerInvariant().Contains(s);
                    });
                }

                using (var sv = new EditorGUILayout.ScrollViewScope(_scroll))
                {
                    _scroll = sv.scrollPosition;
                    foreach (var t in seq)
                    {
                        bool disabled = _isDisabled != null && _isDisabled(t);
                        using (new EditorGUI.DisabledScope(disabled))
                        {
                            var label = NiceName(t) + (disabled ? _disabledHint : "");
                            if (GUILayout.Button(label, EditorStyles.miniButton))
                            {
                                if (!disabled)
                                {
                                    _onPick?.Invoke(t);
                                    Close();
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
#endif