using Animancer;
using UnityEngine;
using Characters.Player.Animation;

namespace Characters.Player.Data
{
    [CreateAssetMenu(fileName = "LocomotionAnimSet", menuName = "Player/Modules/Locomotion AnimSet")]
    public class LocomotionAnimSetSO : ScriptableObject
    {
        #region FadeTimeSettings  移动相关自定义过度时间
        // 替换为统一的 AnimPlayOptions
        public AnimPlayOptions FadeInWalkStartOptions = AnimPlayOptions.Default;
        public AnimPlayOptions FadeInRunStartOptions = AnimPlayOptions.Default;
        public AnimPlayOptions FadeInSprintStartOptions = AnimPlayOptions.Default;
        public AnimPlayOptions FadeInLoopBreakInOptions;
        [Space]
        public AnimPlayOptions FadeInWalkLoopOptions = AnimPlayOptions.Default;
        public AnimPlayOptions FadeInRunLoopOptions = AnimPlayOptions.Default;
        public AnimPlayOptions FadeInSprintLoopOptions = AnimPlayOptions.Default;
        [Space]
        public AnimPlayOptions FadeInStopWalkOptions;
        public AnimPlayOptions FadeInStopRunOptions;
        public AnimPlayOptions FadeInStopSprintOptions;
        [Space]
        [Header("没有相关模块就不用设置")]
        public AnimPlayOptions FadeInJumpOptions;
        public AnimPlayOptions FadeInVaultOptions;
        public AnimPlayOptions FadeInQuickDodgeOptions;
        public AnimPlayOptions FadeInMoveDodgeOptions;
        #endregion
        #region Locomotion Animations 基础移动动画
        [Header("LOCOMOTION - IDLE - 待机动画")]
        public ClipTransition IdleAnim;

        [Header("LOCOMOTION - LOOPS - 循环动画")]
        public ClipTransition WalkLoopFwd_L;
        public ClipTransition WalkLoopFwd_R;
        [Space]
        public ClipTransition JogLoopFwd_L;
        public ClipTransition JogLoopFwd_R;
        [Space]
        public ClipTransition SprintLoopFwd_L;
        public ClipTransition SprintLoopFwd_R;

        [Header("LOCOMOTION - STOPS - 停止动画")]
        public ClipTransition WalkStopLeft;
        public ClipTransition WalkStopRight;
        [Space]
        public ClipTransition RunStopLeft;
        public ClipTransition RunStopRight;
        [Space]
        public ClipTransition SprintStopLeft;
        public ClipTransition SprintStopRight;
        #endregion

        #region Directional Start Animations 方向启动动画
        [Header("STARTS - WALK - 行走启动")]
        public MotionClipData WalkStartFwd;
        public MotionClipData WalkStartBack;
        public MotionClipData WalkStartLeft;
        public MotionClipData WalkStartRight;
        [Space]
        public MotionClipData WalkStartFwdLeft;
        public MotionClipData WalkStartFwdRight;
        public MotionClipData WalkStartBackLeft;
        public MotionClipData WalkStartBackRight;

        [Header("STARTS - RUN - 跑步启动")]
        public MotionClipData RunStartFwd;
        public MotionClipData RunStartBack;
        public MotionClipData RunStartLeft;
        public MotionClipData RunStartRight;
        [Space]
        public MotionClipData RunStartFwdLeft;
        public MotionClipData RunStartFwdRight;
        public MotionClipData RunStartBackLeft;
        public MotionClipData RunStartBackRight;

        [Header("STARTS - SPRINT - 冲刺启动")]
        public MotionClipData SprintStartFwd;
        public MotionClipData SprintStartBack;
        public MotionClipData SprintStartLeft;
        public MotionClipData SprintStartRight;
        [Space]
        public MotionClipData SprintStartFwdLeft;
        public MotionClipData SprintStartFwdRight;
        public MotionClipData SprintStartBackLeft;
        public MotionClipData SprintStartBackRight;
        #endregion
    }
}
