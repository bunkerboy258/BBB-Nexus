using UnityEngine;
using Characters.Player.Animation;

namespace Characters.Player.Data
{
    /// <summary>
    /// 闪避系统配置模块 (ScriptableObject)。
    /// 包含所有与闪避相关的动画数据、体力消耗等。
    /// </summary>
    [CreateAssetMenu(fileName = "DodgingModule", menuName = "Player/Modules/Dodging Module")]
    public class DodgingSO : ScriptableObject
    {
        public float StaminaCost = 20f; // 每次闪避消耗的体力值
        public AnimPlayOptions FadeInIdleOptions;
        public AnimPlayOptions FadeInMoveLoopOptions;

        [Header("Dodge Animations (闪避动画)")]
        [Tooltip("角色在静止或行走时使用的闪避动作 (通常是侧步、后跳)")]
        public WarpedMotionData ForwardDodge;
        public WarpedMotionData BackwardDodge;
        public WarpedMotionData LeftDodge;
        public WarpedMotionData RightDodge;
        public WarpedMotionData ForwardLeftDodge;
        public WarpedMotionData ForwardRightDodge;
        public WarpedMotionData BackwardLeftDodge;
        public WarpedMotionData BackwardRightDodge;

        [Tooltip("角色在跑步或冲刺时使用的闪避动作 (通常是翻滚、滑铲)")]
        public WarpedMotionData MoveForwardDodge;
        public WarpedMotionData MoveBackwardDodge;
        public WarpedMotionData MoveLeftDodge;
        public WarpedMotionData MoveRightDodge;
        public WarpedMotionData MoveForwardLeftDodge;
        public WarpedMotionData MoveForwardRightDodge;
        public WarpedMotionData MoveBackwardLeftDodge;
        public WarpedMotionData MoveBackwardRightDodge;
    }
}
