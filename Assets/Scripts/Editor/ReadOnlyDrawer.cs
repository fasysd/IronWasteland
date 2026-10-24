using UnityEditor;
using UnityEngine;
using IronWasteland;

namespace IronWasteland.EditorTools
{
    /// <summary>
    /// Ve field dat <see cref="ReadOnlyAttribute"/> o che do chi-doc.
    /// - Foldout van MO DUOC de xem cac field con (quan trong voi object long nhu TankStats).
    /// - Cac field con ve trong BeginDisabledGroup -> xam, khong chinh sua duoc.
    /// </summary>
    [CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    public class ReadOnlyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.hasVisibleChildren)
            {
                // Dong foldout: van cho phep click de mo/ra xem noi dung.
                Rect foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
                property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

                if (property.isExpanded)
                {
                    EditorGUI.indentLevel++;
                    float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                    SerializedProperty child = property.Copy();
                    SerializedProperty end = child.GetEndProperty();
                    bool enterChildren = true;

                    while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
                    {
                        enterChildren = false;
                        float height = EditorGUI.GetPropertyHeight(child, true);

                        Rect childRect = new Rect(position.x, y, position.width, height);
                        EditorGUI.BeginDisabledGroup(true);
                        EditorGUI.PropertyField(childRect, child, true);
                        EditorGUI.EndDisabledGroup();

                        y += height + EditorGUIUtility.standardVerticalSpacing;
                    }

                    EditorGUI.indentLevel--;
                }
            }
            else
            {
                // Field khong co con -> toan bo vao nhom disabled.
                EditorGUI.BeginDisabledGroup(true);
                EditorGUI.PropertyField(position, property, label, true);
                EditorGUI.EndDisabledGroup();
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;

            if (property.isExpanded && property.hasVisibleChildren)
            {
                SerializedProperty child = property.Copy();
                SerializedProperty end = child.GetEndProperty();
                bool enterChildren = true;

                while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
                {
                    enterChildren = false;
                    height += EditorGUI.GetPropertyHeight(child, true) + EditorGUIUtility.standardVerticalSpacing;
                }
            }

            return height;
        }
    }
}