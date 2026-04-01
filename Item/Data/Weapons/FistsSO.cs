using Animancer;
using UnityEngine;

namespace BBBNexus
{
    public enum FistsAttackHand
    {
        MainHand = 0,
        OffHand = 1,
        BothHands = 2,
    }

    [System.Serializable]
    public struct FistsDamageWindowSidecar
    {
        [Tooltip("是否启用该伤害窗口侧链。关闭时运行时可回退到旧逻辑。")]
        public bool Enabled;

        [Tooltip("相对动画实际统治时间的归一化起点。0 表示实际统治时间开始，1 表示结束。")]
        [Range(0f, 1f)]
        public float StartNormalized;

        [Tooltip("相对动画实际统治时间的归一化终点。0 表示实际统治时间开始，1 表示结束。")]
        [Range(0f, 1f)]
        public float EndNormalized;

        public static FistsDamageWindowSidecar Default =>
            new FistsDamageWindowSidecar
            {
                Enabled = false,
                StartNormalized = 0.15f,
                EndNormalized = 0.45f,
            };
    }

    [System.Serializable]
    public struct FistsAlignmentWindowSidecar
    {
        [Tooltip("是否启用该前摇对齐窗口侧链。关闭时表示该动作段没有专门的对齐时段。")]
        public bool Enabled;

        [Tooltip("相对动画实际统治时间的归一化起点。0 表示实际统治时间开始，1 表示结束。")]
        [Range(0f, 1f)]
        public float StartNormalized;

        [Tooltip("相对动画实际统治时间的归一化终点。0 表示实际统治时间开始，1 表示结束。")]
        [Range(0f, 1f)]
        public float EndNormalized;

        public static FistsAlignmentWindowSidecar Default =>
            new FistsAlignmentWindowSidecar
            {
                Enabled = false,
                StartNormalized = 0f,
                EndNormalized = 0.25f,
            };
    }

    /// <summary>
    /// 拳头配置。UpperBodyLayerWeight 默认为 0（空手不产生独立上半身姿势）。
    /// </summary>
    [CreateAssetMenu(fileName = "FistsSO", menuName = "BBBNexus/Player/Items/FistsSO")]
    public class FistsSO : EquippableItemSO
    {
        [Header("前摇 / 收招")]
        [Tooltip("首次出拳前的起手式动画，播完后自动触发第一拳。留空则直接出拳")]
        public ClipTransition EnterStanceAnim;

        [Tooltip("连招结束后的收招动画，播完后回归普通状态。留空则直接结束")]
        public ClipTransition ExitStanceAnim;

        [Tooltip("是否将收招作为真正的锁定退出处理。关闭时仅作为视觉收尾，不产生硬直。")]
        public bool ExitUsesLock = false;

        [Header("连招配置")]
        [Tooltip("连招动画序列，按顺序播放，长度决定最大连招段数。ClipTransition 支持预览和独立淡入配置")]
        public FistsComboTransition[] ComboSequence;

        [Tooltip("Which hand deals damage for each combo segment. If empty, segments alternate Main -> Off -> Main ...")]
        public FistsAttackHand[] ComboAttackHands;

        [Tooltip("与 ComboSequence 平行的伤害窗口侧链。使用相对动画实际统治时间的归一化区间，而不是绝对秒数。")]
        public FistsDamageWindowSidecar[] ComboDamageWindows;

        [Tooltip("与 ComboSequence 平行的前摇对齐窗口侧链。仅描述时段，不描述对齐策略。")]
        public FistsAlignmentWindowSidecar[] ComboAlignmentWindows;

        [Tooltip("连招甜蜜期开启时机（归一化时间 0-1），动画播放到此比例后开始接受续招输入")]
        [Range(0f, 1f)]
        public float ComboWindowStart = 0.5f;

        [Tooltip("动画结束后仍可接受续招输入的宽限时间（秒），用于容纳滞后按键")]
        public float ComboLateBuffer = 0.2f;

        [Tooltip("连招优先级，高于普通移动但低于翻滚/闪避")]
        public int ComboPriority = 25;

        [Header("--- 相机表现力覆写 ---")]
        [Tooltip("冲刺时的相机预设。null = 沿用 EquippableItemSO.CameraPreset")]
        public CameraExpressionSO SprintCameraPreset;

        [Header("近战自动锁定")]
        [Tooltip("前摇对齐时使用的最大转向角速度（度/秒）。")]
        public float AutoTargetTurnSpeed = 540f;

        [Header("攻击几何定义")]
        [Tooltip("Attack Clip Geometry Definition 的 MetaLib ID。留空时回退到 <资产名>_AttackSweep。")]
        public string AttackGeometryId;

        public FistsAttackHand GetAttackHand(int comboIndex)
        {
            if (ComboAttackHands != null && comboIndex >= 0 && comboIndex < ComboAttackHands.Length)
            {
                return ComboAttackHands[comboIndex];
            }

            return (comboIndex & 1) == 0 ? FistsAttackHand.MainHand : FistsAttackHand.OffHand;
        }

        public FistsDamageWindowSidecar GetDamageWindow(int comboIndex)
        {
            if (ComboDamageWindows != null && comboIndex >= 0 && comboIndex < ComboDamageWindows.Length)
            {
                var sidecar = ComboDamageWindows[comboIndex];
                if (sidecar.EndNormalized < sidecar.StartNormalized)
                {
                    (sidecar.StartNormalized, sidecar.EndNormalized) = (sidecar.EndNormalized, sidecar.StartNormalized);
                }

                return sidecar;
            }

            return FistsDamageWindowSidecar.Default;
        }

        public FistsAlignmentWindowSidecar GetAlignmentWindow(int comboIndex)
        {
            if (ComboAlignmentWindows != null && comboIndex >= 0 && comboIndex < ComboAlignmentWindows.Length)
            {
                var sidecar = ComboAlignmentWindows[comboIndex];
                if (sidecar.EndNormalized < sidecar.StartNormalized)
                {
                    (sidecar.StartNormalized, sidecar.EndNormalized) = (sidecar.EndNormalized, sidecar.StartNormalized);
                }

                return sidecar;
            }

            return FistsAlignmentWindowSidecar.Default;
        }

        public string GetAttackGeometryId()
        {
            if (!string.IsNullOrWhiteSpace(AttackGeometryId))
            {
                return AttackGeometryId.Trim();
            }

            return $"{name}_AttackSweep";
        }

        public string GetAttackGeometryResourcePath()
        {
            return $"AttackClipGeometry/{GetAttackGeometryId()}";
        }
    }
}
