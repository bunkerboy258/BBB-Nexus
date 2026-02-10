using UnityEngine;

namespace Items.Data
{
    // [注意] 这是一个抽象类，不能直接创建实例
    public abstract class ItemDefinitionSO : ScriptableObject
    {
        [Header("核心信息 (Core)")]
        public string ID;
        public string Name;
        [TextArea] public string Description;
        public Sprite Icon;

        [Header("堆叠 (Stacking)")]
        public bool IsStackable = false;
        public int MaxStackSize = 1;
    }
}
