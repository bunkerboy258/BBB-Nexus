using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 盾牌配置 SO。继承 WeaponSO，天然支持近战连招与远程模块。
    /// 同时持有格挡专属字段，由 ShieldBehaviour 的 IDamageable 实现消费。
    ///
    /// 创建方式：Project 窗口右键 → Create → BBBNexus → Player → Items → ShieldSO
    /// </summary>
    [CreateAssetMenu(fileName = "ShieldSO", menuName = "BBBNexus/Player/Items/ShieldSO")]
    public class ShieldSO : WeaponSO
    {
        [Header("--- 格挡反馈 ---")]
        [Tooltip("盾牌被击中时，施加给攻击者的硬直状态")]
        public StatusEffectSO BlockedEffect;

        [Header("--- 格挡判定 ---")]
        [Tooltip("以盾牌朝向为中心的格挡扇区角度。处于该角度内的攻击一律视为被盾牌拦截。")]
        [Range(0f, 180f)]
        public float BlockArcDegrees = 150f;

        [Tooltip("若盾牌 Prefab 的正面朝向与 transform.forward 相反，则勾选此项。")]
        public bool UseNegativeForwardAsBlockFront = false;
    }
}
