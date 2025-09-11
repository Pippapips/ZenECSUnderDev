#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ZenECS.Integration.Editor
{
    [InitializeOnLoad]
    public static class EcsHierarchyLabel
    {
        static GUIStyle _small;
        static GUIStyle Small
        {
            get
            {
                if (_small != null) return _small;
                GUIStyle baseStyle = null;
                try { if (EditorStyles.miniLabel != null) baseStyle = EditorStyles.miniLabel; } catch {}
                if (baseStyle == null) baseStyle = GUI.skin != null ? GUI.skin.label : new GUIStyle();
                _small = new GUIStyle(baseStyle) { alignment = TextAnchor.MiddleRight, clipping = TextClipping.Clip };
                return _small;
            }
        }

        static EcsHierarchyLabel()
        {
            EditorApplication.hierarchyWindowItemOnGUI += OnGUI;
        }

        static void OnGUI(int id, Rect rect)
        {
            var go = EditorUtility.InstanceIDToObject(id) as GameObject;
            if (!go) return;
            var view = go.GetComponent<EcsEntityView>();
            if (!view || !view.ShowHierarchyBadge) return;

            var r = rect; r.x = r.xMax - 70; r.width = 68;
            var bg = r; bg.x += 2; bg.width -= 4; bg.y += 1; bg.height -= 2;
            EditorGUI.DrawRect(bg, new Color(0.15f, 0.65f, 1f, 0.18f));
            GUI.Label(r, $"[ECS:{view.EntityId}]", Small);
        }
    }
}
#endif