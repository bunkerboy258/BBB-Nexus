using UnityEngine;
using Animancer;

namespace BBBNexus
{
    // 核心功能模块 它是玩家基础系统的心脏 负责转接物理 视角 速度等底层参数 
    // 别乱改这里的数值 一个小数点的偏差就能让整个控制手感完全变样 
    [CreateAssetMenu(fileName = "CoreSO", menuName = "BBBNexus/Player/Modules/CoreSO")]
    public class CoreSO : ScriptableObject
    {
        #region 基础游戏属性
        [Tooltip("生命值的上限")]
        public float MaxHealth = 100f;

        [Tooltip("死亡动画 生命值归零时强制播放")]
        public AnimationClip DeathAnim;
        #endregion

        #region LOD 性能降级设置

        [Header("性能降级设置 (LOD) - 决定同屏AI的性能消耗")]

        [Tooltip("LOD 距离与状态检测的间隔时间(秒)")]
        public float LODCheckInterval = 0.5f;

        [Tooltip("大于此距离进入 Medium 级别：关闭面部表情与精细手脚 IK")]
        public float MediumLODDistance = 15f;

        [Tooltip("大于此距离进入 Low 级别：动画底层进入剔除")]
        public float LowLODDistance = 30f;

        #endregion

        #region 移动与视角

        [Header("视角与转向")]
        
        [Tooltip("鼠标灵敏度 X=水平转向速率 Y=垂直俯仰速率 单位：度/帧 ")]
        public Vector2 LookSensitivity = new Vector2(150f, 150f);

        [Tooltip("俯仰角限制 X=最小俯仰角(向下看) Y=最大俯仰角(向上看) ")]
        public Vector2 PitchLimits = new Vector2(-70f, 70f);

        [Tooltip("旋转平滑时间")]
        public float RotationSmoothTime = 0.12f;

        [Header("移动速度")]
        
        [Tooltip("行走速度")]
        public float WalkSpeed = 2f;
        
        [Tooltip("慢跑速度")]
        public float JogSpeed = 4f;
        
        [Tooltip("冲刺速度")]
        public float SprintSpeed = 7f;

        [Header("物理与控制")]
        
        [Tooltip("重力加速度 负数向下 单位m/s")]
        public float Gravity = -20f;
        
        [Tooltip("反弹力度")]
        public float ReboundForce = -1f;
        
        [Range(0f, 1f)]
        [Tooltip("空中控制系数 0=无法转向 1=完全控制")]
        public float AirControl = 0.5f;
        
        [Tooltip("移动速度平滑时间")]
        public float MoveSpeedSmoothTime = 0.15f;

        [Header("动画混合")]
        
        [Tooltip("前后方向(X)动画参数平滑时间")]
        public float XAnimBlendSmoothTime = 0.2f;
        
        [Tooltip("左右方向(Y)动画参数平滑时间 建议和X一样")]
        public float YAnimBlendSmoothTime = 0.2f;
        
        #endregion

        #region 体力系统

        [Header("体力系统")]
        
        [Tooltip("最大体力值")]
        public float MaxStamina = 1000f;
        
        [Tooltip("体力消耗速率 m/s")]
        public float StaminaDrainRate = 20f;
        
        [Tooltip("体力恢复速率 m/s")]
        public float StaminaRegenRate = 15f;
        
        [Range(0.5f, 2.0f)]
        [Tooltip("行走时恢复加速倍数")]
        public float WalkStaminaRegenMult = 1.5f;
        
        [Range(0f, 1f)]
        [Tooltip("体力恢复阈值")]
        public float StaminaRecoverThreshold = 0.2f;
        
        #endregion

        #region 分层动作与遮罩

        [Header("分层动作与遮罩")]
        
        [Tooltip("上半身动画遮罩")]
        public AvatarMask UpperBodyMask;

        [Header("面部动画遮罩")]
        
        [Tooltip("面部表情遮罩")]
        public AvatarMask FacialMask;
        
        
        #endregion

        #region 下落检测

        [Header("下落检测")]
        
        [Range(0, 4)]
        [Tooltip("触发下落状态的最小高度等级 0~4级递增 级数越高触发越难")]
        public int FallHeightLevelThreshold = 1;

        [Tooltip("进入下落状态的最小垂直速度阈值")]
        public float FallVerticalVelocityThreshold = -5f;
        
        #endregion
    }
}
