using UnityEngine;

namespace BBBNexus
{
    // 翻滚系统配置模块 与闪避系统类似 
    [CreateAssetMenu(fileName = "RollSO", menuName = "BBBNexus/Player/Modules/RollSO")]
    public class RollSO : ScriptableObject
    {
        [Header("体力消耗 (Stamina Cost) - 每次翻滚的代价")]
        
        [Tooltip("每次翻滚消耗的体力值 通常大于闪避 因为翻滚耗时更长 建议30~50")]
        public float StaminaCost = 30f;
        
        [Header("淡入参数 (Fade In Options) - 翻滚结束时的动画还原")]
        
        [Tooltip("从翻滚回到待机时的淡入参数")]
        public AnimPlayOptions FadeInIdleOptions;
        
        [Tooltip("从翻滚回到移动循环时的淡入参数")]
        public AnimPlayOptions FadeInMoveLoopOptions;

        [Header("翻滚动画 (Roll Animations) - 8方向的翻滚根运动数据")]
        
        [Tooltip("向前翻滚 翻过去进行冲刺或躲避 这是翻滚的标准方向")]
        public WarpedMotionData ForwardRoll;
        
        [Tooltip("向后翻滚 后退翻滚 用于快速改变局势")]
        public WarpedMotionData BackwardRoll;
        
        [Tooltip("向左翻滚")]
        public WarpedMotionData LeftRoll;
        
        [Tooltip("向右翻滚")]
        public WarpedMotionData RightRoll;
        
        [Tooltip("左前斜翻滚 向左前方45度翻滚")]
        public WarpedMotionData ForwardLeftRoll;
        
        [Tooltip("右前斜翻滚 向右前方45度翻滚")]
        public WarpedMotionData ForwardRightRoll;
        
        [Tooltip("左后斜翻滚 向左后方45度翻滚")]
        public WarpedMotionData BackwardLeftRoll;
        
        [Tooltip("右后斜翻滚 向右后方45度翻滚")]
        public WarpedMotionData BackwardRightRoll;
    }
}
