using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BBBNexus
{
    [CreateAssetMenu(fileName = "New CannonSO", menuName = "BBBNexus/Items/Weapons/Cannon")]
    public class CannonSO : RangedWeaponSO
    {
        [Header("--- Cannon专属动画参数 ---")]
        [Tooltip("拿出动画允许退出时间")] public float EquipEndTime = 0.5f;
        [Tooltip("开启IK的时间点（秒），相对于拿出动画开始")] public float EnableIKTime = 0.4f;
        [Tooltip("关闭IK的时间点（秒），相对于收起动画开始")] public float DisableIKTime = 0.4f;

        [Header("--- Projectile ---")]
        [Tooltip("子弹实体的预制体 (带 Rigidbody 和 SimpleProjectile 等脚本)")]
        public GameObject ProjectilePrefab;

        [Tooltip("子弹发射时施加的初速度（以冲量形式，单位大致为 m/s * 质量；具体可调整）")]
        public float ProjectileSpeed = 20f;

        [Header("--- Shooting ---")]
        [Tooltip("射击间隔 (秒)。如果未设置将回退到父类的 FireRate")]
        public float ShootInterval = 0.1f;

        [Tooltip("射击时播放的音效 (会在射击位置播放)")]
        public AudioClip ShootSound;

        [Header("--- Projectile Impact ---")]
        [Tooltip("子弹撞击时播放的音效 (SimpleProjectile will use this if assigned)")]
        public AudioClip ProjectileHitSound;

        [Header("--- Muzzle VFX ---")]
        [Tooltip("枪口火焰/火花的预制体 (会在装备时实例化到 muzzle 下并保持停用)")]
        public GameObject MuzzleVFXPrefab;

        [Header("--- Recoil (后坐力) ---")]
        [Tooltip("后坐力的俯仰角度 (向上看的角度，单位：度)")]
        public float RecoilPitchAngle = 2f;

        [Tooltip("后坐力的偏航角度 (左右晃动，单位：度)")]
        public float RecoilYawAngle = 1f;

        [Header("--- Recoil Randomness (后坐力随机性) ---")]
        [Tooltip("俯仰随机范围（度）。实际俯仰 = RecoilPitchAngle + Random.Range(-RecoilPitchRandomRange, RecoilPitchRandomRange)")]
        public float RecoilPitchRandomRange = 0.5f;

        [Tooltip("偏航随机范围（度）。实际偏航 = RecoilYawAngle + Random.Range(-RecoilYawRandomRange, RecoilYawRandomRange)")]
        public float RecoilYawRandomRange = 0.5f;
    }
}
