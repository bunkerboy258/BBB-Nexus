#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BBBNexus
{
    // 告诉Unity只要看到 SubclassSelectorAttribute  就用这个来接管面板渲染
    [CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
    public class SubclassSelectorDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // 确保它是挂在 [SerializeReference] 上的
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.HelpBox(position, "SubclassSelector 只能用于 [SerializeReference] 字段！", MessageType.Error);
                return;
            }

            // 绘制左侧的变量名 Label
            position = EditorGUI.PrefixLabel(position, label);

            // 获取当前实例的类型名字
            string currentTypeName = "Null (空)";
            if (!string.IsNullOrEmpty(property.managedReferenceFullTypename))
            {
                // Unity 存的 Typename 格式是 "Assembly Name classFullName" 只截取最后一个类名
                currentTypeName = property.managedReferenceFullTypename.Split(' ').Last().Split('.').Last();
            }

            // 绘制右侧的可点击下拉按钮
            if (EditorGUI.DropdownButton(position, new GUIContent(currentTypeName), FocusType.Keyboard))
            {
                ShowDropdownMenu(property);
            }
        }

        private void ShowDropdownMenu(SerializedProperty property)
        {
            GenericMenu menu = new GenericMenu();

            // 添加一个清空选项
            menu.AddItem(new GUIContent("Null (置空)"), string.IsNullOrEmpty(property.managedReferenceFullTypename), () =>
            {
                ApplyInstance(property, null);
            });
            menu.AddSeparator("");

            Type baseType = fieldInfo.FieldType;
            var derivedTypes = TypeCache.GetTypesDerivedFrom(baseType)
                .Where(t => !t.IsAbstract && !t.IsInterface && !t.IsGenericType); // 过滤掉不能被 new 的类型

            foreach (Type type in derivedTypes)
            {
                string menuPath = type.Name; // 如果你想搞多级菜单 可以在这里根据 Namespace 处理

                // Unity的Typename匹配校验
                bool isSelected = property.managedReferenceFullTypename.EndsWith(type.Name);

                menu.AddItem(new GUIContent(menuPath), isSelected, () =>
                {
                    // 使用 Activator 反射创建一个纯 C# 实例
                    object newInstance = Activator.CreateInstance(type);
                    ApplyInstance(property, newInstance);
                });
            }

            menu.ShowAsContext();
        }

        // 统一处理属性的修改与序列化保存
        private void ApplyInstance(SerializedProperty property, object instance)
        {
            property.serializedObject.Update();
            property.managedReferenceValue = instance;
            property.serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
