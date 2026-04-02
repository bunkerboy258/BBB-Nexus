using UnityEngine;

namespace BBBNexus
{
    [CreateAssetMenu(fileName = "New PistolSO", menuName = "BBBNexus/Items/Weapons/Pistol")]
    public class PistolSO : RangedWeaponSO
    {
        [Header("--- Pistol 专属动画参数 ---")]
        [Tooltip("拿出动画允许退出时间")]
        public float EquipEndTime = 0.35f;

        [Header("--- Projectile ---")]
        [Tooltip("子弹实体的预制体 (带 Rigidbody 和 SimpleProjectile 等脚本)")]
        public GameObject ProjectilePrefab;

        [Tooltip("子弹发射速度")]
        public float ProjectileSpeed = 24f;

        [Header("--- Shooting ---")]
        [Tooltip("全自动模式：持续按住左键持续射击。关闭则为半自动（每次按键仅射一发）。")]
        public bool IsFullAuto = false;

        [Tooltip("射击间隔 (秒)。如果未设置将回退到父类的 FireRate")]
        public float ShootInterval = 0.18f;

        [Tooltip("hitscan 射线的最大距离")]
        public float HitScanRange = 80f;

        [Tooltip("hitscan 单发伤害")]
        public float DamageAmount = 10f;

        [Tooltip("曳光弹可见时长（秒）")]
        public float TracerDuration = 0.06f;

        [Header("--- Muzzle VFX ---")]
        [Tooltip("枪口火焰/火花的预制体")]
        public GameObject MuzzleVFXPrefab;

        [Header("--- Recoil (后坐力) ---")]
        [Tooltip("后坐力的俯仰角度 (向上看的角度，单位：度)")]
        public float RecoilPitchAngle = 1.4f;

        [Tooltip("后坐力的偏航角度 (左右晃动，单位：度)")]
        public float RecoilYawAngle = 0.8f;

        [Header("--- 相机表现力覆写 ---")]
        [Tooltip("瞄准时的相机预设。null = 沿用 EquippableItemSO.CameraPreset")]
        public CameraExpressionSO AimingCameraPreset;
        [Tooltip("冲刺时的相机预设。null = 沿用 EquippableItemSO.CameraPreset")]
        public CameraExpressionSO SprintCameraPreset;

        [Header("--- Recoil Randomness (后坐力随机性) ---")]
        [Tooltip("俯仰随机范围（度）。实际俯仰 = RecoilPitchAngle + Random.Range(-RecoilPitchRandomRange, RecoilPitchRandomRange)")]
        public float RecoilPitchRandomRange = 0.35f;

        [Tooltip("偏航随机范围（度）。实际偏航 = RecoilYawAngle + Random.Range(-RecoilYawRandomRange, RecoilYawRandomRange)")]
        public float RecoilYawRandomRange = 0.35f;
    }
}
