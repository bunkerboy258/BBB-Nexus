using Animancer;
using UnityEngine;

namespace Characters.Player.Data
{
    [CreateAssetMenu(fileName = "LocomotionAnimSet", menuName = "Player/Modules/Locomotion AnimSet")]
    public class LocomotionAnimSetSO : ScriptableObject
    {
        #region FadeTimeSettings  移动相关自定义过度时间
        public float FadeInWalkStart = 0f;
        public float FadeInRunStart = 0f;
        public float FadeInSprintStart = 0f;
        public float FadeInLoopBreakIn = 0.4f;
        [Space]
        public float FadeInWalkLoop = 0f;
        public float FadeInRunLoop = 0f;
        public float FadeInSprintLoop = 0f;
        [Space]
        public float FadeInStopWalk = 0.3f;
        public float FadeInStopRun = 0.3f;
        public float FadeInStopSprint = 0.3f;
        [Space]
        [Header("没有相关模块就不用设置")]
        public float FadeInJump = 0.2f;
        public float FadeInVault = 0.3f;
        public float FadeInQuickDodge = 0.3f;
        public float FadeInMoveDodge = 0.3f;
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
