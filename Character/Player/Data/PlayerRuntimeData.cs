using Items.Data;
using Items.Logic;
using UnityEngine;

namespace Characters.Player.Data
{

    // 装备快照类
    public class EquipmentSnapshot
    {
        public ItemDefinitionSO Definition;
        public InteractableItem Instance;
        public DeviceController DeviceLogic;
        public bool HasItem => Instance != null;
        public bool HasDevice => DeviceLogic != null;
    }
    /// <summary>
    /// 存储玩家角色运行时的动态变化状态数据。
    /// 作为纯数据容器，仅承载状态值，不包含业务逻辑，供各控制器读取/写入。
    /// </summary>
    public class PlayerRuntimeData
    {
        // =================================================================================
        #region Input Data & Intent (输入与意图)
        // 由 InputReader / InputIntentProcessor 写入
        // =================================================================================

        // 🔥 [新增] 视角输入 (鼠标/右摇杆) 🔥
        public Vector2 LookInput;
        // 🔥 [新增] 角色当前的 Y 轴朝向 (Degrees) 🔥
        // 由 MotionDriver 维护，作为旋转的权威数据源
        public float CurrentYaw;
        /// <summary>
        /// [InputReader ->] 当前帧的原始移动输入值 (-1 to 1)。
        /// </summary>
        public Vector2 MoveInput;

        /// <summary>
        /// [InputIntentProcessor ->] 上一帧非零的移动输入，用于旋转方向判断。
        /// </summary>
        public Vector2 LastNonZeroMoveInput;

        /// <summary>
        /// [StaminaController ->] 是否想要奔跑（按住 Shift 等加速按键触发）。
        /// </summary>
        public bool WantToRun;

        public bool WantsToJump;
        public bool IsAiming;
        // [Inventory ->] 目标装备 (玩家想装备什么)
        public ItemDefinitionSO DesiredItemDefinition;

        // [Inventory ->] 当前实际手持的装备 (模型已生成)
        public EquipmentSnapshot CurrentEquipment = new EquipmentSnapshot();  


        #endregion



        [Header("IK 意图")]
        public bool WantsLeftHandIK;
        public bool WantsRightHandIK;
        public bool WantsLookAtIK;    // State 告诉我们：现在需要注视

        public Transform LeftHandGoal; // 具体的抓取点
        public Transform RightHandGoal;
        public Vector3 LookAtPosition;
        /// <summary>
        /// 接地状态
        /// </summary>
        [Header("接地状态")]
        public bool IsGrounded; // 当前是否落地
        public float VerticalVelocity; // 当前垂直速度（Y轴）

        /// <summary>
        /// 翻越状态
        /// </summary>
        [Header("翻越状态")]
        // [VaultIntentProcessor ->] 当前是否满足翻越条件且按下了键
        public bool WantsToVault;

        // [VaultState ->] 当前是否正在执行翻越动作
        public bool IsVaulting;

        // =================================================================================
        #region Locomotion & Stamina (移动与耐力)
        // 由 StaminaController / MotionDriver 写入
        // =================================================================================

        /// <summary>
        /// [StaminaController ->] 角色当前是否处于奔跑/消耗耐力状态。
        /// </summary>
        public bool IsRunning;

        /// <summary>
        /// [StaminaController ->] 当前耐力值（0 ~ MaxStamina）。
        /// </summary>
        public float CurrentStamina;

        /// <summary>
        /// [MotionDriver / States ->] 角色旋转时的速度（Degrees/s），用于状态机中平滑旋转。
        /// </summary>
        public float RotationVelocity;

        #endregion

        // =================================================================================
        #region Animation Parameters (动画参数)
        // 由 ParameterProcessor / States 写入
        // =================================================================================

        /// <summary>
        /// [ParameterProcessor ->] 当前Loop动画的Y轴混合值 (0.7-1.0)，用于速度平滑过渡。
        /// </summary>
        public float CurrentAnimBlendY;

        /// <summary>
        /// [ParameterProcessor ->] 当前Loop动画的X轴混合值（角度），用于转向平滑过渡。
        /// </summary>
        public float CurrentAnimBlendX;


        /// <summary>
        /// [LoopState ->] 当前移动循环动画的归一化时间 (0-1)，用于停止判定。
        /// </summary>
        public float CurrentRunCycleTime;

        /// <summary>
        /// [StartState ->] 期望的下一个循环动画启动时的脚步相位。
        /// </summary>
        public FootPhase ExpectedFootPhase;

        #endregion

        // =================================================================================
        #region Shared References (共享引用)
        // 由 PlayerController 写入
        // =================================================================================

        /// <summary>
        /// 主摄像机Transform，用于计算移动朝向。
        /// </summary>
        public Transform CameraTransform;

        #endregion

        // 构造函数：对可能未初始化的字段预设默认值
        public PlayerRuntimeData()
        {
            // 示例：将 IsRunning 默认设为 false
            IsRunning = false;
        }

        public void ResetIntetnt()
        {
            WantsToVault = false;
            WantToRun = false;
            WantsToJump = false;
        }
    }
}