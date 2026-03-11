using UnityEngine;
using Animancer;

namespace Characters.Player.Data
{
    // 核心功能模块 它是玩家基础系统的心脏 负责转接物理 视角 速度等底层参数 
    // 别乱改这里的数值 一个小数点的偏差就能让整个控制手感完全变样 
    [CreateAssetMenu(fileName = "CoreModule", menuName = "BBBNexus/Player/Modules/Core Module")]
    public class CoreModuleSO : ScriptableObject
    {
        #region Movement & View 移动与视角 - 根运动映射层 决定玩家如何响应输入
        
        [Header("视角与转向 (View & Rotation) - 相机系统的心脏")]
        
        [Tooltip("鼠标灵敏度 X=水平转向速率 Y=垂直俯仰速率 单位：度/帧 建议150左右")]
        public Vector2 LookSensitivity = new Vector2(150f, 150f);

        [Tooltip("俯仰角限制 X=最小俯仰角(向下看) Y=最大俯仰角(向上看) 常规值为-70~70度")]
        public Vector2 PitchLimits = new Vector2(-70f, 70f);

        [Tooltip("旋转平滑时间 为0时无延迟 增大此值会让转向更跟手(肌肉记忆友好) 建议0.1~0.15秒")]
        public float RotationSmoothTime = 0.12f;

        [Header("移动速度 (Movement Speeds) - 动画混合树的输入源")]
        
        [Tooltip("行走速度 m/s 低速移动时使用 建议2~3")]
        public float WalkSpeed = 2f;
        
        [Tooltip("慢跑速度 m/s 过渡速度 建议4~5")]
        public float JogSpeed = 4f;
        
        [Tooltip("冲刺速度 m/s 全速移动 建议6~8")]
        public float SprintSpeed = 7f;

        [Header("物理与控制 (Physics & Control) - 引擎层的物理感受")]
        
        [Tooltip("重力加速度 负数向下 单位m/s? 一般用-15~-25 越负跌落越快")]
        public float Gravity = -20f;
        
        [Tooltip("反弹力度 碰到天花板时的反弹力 通常为负值 -1表示完全消去垂直速度 别设太大不然能跳穿天")]
        public float ReboundForce = -1f;
        
        [Range(0f, 1f)]
        [Tooltip("空中控制系数 0=无法转向 1=完全控制 空中转向灵敏度的关键参数")]
        public float AirControl = 0.5f;
        
        [Tooltip("移动速度平滑时间 越大越滑 0表示瞬时加速 常规值0.1~0.2秒")]
        public float MoveSpeedSmoothTime = 0.15f;

        [Header("动画混合 (Animation Blending) - 让动画跟上意图管线")]
        
        [Tooltip("前后方向(X)动画参数平滑时间 越大越软 0.2秒左右比较顺手")]
        public float XAnimBlendSmoothTime = 0.2f;
        
        [Tooltip("左右方向(Y)动画参数平滑时间 建议和X一样")]
        public float YAnimBlendSmoothTime = 0.2f;
        
        #endregion

        #region Stamina System 体力系统 - 限制玩家的高耗能动作

        [Header("体力系统 (Stamina System) - 控制玩家体力上限与恢复")]
        
        [Tooltip("最大体力值 作为体力的上限 太小不够用 太大没什么压力感")]
        public float MaxStamina = 1000f;
        
        [Tooltip("体力消耗速率 m/s 冲刺时每秒扣除的体力数值 越大越耗体力")]
        public float StaminaDrainRate = 20f;
        
        [Tooltip("体力恢复速率 m/s 静止或低速时每秒回复的体力数值")]
        public float StaminaRegenRate = 15f;
        
        [Range(0.5f, 2.0f)]
        [Tooltip("行走时恢复加速倍数 大于1则加快恢复 小于1则减速恢复 让玩家感觉行走时能更快回气")]
        public float WalkStaminaRegenMult = 1.5f;
        
        [Range(0f, 1f)]
        [Tooltip("体力恢复阈值 体力值低于此占比时 无法解除枯竭状态(避免瞬间切换) 建议0.2~0.3")]
        public float StaminaRecoverThreshold = 0.2f;
        
        #endregion

        #region Layered Actions & Masks 分层动作与遮罩 - 实现上半身独立驱动

        [Header("分层动作与遮罩 (Layered Actions & Masks) - 上下半身分离的核心")]
        
        [Tooltip("上半身动画遮罩 决定上半身动画能控制哪些骨头(不含双腿) 用于使武器动画不影响下肢")]
        public AvatarMask UpperBodyMask;
        
        [Tooltip("通用攻击动画 作为默认的攻击占位符(实际动画来自武器配置)")]
        public ClipTransition AttackAnim;

        [Header("面部表情 (Facial) - 可选的面部动画层")]
        
        [Tooltip("面部表情遮罩 只控制头部与脸部骨骼 避免影响身体其他部分")]
        public AvatarMask FacialMask;
        
        [Tooltip("眨眼动画 循环播放 可用于心跳表现或其他周期动作")]
        public ClipTransition BlinkAnim;
        
        [Tooltip("受伤脸部表情 瞬时播放 表示角色受伤的那一刻")]
        public ClipTransition HurtFaceAnim;
        
        #endregion

        #region Fall Detection 下落检测 - 判断玩家何时进入自由落体

        [Header("下落检测 (Fall Detection) - 进入下落状态的条件")]
        
        [Range(0, 4)]
        [Tooltip("触发下落状态的最小高度等级 0~4级递增 级数越高触发越难 推荐1级(跳跃高度以上) 可防止走下楼时误触发")]
        public int FallHeightLevelThreshold = 1;

        [Tooltip("进入下落状态的最小垂直速度阈值 负数(向下为负) 单位m/s 建议-5~-10 越接近0越敏感")]
        public float FallVerticalVelocityThreshold = -5f;
        
        #endregion
    }
}
