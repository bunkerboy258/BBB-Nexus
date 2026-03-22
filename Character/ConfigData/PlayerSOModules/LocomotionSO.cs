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

        [Header("淡入过渡参数 (Fade In Options) - 每个动画切换的平滑方案")]
        
        [Tooltip("行走启动动画的淡入选项 控制启动时的动画过渡参数(速率 起始位置等)")]
        public AnimPlayOptions FadeInWalkStartOptions = AnimPlayOptions.Default;
        
        [Tooltip("跑步启动动画的淡入选项")]
        public AnimPlayOptions FadeInRunStartOptions = AnimPlayOptions.Default;
        
        [Tooltip("冲刺启动动画的淡入选项")]
        public AnimPlayOptions FadeInSprintStartOptions = AnimPlayOptions.Default;
        
        [Tooltip("循环断裂处理 用于在中途中断循环时的平滑处理(可选 通常留空)")]
        public AnimPlayOptions FadeInLoopBreakInOptions;
        
        [Space]
        [Tooltip("行走循环动画的淡入选项 进入持续行走时使用")]
        public AnimPlayOptions FadeInWalkLoopOptions = AnimPlayOptions.Default;
        
        [Tooltip("跑步循环动画的淡入选项")]
        public AnimPlayOptions FadeInRunLoopOptions = AnimPlayOptions.Default;
        
        [Tooltip("冲刺循环动画的淡入选项")]
        public AnimPlayOptions FadeInSprintLoopOptions = AnimPlayOptions.Default;
        
        [Space]
        [Tooltip("行走停止动画的淡入选项 从行走转向停止时使用")]
        public AnimPlayOptions FadeInStopWalkOptions = AnimPlayOptions.Default;
        
        [Tooltip("跑步停止动画的淡入选项")]
        public AnimPlayOptions FadeInStopRunOptions = AnimPlayOptions.Default;
        
        [Tooltip("冲刺停止动画的淡入选项")]
        public AnimPlayOptions FadeInStopSprintOptions = AnimPlayOptions.Default;
        
        [Space]
        [Header("高级动作淡入 (Advanced Action Options) - 跳跃 下落 翻越等")]
        
        [Tooltip("跳跃动画的淡入选项 触发跳跃时使用 通常要求快速反应")]
        public AnimPlayOptions FadeInJumpOptions = AnimPlayOptions.Default;
        
        [Tooltip("下落动画的淡入选项 空中坠落时使用")]
        public AnimPlayOptions FadeInFallOptions = AnimPlayOptions.Default;
        
        [Tooltip("翻越动画的淡入选项 越过障碍物时使用")]
        public AnimPlayOptions FadeInVaultOptions = AnimPlayOptions.Default;
        
        [Tooltip("快速闪避的淡入选项 原地闪避时使用")]
        public AnimPlayOptions FadeInQuickDodgeOptions = AnimPlayOptions.Default;
        
        [Tooltip("移动中闪避的淡入选项 边跑边躲时使用")]
        public AnimPlayOptions FadeInMoveDodgeOptions = AnimPlayOptions.Default;
        
        #endregion

        #region Fall Detection Settings 下落检测 - 触发下落动画的条件

        [Header("下落检测 (Fall Detection) - 何时播放下落动画")]
        
        [Tooltip("空中时间阈值 秒数 超过此时长后 WantsToFall 意图才为真 防止跳跃时误触发下落 建议0.3秒")]
        public float AirborneTimeThresholdForFall = 0.3f;
        
        #endregion

        #region Locomotion Animations 基础动画 - 行走 跑步 冲刺的核心动画集

        [Header("基础动画库 (Basic Animations) - 低速 中速 高速的循环与停止")]
        
        [Tooltip("待机动画 玩家静止不动时播放的动画")]
        public ClipTransition IdleAnim;
        
        [Header("下落动画 (Fall Animation) - 空中坠落时的动画")]
        [Tooltip("下落动画 玩家失去接地点后播放 通常是开放手臂或蜷缩的动作")]
        public ClipTransition FallAnim;

        [Header("循环动画 (Loop Animations) - 持续移动时的动画")]
        
        [Tooltip("行走向前循环动画 左脚领先版本 用于循环播放")]
        public ClipTransition WalkLoopFwd_L;
        
        [Tooltip("行走向前循环动画 右脚领先版本 这两个轮流播放实现无缝循环")]
        public ClipTransition WalkLoopFwd_R;
        
        [Space]
        [Tooltip("跑步向前循环动画 左脚领先")]
        public ClipTransition JogLoopFwd_L;
        
        [Tooltip("跑步向前循环动画 右脚领先")]
        public ClipTransition JogLoopFwd_R;
        
        [Space]
        [Tooltip("冲刺向前循环动画 左脚领先")]
        public ClipTransition SprintLoopFwd_L;
        
        [Tooltip("冲刺向前循环动画 右脚领先")]
        public ClipTransition SprintLoopFwd_R;

        [Header("停止动画 (Stop Animations) - 从运动停下来时的动画")]
        
        [Tooltip("行走停止动画 左脚停止版本 从行走状态制动时播放")]
        public ClipTransition WalkStopLeft;
        
        [Tooltip("行走停止动画 右脚停止版本")]
        public ClipTransition WalkStopRight;
        
        [Space]
        [Tooltip("跑步停止动画 左脚停止版本")]
        public ClipTransition RunStopLeft;
        
        [Tooltip("跑步停止动画 右脚停止版本")]
        public ClipTransition RunStopRight;
        
        [Space]
        [Tooltip("冲刺停止动画 左脚停止版本")]
        public ClipTransition SprintStopLeft;
        
        [Tooltip("冲刺停止动画 右脚停止版本")]
        public ClipTransition SprintStopRight;
        
        #endregion

        #region Directional Start Animations 启动动画 - 8方向启动的根运动动画

        [Header("启动动画库 (Start Animations) - 带根运动的8方向启动")]
        
        [Header("行走启动 (Walk Start) - 从静止加速到行走的根运动动画")]
        [Tooltip("向前行走启动 根运动已烘焙 触发行走时播放")]
        public MotionClipData WalkStartFwd;
        
        [Tooltip("向后行走启动 后退时使用")]
        public MotionClipData WalkStartBack;
        
        [Tooltip("左转行走启动")]
        public MotionClipData WalkStartLeft;
        
        [Tooltip("右转行走启动")]
        public MotionClipData WalkStartRight;
        
        [Space]
        [Tooltip("左前方斜行启动 对角线运动")]
        public MotionClipData WalkStartFwdLeft;
        
        [Tooltip("右前方斜行启动")]
        public MotionClipData WalkStartFwdRight;
        
        [Tooltip("左后方斜行启动")]
        public MotionClipData WalkStartBackLeft;
        
        [Tooltip("右后方斜行启动")]
        public MotionClipData WalkStartBackRight;

        [Header("跑步启动 (Run Start) - 跑步的8方向启动")]
        
        [Tooltip("向前跑步启动")]
        public MotionClipData RunStartFwd;
        
        [Tooltip("向后跑步启动")]
        public MotionClipData RunStartBack;
        
        [Tooltip("左转跑步启动")]
        public MotionClipData RunStartLeft;
        
        [Tooltip("右转跑步启动")]
        public MotionClipData RunStartRight;
        
        [Space]
        [Tooltip("左前方斜跑启动")]
        public MotionClipData RunStartFwdLeft;
        
        [Tooltip("右前方斜跑启动")]
        public MotionClipData RunStartFwdRight;
        
        [Tooltip("左后方斜跑启动")]
        public MotionClipData RunStartBackLeft;
        
        [Tooltip("右后方斜跑启动")]
        public MotionClipData RunStartBackRight;

        [Header("冲刺启动 (Sprint Start) - 冲刺的8方向启动")]
        
        [Tooltip("向前冲刺启动")]
        public MotionClipData SprintStartFwd;
        
        [Tooltip("向后冲刺启动")]
        public MotionClipData SprintStartBack;
        
        [Tooltip("左转冲刺启动")]
        public MotionClipData SprintStartLeft;
        
        [Tooltip("右转冲刺启动")]
        public MotionClipData SprintStartRight;
        
        [Space]
        [Tooltip("左前方斜冲启动")]
        public MotionClipData SprintStartFwdLeft;
        
        [Tooltip("右前方斜冲启动")]
        public MotionClipData SprintStartFwdRight;
        
        [Tooltip("左后方斜冲启动")]
        public MotionClipData SprintStartBackLeft;
        
        [Tooltip("右后方斜冲启动")]
        public MotionClipData SprintStartBackRight;
        
        #endregion
    }
}
