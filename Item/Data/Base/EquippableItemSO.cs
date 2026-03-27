using Animancer;
using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 可装备物品图纸基类：包含了实例化所需的外壳 Prefab 和基础通用动画。
    /// </summary>
    public abstract class EquippableItemSO : ItemDefinitionSO
    {
        [Header("--- 物理表现 ---")]
        [Tooltip("实例化到玩家手里的游戏对象 (必须包含实现了 IHoldableItem 的脚本)")]
        public GameObject Prefab;

        public Vector3 HoldPositionOffset;
        public Quaternion HoldRotationOffset;

        [Header("--- 通用表现动画 ---")]
        [Tooltip("拔出/装备时的动画")]
        public ClipTransition EquipAnim;
        public AnimPlayOptions EquipAnimPlayOptions = AnimPlayOptions.UpperBodyDefault;

        [Tooltip("收起时的动画")]
        public ClipTransition UnEquipAnim;
        public AnimPlayOptions UnEquipAnimPlayOptions = AnimPlayOptions.UpperBodyDefault;

        [Tooltip("持有时默认的待机动画")]
        public ClipTransition EquipIdleAnim;
        public AnimPlayOptions EquipIdleAnimOptions= AnimPlayOptions.UpperBodyDefault;

    }
}