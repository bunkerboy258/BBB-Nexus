using Items.Core;
using UnityEngine;
using Characters.Player.Animation;
using Characters.Player.Arbitration;
using Core.StateMachine;

namespace Characters.Player.Data
{
    /// <summary>
    /// 玩家运行时数据：用于在输入、物理、动画与 IK 之间共享的帧级黑板。
    /// 仅承载状态与意图，不包含行为逻辑。
    /// </summary>
    public class PlayerRuntimeData
    {
        public struct OverrideContext
        {
            public bool IsActive;
            public ActionRequest Request;
            public BaseState ReturnState;

            public void Clear()
            {
                IsActive = false;
                Request = default;
                ReturnState = null;
            }
        }

        public struct ArbitrationFlags
        {
            public bool BlockInput;
            public bool BlockUpperBody;
            public bool BlockFacial;
            public bool BlockIK;
            public bool BlockInventory;
            public bool IsDead;

            public void Clear()
            {
                BlockInput = false;
                BlockUpperBody = false;
                BlockFacial = false;
                BlockIK = false;
                BlockInventory = false;
                IsDead = false;
            }
        }

        public OverrideContext Override;
        public ArbitrationFlags Arbitration;

        public PlayerRuntimeData(PlayerController player)
        {
            CurrentHealth = player.Config.Core.MaxHealth;
            CameraTransform = player.PlayerCamera;
            CurrentStamina = player.Config.Core.MaxStamina;
            Override.Clear();
            Arbitration.Clear();
        }

        #region 核心状态
        public CharacterLOD CurrentLOD { get; set; } = CharacterLOD.High;
        public float CurrentHealth;
        public bool IsDead;
        #endregion

        #region 输入（来自输入系统）

        [Header("输入 - 玩家原始输入")]

        [Tooltip("相机视角输入：X=水平 Y=竖直（-1~1）")]
        public Vector2 LookInput;

        [Tooltip("移动摇杆输入：X=前后 Y=左右（-1~1）")]
        public Vector2 MoveInput;

        #endregion

        #region 视角与朝向（相机/朝向状态）

        [Header("视角 - 相机与朝向状态")]

        [Tooltip("视角水平角（度），对应相机 yaw")]
        public float ViewYaw;

        [Tooltip("视角俯仰角（度），对应相机 pitch")]
        public float ViewPitch;

        [Tooltip("权威朝向水平角（供 IK/上身使用）")]
        public float AuthorityYaw;

        [Tooltip("权威朝向俯仰角（供瞄准使用）")]
        public float AuthorityPitch;

        [Tooltip("权威旋转（四元数），代表相机期望的角色朝向")]
        public Quaternion AuthorityRotation;

        [Tooltip("角色当前朝向水平角（平滑跟随）")]
        public float CurrentYaw;

        [Tooltip("旋转速度，用于平滑转向")]
        public float RotationVelocity;

        #endregion

        #region 物理与移动（物理反馈）

        [Header("物理与移动 - 地面/速度/移动状态")]

        [Tooltip("是否接地")]
        public bool IsGrounded;

        [Tooltip("是否处于闪避中")]
        public bool IsDodgeing;

        [Tooltip("竖直速度，m/s")]
        public float VerticalVelocity;

        [Tooltip("刚着陆的瞬间标志（仅一帧）")]
        public bool JustLanded;

        [Tooltip("刚离地的瞬间标志（仅一帧）")]
        public bool JustLeftGround;

        [Tooltip("是否瞄准（影响上半身/动画树）")]
        public bool IsAiming;

        [Tooltip("上一帧下半身运动状态")]
        public LocomotionState LastLocomotionState = LocomotionState.Idle;

        [Tooltip("当前下半身运动状态（用于动画混合）")]
        public LocomotionState CurrentLocomotionState = LocomotionState.Idle;

        [Space]
        [Tooltip("期望移动方向（世界空间单位向量）")]
        public Vector3 DesiredWorldMoveDir;

        [Tooltip("相对于身体的期望移动角度（度）")]
        public float DesiredLocalMoveAngle;

        [Header("实时速度")]
        [Tooltip("当前水平速度，m/s")]
        public float CurrentSpeed;

        #endregion

        #region 装备（物品与指向基准）

        [Header("装备 - 当前物品与指向基准")]

        [Tooltip("快捷栏装备意图：-1 无意图，>=0 对应槽位")]
        public int WantsToEquipHotbarIndex = -1;

        [Tooltip("当前装备的物品实例，为 null 表示空手")]
        public ItemInstance CurrentItem;

        [Tooltip("指向基准 Transform（枪口、灯泡 或 头部）")]
        public Transform CurrentAimReference;

        #endregion

        #region 意图（帧级意图，需每帧清理）

        [Header("意图 - 帧级临时标志")]

        [Tooltip("瞄准目标点（世界坐标）")]
        public Vector3 TargetAimPoint;

        [Tooltip("相机朝向向量（用于上半身/动画）")]
        public Vector3 CameraLookDirection;

        [Tooltip("本帧是否想跑（由意图管线判断）")]
        public bool WantToRun;

        [Tooltip("本帧是否想闪避")]
        public bool WantsToDodge;

        [Tooltip("本帧是否想翻滚")]
        public bool WantsToRoll;

        [Tooltip("本帧是否想跳跃")]
        public bool WantsToJump;

        [Tooltip("本帧是否想二段跳")]
        public bool WantsDoubleJump;

        [Tooltip("二段跳方向")]
        public DoubleJumpDirection DoubleJumpDirection = DoubleJumpDirection.Up;

        [Space]
        [Tooltip("本帧是否想翻越")]
        public bool WantsToVault;

        [Tooltip("是否低翻越")]
        public bool WantsLowVault;

        [Tooltip("是否高翻越")]
        public bool WantsHighVault;

        [Tooltip("有效的翻越障碍物信息")]
        public VaultObstacleInfo CurrentVaultInfo;

        [Tooltip("量化的移动方向（8向）")]
        public DesiredDirection QuantizedDirection;

        [Space]
        [Header("下落与开火意图")]
        [Tooltip("本帧是否进入下落状态")]
        public bool WantsToFall;

        [Tooltip("本帧是否想开火")]
        public bool WantsToFire;

        [Space]
        [Header("表情意图")]
        [Tooltip("表情1 ")]
        public bool WantsExpression1;
        [Tooltip("表情2")]
        public bool WantsExpression2;
        [Tooltip("表情3")]
        public bool WantsExpression3;
        [Tooltip("表情4")]
        public bool WantsExpression4;

        #endregion

        #region 变形与翻越（根运动与翻越数据）

        [Header("根运动变形与翻越")]

        [Tooltip("是否处于根运动变形中")]
        public bool IsWarping;

        [Tooltip("是否处于翻越状态")]
        public bool IsVaulting;

        [Tooltip("当前激活的变形数据")]
        public WarpedMotionData ActiveWarpData;

        [Tooltip("变形的归一化时间（0~1）")]
        public float NormalizedWarpTime;

        [Header("变形期间的 IK 目标")]
        [Tooltip("左手 IK 目标点（世界）")]
        public Vector3 WarpIKTarget_LeftHand;

        [Tooltip("右手 IK 目标点（世界）")]
        public Vector3 WarpIKTarget_RightHand;

        [Tooltip("手部 IK 朝向（四元数）")]
        public Quaternion WarpIKRotation_Hand;

        #endregion

        #region 动画参数（动画混合与过渡）

        [Header("动画参数 - 混合与周期")]

        [Tooltip("前后混合（-1 后退，1 前进）")]
        public float CurrentAnimBlendX;

        [Tooltip("左右混合（-1 左，1 右）")]
        public float CurrentAnimBlendY;

        [Tooltip("跑步循环时间，用于脚步判定")]
        public float CurrentRunCycleTime;

        [Tooltip("预期脚相，用于选择动画过渡")]
        public FootPhase ExpectedFootPhase;

        [Header("着陆等级")]
        [Tooltip("下落高度等级（用于选取落地表现）")]
        public int FallHeightLevel;
        #endregion

        #region 播放选项覆盖（下一次播放生效）
        [Tooltip("下半身播放选项覆写，为 null 使用默认")]
        public AnimPlayOptions? NextStatePlayOptions = null;

        [Tooltip("上半身播放选项覆写，为 null 使用默认")]
        public AnimPlayOptions? NextUpperBodyStatePlayOptions = null;
        #endregion

        #region IK 驱动目标
        [Header("IK 目标")]
        [Tooltip("启用左手 IK")]
        public bool WantsLeftHandIK;

        [Tooltip("启用右手 IK")]
        public bool WantsRightHandIK;

        [Tooltip("启用头部 LookAt IK")]
        public bool WantsLookAtIK;

        [Tooltip("左手目标 Transform")]
        public Transform LeftHandGoal;

        [Tooltip("右手目标 Transform")]
        public Transform RightHandGoal;

        [Tooltip("头部注视点（世界坐标）")]
        public Vector3 LookAtPosition;
        #endregion

        #region 状态与追踪

        [Tooltip("当前体力值")]
        public float CurrentStamina;

        [Tooltip("体力枯竭标志")]
        public bool IsStaminaDepleted;

        [Tooltip("本次空中是否已使用二段跳")]
        public bool HasPerformedDoubleJumpInAir;

        [Tooltip("玩家相机 Transform，用于计算视角与朝向")]
        public Transform CameraTransform;
        #endregion

        #region 方法

        public PlayerRuntimeData()
        {
            CurrentLocomotionState = LocomotionState.Idle;
        }

        /// <summary>
        /// 清除所有一帧意图标志（仅影响帧级意图）
        /// </summary>
        public void ResetIntetnt()
        {
            WantsToVault = false;
            WantToRun = false;
            WantsToJump = false;
            WantsDoubleJump = false;
            WantsToDodge = false;
            WantsToRoll = false;
            WantsLowVault = false;
            WantsHighVault = false;
            WantsToFire = false; 
            WantsExpression1 = false;
            WantsExpression2 = false;
            WantsExpression3 = false;
            WantsExpression4 = false;
        }

        #endregion
    }
}
