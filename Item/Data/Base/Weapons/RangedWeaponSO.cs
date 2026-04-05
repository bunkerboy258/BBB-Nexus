using Animancer;
using UnityEngine;

namespace BBBNexus
{
    [CreateAssetMenu(fileName = "New Ranged Weapon", menuName = "BBBNexus/Items/Weapons/Ranged Weapon")]
    public class RangedWeaponSO : EquippableItemSO
    {
        [Header("--- 枪械独有配置 (Ranged Stats) ---")]
        [Tooltip("右键/瞄准输入是否会把角色带入 Aim 全身状态。开启后，下半身会使用 PlayerSO.Aiming 的持枪移动配置。")]
        public bool EnablesAimState = true;

        [Tooltip("是否需要脊椎朝向矫正（SpineAimDriver）。手枪等单手枪械开启；AK47、拳头等不需要。")]
        public bool UseAimCorrection = false;

        [Tooltip("瞄准动画")]
        public ClipTransition AimAnim;
        public AnimPlayOptions AnimPlayOptions=AnimPlayOptions.UpperBodyDefault;

        [Tooltip("最大弹药量")]
        public int MaxAmmo = 30;

        [Tooltip("开火间隔 (秒)")]
        public float FireRate = 0.1f;

        [Tooltip("远程命中头部时的伤害倍率。<= 0 时回退到 1.5。")]
        [Min(0f)]
        public float HeadDamageMultiplier = 1.5f;

        [Tooltip("远程命中躯干时的伤害倍率。<= 0 时回退到 1。")]
        [Min(0f)]
        public float TorsoDamageMultiplier = 1f;

        [Tooltip("远程命中手臂时的伤害倍率。<= 0 时回退到 0.9。")]
        [Min(0f)]
        public float ArmDamageMultiplier = 0.9f;

        [Tooltip("远程命中腿部时的伤害倍率。<= 0 时回退到 0.85。")]
        [Min(0f)]
        public float LegDamageMultiplier = 0.85f;

        [Header("--- 弹药系统 (Ammo System) ---")]
        [Tooltip("弹匣容量")]
        public int MagazineSize = 12;

        [Tooltip("换弹时间（秒）")]
        public float ReloadTime = 1.5f;

        [Tooltip("战术换弹时间（弹匣未空时换弹，更快）")]
        public float TacticalReloadTime = 1.2f;

        [Tooltip("换弹动画")]
        public ClipTransition ReloadAnim;
        public AnimPlayOptions ReloadAnimOptions = AnimPlayOptions.UpperBodyDefault;

        [Tooltip("该枪消耗的背包弹药类型。弹匣状态仍保留在 .ammo，备用弹药改走 inventory/.item。")]
        public AmmoItemSO AmmoItem;

        public float ResolveHitZoneDamageMultiplier(DamageHitZoneType zone)
        {
            switch (zone)
            {
                case DamageHitZoneType.Head:
                    return HeadDamageMultiplier > 0f ? HeadDamageMultiplier : 1.5f;
                case DamageHitZoneType.Arm:
                    return ArmDamageMultiplier > 0f ? ArmDamageMultiplier : 0.9f;
                case DamageHitZoneType.Leg:
                    return LegDamageMultiplier > 0f ? LegDamageMultiplier : 0.85f;
                case DamageHitZoneType.Torso:
                case DamageHitZoneType.Default:
                default:
                    return TorsoDamageMultiplier > 0f ? TorsoDamageMultiplier : 1f;
            }
        }

        // 如果你有专门的瞄准动画、换弹动画，统统配在这里
        // public ClipTransition AimIdleAnim;
    }
}
