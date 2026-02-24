using Animancer;
using Items.Data;
using System.Collections.Generic;
using UnityEngine;

namespace Characters.Player.Data
{
    #region Basic Data Structures 基础数据结构定义

    /// <summary>
    /// Foot phase (left or right foot down) - 脚相位（左脚或右脚着地）
    /// </summary>
    public enum FootPhase { LeftFootDown, RightFootDown }

    /// <summary>
    /// Motion type for animation driving - 动画驱动类型
    /// </summary>
    [System.Serializable]
    public enum MotionType
    {
        InputDriven,   // Driven by input vector (loop) - 由输入向量控制（循环）
        CurveDriven,   // Driven by baked curve (start, stop, etc) - 由烘焙曲线控制（启动、停止等）
        mixed          // Curve to input switch - 曲线到输入切换
    }

    public enum WarpedType
    {
        None,           // No warping, play as is - 不进行扭曲，按原样播放
        Vault,
        Dodge,
        Simple
    }
    /// <summary>
    /// Animation clip data for motion - 运动动画片段数据
    /// </summary>
    [System.Serializable]
    public class MotionClipData
    {
        [Header("Animation Source - 动画资源")]
        [Tooltip("Animation transition resource to play - 要播放的动画过渡资源")]
        public ClipTransition Clip;
        [Tooltip("Motion driving type: input/curve/mixed - 运动驱动方式：输入/曲线/混合")]
        public MotionType Type = MotionType.CurveDriven;

        [Header("Playback Settings - 播放设置")]
        [Tooltip("Target duration for playback speed calculation (0 = original) - 期望目标时长（用于计算播放速度，0为原始时长）")]
        public float TargetDuration = 0f;
        [Tooltip("Early end time in seconds (<=0 means not used) - 基于播放时间的提前结束点（秒），<=0则不生效")]
        public float EndTime = 0f;

        [Header("Root Motion Configuration - 根运动配置")]
        [Tooltip("Allow baker to write target local direction (for dodge, etc) - 是否允许烘焙器写入目标局部方向（多用于闪避/位移类动画）")]
        public bool AllowBakeTargetLocalDirection = false;
        [Tooltip("Target local direction relative to character - 相对于角色自身的局部目标方向")]
        public Vector3 TargetLocalDirection = Vector3.zero;

        [Header("Baked Runtime Data - 烘焙运行时数据")]
        [Tooltip("Foot phase at animation end - 动画结束时的足部相位")]
        public FootPhase EndPhase = FootPhase.LeftFootDown;
        [Tooltip("Playback speed calculated - 计算得出的播放倍速")]
        public float PlaybackSpeed = 1f;
        [Tooltip("Baked speed curve - 烘焙生成的位移速度曲线")]
        public AnimationCurve SpeedCurve;
        [Tooltip("Baked yaw rotation curve - 烘焙生成的偏航角旋转曲线")]
        public AnimationCurve RotationCurve;
        [Tooltip("Time when rotation is finished (for mixed mode) - 旋转动作完成的时间戳（用于混合模式切换）")]
        public float RotationFinishedTime = 0f;

        public MotionClipData()
        {
            SpeedCurve = new AnimationCurve();
            RotationCurve = new AnimationCurve();
        }
    }

    /// <summary>
    /// Defines a special moment (warp point) in animation - 定义动画中特征时刻（Warp点）
    /// </summary>
    [System.Serializable]
    public class WarpPointDef
    {
        [Tooltip("Feature point name for identification - 特征点识别名称")]
        public string PointName;
        [Tooltip("Normalized time to trigger this point (0-1) - 触发该特征点的动画归一化时间 (0-1)")]
        [Range(0f, 1f)]
        public float NormalizedTime;
        [Tooltip("Local offset relative to the feature point - 相对于特征点的局部坐标偏移")]
        public Vector3 TargetPositionOffset;

        [Header("Baking Results - 烘焙结果")]
        [Tooltip("Baked local offset from start to this moment - 从起点到此时刻的烘焙局部位移")]
        public Vector3 BakedLocalOffset;
        [Tooltip("Baked local rotation from start to this moment - 从起点到此时刻的烘焙局部旋转")]
        public Quaternion BakedLocalRotation = Quaternion.identity;
    }

    /// <summary>
    /// Animation data for advanced motion warping - 高级空间扭曲动画数据
    /// </summary>
    [System.Serializable]
    public class WarpedMotionData
    {
        [Header("Animation Source - 动画资源")]
        public ClipTransition Clip;

        [Header("Timing Control - 时序控制")]
        public float EndTime = 0f;
        public FootPhase EndPhase = FootPhase.LeftFootDown;

        [Header("Reference Curves (Baked) - 参考曲线（烘焙）")]
        [Tooltip("Original animation duration - 动画原始时长")]
        public float BakedDuration;
        public AnimationCurve LocalVelocityX = new AnimationCurve();
        public AnimationCurve LocalVelocityY = new AnimationCurve();
        public AnimationCurve LocalVelocityZ = new AnimationCurve();
        public AnimationCurve LocalRotationY = new AnimationCurve();

        public WarpedType Type = WarpedType.None; // 默认不自动烘焙点

        [Header("Warping Definition - 扭曲定义")]
        [Tooltip("Warp points in time order - 空间对齐特征点序列，需按时间升序排列")]
        public List<WarpPointDef> WarpPoints = new List<WarpPointDef>();
        public AnimationCurve HandIKWeightCurve = new AnimationCurve();

        [HideInInspector]
        public Vector3 TotalBakedLocalOffset;
    }

    #endregion

    /// <summary>
    /// Player configuration asset (all configurable parameters and animation resources) - 玩家配置文件（所有可配置参数和动画资源）
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerConfig", menuName = "Player/PlayerConfig")]
    public class PlayerSO : ScriptableObject
    {
        [Header("BASE CONFIGURATION - 基础配置")]
        public WarpedMotionData defaultclip;

        #region Movement & View 移动与视角
        [Header("VIEW AND STEERING - 视角与转向")]
        [Tooltip("Mouse sensitivity: X=horizontal, Y=vertical - 鼠标灵敏度：X=水平，Y=垂直")]
        public Vector2 LookSensitivity = new Vector2(150f, 150f);
        [Tooltip("Pitch limits: X=min, Y=max - 俯仰角限制：X=最小，Y=最大")]
        public Vector2 PitchLimits = new Vector2(-70f, 70f);
        [Tooltip("Rotation smoothing time - 旋转平滑延迟时间")]
        public float RotationSmoothTime = 0.12f;

        [Header("MOVEMENT SPEEDS - 移动速度")]
        public float WalkSpeed = 2f;
        public float JogSpeed = 4f;
        public float SprintSpeed = 7f;

        [Header("PHYSICS AND CONTROL - 物理与控制")]
        public float Gravity = -20f;
        [Range(0f, 1f)]
        public float AirControl = 0.5f;

        [Header("ANIMATION BLENDING - 动画混合")]
        [Tooltip("Animation blend curve for movement state switching - 移动状态切换时动画参数变化的参照曲线")]
        public AnimationCurve SprintBlendCurve = AnimationCurve.EaseInOut(0, 0, 0.3f, 1);
        public float XAnimBlendSmoothTime = 0.2f;
        public float YAnimBlendSmoothTime = 0.2f;
        public float AimXAnimBlendSmoothTime = 0.2f;
        public float AimYAnimBlendSmoothTime = 0.2f;
        #endregion

        #region Stamina System 耐力系统
        [Header("STAMINA SYSTEM - 耐力系统")]
        public float MaxStamina = 1000f;
        public float StaminaDrainRate = 20f;
        public float StaminaRegenRate = 15f;
        [Range(0.5f, 2.0f)]
        public float WalkStaminaRegenMult = 1.5f;
        [Range(0f, 1f)]
        public float StaminaRecoverThreshold = 0.2f;
        #endregion

        #region Aiming System 瞄准系统
        [Header("AIMING SYSTEM - 瞄准系统")]
        public float AimSensitivity = 1f;
        public float AimWalkSpeed = 1.5f;
        public float AimJogSpeed = 2.5f;
        public float AimSprintSpeed = 5.0f;
        public float AimRotationSmoothTime = 0.05f;
        public MixerTransition2D AimLocomotionMixer;
        #endregion

        #region Jump & Double Jump 跳跃与二段跳
        [Header("JUMP - BASE - 跳跃基础")]
        public float JumpForce = 6f;
        public MotionClipData JumpAirAnim;

        [Header("JUMP - WALK/JOG VARIATIONS - 行走/慢跑跳跃变体")]
        public float JumpForceWalk = 5f;
        public MotionClipData JumpAirAnimWalk;
        public float JumpToLandFadeInTime_WalkJog = 0.2f;

        [Header("JUMP - SPRINT VARIATIONS - 冲刺跳跃变体")]
        public float JumpForceSprint = 7f;
        public MotionClipData JumpAirAnimSprint;
        public float JumpToLandFadeInTime_Sprint = 0.3f;

        [Header("JUMP - SPRINT EMPTY HANDED - 空手冲刺跳跃")]
        public float JumpForceSprintEmpty = 8f;
        public MotionClipData JumpAirAnimSprintEmpty;
        public float JumpToLandFadeInTime_SprintEmpty = 0.4f;

        [Header("DOUBLE JUMP - 二段跳")]
        public float DoubleJumpForceUp = 6f;
        public MotionClipData DoubleJumpUp;
        public float DoubleJumpFadeInTime = 0.2f;
        public float DoubleJumpToLandFadeInTime = 0.2f;
        [Space]
        public MotionClipData DoubleJumpSprintRoll;
        public float DoubleJumpSprintRollFadeInTime = 0.2f;
        public float DoubleJumpSprintRollToLandFadeInTime = 0.2f;
        #endregion

        #region Landing System 落地系统
        [Header("LANDING - HEIGHT THRESHOLDS - 落地高度阈值")]
        public float LandHeightWalkJog_Level1 = 2f;
        public float LandHeightWalkJog_Level2 = 5f;
        public float LandHeightWalkJog_Level3 = 8f;
        public float LandHeightWalkJog_Level4 = 12f;
        public float LandHeightLimit = 15f;

        [Header("LANDING - WALK/JOG ANIMATIONS - 行走/慢跑落地动画")]
        public MotionClipData LandBuffer_WalkJog_L1;
        public float LandToLoopFadeInTime_WalkJog_L1 = 0.2f;
        public MotionClipData LandBuffer_WalkJog_L2;
        public float LandToLoopFadeInTime_WalkJog_L2 = 0.3f;
        public MotionClipData LandBuffer_WalkJog_L3;
        public float LandToLoopFadeInTime_WalkJog_L3 = 0.4f;
        public MotionClipData LandBuffer_WalkJog_L4;
        public float LandToLoopFadeInTime_WalkJog_L4 = 0.5f;

        [Header("LANDING - SPRINT ANIMATIONS - 冲刺落地动画")]
        public MotionClipData LandBuffer_Sprint_L1;
        public float LandToLoopFadeInTime_Sprint_L1 = 0.2f;
        public MotionClipData LandBuffer_Sprint_L2;
        public float LandToLoopFadeInTime_Sprint_L2 = 0.3f;
        public MotionClipData LandBuffer_Sprint_L3;
        public float LandToLoopFadeInTime_Sprint_L3 = 0.4f;
        public MotionClipData LandBuffer_Sprint_L4;
        public float LandToLoopFadeInTime_Sprint_L4 = 0.8f;

        [Header("LANDING - CRITICAL - 超限落地动画")]
        public MotionClipData LandBuffer_ExceedLimit;
        public float LandToLoopFadeInTime_ExceedLimit = 0.7f;
        #endregion

        #region Locomotion Animations 基础移动动画
        [Header("LOCOMOTION - IDLE - 待机动画")]
        public ClipTransition IdleAnim;

        [Header("LOCOMOTION - LOOPS - 循环动画")]
        public ClipTransition WalkLoopFwd_L;
        public ClipTransition WalkLoopFwd_R;
        [Space]
        public ClipTransition JogLoopFwd_L;
        public ClipTransition JogLoopFwd_R;
        [Space]
        public ClipTransition SprintLoopFwd_L;
        public ClipTransition SprintLoopFwd_R;

        [Header("LOCOMOTION - STOPS - 停止动画")]
        public ClipTransition WalkStopLeft;
        public ClipTransition WalkStopRight;
        [Space]
        public ClipTransition RunStopLeft;
        public ClipTransition RunStopRight;
        [Space]
        public ClipTransition SprintStopLeft;
        public ClipTransition SprintStopRight;
        #endregion

        #region Directional Start Animations 方向启动动画
        [Header("STARTS - WALK - 行走启动")]
        public MotionClipData WalkStartFwd;
        public MotionClipData WalkStartBack;
        public MotionClipData WalkStartLeft;
        public MotionClipData WalkStartRight;
        [Space]
        public MotionClipData WalkStartFwdLeft;
        public MotionClipData WalkStartFwdRight;
        public MotionClipData WalkStartBackLeft;
        public MotionClipData WalkStartBackRight;

        [Header("STARTS - RUN - 跑步启动")]
        public MotionClipData RunStartFwd;
        public MotionClipData RunStartBack;
        public MotionClipData RunStartLeft;
        public MotionClipData RunStartRight;
        [Space]
        public MotionClipData RunStartFwdLeft;
        public MotionClipData RunStartFwdRight;
        public MotionClipData RunStartBackLeft;
        public MotionClipData RunStartBackRight;

        [Header("STARTS - SPRINT - 冲刺启动")]
        public MotionClipData SprintStartFwd;
        public MotionClipData SprintStartBack;
        public MotionClipData SprintStartLeft;
        public MotionClipData SprintStartRight;
        [Space]
        public MotionClipData SprintStartFwdLeft;
        public MotionClipData SprintStartFwdRight;
        public MotionClipData SprintStartBackLeft;
        public MotionClipData SprintStartBackRight;
        #endregion

        #region Vault System 翻越系统
        [Header("VAULT - DETECTION - 翻越检测")]
        public LayerMask ObstacleLayers;
        public float VaultForwardRayLength = 1.5f;
        public float VaultForwardRayHeight = 1.0f;
        public float VaultDownwardRayOffset = 0.5f;
        public float VaultDownwardRayLength = 2.0f;
        [Space]
        public float VaultHandSpread = 0.4f;
        public float VaultLandDistance = 1.5f;
        public float VaultLandRayLength = 3.0f;
        public bool RequireGroundBehindWall = true;

        [Header("VAULT - ANIMATION DATA - 翻越动画数据")]
        public WarpedMotionData lowVaultAnim;
        public WarpedMotionData highVaultAnim;

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

        [Header("VAULT - HEIGHT THRESHOLDS - 翻越高度阈值")]
        public float LowVaultMinHeight = 0.5f;
        public float LowVaultMaxHeight = 1.2f;
        [Space]
        public float HighVaultMinHeight = 1.2f;
        public float HighVaultMaxHeight = 2.5f;
        #endregion

        #region Layered Actions & Masks 分层动作与遮罩
        [Header("LAYERED - UPPER BODY - 上半身分层")]
        public AvatarMask UpperBodyMask;
        public ClipTransition AttackAnim;

        [Header("LAYERED - FACIAL - 面部分层")]
        public AvatarMask FacialMask;
        public ClipTransition BlinkAnim;
        public ClipTransition HurtFaceAnim;
        #endregion
    }
}
