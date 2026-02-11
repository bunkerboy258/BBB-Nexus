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
        public ClipTransition Clip;
        public MotionType Type = MotionType.CurveDriven;

        [Header("Process Settings")]
        public bool AutoCalculateExitTime = true;
        public bool UseExitTime = true;
        public float TargetDuration = 0f;
        public bool ManualExitTime = false;
        public float ManualExitTimeValue = 0.5f;

        [Header("Baked Data")]
        public FootPhase EndPhase = FootPhase.LeftFootDown;
        [Tooltip("目标相位：None=不指定，Left=左脚，Right=右脚")]
        public FootPhase TargetFootPhase = FootPhase.LeftFootDown;
        
        public float Duration;
        public float EffectiveExitTime;
        public float PlaybackSpeed = 1f;
        public AnimationCurve SpeedCurve;
        public AnimationCurve RotationCurve;
        public float RotationFinishedTime = 0f;

        public MotionClipData()
        {
            SpeedCurve = new AnimationCurve();
            RotationCurve = new AnimationCurve();
        }
    }

    /// <summary>
    /// 玩家配置文件（所有可配置参数和动画资源）
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerConfig", menuName = "Player/PlayerConfig")]
    public class PlayerSO : ScriptableObject
    {
        #region Reference (烘焙参考)

        public ClipTransition ReferenceRunLoop_L;
        public ClipTransition ReferenceRunLoop_R;

        #endregion

        #region Movement (移动参数)

        [Header("Movement")]
        public float MoveSpeed = 4f;
        public float RunSpeed = 7f;
        public float RotationSmoothTime = 0.12f;
        [Range(0f, 1f)]
        public float AirControl = 0.5f;

        #endregion

        #region Physics (物理参数)

        [Header("Physics")]
        public float Gravity = -20f;
        public float GroundingForce = -5f;

        #endregion

        #region Stamina (耐力系统)

        [Header("Stamina")]
        public float MaxStamina = 1000f;
        public float StaminaDrainRate = 20f;
        public float StaminaRegenRate = 15f;

        #endregion

        #region Animation Smoothing (动画平滑)

        [Header("Animation Smoothing")]
        public AnimationCurve SprintBlendCurve = AnimationCurve.EaseInOut(0, 0, 0.3f, 1);
        public float XAnimBlendSmoothTime = 0.2f;
        public float YAnimBlendSmoothTime = 0.2f;

        #endregion

        #region Aiming (瞄准系统)

        [Header("Aiming")]
        public float AimSensitivity = 1f;
        public float AimWalkSpeed = 2.5f;
        public float AimRunSpeed = 5.0f;
        public float AimRotationSmoothTime = 0.05f;
        public float AimHoldDuration = 1f;
        public ClipTransition AimIdleAnim;
        public MixerTransition2D AimLocomotionMixer;

        #endregion

        #region Jump (跳跃系统)

        [Header("Jump")]
        public float JumpForce = 6f;
        public MotionClipData JumpAirAnim;
        public MotionClipData LandToRunStart;

        #endregion

        #region Locomotion Animations (基础移动动画)

        [Header("Locomotion Animations")]
        public ClipTransition IdleAnim;
        public MixerTransition2D MoveLoopMixer_L;
        public MixerTransition2D MoveLoopMixer_R;
        public ClipTransition WalkStopLeft;
        public ClipTransition WalkStopRight;
        public ClipTransition RunStopLeft;
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
        public LayerMask ObstacleLayers;
        public float VaultMinHeight = 0.5f;
        public float VaultMaxHeight = 1.2f;
        public float VaultCheckDistance = 1.0f;
        public MotionClipData VaultFenceAnim;

        #endregion

        #region Layered Animations (分层动画)

        [Header("Upper Body Layer")]
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
        public AvatarMask FacialMask;
        public ClipTransition BlinkAnim;
        public ClipTransition HurtFaceAnim;

        #endregion
    }
}