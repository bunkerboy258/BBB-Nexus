using UnityEngine;

namespace BBBNexus
{
    // 闪避系统配置模块 它统一管理所有8方向闪避的根运动与体力消耗 
    // 闪避的核心是WarpdMotion 在动画播放时动态修改根运动轨迹 实现躲闪效果 
    [CreateAssetMenu(fileName = "DodgingSO", menuName = "BBBNexus/Player/Modules/DodgingSO")]
    public class DodgingSO : ScriptableObject
    {
        [Header("体力消耗 (Stamina Cost) - 每次闪避的代价")]
        
        [Tooltip("每次闪避消耗的体力值 建议20~30 太少闪避成了无消耗通道 太多玩家没快感")]
        public float StaminaCost = 20f;
        
        [Header("淡入参数 (Fade In Options) - 闪避结束时的动画还原")]
        
        [Tooltip("从闪避回到待机时的淡入参数")]
        public AnimPlayOptions FadeInIdleOptions;
        
        [Tooltip("从闪避回到移动循环时的淡入参数")]
        public AnimPlayOptions FadeInMoveLoopOptions;

        [Header("闪避动画 (Dodge Animations) - 8方向的闪避根运动数据")]
        
        [Tooltip("向前闪避 直线躲开前方危险 这是最频繁使用的闪避方向")]
        public WarpedMotionData ForwardDodge;
        
        [Tooltip("向后闪避 后退躲闪 通常在追击时使用")]
        public WarpedMotionData BackwardDodge;
        
        [Tooltip("左转闪避 向左侧闪开")]
        public WarpedMotionData LeftDodge;
        
        [Tooltip("右转闪避 向右侧闪开")]
        public WarpedMotionData RightDodge;
        
        [Tooltip("左前斜闪避 向左前方45度闪开")]
        public WarpedMotionData ForwardLeftDodge;
        
        [Tooltip("右前斜闪避 向右前方45度闪开")]
        public WarpedMotionData ForwardRightDodge;
        
        [Tooltip("左后斜闪避 向左后方45度闪开")]
        public WarpedMotionData BackwardLeftDodge;
        
        [Tooltip("右后斜闪避 向右后方45度闪开")]
        public WarpedMotionData BackwardRightDodge;
    }
}
