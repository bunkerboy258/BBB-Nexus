using Animancer;
using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 近战武器基类：定义近战武器的基础属性和动画配置。
    /// 继承自 EquippableItemSO，添加近战特定的动画和参数。
    /// </summary>
    public abstract class MeleeWeaponSO : EquippableItemSO
    {
        [Header("--- 拔出硬直 ---")]
        [Tooltip("拔出动画允许退出时间（秒）")]
        public float EquipEndTime = 0.5f;

        [Header("--- 近战武器独有配置 ---")]
        [Tooltip("攻击冷却时间 (秒)")]
        public float AttackCooldown = 0.5f;

        [Tooltip("是否启用 IK（近战通常不需要）")]
        public bool EnableIK = false;
    }
}
