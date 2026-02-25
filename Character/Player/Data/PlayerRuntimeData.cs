using Items.Data;
using Items.Logic;
using System.Collections.Generic;
using UnityEngine;

namespace Characters.Player.Data
{
    #region 数据结构与枚举
    /// <summary>
    /// 离散化的角色意图方向（8方向）。
    /// </summary>
    public enum DesiredDirection
    {
        None,
        Forward,
        Backward,
        Left,
        Right,
        ForwardLeft,
        ForwardRight,
        BackwardLeft,
        BackwardRight
    }
    public struct VaultObstacleInfo
    {
        public bool IsValid;
        public Vector3 WallPoint;
        public Vector3 WallNormal;
        public float Height;
        public Vector3 LedgePoint;
        public Vector3 LeftHandPos;
        public Vector3 RightHandPos;
        public Quaternion HandRot;
        [Tooltip("翻越后的预期着陆点")]
        public Vector3 ExpectedLandPoint;
    }

    public enum LocomotionState
    {
        Idle = 0,
        Walk = 1,
        Jog = 2,
        Sprint = 3,
    }

    public enum DoubleJumpDirection
    {
        Up = 0,
        Left = 1,
        Right = 2,
    }

    public class EquipmentSnapshot
    {
        public ItemDefinitionSO Definition;
        public InteractableItem Instance;
        public DeviceController DeviceLogic;

        public bool HasItem => Instance != null;
        public bool HasDevice => DeviceLogic != null;
    }

    #endregion

    /// <summary>
    /// 玩家运行时数据容器
    /// 负责整合输入、状态、意图及驱动动画/物理所需的参数
    /// </summary>
    public class PlayerRuntimeData
    {
        #region INPUT - 原始输入数据
        [Header("Input Data")]
        public Vector2 LookInput;
        public Vector2 MoveInput;
        #endregion

        #region VIEW & ROTATION - 视角与朝向状态
        [Header("View & Orientation")]
        public float ViewYaw;
        public float ViewPitch;
        public float AuthorityYaw;
        public float AuthorityPitch;
        public Quaternion AuthorityRotation;
        public float CurrentYaw;
        public float RotationVelocity;
        #endregion

        #region PHYSICS & MOVEMENT - 物理与移动状态
        [Header("Physics & Movement")]
        public bool IsGrounded;
        public bool IsDodgeing;
        public float VerticalVelocity;
        public bool JustLanded;
        public bool JustLeftGround;
        public bool IsAiming;
        public LocomotionState CurrentLocomotionState = LocomotionState.Idle;

        [Space]
        public Vector3 DesiredWorldMoveDir;
        public float DesiredLocalMoveAngle;

        [Header("Runtime Speed")]
        [Tooltip("Current horizontal movement speed (m/s) - 当前水平移动速度（米/秒）")]
        public float CurrentSpeed;
        #endregion

        #region INTENT - 单帧动作意图
        [Header("Action Intents")]
        public Vector3 TargetAimPoint;
        public Vector3 CameraLookDirection;
        public bool WantToRun;
        public bool WantsToDodge;
        public bool WantsToRoll;
        public bool WantsToJump;
        public bool WantsDoubleJump;
        public DoubleJumpDirection DoubleJumpDirection = DoubleJumpDirection.Up;

        [Space]
        public bool WantsToVault;
        public bool WantsLowVault;
        public bool WantsHighVault;
        public VaultObstacleInfo CurrentVaultInfo;

        [Header("Switching Intent")]
        public ItemDefinitionSO DesiredItemDefinition;

        public DesiredDirection QuantizedDirection;
        #endregion

        #region WARPING & VAULTING - 空间扭曲与翻越逻辑
        [Header("Motion Warping")]
        public bool IsWarping;
        public bool IsVaulting;
        public WarpedMotionData ActiveWarpData;
        public float NormalizedWarpTime;

        [Header("Warp IK Targets")]
        public Vector3 WarpIKTarget_LeftHand;
        public Vector3 WarpIKTarget_RightHand;
        public Quaternion WarpIKRotation_Hand;
        #endregion

        #region ANIMATION PARAMETERS - 动画驱动参数
        [Header("Animator Parameters")]
        public float CurrentAnimBlendX;
        public float CurrentAnimBlendY;
        public float CurrentRunCycleTime;
        public FootPhase ExpectedFootPhase;

        [Header("Landing & Transition")]
        public int FallHeightLevel;

        /// <summary>
        /// 一次性的动画过渡覆写。
        /// 由触发打断的状态写入，由下一个状态消费。一旦消费，立刻清空。
        /// </summary>
        public float? NextStateFadeOverride =null;
        //不设置成0是为了为了清晰区分 “未设置 / 不生效” 和 “明确设置为 0” 这两种完全不同的业务逻辑状态

        public float idleFadeInTime;
        public float moveStartFadeInTime;
        public float LandFadeInTime;
        public float loopFadeInTime;
        public float stopFadeInTime;
        #endregion

        #region IK & EQUIPMENT - 装备与肢体对齐
        [Header("Equipment Snapshot")]
        public EquipmentSnapshot CurrentEquipment = new EquipmentSnapshot();

        [Header("IK Goals")]
        public bool WantsLeftHandIK;
        public bool WantsRightHandIK;
        public bool WantsLookAtIK;
        public Transform LeftHandGoal;
        public Transform RightHandGoal;
        public Vector3 LookAtPosition;
        #endregion

        [Header("Item & Aiming Reference (物品与指向基准)")]

        /// <summary>
        /// 当前用于执行“指向”动作的基准 Transform。
        /// 例如：如果玩家拿着一个手电筒，这里就是手电筒灯泡的 Transform。
        /// IK 系统会让这个 Transform 的 Z 轴精确对准 TargetAimPoint。
        /// 如果为空，通常默认使用角色的头部进行指向。
        /// </summary>
        public Transform CurrentAimReference;

        #region ATTRIBUTES & TRACKING - 数值与周期追踪
        [Header("Attributes")]
        public float CurrentStamina;
        public bool IsStaminaDepleted;

        [Header("Tracking")]
        public bool HasPerformedDoubleJumpInAir;

        [Header("Global References")]
        public Transform CameraTransform;
        #endregion

        #region API 方法

        public PlayerRuntimeData()
        {
            CurrentLocomotionState = LocomotionState.Idle;
        }

        /// <summary>
        /// 每帧清理一次性意图标志位
        /// </summary>
        public void ResetIntetnt()
        {
            WantsToVault = false;
            WantToRun = false;
            WantsToJump = false;
            WantsDoubleJump = false;
            WantsLowVault = false;
            WantsHighVault = false;
        }

        #endregion


    }
}
