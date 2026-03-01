using Animancer;
using Characters.Player.Animation;
using Items.Core;
using UnityEngine;

namespace Items.Data
{
    /// <summary>
    /// 可装备物品图纸基类：包含了实例化所需的外壳 Prefab 和基础通用动画。
    /// </summary>
    public abstract class EquippableItemSO : ItemDefinitionSO
    {
        [Header("--- 物理表现 (Physical Avatar) ---")]
        [Tooltip("实例化到玩家手里的游戏对象 (必须包含实现了 IHoldableItem 的脚本)")]
        public GameObject Prefab;

        public Vector3 HoldPositionOffset;
        public Quaternion HoldRotationOffset;

        [Header("--- 通用表现动画 (Universal Animations) ---")]
        [Tooltip("拔出/装备时的动画")]
        public ClipTransition EquipAnim;

        [Tooltip("持有时默认的待机动画 (可选)")]
        public ClipTransition EquipIdleAnim;

        /// <summary>
        /// 提供一个快捷方法，用于生成默认的上半身播放选项。
        /// </summary>
        public AnimPlayOptions GetUpperBodyPlayOptions()
        {
            return new AnimPlayOptions
            {
                Layer = 1,          // 强制指定到上半身层级 (Layer 1)
                FadeDuration = -1f, // 使用 ClipTransition Inspector 中的默认值
                Speed = -1f,
                NormalizedTime = -1f,
                ForcePhaseSync = false
            };
        }
    }
}