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

        [Header("动画表现")]

        [Tooltip("状态期间循环/播放的动画 Clip")]
        public ClipTransition Clip;

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
        }

        #endregion
    }
}
