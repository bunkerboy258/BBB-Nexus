using UnityEngine;

namespace Items.Data
{
    /// <summary>
    /// 静态物品图纸基类：定义物品的只读基础属性。
    /// </summary>
    public abstract class ItemDefinitionSO : ScriptableObject
    {
        [Header("--- 基础信息 (Basic Info) ---")]
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

        // 在编辑器中自动生成或校验 ID
        protected virtual void OnValidate()
        {
            if (string.IsNullOrEmpty(ItemID))
            {
                ItemID = System.Guid.NewGuid().ToString();
            }
        }
    }
}