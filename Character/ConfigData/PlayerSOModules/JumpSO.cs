using UnityEngine;

namespace BBBNexus
{
    // 跳跃与落地模块 它统一管理所有竖直方向的运动 包括跳跃 二段跳 落地恢复等 
    // 这里的所有参数都会被重力系统持续查询
    [CreateAssetMenu(fileName = "JumpSO", menuName = "BBBNexus/Player/Modules/JumpSO")]
    public class JumpSO : ScriptableObject
    {
        #region Jump & Double Jump 跳跃系统 - 竖直方向的冲量控制

        [Header("基础跳跃 (Base Jump) - 默认跳跃高度与动画")]
        
        [Tooltip("跳跃初速度 m/s 正数向上 这是跳跃的原始冲量 决定最大跳跃高度 建议5~8")]
        public float JumpForce = 6f;
        
        [Tooltip("跳跃动画数据 包含根运动与播放速率等参数")]
        public MotionClipData JumpAirAnim;

        [Header("行走跳跃 (Walk Jump) - 行走时跳跃的弱化版本")]
        
        [Tooltip("行走时跳跃初速度 通常小于默认值 让移动中跳跃不会太高")]
        public float JumpForceWalk = 5f;
        
        [Tooltip("行走跳跃的动画")]
        public MotionClipData JumpAirAnimWalk;

        [Header("冲刺跳跃 (Sprint Jump) - 冲刺时的蓄力跳跃")]
        
        [Tooltip("冲刺时跳跃初速度 通常大于默认值 冲刺跳得更高")]
        public float JumpForceSprint = 7f;
        
        [Tooltip("冲刺跳跃的动画")]
        public MotionClipData JumpAirAnimSprint;

        [Header("空手冲刺跳跃 (Sprint Empty Hand Jump) - 没有武器时的额外加成")]
        
        [Tooltip("空手冲刺时的跳跃初速度 比持武器版本更高 为了手感激励")]
        public float JumpForceSprintEmpty = 8f;
        
        [Tooltip("空手冲刺跳跃的动画")]
        public MotionClipData JumpAirAnimSprintEmpty;

        [Header("二段跳 (Double Jump) - 空中的第二次起跳")]
        
        [Tooltip("二段跳向上初速度 m/s 不建议太大 一般2~3倍重力加速度即可")]
        public float DoubleJumpForceUp = 6f;
        
        [Tooltip("空手冲刺时二段跳的初速度 通常比持武器版本高")]
        public float DoubleJumpEmptyHandSprintForceUp = 8f;

        [Tooltip("二段跳动画及其根运动数据")]
        public MotionClipData DoubleJumpUp;
        
        [Tooltip("二段跳的淡入参数 通常需要快速反应")]
        public AnimPlayOptions DoubleJumpFadeInOptions;

        [Tooltip("冲刺二段跳可能触发翻滚的替代动画(可选)")]
        public MotionClipData DoubleJumpSprintRoll;
        
        [Tooltip("翻滚版二段跳的淡入参数")]
        public AnimPlayOptions DoubleJumpSprintRollFadeInOptions;
        
        #endregion

        #region Landing System 落地系统 - 从空中回到地面的缓冲与恢复

        [Header("落地高度等级 (Fall Height Levels) - 根据坠落高度分级处理")]
        
        [Tooltip("0级落地高度阈值 米 最低跌落距离 超过此值但未到1级时播放0级动画")]
        public float LandHeight_Level0 = 2f;
        
        [Tooltip("0级落地的淡入选项 轻微着陆")]
        public AnimPlayOptions LandHeight_Level0_options;
        
        [Tooltip("1级落地高度阈值 米")]
        public float LandHeight_Level1 = 2f;
        
        [Tooltip("1级落地的淡入选项 中等着陆")]
        public AnimPlayOptions LandHeight_Level1_options;
        
        [Tooltip("2级落地高度阈值 米")]
        public float LandHeight_Level2 = 5f;
        
        [Tooltip("2级落地的淡入选项 重力着陆")]
        public AnimPlayOptions LandHeight_Level2_options;
        
        [Tooltip("3级落地高度阈值 米")]
        public float LandHeight_Level3 = 8f;
        
        [Tooltip("3级落地的淡入选项 冲击着陆 可能要求特殊姿态")]
        public AnimPlayOptions LandHeight_Level3_options;
        
        [Tooltip("4级落地高度阈值 米 极限高度 超过此值为最危险着陆")]
        public float LandHeight_Level4 = 12f;
        
        [Tooltip("4级落地的淡入选项 极限着陆 通常伴随伤害或特殊反应")]
        public AnimPlayOptions LandHeight_Level4_options;

        [Header("落地恢复动画 (Landing Recovery) - 从不同高度着陆后的恢复")]
        
        [Tooltip("落地后回到待机状态时的淡入参数")]
        public AnimPlayOptions LandToIdleOptions = AnimPlayOptions.Default;

        [Header("行走/慢跑落地缓冲 (Walk/Jog Landing Buffer) - 低速状态下的落地")]
        
        [Tooltip("0级高度的着陆缓冲动画 行走/慢跑版本")]
        public MotionClipData LandBuffer_WalkJog_L0;
        
        [Tooltip("0级着陆后回到循环的淡入参数")]
        public AnimPlayOptions LandToLoopFadeInTime_WalkJog_L0ptions = AnimPlayOptions.Default;
        
        [Tooltip("1级高度的着陆缓冲动画 行走/慢跑版本")]
        public MotionClipData LandBuffer_WalkJog_L1;
        
        [Tooltip("1级着陆后回到循环的淡入参数")]
        public AnimPlayOptions LandToLoopFadeInTime_WalkJog_L1ptions = AnimPlayOptions.Default;
        
        [Tooltip("2级高度的着陆缓冲动画 行走/慢跑版本")]
        public MotionClipData LandBuffer_WalkJog_L2;
        
        [Tooltip("2级着陆后回到循环的淡入参数")]
        public AnimPlayOptions LandToLoopFadeInTime_WalkJog_L2ptions = AnimPlayOptions.Default;
        
        [Tooltip("3级高度的着陆缓冲动画 行走/慢跑版本")]
        public MotionClipData LandBuffer_WalkJog_L3;
        
        [Tooltip("3级着陆后回到循环的淡入参数")]
        public AnimPlayOptions LandToLoopFadeInTime_WalkJog_L3ptions = AnimPlayOptions.Default;

        [Header("冲刺落地缓冲 (Sprint Landing Buffer) - 高速状态下的落地")]
        
        [Tooltip("0级高度的着陆缓冲动画 冲刺版本")]
        public MotionClipData LandBuffer_Sprint_L0;
        
        [Tooltip("0级着陆后回到循环的淡入参数")]
        public AnimPlayOptions LandToLoopFadeInTime_Sprint_L0ptions = AnimPlayOptions.Default;
        
        [Tooltip("1级高度的着陆缓冲动画 冲刺版本")]
        public MotionClipData LandBuffer_Sprint_L1;
        
        [Tooltip("1级着陆后回到循环的淡入参数")]
        public AnimPlayOptions LandToLoopFadeInTime_Sprint_L1ptions = AnimPlayOptions.Default;
        
        [Tooltip("2级高度的着陆缓冲动画 冲刺版本")]
        public MotionClipData LandBuffer_Sprint_L2;
        
        [Tooltip("2级着陆后回到循环的淡入参数")]
        public AnimPlayOptions LandToLoopFadeInTime_Sprint_L2ptions = AnimPlayOptions.Default;
        
        [Tooltip("3级高度的着陆缓冲动画 冲刺版本")]
        public MotionClipData LandBuffer_Sprint_L3;
        
        [Tooltip("3级着陆后回到循环的淡入参数")]
        public AnimPlayOptions LandToLoopFadeInTime_Sprint_L3ptions = AnimPlayOptions.Default;

        [Header("极限落地 (Critical Landing) - 超越限制高度的致命着陆")]
        
        [Tooltip("超过4级极限高度后的着陆缓冲动画 通常包含伤害或摔倒效果")]
        public MotionClipData LandBuffer_ExceedLimit;
        
        [Tooltip("极限着陆后的恢复参数")]
        public AnimPlayOptions LandToLoopFadeInTime_ExceedLimitOptions = AnimPlayOptions.Default;
        
        #endregion
    }
}
