using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BBBNexus
{
    /// <summary>
    /// 静态物品图纸基类：定义物品的只读基础属性。
    /// </summary>
    public abstract class ItemDefinitionSO : ScriptableObject
    {
        [Header("--- 基础信息 ---")]
        [Tooltip("物品的全局唯一静态ID (用于配表和存档读取)")]
        public string ItemID;

        [Tooltip("物品的本地化名称")]
        public string DisplayName;

        [Tooltip("UI 中显示的图标")]
        public Sprite Icon;

        [TextArea(2, 4)]
        [Tooltip("物品的文本描述")]
        public string Description;

        [Tooltip("最大堆叠数量")]
        public int MaxStack = 1;

        // 在编辑器中自动校验 ID：ItemID 必须与资产文件名一致
        protected virtual void OnValidate()
        {
#if UNITY_EDITOR
            string canonicalName = name;
            string assetPath = AssetDatabase.GetAssetPath(this);
            if (!string.IsNullOrWhiteSpace(assetPath))
            {
                canonicalName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            }

            if (!string.IsNullOrWhiteSpace(canonicalName) && ItemID != canonicalName)
            {
                ItemID = canonicalName;
                EditorUtility.SetDirty(this);
            }
#endif
        }
    }
}
