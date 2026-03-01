using Items.Data;
using UnityEngine;

namespace Items.Data.Weapons
{
    [CreateAssetMenu(fileName = "New Ranged Weapon", menuName = "BBBNexus/Items/Ranged Weapon")]
    public class RangedWeaponSO : EquippableItemSO
    {
        [Header("--- 枪械独有配置 (Ranged Stats) ---")]
        [Tooltip("最大弹药量")]
        public int MaxAmmo = 30;

        [Tooltip("开火间隔 (秒)")]
        public float FireRate = 0.1f;

        // 如果你有专门的瞄准动画、换弹动画，统统配在这里
        // public ClipTransition AimIdleAnim; 
    }
}