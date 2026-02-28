using UnityEngine;
using Characters.Player.Animation;

namespace Characters.Player.Data
{
    /// <summary>
    /// 翻滚系统配置模块 (ScriptableObject)。
    /// 包含所有与翻滚相关的动画数据、体力消耗等。
    /// 逻辑与闪避系统类似，但使用不同的动画资源。
    /// </summary>
    [CreateAssetMenu(fileName = "RollModule", menuName = "Player/Modules/Roll Module")]
    public class RollSO : ScriptableObject
    {
        public float StaminaCost = 30f; // 每次翻滚消耗的体力值
        public AnimPlayOptions FadeInIdleOptions;
        public AnimPlayOptions FadeInMoveLoopOptions;

        [Header("Roll Animations (翻滚动画)")]
        [Tooltip("角色向前、向后、向左、向右翻滚动作")]
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
