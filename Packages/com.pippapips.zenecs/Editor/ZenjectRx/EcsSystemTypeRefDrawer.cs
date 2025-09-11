#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ZenECS.Core;

namespace ZenECS.Integration.ZenjectRx.Editor
{
    [CustomPropertyDrawer(typeof(EcsFeatureInstaller.SystemTypeRef))]
    public sealed class EcsSystemTypeRefDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var typeProp = property.FindPropertyRelative("_typeName");
            var type = Resolve(typeProp.stringValue);

            EditorGUI.BeginProperty(position, label, property);
            var labelRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(labelRect, label);

            var fieldRect = new Rect(position.x + EditorGUIUtility.labelWidth, position.y, position.width - EditorGUIUtility.labelWidth - 64f, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(fieldRect, type != null ? type.FullName : "(None)", EditorStyles.helpBox);

            var btnRect = new Rect(position.xMax - 60f, position.y, 60f, EditorGUIUtility.singleLineHeight);
            if (GUI.Button(btnRect, "Select"))
            {
                var menu = new GenericMenu();
                foreach (var t in FindSystemTypes())
                    menu.AddItem(new GUIContent(t.FullName), false, ()=> { typeProp.stringValue = t.AssemblyQualifiedName; typeProp.serializedObject.ApplyModifiedProperties(); });
                if (menu.GetItemCount()==0) menu.AddDisabledItem(new GUIContent("(No systems)"));
                menu.DropDown(btnRect);
            }

            HandleDragAndDrop(position, typeProp);
            EditorGUI.EndProperty();
        }

        static IEnumerable<Type> FindSystemTypes()
        {
            var runT=typeof(IRunSystem); var initT=typeof(IInitSystem); var stopT=typeof(IStopSystem);
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types; try { types = asm.GetTypes(); } catch { continue; }
                foreach (var t in types)
                    if (!t.IsAbstract && !t.IsInterface && (runT.IsAssignableFrom(t) || initT.IsAssignableFrom(t) || stopT.IsAssignableFrom(t)))
                        yield return t;
            }
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

        static void HandleDragAndDrop(Rect rect, SerializedProperty typeProp)
        {
            var e = Event.current;
            if (!rect.Contains(e.mousePosition)) return;
            if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform) return;
            var mono = DragAndDrop.objectReferences.OfType<MonoScript>().FirstOrDefault();
            if (mono == null) return;
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                var t = mono.GetClass();
                if (t != null && FindSystemTypes().Contains(t))
                {
                    typeProp.stringValue = t.AssemblyQualifiedName;
                    typeProp.serializedObject.ApplyModifiedProperties();
                }
            }
            e.Use();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUIUtility.singleLineHeight + 4f;
    }
}
#endif
