using Animancer;
using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 异常状态配置 SO
    ///
    /// 定义施加在 BBBCharacterController 上的异常状态表现，
    /// 例如【被格挡】【硬直】【眩晕】等。
    ///
    /// 运行时由 StatusEffectArbiter 管理激活/过期/覆盖逻辑。
    /// </summary>
    [CreateAssetMenu(fileName = "StatusEffectSO", menuName = "BBBNexus/Combat/StatusEffect")]
    public class StatusEffectSO : ScriptableObject
    {
        #region 基本信息

        [Header("基本信息")]

        [Tooltip("状态显示名称（调试/UI用）")]
        public string DisplayName = "未命名状态";

        [Tooltip("状态描述")]
        [TextArea(2, 4)]
        public string Description;

        #endregion

        #region 动画

        [Header("通用 / 被格挡 Clip")]

        [Tooltip("通用动画 Clip，同时作为方向性受击的 fallback（方向槽为空时使用此项）")]
        public ClipTransition Clip;

        [Header("方向性受击僵直 Clips（4向）")]

        [Tooltip("正面受击僵直")]
        public ClipTransition ClipHitFront;

        [Tooltip("背面受击僵直")]
        public ClipTransition ClipHitBack;

        [Tooltip("左侧受击僵直")]
        public ClipTransition ClipHitLeft;

        [Tooltip("右侧受击僵直")]
        public ClipTransition ClipHitRight;

        [Header("方向性击退 Clips（4向）")]

        [Tooltip("正面击退")]
        public ClipTransition ClipKnockbackFront;

        [Tooltip("背面击退")]
        public ClipTransition ClipKnockbackBack;

        [Tooltip("左侧击退")]
        public ClipTransition ClipKnockbackLeft;

        [Tooltip("右侧击退")]
        public ClipTransition ClipKnockbackRight;

        [Header("击倒")]

        [Tooltip("击倒动画 Clip（倒地）")]
        public ClipTransition ClipKnockdown;

        [Header("动画播放参数")]

        [Tooltip("动画播放参数（层级、淡入时间、速度等）\n" +
                 "Layer=0 全身  Layer=1 上半身")]
        public AnimPlayOptions PlayOptions = AnimPlayOptions.Default;

        [Tooltip("状态结束后淡回正常动画的时间（秒）")]
        [Min(0f)]
        public float FadeOutDuration = 0.2f;

        #endregion

        #region 持续时间与优先级

        [Header("持续时间与优先级")]

        [Tooltip("状态持续时间（秒）。0 = 永久，需外部手动结束")]
        [Min(0f)]
        public float Duration = 1f;

        [Tooltip("重复施加时是否刷新计时（true=刷新，false=忽略重复）")]
        public bool CanBeRefreshed = true;

        [Tooltip("优先级。高优先级状态可覆盖低优先级状态（同优先级不覆盖）")]
        [Min(0)]
        public int Priority = 10;

        [Header("仲裁语义")]

        [Tooltip("None = 不断连段；Soft = 受影响但尽量不断连段；Hard = 明确断连段并接管状态域。")]
        public StatusInterruptMode InterruptMode = StatusInterruptMode.Hard;

        #endregion

        #region 仲裁标志（状态期间阻断哪些系统）

        [Header("仲裁阻断（状态期间生效）")]

        [Tooltip("是否阻断输入处理")]
        public bool BlockInput = false;

        [Tooltip("是否阻断动作（OverrideState 请求）")]
        public bool BlockAction = false;

        [Tooltip("是否阻断上半身系统")]
        public bool BlockUpperBody = false;

        [Tooltip("是否阻断背包/装备切换")]
        public bool BlockInventory = false;

        [Tooltip("是否阻断 IK 解算")]
        public bool BlockIK = false;

        [Tooltip("是否使盾牌格挡失效（用于完美弹反后卸甲效果）")]
        public bool BlockShield = false;

        [Header("HitStop")]

        [HideInInspector]
        [Tooltip("是否为卡肉状态。启用后不会播放新的受击动画，而是冻结角色当前时间。")]
        public bool IsHitStop = false;

        [Tooltip("角色动画速度倍率。0 = 完全暂停，0.05 = 极慢推进。")]
        [Range(0f, 1f)]
        public float HitStopAnimationSpeed = 0f;

        [Tooltip("是否冻结角色运动。")]
        public bool FreezeMotion = true;

        #endregion

        #region 工具方法

        /// <summary>
        /// 将阻断配置合并写入仲裁标志（叠加模式，不清除已有标志）
        /// </summary>
        public void ApplyBlockFlagsTo(ref ArbitrationFlags flags)
        {
            if (BlockInput)      flags.BlockInput      = true;
            if (BlockAction)     flags.BlockAction     = true;
            if (BlockUpperBody)  flags.BlockUpperBody  = true;
            if (BlockInventory)  flags.BlockInventory  = true;
            if (BlockIK)         flags.BlockIK         = true;
            if (BlockShield)     flags.BlockShield     = true;
        }

        /// <summary>
        /// 根据受击方向角（相对角色自身朝向，度数）选取对应的方向性 Clip。
        /// 无对应方向 Clip 时 fallback 到 <see cref="Clip"/>。
        /// </summary>
        /// <param name="angleFromForward">
        /// 攻击来源方向与角色 forward 的夹角（-180~180°，正值=右侧）。
        /// 传 float.NaN 或不关心方向时直接返回 <see cref="Clip"/>。
        /// </param>
        public ClipTransition SelectHitClip(float angleFromForward = float.NaN)
        {
            if (!float.IsNaN(angleFromForward))
            {
                float abs = Mathf.Abs(angleFromForward);

                ClipTransition directional =
                    abs <= 45f   ? ClipHitFront :   // 正面
                    abs >= 135f  ? ClipHitBack  :   // 背面
                    angleFromForward > 0f ? ClipHitRight : ClipHitLeft; // 左右

                if (directional?.Clip != null)
                    return directional;
            }

            return Clip;
        }

        #endregion
    }
}
