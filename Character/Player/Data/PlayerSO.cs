using Animancer;
using Items.Data;
using UnityEngine;

namespace Characters.Player.Data
{
    // --- 移动数据（供外部运动驱动逻辑调用） ---

    /// <summary>
    /// 动画播放时的脚步相位（区分左右脚落地状态）
    /// </summary>
    public enum FootPhase { LeftFootDown, RightFootDown }

    /// <summary>
    /// 动画片段的移动计算模式
    /// </summary>
    [System.Serializable]
    public enum MotionType
    {
        InputDriven,        // 由输入向量控制 (Loop)
        CurveDriven,        // 由烘焙曲线控制 (Start, Stop, Pivot, Vault)
        mixed               // 在完成转向前由曲线控制，转向后由输入控制 
    }

    /// <summary>
    /// 存储动画片段中烘焙的曲线数据的通用载体
    /// </summary>
    [System.Serializable]
    /// <summary>
    /// 单个动画的完整配置与数据容器
    /// </summary>
    public class MotionClipData
    {
        // ================= Source Config (源配置) =================
        [Header("--- 源配置 (Source) ---")]
        public ClipTransition Clip;
        public MotionType Type = MotionType.CurveDriven;

        [Header("--- 处理设置 (Process Settings) ---")]

        [Tooltip("是否开启智能截断 (自动计算最佳截断点)")]
        public bool AutoCalculateExitTime = true; // 是否开启智能截断

        [Tooltip("是否启用截断点 (提前结束动画)")]
        public bool UseExitTime = true;

        [Tooltip("目标播放时长 (秒)。设为 0 则使用原始时长 (或截断后时长)")]
        public float TargetDuration = 0f;

        [Tooltip("是否手动指定截断点 (不勾选则使用烘焙器自动计算的最佳点)")]
        public bool ManualExitTime = false;

        [Tooltip("手动截断时间点 (秒)")]
        public float ManualExitTimeValue = 0.5f;

        // ================= Baked Data (烘焙产物) =================
        // 这些数据由 Editor 工具自动生成，通常不需要手动改
        [Header("--- 烘焙数据 (Baked - Read Only) ---")]
        public FootPhase EndPhase = FootPhase.LeftFootDown;

        [Tooltip("动画总时长 (原始长度)")]
        public float Duration;

        [Tooltip("实际使用的截断时间 (可能是自动算的，也可能是手动的)")]
        public float EffectiveExitTime;

        [Tooltip("为了达到 TargetDuration 所需的播放倍速")]
        public float PlaybackSpeed = 1f;

        [Tooltip("速度曲线 (已应用 PlaybackSpeed 缩放)")]
        public AnimationCurve SpeedCurve;

        [Tooltip("旋转曲线 (已应用 PlaybackSpeed 缩放)")]
        public AnimationCurve RotationCurve;

        [Tooltip("动画完成转向的时间点 为  中间产物不影响运动)")]
        public float RotationFinishedTime=0f;

        // 构造函数
        public MotionClipData()
        {
            SpeedCurve = new AnimationCurve();
            RotationCurve = new AnimationCurve();
        }
    }

    /// <summary>
    /// 玩家配置类：基于 ScriptableObject 存储角色的所有可配置参数和动画资源
    /// 包含移动参数、耐力系统、动画曲线、分层动画等所有可序列化配置项。
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerConfig", menuName = "Player/PlayerConfig")]
    public class PlayerSO : ScriptableObject
    {

        [Header("--- 烘焙参考标准 (Reference) ---")]
        [Tooltip("用于相位匹配的标准左脚起步循环 (参考帧: 第0帧)")]
        public ClipTransition ReferenceRunLoop_L;

        [Tooltip("用于相位匹配的标准右脚起步循环 (参考帧: 第0帧)")]
        public ClipTransition ReferenceRunLoop_R;

        // =================================================================================
        // 核心参数（Core Parameters）
        // =================================================================================
        [Header("--- 基础移动参数 ---")]
        [Space(5)]
        [Tooltip("行走基础速度 (m/s)")]
        public float MoveSpeed = 4f;

        [Tooltip("奔跑基础速度 (m/s)")]
        public float RunSpeed = 7f;

        [Tooltip("角色旋转平滑时间（秒，值越小旋转越快）")]
        public float RotationSmoothTime = 0.12f;

        [Header("--- 重力/接地 ---")]
        [Tooltip("重力加速度（建议 -15 ~ -25，可根据实际手感调整）")]
        public float Gravity = -20f;

        [Tooltip("接地下压力（防止浮空时角色飘起来）")]
        public float GroundingForce = -5f;

        [Header("--- 耐力系统 ---")]
        [Space(5)]
        public float MaxStamina = 1000f;
        public float StaminaDrainRate = 20f;
        public float StaminaRegenRate = 15f;

        [Header("--- 动画平滑 ---")]
        [Space(5)]
        [Tooltip("走/跑切换的平滑过渡曲线")]
        public AnimationCurve SprintBlendCurve = AnimationCurve.EaseInOut(0, 0, 0.3f, 1);

        [Tooltip("Loop 状态下，转向输入的动画平滑时间")]
        public float XAnimBlendSmoothTime = 0.2f;

        [Tooltip("Loop 状态下，速度输入的动画平滑时间")]
        public float YAnimBlendSmoothTime = 0.2f;

        // =================================================================================
        // 跳跃系统 (Jump System)
        // =================================================================================
        [Header("--- 跳跃参数 ---")]
        [Space(5)]

        [Tooltip("起跳瞬间的垂直向上速度 (m/s)")]
        public float JumpForce = 6f;

        [Tooltip("空中水平移动控制系数 (0=不可控, 1=全速控制)")]
        [Range(0f, 1f)]
        public float AirControl = 0.5f;

        [Header("跳跃动画")]
        // 这个 Clip 应该包含起跳动作 + 一段较长的滞空 Pose (Loop)
        // 或者是：起跳 -> 滞空 (不循环，直到落地)
        public MotionClipData JumpAirAnim; // 原 JumpLaunch

        [Header("落地动画")]
        // [新增] 落地接跑动起步 (Land -> Run Start)
        // 如果你有专门的 "落地后顺势起跑" 的动画，可以在这里配置
        // 如果没有，就复用普通的 RunStart
        public MotionClipData LandToRunStart;


        // =================================================================================
        // 基础移动动画（Base Locomotion Animations）
        // =================================================================================
        [Header("--- 基础层动画 (Layer 0) ---")]
        [Space(10)]
        public ClipTransition IdleAnim;

        [Header("循环移动 (Loop)")]
        public MixerTransition2D MoveLoopMixer_L;
        public MixerTransition2D MoveLoopMixer_R;

        [Header("--- 瞄准系统 (Aiming) ---")]
        public float AimSensitivity = 1f;

        public float AimWalkSpeed = 2.5f;
        public float AimRunSpeed = 5.0f;
        public float AimRotationSmoothTime = 0.05f;
        public float AimHoldDuration = 1f;

        [Tooltip("瞄准时的站立姿势")]
        public ClipTransition AimIdleAnim;
        [Header("瞄准移动混合树 (Strafing Mixer)")]
        public MixerTransition2D AimLocomotionMixer;


        [Header("停止 (Stop)")]
        public ClipTransition WalkStopLeft;
        public ClipTransition WalkStopRight;
        public ClipTransition RunStopLeft;
        public ClipTransition RunStopRight;

        // =================================================================================
        // 烘焙动画（Baked Motion Clips）
        // =================================================================================
        [Header("--- 烘焙动画片段 ---")]
        [Space(10)]

        [Header("行走启动 (Walk Start)")]
        public MotionClipData WalkStartFwd;
        public MotionClipData WalkStartBack;
        public MotionClipData WalkStartLeft;
        public MotionClipData WalkStartRight;
        public MotionClipData WalkStartFwdLeft;
        public MotionClipData WalkStartFwdRight;
        public MotionClipData WalkStartBackLeft;
        public MotionClipData WalkStartBackRight;

        [Header("奔跑启动 (Run Start)")]
        public MotionClipData RunStartFwd;
        public MotionClipData RunStartBack;
        public MotionClipData RunStartLeft;
        public MotionClipData RunStartRight;
        public MotionClipData RunStartFwdLeft;
        public MotionClipData RunStartFwdRight;
        public MotionClipData RunStartBackLeft;
        public MotionClipData RunStartBackRight;

        [Header("--- 翻越系统 (Vault) ---")]
        [Tooltip("障碍物层级")]
        public LayerMask ObstacleLayers;

        [Tooltip("最小触发高度 (比如 0.5m)")]
        public float VaultMinHeight = 0.5f;

        [Tooltip("最大触发高度 (比如 1.2m，超过算攀爬)")]
        public float VaultMaxHeight = 1.2f;

        [Tooltip("检测距离 (离墙多远可以翻越)")]
        public float VaultCheckDistance = 1.0f;

        [Header("翻越动画")]
        // 使用 MotionClipData，利用烘焙曲线驱动精准位移
        public MotionClipData VaultFenceAnim; // 矮栏杆翻越 (手撑一下跳过去)
                                              // 以后可以加 VaultBoxAnim (大箱子翻越)

        // =================================================================================
        // 分层动画 (Layered Animations)
        // =================================================================================
        [Header("--- 分层动画 (Layers) ---")]
        [Space(10)]

        [Header("--- 上半身层 (Layer 1) ---")]
        public AvatarMask UpperBodyMask;

        [Header("基础姿势 (Base Poses)")]
        public ClipTransition RiflePose;     // 持步枪姿势
        public ClipTransition AimPose;       // 瞄准

        [Header("动作 (Actions)")]
        public ClipTransition WaveAnim;
        public ClipTransition AttackAnim;
        public ClipTransition HitReactionAnim;

        [Header("--- 上半身姿态库 (Base Poses) ---")]

        [Tooltip("单手武器姿态 (手枪)")]
        public ClipTransition OneHandedPose;

        [Tooltip("双手武器姿态 (步枪)")]
        public ClipTransition TwoHandedPose;


        [Header("面部层 (Layer 2)")]
        public AvatarMask FacialMask;
        public ClipTransition BlinkAnim;
        public ClipTransition HurtFaceAnim;
    }
}