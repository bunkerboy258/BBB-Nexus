using Items.Data;
using Items.Logic;
using System.Collections.Generic;
using UnityEngine;

namespace Characters.Player.Data
{
    #region 数据结构与枚举

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
