using Animancer;
using Items.Data;
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
        [Tooltip("是否由烘焙器自动计算截断点（有效退出时间）")]
        public bool AutoCalculateExitTime = true;

        [Tooltip("是否使用截断点（某些流程可无视截断点直接完整播放）")]
        public bool UseExitTime = true;

        [Tooltip("期望的目标时长（用于计算 PlaybackSpeed；0 表示不缩放）")]
        public float TargetDuration = 0f;

        [Tooltip("是否手动指定截断点")]
        public bool ManualExitTime = false;

        [Tooltip("手动截断时间（秒）")]
        public float ManualExitTimeValue = 0.5f;

        [Header("Baked Data")]
        [Tooltip("动画结束时的脚相位（用于 Loop_L/Loop_R 或 Stop 选择）")]
        public FootPhase EndPhase = FootPhase.LeftFootDown;

        [Tooltip("目标相位：用于 Pose Matching（未使用时可忽略）")]
        public FootPhase TargetFootPhase = FootPhase.LeftFootDown;

        [Tooltip("该资源的最终时长（可能是 TargetDuration 或 EffectiveExitTime）")]
        public float Duration;

        [Tooltip("有效退出时间（烘焙器计算或手动指定），用于 curve->input 截断")]
        public float EffectiveExitTime;

        [Tooltip("播放倍速（由 Duration/TargetDuration 推导）")]
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
        #region Reference (烘焙参考)

        [Header("RootMotion 烘焙参考")]
        [Tooltip("用于 PoseMatching 的跑步循环参考（左脚落地相位）")]
        public ClipTransition ReferenceRunLoop_L;

        [Tooltip("用于 PoseMatching 的跑步循环参考（右脚落地相位）")]
        public ClipTransition ReferenceRunLoop_R;

        #endregion

        #region Movement (移动参数)

        [Header("--- 视角控制 (View) ---")]
        [Tooltip("鼠标灵敏度：X=水平，Y=垂直")]
        public Vector2 LookSensitivity = new Vector2(150f, 150f);

        [Tooltip("俯仰角限制：X=最小(低头)，Y=最大(抬头)")]
        public Vector2 PitchLimits = new Vector2(-70f, 70f);

        [Header("Movement")]
        [Tooltip("探索模式（未瞄准）基础移动速度")]
        public float MoveSpeed = 4f;

        [Tooltip("探索模式（未瞄准）奔跑速度")]
        public float RunSpeed = 7f;

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

        // [Removed] GroundingForce：当前 MotionDriver 采用“VerticalVelocity=-2f 贴地”策略，
        // 本字段在 Player 代码内无引用，保留只会增加配置面噪音。
        // 如后续需要更精细的贴地力/斜坡吸附，可恢复并在 MotionDriver.CalculateGravity 中使用。

        #endregion

        #region Stamina (耐力系统)

        [Header("Stamina")]
        [Tooltip("耐力上限")]
        public float MaxStamina = 1000f;

        [Tooltip("奔跑时每秒消耗耐力")]
        public float StaminaDrainRate = 20f;

        [Tooltip("非奔跑时每秒恢复耐力")]
        public float StaminaRegenRate = 15f;

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
        public float AimWalkSpeed = 2.5f;

        [Tooltip("瞄准模式奔跑速度（Strafe）")]
        public float AimRunSpeed = 5.0f;

        [Tooltip("瞄准模式旋转平滑时间（SmoothDampAngle）")]
        public float AimRotationSmoothTime = 0.05f;

        [Tooltip("松开瞄准键后保持瞄准的延迟（用于手感与容错）")]
        public float AimHoldDuration = 1f;

        [Tooltip("瞄准待机动画（当前工程未必使用，但保留以便扩展）")]
        public ClipTransition AimIdleAnim;

        [Tooltip("瞄准移动混合器（Strafe locomotion）")]
        public MixerTransition2D AimLocomotionMixer;

        #endregion

        #region Jump (跳跃系统)

        [Header("Jump")]
        [Tooltip("跳跃初速度/力度")]
        public float JumpForce = 6f;

        [Tooltip("空中动画数据（通常为曲线驱动或输入驱动）")]
        public MotionClipData JumpAirAnim;

        [Tooltip("落地后到跑动的起步衔接动画")]
        public MotionClipData LandToRunStart;

        #endregion

        #region Locomotion Animations (基础移动动画)

        [Header("Locomotion Animations")]
        [Tooltip("Idle 动画")]
        public ClipTransition IdleAnim;

        [Tooltip("移动循环混合器（左脚相位）")]
        public MixerTransition2D MoveLoopMixer_L;

        [Tooltip("移动循环混合器（右脚相位）")]
        public MixerTransition2D MoveLoopMixer_R;

        [Tooltip("走路停止（左脚）")]
        public ClipTransition WalkStopLeft;

        [Tooltip("走路停止（右脚）")]
        public ClipTransition WalkStopRight;

        [Tooltip("跑步停止（左脚）")]
        public ClipTransition RunStopLeft;

        [Tooltip("跑步停止（右脚）")]
        public ClipTransition RunStopRight;

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

        #endregion

        #region Vault (翻越系统)

        [Header("Vault")]
        [Tooltip("可翻越物体层")]
        public LayerMask ObstacleLayers;

        [Tooltip("可翻越最小高度")]
        public float VaultMinHeight = 0.5f;

        [Tooltip("可翻越最大高度")]
        public float VaultMaxHeight = 1.2f;

        [Tooltip("翻越检测距离")]
        public float VaultCheckDistance = 1.0f;

        [Tooltip("翻越动画数据")]
        public MotionClipData VaultFenceAnim;

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