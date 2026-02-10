using UnityEngine;
using Animancer;

namespace Items.Data
{
    [CreateAssetMenu(fileName = "NewRangedWeapon", menuName = "Items/Devices/Ranged Weapon")]
    public class RangedWeaponSO : DeviceItemSO
    {
        [Header("--- 枪械属性 (Ranged Stats) ---")]
        public float FireRate = 0.1f;       // 射速
        public int MagazineSize = 30;       // 弹夹
        public float ReloadTime = 2.0f;     // 换弹时间
        public bool IsAutomatic = true;     // 是否全自动

        [Header("--- 表现 (Feedback) ---")]
        public ClipTransition AimAnim;      // 瞄准动画
        public ClipTransition ShootAnim;    // 射击动画 (Additive)
        public ClipTransition ReloadAnim;   // 换弹动画
        public GameObject MuzzleFlashPrefab;
        public GameObject BulletTrailPrefab;

        // 可以在这里加后坐力配置 (RecoilProfile)
    }
}
