using Animancer;
using UnityEngine;
using System;

namespace BBBNexus
{
    [Serializable]
    public struct VirtualOtherSlotLink
    {
        [Tooltip("是否启用主武器对 otherslot 的联动托管。")]
        public bool Enabled;

        [Tooltip("主武器切换时要接管的 otherslot。")]
        public EquipmentSlot TargetSlot;

        [Tooltip("virtualpack 中不存在托管文件时，默认创建/装入的另一半装备 id。")]
        public string ItemId;
    }

    /// <summary>
    /// 装备槽位枚举：定义物品可以装备到的位置
    /// </summary>
    public enum EquipmentSlot
    {
        None = 0,      // 无槽位
        MainHand = 1,  // 主手（右手）
        OffHand = 2,   // 副手（左手）
    }

    /// <summary>
    /// 可装备物品图纸基类：包含了实例化所需的外壳 Prefab 和基础通用动画。
    /// </summary>
    public abstract class EquippableItemSO : ItemDefinitionSO
    {
        [Header("--- 物理表现 ---")]
        [Tooltip("实例化到玩家手里的游戏对象 (必须包含实现了 IHoldableItem 的脚本)")]
        public GameObject Prefab;

        [Tooltip("装备到此槽位，此字段已废弃，等待重置，装备槽位由 EquipmentDriver 根据配置决定，不要使用此字段")]
        [Obsolete("此字段已废弃，等待重置，装备槽位由 EquipmentDriver 根据配置决定，不要使用此字段")]
        public EquipmentSlot EquipSlot = EquipmentSlot.MainHand;

        public Vector3 HoldPositionOffset;
        public Quaternion HoldRotationOffset;

        [Header("--- 上半身层控制 ---")]
        [Tooltip("装备此物品时上半身动画层的目标权重。拳头/默认状态填 0，持枪/持剑等需要独立上半身姿势填 1")]
        public float UpperBodyLayerWeight = 1f;

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

        [Header("--- 相机表现力 ---")]
        [Tooltip("装备此物品时的相机预设。null = 沿用当前相机默认配置，不做任何覆写。")]
        public CameraPresetSO CameraPreset;

        [Header("--- otherslot 联动 ---")]
        [Tooltip("主武器可声明一个 virtualpack 联动槽。装备主手时，系统会把 virtualpack 的另一半搬到对应 otherslot；切走主手时再搬回。")]
        public VirtualOtherSlotLink VirtualOtherSlot;

    }
}
