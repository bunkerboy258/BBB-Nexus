using UnityEngine;
using Animancer;

namespace Characters.Player.Data
{
    [CreateAssetMenu(fileName = "CoreModule", menuName = "Player/Modules/Core Module")]
    public class CoreModuleSO : ScriptableObject
    {
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
        public float ReboundForce = -1f;
        [Range(0f, 1f)] public float AirControl = 0.5f;
        [Tooltip("Move speed smoothing time (物理速度变化平滑时间，单位秒)")]
        public float MoveSpeedSmoothTime = 0.15f;

        [Header("ANIMATION BLENDING - 动画混合")]
        [Tooltip("Animation blend curve for movement state switching - 移动状态切换时动画参数变化的参照曲线")]
        public AnimationCurve SprintBlendCurve = AnimationCurve.EaseInOut(0, 0, 0.3f, 1);
        public float XAnimBlendSmoothTime = 0.2f;
        public float YAnimBlendSmoothTime = 0.2f;
        #endregion

        #region Stamina System 耐力系统
        [Header("STAMINA SYSTEM - 耐力系统")]
        public float MaxStamina = 1000f;
        public float StaminaDrainRate = 20f;
        public float StaminaRegenRate = 15f;
        [Range(0.5f, 2.0f)] public float WalkStaminaRegenMult = 1.5f;
        [Range(0f, 1f)] public float StaminaRecoverThreshold = 0.2f;
        #endregion

        #region Layered Actions & Masks 分层动作与遮罩
        [Header("LAYERED ACTIONS & MASKS - 分层动作与遮罩")]
        public AvatarMask UpperBodyMask;
        public ClipTransition AttackAnim;

        [Header("FACIAL - 面部分层")]
        public AvatarMask FacialMask;
        public ClipTransition BlinkAnim;
        public ClipTransition HurtFaceAnim;
        #endregion

        #region Fall Detection 下落检测
        [Header("FALL DETECTION - 下落检测")]
        [Tooltip("下落状态的最小高度等级（用于进入 FallState 的判断）- 如果 FallHeightLevel >= 此值，触发下落检测")]
        [Range(0, 4)]
        public int FallHeightLevelThreshold = 1;

        [Tooltip("进入下落状态的最小垂直速度阈值（向下为负数）- 单位 m/s，建议 3-10")]
        public float FallVerticalVelocityThreshold = -5f;
        #endregion
    }
}
