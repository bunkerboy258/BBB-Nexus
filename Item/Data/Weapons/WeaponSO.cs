using Animancer;
using System;
using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 通用武器配置。近战字段、远程字段可独立使用，也可同时填写实现双模式武器。
    /// 哪些字段有内容，WeaponBehaviour 就开放哪些功能。
    /// </summary>
    [CreateAssetMenu(fileName = "New WeaponSO", menuName = "BBBNexus/Items/Weapons/Weapon")]
    public class WeaponSO : EquippableItemSO
    {
        // ───────────────────────────────────────────
        // 近战通用
        // ───────────────────────────────────────────

        [Header("--- 近战通用 ---")]
        [Tooltip("拔出动画允许退出时间（秒）")]
        public float EquipEndTime = 0.5f;

        [Tooltip("是否启用 IK（近战通常不需要）")]
        public bool EnableIK = false;

        // ───────────────────────────────────────────
        // 近战连招（来自 FistsSO）
        // ───────────────────────────────────────────

        [Header("--- 近战连招 ---")]
        [Tooltip("首次出手前的起手式动画，播完后自动触发第一击。留空则直接出手")]
        public ClipTransition EnterStanceAnim;

        [Tooltip("连招结束后的收招动画，播完后回归普通状态。留空则直接结束")]
        public ClipTransition ExitStanceAnim;

        [Tooltip("是否将收招作为真正的锁定退出处理。关闭时仅作为视觉收尾，不产生硬直")]
        public bool ExitUsesLock = false;

        [Tooltip("连招动画序列，按顺序播放，长度决定最大连招段数")]
        public FistsComboTransition[] ComboSequence;

        [Tooltip("每段连招的出手（MainHand / OffHand / BothHands）")]
        public FistsAttackHand[] ComboAttackHands;

        [Tooltip("与 ComboSequence 平行的伤害窗口侧链（归一化区间）")]
        public FistsDamageWindowSidecar[] ComboDamageWindows;

        [Tooltip("与 ComboSequence 平行的前摇对齐窗口侧链（归一化区间）")]
        public FistsAlignmentWindowSidecar[] ComboAlignmentWindows;

        [Tooltip("续招开放时机（归一化 0-1），动画播到此比例后开始接受续招输入")]
        [Range(0f, 1f)]
        public float ComboWindowStart = 0.5f;

        [Tooltip("动画结束后仍可接受续招输入的宽限时间（秒）")]
        public float ComboLateBuffer = 0.2f;

        [Tooltip("连招动作优先级，高于普通移动但低于翻滚/闪避")]
        public int ComboPriority = 25;

        [Tooltip("前摇对齐时使用的最大转向角速度（度/秒）")]
        public float AutoTargetTurnSpeed = 540f;

        [Tooltip("Attack Clip Geometry Definition 的 MetaLib ID。留空时回退到 <资产名>_AttackSweep")]
        public string AttackGeometryId;

        [Tooltip("冲刺时的相机预设（近战）。null = 沿用 CameraPreset")]
        public CameraExpressionSO SprintCameraPreset;

#if UNITY_EDITOR
        [HideInInspector] public GameObject BakingCharacterPrefab;
        [HideInInspector] public GameObject BakingWeaponPrefab;
#endif

        // ───────────────────────────────────────────
        // 远程通用（来自 RangedWeaponSO）
        // ───────────────────────────────────────────

        [Header("--- 远程通用 ---")]
        [Tooltip("右键/瞄准输入是否会把角色带入 Aim 全身状态")]
        public bool EnablesAimState = true;

        [Tooltip("是否需要脊椎朝向矫正（SpineAimDriver）。单手枪械开启；双手/近战不需要")]
        public bool UseAimCorrection = false;

        [Tooltip("瞄准动画")]
        public ClipTransition AimAnim;
        public AnimPlayOptions AimAnimPlayOptions = AnimPlayOptions.UpperBodyDefault;

        [Tooltip("最大弹药量")]
        public int MaxAmmo = 30;

        [Tooltip("开火间隔（秒）")]
        public float FireRate = 0.1f;

        [Tooltip("弹匣容量")]
        public int MagazineSize = 12;

        [Tooltip("换弹时间（秒）")]
        public float ReloadTime = 1.5f;

        [Tooltip("战术换弹时间（弹匣未空时换弹，更快）")]
        public float TacticalReloadTime = 1.2f;

        [Tooltip("换弹动画")]
        public ClipTransition ReloadAnim;
        public AnimPlayOptions ReloadAnimOptions = AnimPlayOptions.UpperBodyDefault;

        [Tooltip("该武器消耗的背包弹药类型")]
        public AmmoItemSO AmmoItem;

        // ───────────────────────────────────────────
        // 远程专属（来自 PistolSO）
        // ───────────────────────────────────────────

        [Header("--- 远程专属 ---")]
        [Tooltip("子弹实体预制体（带 Rigidbody 和 SimpleProjectile 等脚本）")]
        public GameObject ProjectilePrefab;

        [Tooltip("子弹发射速度")]
        public float ProjectileSpeed = 24f;

        [Tooltip("全自动模式：持续按住持续射击。关闭则为半自动（每次按键仅射一发）")]
        public bool IsFullAuto = false;

        [Tooltip("射击间隔（秒）。若为 0 则回退到 FireRate")]
        public float ShootInterval = 0.18f;

        [Tooltip("hitscan 射线的最大距离")]
        public float HitScanRange = 80f;

        [Tooltip("hitscan 单发伤害")]
        public float DamageAmount = 10f;

        [Tooltip("曳光弹可见时长（秒）")]
        public float TracerDuration = 0.06f;

        [Tooltip("枪口火焰/火花的预制体")]
        public GameObject MuzzleVFXPrefab;

        [Tooltip("后坐力俯仰角度（度）")]
        public float RecoilPitchAngle = 1.4f;

        [Tooltip("后坐力偏航角度（度）")]
        public float RecoilYawAngle = 0.8f;

        [Tooltip("俯仰随机范围（度）")]
        public float RecoilPitchRandomRange = 0.35f;

        [Tooltip("偏航随机范围（度）")]
        public float RecoilYawRandomRange = 0.35f;

        [Tooltip("瞄准时的相机预设。null = 沿用 CameraPreset")]
        public CameraExpressionSO AimingCameraPreset;

        // ───────────────────────────────────────────
        // 辅助方法（与 FistsSO 一致）
        // ───────────────────────────────────────────

        public bool HasMelee => ComboSequence != null && ComboSequence.Length > 0;
        public bool HasRanged => MagazineSize > 0 || HitScanRange > 0f;

        public FistsAttackHand GetAttackHand(int comboIndex)
        {
            if (ComboAttackHands != null && comboIndex >= 0 && comboIndex < ComboAttackHands.Length)
                return ComboAttackHands[comboIndex];
            return (comboIndex & 1) == 0 ? FistsAttackHand.MainHand : FistsAttackHand.OffHand;
        }

        public FistsDamageWindowSidecar GetDamageWindow(int comboIndex)
        {
            if (ComboDamageWindows != null && comboIndex >= 0 && comboIndex < ComboDamageWindows.Length)
            {
                var s = ComboDamageWindows[comboIndex];
                if (s.EndNormalized < s.StartNormalized)
                    (s.StartNormalized, s.EndNormalized) = (s.EndNormalized, s.StartNormalized);
                return s;
            }
            return FistsDamageWindowSidecar.Default;
        }

        public FistsAlignmentWindowSidecar GetAlignmentWindow(int comboIndex)
        {
            if (ComboAlignmentWindows != null && comboIndex >= 0 && comboIndex < ComboAlignmentWindows.Length)
            {
                var s = ComboAlignmentWindows[comboIndex];
                if (s.EndNormalized < s.StartNormalized)
                    (s.StartNormalized, s.EndNormalized) = (s.EndNormalized, s.StartNormalized);
                return s;
            }
            return FistsAlignmentWindowSidecar.Default;
        }

        public string GetAttackGeometryId()
        {
            if (!string.IsNullOrWhiteSpace(AttackGeometryId))
                return AttackGeometryId.Trim();
            return $"{name}_AttackSweep";
        }

        public string GetAttackGeometryResourcePath()
            => $"AttackClipGeometry/{GetAttackGeometryId()}";
    }
}
