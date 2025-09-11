#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using ZenECS.Core;
using ZenECS.Integration;

namespace ZenECS.Integration.Editor
{
    [CustomEditor(typeof(EntityBlueprint))]
    public sealed class EntityBlueprintInspector : UnityEditor.Editor
    {
        ReorderableList _list;
        SerializedProperty _components;

        const string PREF_DISALLOW_DUP = "ZenECS.Blueprint.DisallowDuplicates";
        const string PREF_GROUP_FILTER = "ZenECS.Blueprint.GroupFilter";

        bool _disallowDup;
        string _groupFilter;

        FavoritesStore _favStore;
        RecentsStore _recents;

        // 파일 상단 클래스 안에 추가
        float LINE;
        float VSP;

        // AutoForm가 실제로 그리는 높이를 타입 기준으로 계산
        float GetAutoFormHeight(Type t)
        {
            float h = 0f;
            var flags = BindingFlags.Public | BindingFlags.Instance;
            foreach (var f in t.GetFields(flags))
            {
                var ft = f.FieldType;
                h += GetFieldHeight(ft) + VSP;
            }
            return Mathf.Max(h, LINE); // 최소 한 줄
        }

        float GetFieldHeight(Type ft)
        {
            // 우리가 AutoForm에서 사용하는 그리기 방식과 1:1로 맞춤
            if (ft == typeof(int) || ft == typeof(float) || ft == typeof(bool) ||
                ft == typeof(string) || ft == typeof(Color))
                return LINE;

            if (ft == typeof(Vector2) || ft == typeof(Vector3) || ft == typeof(Vector4))
                return LINE; // Vector 필드도 한 줄 높이(라벨 포함)로 계산

            if (ft == typeof(Quaternion))
                return LINE; // Euler(Vector3)로 한 줄 그려서 높이 1줄로 맞춤

            // 기타/미지원 타입은 텍스트 1줄
            return LINE;
        }

        // 인스펙터 요소(헤더 + 본문) 총 높이 계산
        float CalcElementHeight(SerializedProperty listProp, int index)
        {
            // 헤더(라벨/버튼들) 한 줄
            float h = LINE + VSP * 2f;

            // 해당 드래프트/블루프린트 항목의 타입
            var el = listProp.GetArrayElementAtIndex(index);
            var typeProp = el.FindPropertyRelative("TypeName");
            var t = Resolve(typeProp.stringValue);

            if (t != null)
            {
                // AutoForm 바디 높이
                h += GetAutoFormHeight(t) + VSP * 2f;
            }
            else
            {
                // 타입 미선택일 때 Raw JSON 2줄 정도
                h += LINE * 2f + VSP * 2f;
            }
            // 약간의 여백
            return h + 6f;
        }
        
        void OnEnable()
        {
            LINE = EditorGUIUtility.singleLineHeight;
            VSP  = EditorGUIUtility.standardVerticalSpacing;
            
            _components = serializedObject.FindProperty("_components");

            _disallowDup = EditorPrefs.GetBool(PREF_DISALLOW_DUP, true);
            _groupFilter = EditorPrefs.GetString(PREF_GROUP_FILTER, "(All)");

            _favStore = FavoritesStore.Load();
            _recents  = RecentsStore.Load();

            _list = new ReorderableList(serializedObject, _components, true, true, true, true);
            _list.drawHeaderCallback = DrawHeader;
            _list.drawElementCallback = DrawElement;
            _list.elementHeightCallback = i => CalcElementHeight(_components, i);

            _list.onAddDropdownCallback = (buttonRect, list) =>
            {
                var screenRect = GUIToScreenRect(buttonRect);
                EditorApplication.delayCall += () =>
                {
                    ComponentPickerPopup.Open(
                        screenRect,
                        picked =>
                        {
                            if (picked == null) return;
                            if (_disallowDup && HasDup(picked, -1))
                            {
                                EditorUtility.DisplayDialog("중복", $"'{picked.FullName}' 는 이미 추가되어 있습니다.", "OK");
                                return;
                            }
                            _components.arraySize++;
                            var el = _components.GetArrayElementAtIndex(_components.arraySize - 1);
                            el.FindPropertyRelative("TypeName").stringValue = picked.AssemblyQualifiedName;
                            //el.FindPropertyRelative("Json").stringValue = JsonUtility.ToJson(Activator.CreateInstance(picked));
                            var defJson = ComponentDefaults.GetDefaultJson(picked);
                            el.FindPropertyRelative("Json").stringValue =
                                !string.IsNullOrEmpty(defJson) ? defJson : JsonUtility.ToJson(Activator.CreateInstance(picked));                            
                            serializedObject.ApplyModifiedProperties();
                            _recents.Push(picked); _recents.Save();
                        },
                        filterSearch: "",
                        groupFilter: _groupFilter,
                        favoritesOnly: false,
                        recentsOnly: false,
                        favs: _favStore,
                        recents: _recents,
                        isDisabled: (Type t) => _disallowDup && HasDup(t, -1),
                        disabledHint: " (이미 추가됨)"
                    );
                };
            };

            _list.onRemoveCallback = list =>
            {
                if (list.index >= 0 && list.index < _components.arraySize)
                {
                    _components.DeleteArrayElementAtIndex(list.index);
                    serializedObject.ApplyModifiedProperties();
                }
            };
        }

        void DrawHeader(Rect _)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Blueprint Components", EditorStyles.boldLabel, GUILayout.Width(160));

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
                if (dupNew != _disallowDup) { _disallowDup = dupNew; EditorPrefs.SetBool(PREF_DISALLOW_DUP, _disallowDup); }

                GUILayout.FlexibleSpace();
            }
        }

        void DrawElement(Rect rect, int index, bool active, bool focused)
        {
            var el = _components.GetArrayElementAtIndex(index);
            var typeProp = el.FindPropertyRelative("TypeName");
            var jsonProp = el.FindPropertyRelative("Json");

            var t = Resolve(typeProp.stringValue);
            var display = t != null ? NiceName(t) : "(None)";

            // 1행: 라벨 + 즐겨찾기 + Select
            var rLabel = new Rect(rect.x, rect.y + 2, rect.width - 200, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(rLabel, display, EditorStyles.miniBoldLabel);

            var rFav = new Rect(rLabel.xMax + 4, rect.y + 2, 24, EditorGUIUtility.singleLineHeight);
            if (t != null)
            {
                var fav = _favStore.Contains(t);
                var nfav = GUI.Toggle(rFav, fav, EditorGUIUtility.IconContent(fav ? "Favorite" : "Favorite Icon"), "InvisibleButton");
                if (nfav != fav) { if (nfav) _favStore.Add(t); else _favStore.Remove(t); _favStore.Save(); }
            }

            var rPick = new Rect(rect.xMax - 170, rect.y + 2, 80, EditorGUIUtility.singleLineHeight);
            if (GUI.Button(rPick, "Select"))
            {
                var screenRect = GUIToScreenRect(rPick);
                EditorApplication.delayCall += () =>
                {
                    ComponentPickerPopup.Open(
                        screenRect,
                        picked =>
                        {
                            if (picked == null) return;
                            if (_disallowDup && HasDup(picked, index))
                            {
                                EditorUtility.DisplayDialog("중복", $"'{picked.FullName}' 는 이미 추가되어 있습니다.", "OK");
                                return;
                            }
                            typeProp.stringValue = picked.AssemblyQualifiedName;
                            if (string.IsNullOrEmpty(jsonProp.stringValue))
                            {
                                // object boxed = Activator.CreateInstance(picked);
                                // jsonProp.stringValue = JsonUtility.ToJson(boxed);
                                
                                object boxed = ComponentDefaults.CreateWithDefaults(picked);
                                jsonProp.stringValue = JsonUtility.ToJson(boxed);
                            }
                            serializedObject.ApplyModifiedProperties();
                            _recents.Push(picked); _recents.Save();
                        },
                        filterSearch: "",
                        groupFilter: _groupFilter,
                        favoritesOnly: false,
                        recentsOnly: false,
                        favs: _favStore,
                        recents: _recents,
                        isDisabled: (Type tt) => _disallowDup && HasDup(tt, index), // 현재 슬롯 제외
                        disabledHint: " (이미 추가됨)"
                    );
                };
            }

            var rRaw = new Rect(rect.xMax - 84, rect.y + 2, 84, EditorGUIUtility.singleLineHeight);
            bool showRaw = GUI.Button(rRaw, "Raw JSON");

            // 2~3행: 폼 / Raw JSON
            var body = new Rect(rect.x, rLabel.yMax + 2, rect.width, EditorGUIUtility.singleLineHeight * 2f);

            if (t != null && !showRaw)
            {
                object instance = Activator.CreateInstance(t);
                try
                {
                    if (!string.IsNullOrEmpty(jsonProp.stringValue))
                        JsonUtility.FromJsonOverwrite(jsonProp.stringValue, instance);
                }
                catch { /* ignore */ }

                EditorGUI.BeginChangeCheck();
                DrawAutoForm(body, t, instance);
                if (EditorGUI.EndChangeCheck())
                {
                    try { jsonProp.stringValue = JsonUtility.ToJson(instance); }
                    catch (Exception ex) { Debug.LogWarning($"[ZenECS] JSON serialize failed: {ex.Message}"); }
                    serializedObject.ApplyModifiedProperties();
                }
            }
            else
            {
                jsonProp.stringValue = EditorGUI.TextArea(body, jsonProp.stringValue ?? "");
                serializedObject.ApplyModifiedProperties();
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // 중복 경고 표시
            var dups = FindDuplicates();
            if (dups.Count > 0)
            {
                EditorGUILayout.HelpBox($"중복 컴포넌트: {string.Join(", ", dups.Select(NiceName))}", MessageType.Warning);
                if (GUILayout.Button("중복 제거(앞의 것만 유지)"))
                    RemoveDuplicatesKeepingFirst();
            }

            EditorGUILayout.Space(4);
            _list.DoLayoutList();

            serializedObject.ApplyModifiedProperties();
        }

        // ─────────── Helpers ───────────
        static Rect GUIToScreenRect(Rect r)
        {
            var p = GUIUtility.GUIToScreenPoint(new Vector2(r.x, r.y));
            return new Rect(p.x, p.y, r.width, r.height);
        }

        static Type Resolve(string tn)
        {
            if (string.IsNullOrEmpty(tn)) return null;
            var t = Type.GetType(tn, false);
            if (t != null) return t;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            { try { t = asm.GetType(tn, false); if (t != null) return t; } catch {} }
            return null;
        }

        static string NiceName(Type t)
        {
            var ns = string.IsNullOrEmpty(t.Namespace) ? "Global" : t.Namespace;
            return $"{ns}.{t.Name}";
        }

        bool HasDup(Type picked, int ignoreIndex)
        {
            for (int i = 0; i < _components.arraySize; i++)
            {
                if (i == ignoreIndex) continue;
                var el = _components.GetArrayElementAtIndex(i);
                var tn = el.FindPropertyRelative("TypeName").stringValue;
                var tt = Resolve(tn);
                if (tt == picked) return true;
            }
            return false;
        }

        List<Type> FindDuplicates()
        {
            var map = new Dictionary<Type, int>();
            var dup = new List<Type>();
            for (int i = 0; i < _components.arraySize; i++)
            {
                var el = _components.GetArrayElementAtIndex(i);
                var t = Resolve(el.FindPropertyRelative("TypeName").stringValue);
                if (t == null) continue;
                if (map.ContainsKey(t)) { if (!dup.Contains(t)) dup.Add(t); }
                else map[t] = 1;
            }
            return dup;
        }

        void RemoveDuplicatesKeepingFirst()
        {
            var seen = new HashSet<Type>();
            for (int i = _components.arraySize - 1; i >= 0; i--)
            {
                var el = _components.GetArrayElementAtIndex(i);
                var t = Resolve(el.FindPropertyRelative("TypeName").stringValue);
                if (t == null) continue;
                if (seen.Contains(t))
                    _components.DeleteArrayElementAtIndex(i);
                else
                    seen.Add(t);
            }
            serializedObject.ApplyModifiedProperties();
        }

        static void DrawAutoForm(Rect area, Type t, object box)
        {
            var r = new Rect(area.x, area.y, area.width, EditorGUIUtility.singleLineHeight);
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object val = f.GetValue(box);
                var ft = f.FieldType;

                if      (ft == typeof(int))      val = EditorGUI.IntField(r, f.Name, (int)val);
                else if (ft == typeof(float))    val = EditorGUI.FloatField(r, f.Name, (float)val);
                else if (ft == typeof(bool))     val = EditorGUI.Toggle(r, f.Name, (bool)val);
                else if (ft == typeof(string))   val = EditorGUI.TextField(r, f.Name, (string)val ?? "");
                else if (ft == typeof(Vector2))  val = EditorGUI.Vector2Field(r, f.Name, (Vector2)val);
                else if (ft == typeof(Vector3))  val = EditorGUI.Vector3Field(r, f.Name, (Vector3)val);
                else if (ft == typeof(Vector4))  val = EditorGUI.Vector4Field(r, f.Name, (Vector4)val);
                else if (ft == typeof(Quaternion))
                {
                    var q = (Quaternion)val;
                    var eul = q.eulerAngles;
                    eul = EditorGUI.Vector3Field(r, f.Name + " (Euler)", eul);
                    val = Quaternion.Euler(eul);
                }
                else if (ft == typeof(Color))    val = EditorGUI.ColorField(r, f.Name, (Color)val);
                else
                {
                    EditorGUI.LabelField(r, f.Name, val != null ? val.ToString() : "null");
                    r.y += EditorGUIUtility.singleLineHeight + 2;
                    continue;
                }

                f.SetValue(box, val);
                r.y += EditorGUIUtility.singleLineHeight + 2;
            }
        }

        // ─────────── 카탈로그/저장소/피커 ───────────
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
            const string PREF = "ZenECS.Blueprint.Favorites";
            readonly HashSet<string> _set = new HashSet<string>();

            public bool Contains(Type t) => t != null && _set.Contains(t.AssemblyQualifiedName);
            public void Add(Type t) { if (t != null) _set.Add(t.AssemblyQualifiedName); }
            public void Remove(Type t) { if (t != null) _set.Remove(t.AssemblyQualifiedName); }

            public void Save() => EditorPrefs.SetString(PREF, string.Join("|", _set));
            public static FavoritesStore Load()
            {
                var s = new FavoritesStore();
                var raw = EditorPrefs.GetString(PREF, "");
                foreach (var token in raw.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                    s._set.Add(token);
                return s;
            }

            public IEnumerable<Type> Types()
            {
                foreach (var tn in _set)
                {
                    var t = Resolve(tn);
                    if (t != null) yield return t;
                }
            }
        }

        sealed class RecentsStore
        {
            const string PREF = "ZenECS.Blueprint.Recents";
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
            public IEnumerable<Type> Types()
            {
                foreach (var tn in _list)
                {
                    var t = Resolve(tn);
                    if (t != null) yield return t;
                }
            }
        }

        sealed class ComponentPickerPopup : EditorWindow
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
                                    Func<Type,bool> isDisabled = null,
                                    string disabledHint = " (이미 추가됨)")
            {
                var w = CreateInstance<ComponentPickerPopup>();
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

                _scroll = EditorGUILayout.BeginScrollView(_scroll);
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
                EditorGUILayout.EndScrollView();
            }
        }
    }
}
#endif
