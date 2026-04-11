using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;

namespace BBBNexus
{
    [CustomPropertyDrawer(typeof(ServiceTypeFieldAttribute))]
    public class ServiceTypeFieldDrawer : PropertyDrawer
    {
        // 缓存：接口类型 → 实现类全名列表（编辑器会话内有效）
        private static readonly Dictionary<Type, string[]> _typeCache = new();

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, "[ServiceTypeField] 只能标注 string 字段");
                return;
            }

            var attr = (ServiceTypeFieldAttribute)attribute;
            var fullNames = GetImplementationFullNames(attr.InterfaceType);

            // 显示名 = 短类名；存储名 = 全限定名（用于跨程序集反射查找）
            var displayNames = new string[fullNames.Length + 1];
            displayNames[0] = "(None)";
            for (int i = 0; i < fullNames.Length; i++)
                displayNames[i + 1] = ToShortName(fullNames[i]);

            string currentValue = property.stringValue;
            int selectedIndex = Array.IndexOf(fullNames, currentValue) + 1; // +1 因为 None 在 0
            if (selectedIndex < 0) selectedIndex = 0;

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUI.Popup(position, label.text, selectedIndex, displayNames);
            if (EditorGUI.EndChangeCheck())
            {
                property.stringValue = newIndex == 0 ? string.Empty : fullNames[newIndex - 1];
            }
        }

        private static string[] GetImplementationFullNames(Type interfaceType)
        {
            if (_typeCache.TryGetValue(interfaceType, out var cached))
                return cached;

            var result = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .Where(t => t.IsClass && !t.IsAbstract && interfaceType.IsAssignableFrom(t))
                .Select(t => t.FullName)
                .OrderBy(n => n)
                .ToArray();

            _typeCache[interfaceType] = result;
            return result;
        }

        /// <summary>
        /// 将 "BBBNexus.NekoGraphHub" 变成 "NekoGraphHub"，方便阅读
        /// </summary>
        private static string ToShortName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return fullName;
            int dot = fullName.LastIndexOf('.');
            return dot >= 0 ? fullName.Substring(dot + 1) : fullName;
        }
    }
}
#endif
