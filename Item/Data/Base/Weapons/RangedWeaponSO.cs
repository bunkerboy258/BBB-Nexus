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

        [Tooltip("瞄准动画")]
        public ClipTransition AimAnim;
        public AnimPlayOptions AnimPlayOptions=AnimPlayOptions.UpperBodyDefault;

        [Tooltip("最大弹药量")]
        public int MaxAmmo = 30;

        [Tooltip("开火间隔 (秒)")]
        public float FireRate = 0.1f;

        // 如果你有专门的瞄准动画、换弹动画，统统配在这里
        // public ClipTransition AimIdleAnim; 
    }
}
