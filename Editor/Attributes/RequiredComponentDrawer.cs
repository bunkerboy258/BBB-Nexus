using UnityEngine;
using UnityEditor;

namespace BBBNexus
{
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(RequiredComponentAttribute))]
    public class RequiredComponentDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var requiredAttr = attribute as RequiredComponentAttribute;
            
            // 绘制字段
            EditorGUI.PropertyField(position, property, label);

            // 验证
            if (property.propertyType == SerializedPropertyType.ObjectReference)
            {
                var obj = property.objectReferenceValue as GameObject;
                if (obj != null && !obj.TryGetComponent(requiredAttr.ComponentType, out _))
                {
                    // 绘制红色边框
                    EditorGUI.DrawRect(position, new Color(1f, 0.3f, 0.3f, 0.1f));
                    
                    // 如果用户选择了无效对象，显示警告
                    if (GUI.Button(new Rect(position.xMax - 24, position.y, 20, position.height), "⚠️"))
                    {
                        Debug.LogWarning(requiredAttr.ErrorMessage, obj);
                    }
                }
            }
        }
    }
#endif
}
