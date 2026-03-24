using Animancer;
using UnityEngine;

namespace BBBNexus
{
    // 动画集合配置模块 它存储所有移动状态的动画与过渡参数 
    // 这里的所有动画都是静态资源 不会在运行时修改 直接从磁盘序列化加载 
    [CreateAssetMenu(fileName = "LocomotionSO", menuName = "BBBNexus/Player/Modules/LocomotionSO")]
    public class LocomotionSO : ScriptableObject
    {
        #region FadeTimeSettings 淡入参数 - 控制动画切换的流畅度

        [Header("淡入过渡参数")]
        
        [Tooltip("行走启动动画的淡入选项")]
        public AnimPlayOptions FadeInWalkStartOptions = AnimPlayOptions.Default;
        
        [Tooltip("跑步启动动画的淡入选项")]
        public AnimPlayOptions FadeInRunStartOptions = AnimPlayOptions.Default;
        
        [Tooltip("冲刺启动动画的淡入选项")]
        public AnimPlayOptions FadeInSprintStartOptions = AnimPlayOptions.Default;
        
        [Tooltip("打断进入循环动画的淡入选项")]
        public AnimPlayOptions FadeInLoopBreakInOptions;
        
        [Space]
        [Tooltip("行走循环动画的淡入选项")]
        public AnimPlayOptions FadeInWalkLoopOptions = AnimPlayOptions.Default;
        
        [Tooltip("跑步循环动画的淡入选项")]
        public AnimPlayOptions FadeInRunLoopOptions = AnimPlayOptions.Default;
        
        [Tooltip("冲刺循环动画的淡入选项")]
        public AnimPlayOptions FadeInSprintLoopOptions = AnimPlayOptions.Default;
        
        [Space]
        [Tooltip("行走停止动画的淡入选项")]
        public AnimPlayOptions FadeInStopWalkOptions = AnimPlayOptions.Default;
        
        [Tooltip("跑步停止动画的淡入选项")]
        public AnimPlayOptions FadeInStopRunOptions = AnimPlayOptions.Default;
        
        [Tooltip("冲刺停止动画的淡入选项")]
        public AnimPlayOptions FadeInStopSprintOptions = AnimPlayOptions.Default;
        
        [Space]
        [Header("高级动作淡入")]
        
        [Tooltip("跳跃动画的淡入选项")]
        public AnimPlayOptions FadeInJumpOptions = AnimPlayOptions.Default;
        
        [Tooltip("下落动画的淡入选项")]
        public AnimPlayOptions FadeInFallOptions = AnimPlayOptions.Default;
        
        [Tooltip("翻越动画的淡入选项")]
        public AnimPlayOptions FadeInVaultOptions = AnimPlayOptions.Default;
        
        [Tooltip("闪避的淡入选项")]
        public AnimPlayOptions FadeInQuickDodgeOptions = AnimPlayOptions.Default;
        
        [Tooltip("移动中闪避的淡入选项")]
        public AnimPlayOptions FadeInMoveDodgeOptions = AnimPlayOptions.Default;
        
        #endregion

        #region  下落检测

        [Header("下落检测")]
        
        [Tooltip("触发下落的滞空时间阈值")]
        public float AirborneTimeThresholdForFall = 0.3f;
        
        #endregion

        #region Locomotion Animations 基础动画 - 行走 跑步 冲刺的核心动画集

        [Header("基础动画库")]
        
        [Tooltip("待机动画")]
        public ClipTransition IdleAnim;
        
        [Header("下落动画")]
        [Tooltip("下落动画 ")]
        public ClipTransition FallAnim;

        [Header("循环动画")]
       
        public ClipTransition WalkLoopFwd_L;
        public ClipTransition WalkLoopFwd_R;
        
        [Space]
        public ClipTransition JogLoopFwd_L;
        public ClipTransition JogLoopFwd_R;
        
        [Space]
        public ClipTransition SprintLoopFwd_L;
        public ClipTransition SprintLoopFwd_R;

        [Header("急停动画")]
        public ClipTransition WalkStopLeft;
        public ClipTransition WalkStopRight;
        
        [Space]
        public ClipTransition RunStopLeft;
        public ClipTransition RunStopRight;
        
        [Space]
        public ClipTransition SprintStopLeft;
        public ClipTransition SprintStopRight;
        
        #endregion

        #region 起步动画

        [Header("起步动画")]
        
        [Header("行走起步")]
        public MotionClipData WalkStartFwd;
        public MotionClipData WalkStartBack;
        public MotionClipData WalkStartLeft;
        public MotionClipData WalkStartRight;
        public MotionClipData WalkStartFwdLeft;
        public MotionClipData WalkStartFwdRight;
        public MotionClipData WalkStartBackLeft;
        public MotionClipData WalkStartBackRight;

        [Header("跑步起步")]
        public MotionClipData RunStartFwd;
        public MotionClipData RunStartBack;
        public MotionClipData RunStartLeft;
        public MotionClipData RunStartRight;
        public MotionClipData RunStartFwdLeft;
        public MotionClipData RunStartFwdRight;
        public MotionClipData RunStartBackLeft;
        public MotionClipData RunStartBackRight;

        [Header("冲刺起步")]
        public MotionClipData SprintStartFwd;
        public MotionClipData SprintStartBack;
        public MotionClipData SprintStartLeft;
        public MotionClipData SprintStartRight;
        public MotionClipData SprintStartFwdLeft;
        public MotionClipData SprintStartFwdRight;
        public MotionClipData SprintStartBackLeft;
        public MotionClipData SprintStartBackRight;
        
        #endregion
    }
}
