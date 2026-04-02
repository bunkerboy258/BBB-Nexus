using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 盾牌配置 SO。
    /// 盾牌自身持有碰撞体，是 Attackable 的；
    /// 被击中时对攻击者施加硬直，持盾者本帧免伤。
    ///
    /// 创建方式：Project 窗口右键 → Create → BBBNexus → Player → Items → ShieldSO
    /// </summary>
    [CreateAssetMenu(fileName = "ShieldSO", menuName = "BBBNexus/Player/Items/ShieldSO")]
    public class ShieldSO : EquippableItemSO
    {
        [Header("格挡反馈")]
        [Tooltip("盾牌被击中时，施加给攻击者的硬直状态")]
        public StatusEffectSO BlockedEffect;
    }
}
