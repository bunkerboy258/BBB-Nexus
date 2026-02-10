using Animancer;
using Characters.Player.Core;      // For MotionDriver
using Characters.Player.Data;
using Characters.Player.Input;
using Characters.Player.Layers;
using Characters.Player.Parameters;
using Characters.Player.States;
using Core.StateMachine;
using Items.Core;
using Items.Data;
using MagicaCloth2;
using UnityEngine;

namespace Characters.Player
{
    /// <summary>
    /// 玩家角色的核心控制器。
    /// 职责:
    /// 1. 作为整个玩家系统的根节点（Root）。
    /// 2. 初始化并持有核心依赖（状态机、运动驱动、输入、数据）。
    /// 3. 在 Update 循环中，按固定顺序驱动各子系统更新。
    /// 4. 不包含具体游戏逻辑，仅负责组件整合、指令分发。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputReader))]
    [RequireComponent(typeof(AnimancerComponent))]
    public class PlayerController : MonoBehaviour
    {
        // --- 配置字段（在 Inspector 面板赋值） ---
        [Header("Configuration")]
        [Tooltip("玩家的配置文件（ScriptableObject）")]
        public PlayerSO Config;


        [Tooltip("玩家摄像机（可选，未指定时自动获取 MainCamera）")]
        public Transform PlayerCamera;

        public Animator animator; // 预留 Animator 引用，供特殊需求使用（如 IK）
        public event System.Action OnEquipmentChanged;
        // 武器挂载容器 (在 Hierarchy 中手动创建一个空物体，放在 Player 下)
        [Header("Runtime References")]
        public Transform WeaponContainer;
        // 右手骨骼引用 (用于约束)
        public Transform RightHandBone { get; private set; }

        [Header("--- 调试选项 (Debug Options) ---")]
        [Space(5)]
        [Tooltip("如果配置了此项，游戏开始时会自动装备这个物品")]
        public ItemDefinitionSO DefaultEquipment;

        // --- 核心系统引用（供外部系统访问） ---
        public StateMachine StateMachine { get; private set; }
        public PlayerRuntimeData RuntimeData { get; private set; }
        public PlayerInventoryController  InventoryController{ get; private set; }
        public PlayerInputReader InputReader { get; private set; } // 供状态机（如 IdleState）访问
        public AnimancerComponent Animancer { get; private set; }
        public CharacterController CharController { get; private set; }
        public MotionDriver MotionDriver { get; private set; }
        public EquipmentDriver EquipmentDriver { get; private set; }

        // --- 状态实例 ---
        public PlayerIdleState IdleState { get; private set; }
        public PlayerMoveStartState MoveStartState { get; private set; }
        public PlayerMoveLoopState MoveLoopState { get; private set; }
        public PlayerStopState StopState { get; private set; }
        public PlayerVaultState VaultState { get; private set; } 
        public PlayerJumpState JumpState { get; private set; }
        public PlayerLandState LandState { get; private set; }
        public PlayerAimIdleState AimIdleState { get; private set; }
        public PlayerAimMoveState AimMoveState { get; private set; }

        // --- 私有控制器实例 ---
        private UpperBodyController _upperBodyController;
        private FacialController _facialController;
        private IKController _ikController;
        private StaminaController _staminaController;       // [耐力系统]

        private MovementParameterProcessor _parameterProcessor;
        private JumpInteractionProcessor _jumpInteractionProcessor; // [翻越处理]
        private InputIntentProcessor _inputIntentProcessor; // [输入处理]
        private EquipIntentProcessor _equipIntentProcessor;
        private AimIntentProcessor _aimIntentProcessor; // [瞄准处理]
        private IKIntentProcessor _iKIntentProcessor;


        // --- Unity 生命周期方法 ---
        private void Awake()
        {
            animator = gameObject.GetComponent<Animator>(); // 获取 Animator 组件引用，供 IK 使用
            InitializeData();
            InitializeComponents();
            InitializeProcessors();
            InitializeStates();
            InitializeLayers();
        }

        private void Start()
        {
            // 通过 InventoryController 进行正规初始化 🔥
            if (DefaultEquipment != null)
            {
                // 1. 将默认装备放入槽位 0 (对应按键 1)
                _equipIntentProcessor.AssignItemToSlot(0,DefaultEquipment);
            }
            InitializeCamera();
            StateMachine.Initialize(IdleState);
        }

        private void Update()
        {
            // 1. 输入 -> 原始数据
            RuntimeData.MoveInput = InputReader.MoveInput;

            // 2. 原始数据 -> 逻辑意图
            _inputIntentProcessor.Update();
            _jumpInteractionProcessor.Update();
            _aimIntentProcessor.Update();

            // 3. 意图 -> 状态判定（是否奔跑）
            _staminaController.Update();

            _equipIntentProcessor.update();
            // 4. 状态 -> 动画参数（走/跑混合）
            _parameterProcessor.Update();

            // 5. 更新状态机（状态切换、逻辑更新）
            StateMachine.CurrentState.LogicUpdate();

            // 5.5. 更新上身分层控制器（装备、瞄准等）
            _upperBodyController.Update();

            // 6. 执行物理（执行移动逻辑）
            StateMachine.CurrentState.PhysicsUpdate();

            _iKIntentProcessor.Update();
            _ikController.Update();

            // 7. 重置data意图标记    
            RuntimeData.ResetIntetnt();
        }

        // --- 初始化方法 ---
        /// <summary>
        /// 初始化运行时数据容器，设置初始耐力值
        /// </summary>
        private void InitializeData()
        {
            RuntimeData = new PlayerRuntimeData();
            RuntimeData.CurrentStamina = Config.MaxStamina;
            InventoryController=new PlayerInventoryController(this);
        }

        /// <summary>
        /// 初始化 Unity 组件引用，关闭动画根运动（由 MotionDriver 接管移动）
        /// </summary>
        private void InitializeComponents()
        {
            Animancer = GetComponent<AnimancerComponent>();
            CharController = GetComponent<CharacterController>();
            InputReader = GetComponent<PlayerInputReader>(); // 赋值供外部访问
            Animancer.Animator.applyRootMotion = false;
        }

        /// <summary>
        /// 初始化核心处理器（状态机、运动驱动、输入意图、耐力系统）
        /// </summary>
        private void InitializeProcessors()
        {
            StateMachine = new StateMachine();
            MotionDriver = new MotionDriver(this); // MotionDriver 依赖 Controller，在此初始化

            EquipmentDriver = new EquipmentDriver(this);

            // 处理器注入 Config 和 RuntimeData
            _parameterProcessor = new MovementParameterProcessor(this);
            _inputIntentProcessor = new InputIntentProcessor(this); // 注入 Controller 供内部访问
            _jumpInteractionProcessor = new JumpInteractionProcessor(this); // 同上
            _staminaController = new StaminaController(this);       // 同上
            _equipIntentProcessor=new EquipIntentProcessor(this);
            _aimIntentProcessor = new AimIntentProcessor(this); // 同上
            _iKIntentProcessor=new IKIntentProcessor(this);
        }

        /// <summary>
        /// 初始化所有状态实例，注入 Controller 依赖
        /// </summary>
        private void InitializeStates()
        {
            IdleState = new PlayerIdleState(this);
            MoveStartState = new PlayerMoveStartState(this);
            MoveLoopState = new PlayerMoveLoopState(this);
            VaultState = new PlayerVaultState(this);    
            StopState = new PlayerStopState(this);
            JumpState = new PlayerJumpState(this);
            LandState = new PlayerLandState(this);
            AimIdleState=new PlayerAimIdleState(this);
            AimMoveState =new PlayerAimMoveState(this);
        }

        /// <summary>
        /// 初始化分层动画控制器（上半身、面部）
        /// </summary>
        private void InitializeLayers()
        {
            _upperBodyController = new UpperBodyController(this);
            _facialController = new FacialController(Animancer, Config);
            _ikController=new IKController(this); 
        }

        /// <summary>
        /// 初始化摄像机引用，未指定时自动获取主摄像机
        /// </summary>
        private void InitializeCamera()
        {
            if (PlayerCamera == null && Camera.main != null)
            {
                PlayerCamera = Camera.main.transform;
            }
            RuntimeData.CameraTransform = PlayerCamera;
        }

        // 这是 Unity 引擎的硬性规定 如果使用原生ik必须带上这个方法
        // 并且Unity只会在挂载了 Animator 组件的同一个 GameObject 上的脚本里 寻找并调用这个方法......
        private void OnAnimatorIK(int layerIndex)
        {
            // 转发给管理器
            _ikController?.OnAnimatorIK_Internal(layerIndex);
        }
        // --- 对外 API ---
        public void PlayHurtExpression() => _facialController.PlayHurtExpression();
        public void NotifyEquipmentChanged()
        {
            OnEquipmentChanged?.Invoke();
        }

    }
}