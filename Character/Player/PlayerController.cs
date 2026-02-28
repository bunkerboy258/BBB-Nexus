using Animancer;
using Characters.Player.Animation;
using Characters.Player.Core;      // For MotionDriver
using Characters.Player.Data;
using Characters.Player.Input;
using Characters.Player.Layers;
using Characters.Player.Processing;
using Characters.Player.States;
using Core.StateMachine;
using Items.Core;
using Items.Data;
using MagicaCloth2;
using System.Collections.Generic;
using System.Linq;
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
        [Header("IK System")]
        public PlayerIKSourceBase IKSource;
        public IAnimationFacade AnimFacade { get; private set; }

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

        public bool statedebug = false;
        private PlayerBaseState laststate;

        // [Removed] CameraRoot 同步已迁移到 Core.CameraSystem.CameraRigDriver（场景独立物体）。

        // --- 核心系统引用（供外部系统访问） ---
        public StateMachine StateMachine { get; private set; }

        public GlobalInterruptProcessor InterruptProcessor { get; private set; }
        public PlayerRuntimeData RuntimeData { get; private set; }
        public PlayerInventoryController  InventoryController{ get; private set; }
        public PlayerInputReader InputReader { get; private set; } // 供状态机（如 IdleState）访问
        public AnimancerComponent Animancer { get; private set; }
        public CharacterController CharController { get; private set; }
        public MotionDriver MotionDriver { get; private set; }
        public EquipmentDriver EquipmentDriver { get; private set; }

        // --- 状态实例 ---
        public PlayerStateRegistry StateRegistry { get; private set; }
        // --- 私有控制器实例 ---
        private UpperBodyController _upperBodyController;
        private FacialController _facialController;
        private IKController _ikController;

        private IntentProcessorPipeline _intentProcessorPipeline;
        private CharacterStatusDriver _characterStatusDriver;

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
                _intentProcessorPipeline.Equip.AssignItemToSlot(0, DefaultEquipment);
            }
            InitializeCamera();
            StateMachine.Initialize(StateRegistry.InitialState);
        }

        private void Update()
        {
            laststate = StateMachine.CurrentState as PlayerBaseState;
            // 1. 输入 -> 原始数据
            RuntimeData.MoveInput = InputReader.MoveInput;
            RuntimeData.LookInput = InputReader.LookInput;

            // 2. 原始数据 -> 逻辑意图 (含视角、装备、瞄准、运动)
            _intentProcessorPipeline.UpdateIntentProcessors();

            // 3. 被动状态更新：根据当前角色状态更新核心属性（体力/生命值等）
            _characterStatusDriver.Update();

            // 4. 执行物理（执行移动逻辑） — 先于参数处理，让 grounded/vertical 等反映本帧物理结果
            StateMachine.CurrentState.PhysicsUpdate();

            // 5. 逻辑意图 -> 表现层参数 (含动画参数、IK)
            _intentProcessorPipeline.UpdateParameterProcessors();

            // 6. 更新状态机（状态切换、逻辑更新）
            StateMachine.CurrentState.LogicUpdate();

            // 6.5. 更新上身分层控制器（装备、瞄准等）
            _upperBodyController.Update();

            // 7. 更新 IK
            _ikController.Update();

            // 8. 重置data意图标记    
            RuntimeData.ResetIntetnt();

            if(statedebug&& StateMachine.CurrentState.GetType().Name!=laststate.GetType().Name) Debug.Log(StateMachine.CurrentState.GetType().Name);  

        }

        // [Removed] LateUpdate：CameraRoot 同步由 CameraRigDriver 负责。

        // --- 初始化方法 ---
        /// <summary>
        /// 初始化运行时数据容器，设置初始耐力值
        /// </summary>
        private void InitializeData()
        {
            RuntimeData = new PlayerRuntimeData();
            RuntimeData.CurrentStamina = Config.Core.MaxStamina;
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
            if(GetComponent<AnimancerFacade>()==null)Debug.LogWarning("[PlayerController] 缺少 AnimancerFacade 组件！请确保它与 AnimancerComponent 在同一 GameObject 上，以便状态机正确播放动画。");
            AnimFacade =GetComponent<AnimancerFacade>(); // 获取 AnimancerFacade 组件引用，供状态使用
        }

        /// <summary>
        /// 初始化核心处理器（状态机、运动驱动、意图管线、角色状态系统）
        /// </summary>
        private void InitializeProcessors()
        {
            StateMachine = new StateMachine();

            InterruptProcessor = new GlobalInterruptProcessor(this);

            MotionDriver = new MotionDriver(this); // MotionDriver 依赖 Controller，在此初始化

            EquipmentDriver = new EquipmentDriver(this);

            // 初始化意图处理管道 (统一管理视角、移动、瞄准、装备、IK、参数)
            _intentProcessorPipeline = new IntentProcessorPipeline(this);

            // 初始化角色核心属性 Driver（被动响应）
            _characterStatusDriver = new CharacterStatusDriver(RuntimeData, Config);
        }

        /// <summary>
        /// 初始化所有状态实例，注入 Controller 依赖
        /// </summary>
        private void InitializeStates()
        {
            StateRegistry = new PlayerStateRegistry();

            if (Config != null && Config.Brain != null)
            {
                // 让注册表自己去读配置、造状态
                StateRegistry.InitializeFromBrain(Config.Brain, this);
            }
            else
            {
                Debug.LogError("[PlayerController] 致命错误：未配置 PlayerSO 或 Brain！");
            }
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

        // --- 对外 API ---
        public void PlayHurtExpression() => _facialController.PlayHurtExpression();
        public void NotifyEquipmentChanged()
        {
            OnEquipmentChanged?.Invoke();
        }

    }
}