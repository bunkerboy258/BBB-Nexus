using UnityEngine;

namespace BBBNexus
{
    // 翻滚系统配置模块 与闪避系统类似 
    [CreateAssetMenu(fileName = "RollSO", menuName = "BBBNexus/Player/Modules/RollSO")]
    public class RollSO : ScriptableObject
    {
        
        [Header("淡入参数")]
        
        [Tooltip("从翻滚回到待机时的淡入参数")]
        public AnimPlayOptions FadeInIdleOptions;
        
        [Tooltip("从翻滚回到移动循环时的淡入参数")]
        public AnimPlayOptions FadeInMoveLoopOptions;

        [Header("翻滚")]
        public WarpedMotionData ForwardRoll;
        public WarpedMotionData BackwardRoll;
        public WarpedMotionData LeftRoll;
        public WarpedMotionData RightRoll;
        public WarpedMotionData ForwardLeftRoll;
        public WarpedMotionData ForwardRightRoll;
        public WarpedMotionData BackwardLeftRoll;
        public WarpedMotionData BackwardRightRoll;
    }
}
