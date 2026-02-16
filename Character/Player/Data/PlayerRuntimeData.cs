using Items.Data;
using Items.Logic;
using UnityEngine;

namespace Characters.Player.Data
{
    /// <summary>
    /// 移动状态枚举
    /// 描述角色当前的移动速度和行为状态
    /// </summary>
    public enum LocomotionState
    {
        /// <summary>站立或静止</summary>
        Idle = 0,

        /// <summary>行走（Ctrl 按住，速度 = MoveSpeed * 0.5）</summary>
        Walk = 1,

        /// <summary>慢跑（正常移动，速度 = MoveSpeed）</summary>
        Jog = 2,

        /// <summary>冲刺（Shift 按住，速度 = RunSpeed，消耗体力）</summary>
        Sprint = 3,
    }

    /// <summary>
    /// 装备快照（“当前身上装备了什么”的运行时结果）。
    /// 
    /// 注意：
    /// - Definition 表示“装备的定义资源”；
    /// - Instance/DeviceLogic 是场景中实例化出来的对象（可为空）。
    /// </summary>
    public class EquipmentSnapshot
    {
        /// <summary>当前装备的定义（ScriptableObject）。为 null 表示空手。</summary>
        public ItemDefinitionSO Definition;

        /// <summary>当前装备在场景中的实例（挂在玩家 WeaponContainer 下）。</summary>
        public InteractableItem Instance;

        /// <summary>装置逻辑控制器（例如枪械开火/换弹）。没有装置则为 null。</summary>
        public DeviceController DeviceLogic;

        /// <summary>是否有物品实例（Instance != null）。</summary>
        public bool HasItem => Instance != null;

        /// <summary>是否有可用设备逻辑（DeviceLogic != null）。</summary>
        public bool HasDevice => DeviceLogic != null;
    }

    // ==================================================================================
    // 数据分类图例:
    // ==================================================================================
    // 
    // [INPUT]         - 输入数据: 由 InputReader/Processor 写入，其他系统消费
    // [STATE]         - 状态数据: 由 StateMachine/Driver 维护，描述"当前是什么"
    // [INTENT]        - 意图数据: 由 IntentProcessor 每帧产生，每帧末 Reset（一次性）
    // [PARAMETER]     - 参数数据: 由 ParameterProcessor 计算，供动画层使用
    // [ATTRIBUTE]     - 属性数据: 由 CharacterStatusDriver 驱动，描述角色数值（体力/生命等）
    // [REFERENCE]     - 引用数据: 启动时注入，运行期只读
    //
    // ==================================================================================

    public class PlayerRuntimeData
    {
        #region INPUT (输入数据：由 InputReader 写入)

        /// <summary>
        /// 视角输入（鼠标/右摇杆 delta）。每帧由 InputReader 采集。
        /// </summary>
        public Vector2 LookInput;

        /// <summary>
        /// 移动输入（-1~1 normalized）。X=左右（Strafe），Y=前后（Forward）。
        /// </summary>
        public Vector2 MoveInput;

        #endregion

        #region STATE (状态数据：由引擎/Driver 维护，描述"当前状态")

        // --- 视角与朝向状态 ---

        /// <summary>
        /// 视角水平角（度）。由 ViewRotationProcessor 累加 LookInput 得到。
        /// 用途：FreeLook 模式下的相机/角色参考系。
        /// </summary>
        public float ViewYaw;

        /// <summary>
        /// 视角俯仰角（度）。由 ViewRotationProcessor 累加 LookInput 得到。
        /// 用途：驱动 CameraRoot pitch 或 IK/瞄准仰角。
        /// </summary>
        public float ViewPitch;

        /// <summary>
        /// 权威水平角（度）。由 ViewRotationProcessor 维护（通常等于 ViewYaw）。
        /// 约定：所有"以相机为参考系"的逻辑（移动、对齐）应使用此值，而不是直接读 CameraTransform。
        /// </summary>
        public float AuthorityYaw;

        /// <summary>
        /// 权威俯仰角（度）。由 ViewRotationProcessor 维护（通常等于 ViewPitch）。
        /// </summary>
        public float AuthorityPitch;

        /// <summary>
        /// 权威旋转（世界空间）。等价于 Quaternion.Euler(AuthorityPitch, AuthorityYaw, 0)。
        /// 由 ViewRotationProcessor 每帧更新。
        /// </summary>
        public Quaternion AuthorityRotation;

        /// <summary>
        /// 角色当前世界 yaw（度）。由 MotionDriver 和状态机维护。
        /// 这是"角色身体朝向"的权威结果，供动画/相机对齐逻辑使用。
        /// </summary>
        public float CurrentYaw;

        /// <summary>
        /// SmoothDampAngle 的内部速度缓存。由 MotionDriver 维护。
        /// 重要：瞄准<->探索模式切换时应清零，避免 SmoothDamp 过冲。
        /// </summary>
        public float RotationVelocity;

        // --- 运动状态 ---

        /// <summary>
        /// 是否接地。由 MotionDriver（通过 CharacterController.isGrounded）每帧更新。
        /// 影响：重力计算、跳跃判定、动画选择。
        /// </summary>
        public bool IsGrounded;

        /// <summary>
        /// 角色垂直速度（m/s）。由 MotionDriver 维护。
        /// 用途：重力加速、跳跃初速、贴地力计算。
        /// </summary>
        public float VerticalVelocity;

        // --- 行为模式 ---

        /// <summary>
        /// 是否处于瞄准模式（Strafe）。由 AimIntentProcessor 控制。
        /// 影响：移动速度、转向逻辑、动画层。
        /// </summary>
        public bool IsAiming;

        /// <summary>
        /// 当前移动状态。由 LocomotionIntentProcessor 根据输入、体力、移动状态判定。
        /// 值：Idle/Walk/Jog/Sprint
        /// 消费：MotionDriver（速度计算）、MovementParameterProcessor（动画参数）、CharacterStatusDriver（体力消耗）
        /// </summary>
        public LocomotionState CurrentLocomotionState = LocomotionState.Idle;

        /// <summary>
        /// [Deprecated] 已被 CurrentLocomotionState 替代。
        /// 保留向后兼容，CurrentLocomotionState == LocomotionState.Sprint 时为 true。
        /// </summary>
        public bool IsRunning
        {
            get => CurrentLocomotionState == LocomotionState.Sprint;
            set => CurrentLocomotionState = value ? LocomotionState.Sprint : LocomotionState.Jog;
        }

        /// <summary>
        /// [Deprecated] 已被 CurrentLocomotionState 替代。
        /// 保留向后兼容，CurrentLocomotionState == LocomotionState.Jog 时为 true。
        /// </summary>
        public bool IsJogging
        {
            get => CurrentLocomotionState == LocomotionState.Jog;
            set => CurrentLocomotionState = value ? LocomotionState.Jog : LocomotionState.Idle;
        }

        /// <summary>
        /// 是否正在翻越中（持续状态）。由 VaultState 维护。
        /// 注意：不同于 WantsToVault（一帧意图），此值表示翻越动画正在播放。
        /// </summary>
        public bool IsVaulting;

        #endregion

        #region INTENT (意图数据：由 IntentProcessor 每帧产生，每帧末 Reset)

        /// <summary>
        /// 本帧是否想要奔跑（输入意图）。由 LocomotionIntentProcessor 设置。
        /// 注意：IsRunning 是系统决议结果；WantToRun 是玩家意图。
        /// </summary>
        public bool WantToRun;

        /// <summary>
        /// 本帧是否请求跳跃。由 LocomotionIntentProcessor 在收到跳跃按键时设置。
        /// 消费者：JumpState（状态机负责判定是否真正进入跳跃）。
        /// </summary>
        public bool WantsToJump;

        /// <summary>
        /// 本帧是否请求翻越。由 LocomotionIntentProcessor 在收到跳跃按键且检测到障碍时设置。
        /// 消费者：状态机（决定是切换到 VaultState 还是 JumpState）。
        /// </summary>
        public bool WantsToVault;

        // --- 装备意图 ---

        /// <summary>
        /// 装备意图：玩家希望装备到的物品定义。
        /// 由 EquipIntentProcessor 设置（通过快捷键数字1-5）。
        /// 为 null 表示"想要空手状态"。
        /// 
        /// 注意：不等同于 CurrentEquipment.Definition（后者是实际已装备结果）。
        /// 这个字段表示"我想切换到什么"，EquipmentDriver 负责执行这个意图。
        /// </summary>
        public ItemDefinitionSO DesiredItemDefinition;

        // --- IK 意图 ---

        /// <summary>
        /// 是否想要启用左手 IK。由 IKIntentProcessor 根据装备、瞄准状态判定。
        /// 用途：枪械护木握持、盾牌握持等。
        /// </summary>
        public bool WantsLeftHandIK;

        /// <summary>
        /// 是否想要启用右手 IK。由 IKIntentProcessor 根据装备、瞄准状态判定。
        /// </summary>
        public bool WantsRightHandIK;

        /// <summary>
        /// 是否想要启用注视 IK（头/眼睛看向目标）。由 IKIntentProcessor 根据瞄准状态判定。
        /// </summary>
        public bool WantsLookAtIK;

        #endregion

        #region PARAMETER (参数数据：由 ParameterProcessor 计算，供动画层使用)

        /// <summary>
        /// 动画混合参数 X（通常对应"移动方向"）。由 MovementParameterProcessor 计算并平滑。
        /// 输入给：Animator 2D 混合树的横轴。
        /// </summary>
        public float CurrentAnimBlendX;

        /// <summary>
        /// 动画混合参数 Y（通常对应"移动速度权重" / "走跑混合"）。由 MovementParameterProcessor 计算并平滑。
        /// 输入给：Animator 2D 混合树的纵轴。
        /// </summary>
        public float CurrentAnimBlendY;

        /// <summary>
        /// 进入 MoveLoop 时的淡入时间（秒）。
        /// 由 MoveStartState 在运动状态中途变化时设置，MoveLoopState 在 Enter 时消费并清零。
        /// 0 表示不淡入（直接切换）。
        /// </summary>
        public float LoopFadeInTime;

        /// <summary>
        /// 当前跑步循环相位（0~1）。由 MovementParameterProcessor 维护。
        /// 用途：脚步相位匹配，决定 Loop_L/Loop_R 或 Stop 选择。
        /// </summary>
        public float CurrentRunCycleTime;

        /// <summary>
        /// 期望的脚相位。用于起步/落地时选择合适的起步动画以匹配脚相位。
        /// </summary>
        public FootPhase ExpectedFootPhase;

        /// <summary>
        /// 期望的世界空间移动方向（已融合 MoveInput + AuthorityYaw）。
        /// 由 MovementParameterProcessor 每帧计算。
        /// 约定：状态机与动画层只读此值，避免重复计算导致不一致。
        /// </summary>
        public Vector3 DesiredWorldMoveDir;

        /// <summary>
        /// 期望的本地移动角（度，-180~180）。
        /// 定义：DesiredWorldMoveDir 在角色本地空间中的朝向角。
        /// 用途：起步动画方向选择（前、后、左、右、斜向）。
        /// </summary>
        public float DesiredLocalMoveAngle;

        /// <summary>
        /// 下落高度等级（0-4）。由 MovementParameterProcessor 根据下落距离和运动状态计算。
        /// 
        /// 定义：
        /// 0 = Level 1（最低）
        /// 1 = Level 2
        /// 2 = Level 3
        /// 3 = Level 4（最高）
        /// 4 = Exceed Limit（超过极限）
        /// 
        /// 消费规则（一次性数据）：
        /// - MovementParameterProcessor 每帧计算并写入此字段
        /// - PlayerLandState 在 Enter 时读取此值以选择落地动画
        /// - PlayerLandState.Enter 结束时清零此字段（防止残留影响）
        /// </summary>
        public int FallHeightLevel;

        #endregion

        #region ATTRIBUTE (属性数据：由 CharacterStatusDriver 驱动，描述角色核心数值)

        /// <summary>
        /// 当前体力值。由 CharacterStatusDriver 被动维护。
        /// 消费：LocomotionIntentProcessor（判定是否允许奔跑）。
        /// 变化：IsRunning 时消耗；非奔跑时恢复。
        /// </summary>
        public float CurrentStamina;

        /// <summary>
        /// 体力是否已耗尽。由 CharacterStatusDriver 维护。
        /// 作用：耗尽后需恢复至 MaxStamina * StaminaRecoverThreshold 才能重新奔跑（防止频繁切换）。
        /// </summary>
        public bool IsStaminaDepleted;

        // TODO: 未来扩展字段
        // public float CurrentHealth;
        // public bool IsHealthZero;
        // public float CurrentShield;
        // public Dictionary<int, BuffModifier> ActiveBuffs;

        #endregion

        #region EQUIPMENT (装备系统：当前装备结果)

        /// <summary>
        /// 当前实际装备快照。由 EquipmentDriver 执行 DesiredItemDefinition 变更时写入。
        /// 包含：定义资源、场景实例、逻辑控制器。
        /// </summary>
        public EquipmentSnapshot CurrentEquipment = new EquipmentSnapshot();

        #endregion

        #region IK_TARGETS (IK 目标点：由 IKIntentProcessor 设置)

        /// <summary>
        /// 左手 IK 目标点。由 IKIntentProcessor 根据装备设置。
        /// 通常指向：武器的握持点（护木、盾面等）。
        /// </summary>
        public Transform LeftHandGoal;

        /// <summary>
        /// 右手 IK 目标点。由 IKIntentProcessor 根据装备设置。
        /// 通常指向：武器的握把、扳机部分。
        /// </summary>
        public Transform RightHandGoal;

        /// <summary>
        /// 注视 IK 目标点（世界坐标）。由 IKIntentProcessor 根据瞄准状态设置。
        /// 通常：相机前方某固定距离处，或敌人头部。
        /// </summary>
        public Vector3 LookAtPosition;

        #endregion

        #region REFERENCE (引用数据：启动时注入，运行期只读)

        /// <summary>
        /// 主摄像机 Transform 缓存。在 PlayerController.InitializeCamera 设置。
        /// 用途：Camera-Relative 移动计算、IK 射线投影、世界方向转换等。
        /// </summary>
        public Transform CameraTransform;

        #endregion

        // ==================================================================================

        public PlayerRuntimeData()
        {
            IsRunning = false;
        }

        /// <summary>
        /// 每帧末清理"一次性意图"。
        /// 为什么：意图应由 Processor 每帧重新生成，避免状态残留导致误触发。
        /// 
        /// 清理内容：
        /// - WantToRun：运动意图（已在 CharacterStatusDriver.Update 后消费）
        /// - WantsToJump、WantsToVault：交互意图（已在 StateMachine.LogicUpdate 消费）
        /// </summary>
        public void ResetIntetnt()
        {
            WantsToVault = false;
            WantToRun = false;
            WantsToJump = false;
        }
    }
}