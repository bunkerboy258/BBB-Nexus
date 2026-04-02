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

        // 如果你有专门的瞄准动画、换弹动画，统统配在这里
        // public ClipTransition AimIdleAnim;
    }
}
