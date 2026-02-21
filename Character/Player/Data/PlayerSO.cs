using Animancer;
using Items.Data;
using System.Collections.Generic;
using UnityEngine;

namespace Characters.Player.Data
{
    public enum FootPhase { LeftFootDown, RightFootDown }

    [System.Serializable]
    public enum MotionType
    {
        InputDriven,   // 由输入向量控制 (Loop)
        CurveDriven,   // 由烘焙曲线控制 (Start, Stop, etc)
        mixed          // 曲线→输入切换
    }

    [System.Serializable]
    public class MotionClipData
    {
        [Header("Source")]
        [Tooltip("要播放的动画过渡（Animancer Transition 资源）")]
        public ClipTransition Clip;

        [Tooltip("该段运动的驱动方式：输入驱动 / 曲线驱动 / 混合")]
        public MotionType Type = MotionType.CurveDriven;

        [Header("Process Settings")]
        [Tooltip("期望的目标时长（用于计算 PlaybackSpeed；0 表示不缩放）")]
        public float TargetDuration = 0f;

        [Tooltip("提前结束时间点（单位：秒，基于该段动画的播放时间；<=0 表示不提前结束）。用于 Land 等状态按 endTime 提前切换。")]
        public float EndTime = 0f;

        [Tooltip("是否允许由烘焙器自动写入 TargetLocalDirection。默认关闭；只有闪避/左右跳等“无旋转但有方向”的动画建议开启。")]
        public bool AllowBakeTargetLocalDirection = false;

        [Tooltip("目标局部方向（相对于角色自身 local space）。非零时：曲线阶段速度会沿该方向推进；常用于自带局部方向的动画（向左跳/向后闪避等）。")]
        public Vector3 TargetLocalDirection = Vector3.zero;

        [Header("Baked Data")]
        [Tooltip("动画结束时的脚相位（用于 Loop_L/Loop_R 或 Stop 选择）")]
        public FootPhase EndPhase = FootPhase.LeftFootDown;

        [Tooltip("播放倍速（由 clipLength/TargetDuration 推导）")]
        public float PlaybackSpeed = 1f;

        [Tooltip("烘焙得到的速度曲线（time=>speed）")]
        public AnimationCurve SpeedCurve;

        [Tooltip("烘焙得到的旋转曲线（time=>deltaYaw）")]
        public AnimationCurve RotationCurve;

        [Tooltip("旋转曲线基本完成的时间点（用于 mixed：曲线旋转结束后切输入）")]
        public float RotationFinishedTime = 0f;

        public MotionClipData()
        {
            SpeedCurve = new AnimationCurve();
            RotationCurve = new AnimationCurve();
        }
    }

    [System.Serializable]
    /// <summary>
    /// 定义动画中一个特定的“特征时刻”。
    /// 烘焙工具会自动计算在这个时刻，动画相对于上一阶段起点的预期位移。
    /// </summary>
    public class WarpPointDef
    {
        [Tooltip("特征点名称，方便识别 (如: 'LedgeContact', 'Landing')")]
        public string PointName;

        [Tooltip("动画播放到什么比例时 (0.0~1.0)，角色必须到达这个特征点？")]
        [Range(0f, 1f)]
        public float NormalizedTime;

        // --- 以下数据由烘焙工具自动生成，运行时只读 ---

        [Tooltip("[只读] 从上一个 WarpPoint(或起点) 到此时刻，动画产生的局部位置偏移")]
        public Vector3 BakedLocalOffset;

        [Tooltip("[只读] 此时刻，角色的局部旋转 (相对于起点)")]
        public Quaternion BakedLocalRotation = Quaternion.identity;
    }

    /// <summary>
    /// 专用于高级空间扭曲 (Motion Warping) 的动画数据。
    /// </summary>
    [System.Serializable]
    public class WarpedMotionData
    {
        [Header("Source")]
        [Tooltip("要播放的动画过渡（Animancer Transition 资源）")]
        public ClipTransition Clip;

        [Header("Process Settings")]
        [Tooltip("提前结束时间点（单位：秒。<=0 表示不提前结束）")]
        public float EndTime = 0f;

        [Header("Baked Data (Warping Source)")]
        [Tooltip("动画结束时的脚相位")]
        public FootPhase EndPhase = FootPhase.LeftFootDown;

        [Tooltip("动画总时长 (烘焙时自动写入)")]
        public float BakedDuration;

        [Tooltip("严格还原动画的局部 X 轴速度")]
        public AnimationCurve LocalVelocityX = new AnimationCurve();

        [Tooltip("严格还原动画的局部 Y 轴速度")]
        public AnimationCurve LocalVelocityY = new AnimationCurve();

        [Tooltip("严格还原动画的局部 Z 轴速度")]
        public AnimationCurve LocalVelocityZ = new AnimationCurve();

        [Tooltip("动画原生的局部 Y 轴旋转速度 (度/秒)")]
        public AnimationCurve LocalRotationY = new AnimationCurve();

        [Header("Warp Points (特征点序列)")]
        [Tooltip("定义动画中需要被强制对齐的空间关键时刻。必须按时间顺序排列！")]
        public List<WarpPointDef> WarpPoints = new List<WarpPointDef>();

        public AnimationCurve HandIKWeightCurve=new AnimationCurve();

        [Tooltip("烘焙时计算出的动画总局部位移 (仅供参考)")]
        [HideInInspector]
        public Vector3 TotalBakedLocalOffset;
    }
    /// <summary>
    /// 玩家配置文件（所有可配置参数和动画资源）。
    /// 
    /// 约定：
    /// - PlayerSO 只放“跨角色可复用的静态配置/动画资源”；
    /// - 运行时状态一律放到 PlayerRuntimeData。
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerConfig", menuName = "Player/PlayerConfig")]
    public class PlayerSO : ScriptableObject
    {
        public WarpedMotionData defaultclip;
        #region Movement (移动参数)

        [Header("--- 视角控制 (View) ---")]
        [Tooltip("鼠标灵敏度：X=水平，Y=垂直")]
        public Vector2 LookSensitivity = new Vector2(150f, 150f);

        [Tooltip("俯仰角限制：X=最小(低头)，Y=最大(抬头)")]
        public Vector2 PitchLimits = new Vector2(-70f, 70f);

        [Header("Movement")]
        [Tooltip("探索模式（未瞄准）行走速度（Ctrl 按住）")]
        public float WalkSpeed = 2f;

        [Tooltip("探索模式（未瞄准）慢跑速度（正常移动）")]
        public float JogSpeed = 4f;

        [Tooltip("探索模式（未瞄准）冲刺速度（Shift 按住，消耗体力）")]
        public float SprintSpeed = 7f;

        [Tooltip("探索模式 Orient-to-Movement 的旋转平滑时间（SmoothDampAngle）")]
        public float RotationSmoothTime = 0.12f;

        [Tooltip("空中操控系数（0=完全不可控，1=完全可控）")]
        [Range(0f, 1f)]
        public float AirControl = 0.5f;

        #endregion

        #region Physics (物理参数)

        [Header("Physics")]
        [Tooltip("重力加速度（负值向下）")]
        public float Gravity = -20f;

        // [Deprecated 向后兼容]
        /// <summary>[Deprecated] 已被 JogSpeed 替代，保留向后兼容</summary>
        public float MoveSpeed
        {
            get => JogSpeed;
            set => JogSpeed = value;
        }

        /// <summary>[Deprecated] 已被 SprintSpeed 替代，保留向后兼容</summary>
        public float RunSpeed
        {
            get => SprintSpeed;
            set => SprintSpeed = value;
        }

        #endregion

        #region Stamina (耐力系统)

        [Header("Stamina")]
        [Tooltip("耐力上限")]
        public float MaxStamina = 1000f;

        [Tooltip("冲刺时每秒消耗耐力")]
        public float StaminaDrainRate = 20f;

        [Tooltip("非冲刺时每秒恢复耐力")]
        public float StaminaRegenRate = 15f;

        [Tooltip("行走时耐力恢复倍率（相对于 StaminaRegenRate）")]
        [Range(0.5f, 2.0f)]
        public float WalkStaminaRegenMult = 1.5f;

        [Tooltip("体力耗尽后需要恢复到的百分比（如0.2=20%）才能重新冲刺")]
        [Range(0f, 1f)]
        public float StaminaRecoverThreshold = 0.2f;

        #endregion

        #region Animation Smoothing (动画平滑)

        [Header("Animation Smoothing")]
        [Tooltip("冲刺/走跑混合用曲线（0~1）")]
        public AnimationCurve SprintBlendCurve = AnimationCurve.EaseInOut(0, 0, 0.3f, 1);

        [Tooltip("动画 X 参数平滑时间")]
        public float XAnimBlendSmoothTime = 0.2f;

        [Tooltip("动画 Y 参数平滑时间")]
        public float YAnimBlendSmoothTime = 0.2f;

        #endregion

        #region Aiming (瞄准系统)

        [Header("Aiming")]
        [Tooltip("瞄准模式额外灵敏度倍率（用于微调）")]
        public float AimSensitivity = 1f;

        [Tooltip("瞄准模式行走速度（Strafe）")]
        public float AimWalkSpeed = 1.5f;

        [Tooltip("瞄准模式慢跑速度（Strafe）")]
        public float AimJogSpeed = 2.5f;

        [Tooltip("瞄准模式冲刺速度（Strafe）")]
        public float AimSprintSpeed = 5.0f;

        [Tooltip("瞄准模式旋转平滑时间（SmoothDampAngle）")]
        public float AimRotationSmoothTime = 0.05f;

        [Tooltip("松开瞄准键后保持瞄准的延迟（用于手感与容错）")]
        public float AimHoldDuration = 1f;

        [Tooltip("瞄准待机动画（当前工程未必使用，但保留以便扩展）")]
        public ClipTransition AimIdleAnim;

        [Tooltip("瞄准移动混合器（Strafe locomotion）")]
        public MixerTransition2D AimLocomotionMixer;

        // [Deprecated 向后兼容]
        /// <summary>[Deprecated] 已被 AimJogSpeed 替代，保留向后兼容</summary>
        public float AimWalkSpeedLegacy
        {
            get => AimJogSpeed;
            set => AimJogSpeed = value;
        }

        /// <summary>[Deprecated] 已被 AimSprintSpeed 替代，保留向后兼容</summary>
        public float AimRunSpeed
        {
            get => AimSprintSpeed;
            set => AimSprintSpeed = value;
        }

        #endregion

        #region Jump (跳跃系统)

        [Header("Jump")]
        [Tooltip("跳跃默认回退初速度/力度")]
        public float JumpForce = 6f;

        [Tooltip("空中动画数据（通常为曲线驱动或输入驱动）")]
        public MotionClipData JumpAirAnim;

        [Tooltip("从 Jump 状态切到 Land 状态时，落地动画淡入时间（默认 Jump）。")]
        public float JumpToLandFadeInTime = 0.2f;

        [Tooltip("落地后到跑动的起步衔接动画")]
        public MotionClipData LandToRunStart;

        [Tooltip("从 Land 缓冲切到 MoveLoop 时的循环动画淡入时间（默认 Land）。")]
        public float LandToLoopFadeInTime = 0.3f;

        [Header("--- Jump Variations (不同状态的跳跃配置) ---")]
        [Tooltip("Walk/Jog 状态下的跳跃初速度")]
        public float JumpForceWalk = 5f;

        [Tooltip("Walk/Jog 状态下的跳跃空中动画")]
        public MotionClipData JumpAirAnimWalk;

        [Tooltip("Walk/Jog/Idle 跳跃：切到 Land 的淡入时间")]
        public float JumpToLandFadeInTime_WalkJog = 0.2f;

        [Tooltip("Sprint 状态下的跳跃初速度（有装备）")]
        public float JumpForceSprint = 7f;

        [Tooltip("Sprint 状态下的跳跃空中动画（有装备）")]
        public MotionClipData JumpAirAnimSprint;

        [Tooltip("Sprint(有装备) 跳跃：切到 Land 的淡入时间")]
        public float JumpToLandFadeInTime_Sprint = 0.3f;

        [Tooltip("Sprint 状态下的跳跃初速度（空手）")]
        public float JumpForceSprintEmpty = 8f;

        [Tooltip("Sprint 状态下的跳跃空中动画（空手）")]
        public MotionClipData JumpAirAnimSprintEmpty;

        [Tooltip("Sprint(空手) 跳跃：切到 Land 的淡入时间")]
        public float JumpToLandFadeInTime_SprintEmpty = 0.4f;

        #endregion

        #region DoubleJump (二段跳系统)

        [Header("--- Double Jump (二段跳) ---")]
        [Tooltip("二段跳向上初速度/力度（独立于地面 JumpForce）。")]
        public float DoubleJumpForceUp = 6f;

        [Tooltip("二段跳向上的动画；若为空则降级到标准跳跃动画")]
        public MotionClipData DoubleJumpUp;
        [Tooltip("二段跳动画淡入时间")]
        public float DoubleJumpFadeInTime = 0.2f;
        [Tooltip("二段跳落地时 Land 动画的淡入时间")]
        public float DoubleJumpToLandFadeInTime = 0.2f;

        [Tooltip("Sprint+空手时的二段跳翻滚动画")]
        public MotionClipData DoubleJumpSprintRoll;
        [Tooltip("Sprint+空手二段跳翻滚动画淡入时间")]
        public float DoubleJumpSprintRollFadeInTime = 0.2f;
        [Tooltip("Sprint+空手二段跳翻滚落地时 Land 动画的淡入时间")]
        public float DoubleJumpSprintRollToLandFadeInTime = 0.2f;

        #endregion

        #region Landing Heights (下落高度与动画配置)

        [Header("--- Landing Height Thresholds (下落高度阈值 - Walk/Jog 共享四档) ---")]
        [Tooltip("Walk/Jog 下落高度等级1（最低，缓冲）")]
        public float LandHeightWalkJog_Level1 = 2f;

        [Tooltip("Walk/Jog 下落高度等级2")]
        public float LandHeightWalkJog_Level2 = 5f;

        [Tooltip("Walk/Jog 下落高度等级3")]
        public float LandHeightWalkJog_Level3 = 8f;

        [Tooltip("Walk/Jog 下落高度等级4（最高，缓冲但明显）")]
        public float LandHeightWalkJog_Level4 = 12f;

        // [已移除 Sprint 独立阈值]

        [Tooltip("超过此高度时使用特殊动画（摔倒/踉跄）")]
        public float LandHeightLimit = 15f;

        [Header("--- Landing Buffer Animations (落地缓冲动画 - Walk/Jog) ---")]

        [Tooltip("Walk/Jog 级别1 落地缓冲")]
        public MotionClipData LandBuffer_WalkJog_L1;
        [Tooltip("Walk/Jog 级别1落地后切Loop的淡入时间")]
        public float LandToLoopFadeInTime_WalkJog_L1 = 0.2f;

        [Tooltip("Walk/Jog 级别2 落地缓冲")]
        public MotionClipData LandBuffer_WalkJog_L2;
        [Tooltip("Walk/Jog 级别2落地后切Loop的淡入时间")]
        public float LandToLoopFadeInTime_WalkJog_L2 = 0.3f;

        [Tooltip("Walk/Jog 级别3 落地缓冲")]
        public MotionClipData LandBuffer_WalkJog_L3;
        [Tooltip("Walk/Jog 级别3落地后切Loop的淡入时间")]
        public float LandToLoopFadeInTime_WalkJog_L3 = 0.4f;

        [Tooltip("Walk/Jog 级别4 落地缓冲")]
        public MotionClipData LandBuffer_WalkJog_L4;
        [Tooltip("Walk/Jog 级别4落地后切Loop的淡入时间")]
        public float LandToLoopFadeInTime_WalkJog_L4 = 0.5f;

        [Header("--- Landing Buffer Animations (落地缓冲动画 - Sprint) ---")]


        [Tooltip("Sprint 级别1 落地缓冲")]
        public MotionClipData LandBuffer_Sprint_L1;
        [Tooltip("Sprint 级别1落地后切Loop的淡入时间")]
        public float LandToLoopFadeInTime_Sprint_L1 = 0.2f;

        [Tooltip("Sprint 级别2 落地缓冲")]
        public MotionClipData LandBuffer_Sprint_L2;
        [Tooltip("Sprint 级别2落地后切Loop的淡入时间")]
        public float LandToLoopFadeInTime_Sprint_L2 = 0.3f;

        [Tooltip("Sprint 级别3 落地缓冲")]
        public MotionClipData LandBuffer_Sprint_L3;
        [Tooltip("Sprint 级别3落地后切Loop的淡入时间")]
        public float LandToLoopFadeInTime_Sprint_L3 = 0.4f;

        [Tooltip("Sprint 级别4 落地缓冲")]
        public MotionClipData LandBuffer_Sprint_L4;
        [Tooltip("Sprint 级别4落地后切Loop的淡入时间")]
        public float LandToLoopFadeInTime_Sprint_L4 = 0.8f;

        [Header("--- Landing Special Reaction (超限反应) ---")]

        [Tooltip("超过高度限制时使用（摔倒动画）")]
        public MotionClipData LandBuffer_ExceedLimit;
        [Tooltip("超限落地：切到 MoveLoop 的淡入时间")]
        public float LandToLoopFadeInTime_ExceedLimit = 0.7f;

        #endregion

        #region Locomotion Animations (基础移动动画)

        [Header("Locomotion Animations")]
        [Tooltip("Idle 动画")]
        public ClipTransition IdleAnim;

        [Header("--- 离散循环动画 (Discrete Loop Animations) ---")]
        [Tooltip("行走循环 - 前进（左脚相位）")]
        public ClipTransition WalkLoopFwd_L;
        [Tooltip("行走循环 - 前进（右脚相位）")]
        public ClipTransition WalkLoopFwd_R;

        [Tooltip("慢跑循环 - 前进（左脚相位）")]
        public ClipTransition JogLoopFwd_L;
        [Tooltip("慢跑循环 - 前进（右脚相位）")]
        public ClipTransition JogLoopFwd_R;

        [Tooltip("冲刺循环 - 前进（左脚相位）")]
        public ClipTransition SprintLoopFwd_L;
        [Tooltip("冲刺循环 - 前进（右脚相位）")]
        public ClipTransition SprintLoopFwd_R;

        [Header("--- 停止动画 (Stop Animations) ---")]
        [Tooltip("走路停止（左脚）")]
        public ClipTransition WalkStopLeft;

        [Tooltip("走路停止（右脚）")]
        public ClipTransition WalkStopRight;

        [Tooltip("跑步停止（左脚）")]
        public ClipTransition RunStopLeft;

        [Tooltip("跑步停止（右脚）")]
        public ClipTransition RunStopRight;

        [Tooltip("冲刺停止（左脚）")]
        public ClipTransition SprintStopLeft;

        [Tooltip("冲刺停止（右脚）")]
        public ClipTransition SprintStopRight;

        #endregion

        #region Start Animations (启动动画)

        [Header("Walk Start")]
        public MotionClipData WalkStartFwd;
        public MotionClipData WalkStartBack;
        public MotionClipData WalkStartLeft;
        public MotionClipData WalkStartRight;
        public MotionClipData WalkStartFwdLeft;
        public MotionClipData WalkStartFwdRight;
        public MotionClipData WalkStartBackLeft;
        public MotionClipData WalkStartBackRight;

        [Header("Run Start")]
        public MotionClipData RunStartFwd;
        public MotionClipData RunStartBack;
        public MotionClipData RunStartLeft;
        public MotionClipData RunStartRight;
        public MotionClipData RunStartFwdLeft;
        public MotionClipData RunStartFwdRight;
        public MotionClipData RunStartBackLeft;
        public MotionClipData RunStartBackRight;

        [Header("Sprint Start")]
        public MotionClipData SprintStartFwd;
        public MotionClipData SprintStartBack;
        public MotionClipData SprintStartLeft;
        public MotionClipData SprintStartRight;
        public MotionClipData SprintStartFwdLeft;
        public MotionClipData SprintStartFwdRight;
        public MotionClipData SprintStartBackLeft;
        public MotionClipData SprintStartBackRight;

        #endregion

        #region Vault (翻越系统)

        [Header("Vault")]
        [Tooltip("可翻越物体层")]
        public LayerMask ObstacleLayers;

        [Tooltip("翻越动画数据")]
        public WarpedMotionData lowVaultAnim;
        public WarpedMotionData highVaultAnim;

        [Header("Vault Height Thresholds")]
        [Tooltip("矮翻越最小高度 (m)")]
        public float LowVaultMinHeight = 0.5f;

        [Tooltip("矮翻越最大高度 (m)")]
        public float LowVaultMaxHeight = 1.2f;

        [Tooltip("高翻越最小高度 (m)")]
        public float HighVaultMinHeight = 1.2f;

        [Tooltip("高翻越最大高度 (m)")]
        public float HighVaultMaxHeight = 2.5f;

        [Header("Vault Raycast Detection")]
        [Tooltip("前方墙面检测距离 (m)")]
        public float VaultForwardRayLength = 1.5f;

        [Tooltip("前方射线起点高度 (m，胸部位置)")]
        public float VaultForwardRayHeight = 1.0f;

        [Tooltip("向下射线距离墙面的偏移量 (m)")]
        public float VaultDownwardRayOffset = 0.5f;

        [Tooltip("向下射线长度 (m)")]
        public float VaultDownwardRayLength = 2.0f;

        [Tooltip("双手在墙沿上的间距 (m)")]
        public float VaultHandSpread = 0.4f;

        [Tooltip("越过墙面多远距离开始寻找落地点？(比如 1.5米 代表角色会翻过墙体 1.5米远)")]
        public float VaultLandDistance = 1.5f;

        [Tooltip("寻找落地点的射线长度 (防止跳进无底洞)")]
        public float VaultLandRayLength = 3.0f;

        [Tooltip("是否必须找到墙后的地面才能翻越？如果关闭，即使后面是悬崖也能翻过去(然后自由落体)")]
        public bool RequireGroundBehindWall = true;

        #endregion

        #region Layered Animations (分层动画)

        [Header("Upper Body Layer")]
        [Tooltip("上半身分层遮罩")]
        public AvatarMask UpperBodyMask;
        public ClipTransition RiflePose;
        public ClipTransition AimPose;
        public ClipTransition OneHandedPose;
        public ClipTransition TwoHandedPose;

        [Header("Actions")]
        public ClipTransition WaveAnim;
        public ClipTransition AttackAnim;
        public ClipTransition HitReactionAnim;

        [Header("Facial Layer")]
        [Tooltip("面部表情分层遮罩")]
        public AvatarMask FacialMask;
        public ClipTransition BlinkAnim;
        public ClipTransition HurtFaceAnim;

        #endregion
    }
}