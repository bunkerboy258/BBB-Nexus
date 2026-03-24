using UnityEngine;

namespace BBBNexus
{
    // 闪避系统配置模块 它统一管理所有8方向闪避的根运动与体力消耗 
    // 闪避的核心是WarpdMotion 在动画播放时动态修改根运动轨迹 实现躲闪效果 
    [CreateAssetMenu(fileName = "DodgingSO", menuName = "BBBNexus/Player/Modules/DodgingSO")]
    public class DodgingSO : ScriptableObject
    {
        [Header("淡入参数 (Fade In Options) - 闪避结束时的动画还原")]
        
        [Tooltip("从闪避回到待机时的淡入参数")]
        public AnimPlayOptions FadeInIdleOptions;
        
        [Tooltip("从闪避回到移动循环时的淡入参数")]
        public AnimPlayOptions FadeInMoveLoopOptions;

        [Header("闪避动画")]
        
        public WarpedMotionData ForwardDodge;
        public WarpedMotionData BackwardDodge;
        public WarpedMotionData LeftDodge;
        public WarpedMotionData RightDodge;
        public WarpedMotionData ForwardLeftDodge;
        public WarpedMotionData ForwardRightDodge;
        public WarpedMotionData BackwardLeftDodge;
        public WarpedMotionData BackwardRightDodge;
    }
}
