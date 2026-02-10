using UnityEngine;
using Animancer;

namespace Items.Data
{
    // 这个类仍然是抽象的，因为我们希望用户创建更具体的 "Weapon" 或 "Tool"
    public abstract class EquippableItemSO : ItemDefinitionSO
    {
        [Header("装备属性 (Equippable)")]
        public GameObject Prefab; // 场景模型
        public ItemHoldType HoldType; // 握持姿态

        [Header("动画配置")]
        public ClipTransition EquipIdleAnim;
        public ClipTransition EquipAnim;
        public ClipTransition UnequipAnim;
    }
}
