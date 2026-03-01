using Animancer;
using Characters.Player.Animation;
using Characters.Player.Core;       // For MotionDriver
using Characters.Player.Data;
using Characters.Player.Input;
using Characters.Player.Layers;
using Characters.Player.Processing;
using Characters.Player.States;
using Core.StateMachine;
using Items.Data;
using System.Collections.Generic;
using UnityEngine;

namespace Characters.Player
{
    /// <summary>
    /// 玩家角色的核心控制器。
    /// 职责:
    /// 1. 作为整个玩家系统的根节点（Root）。
    /// 2. 严格遵循黄金初始化三阶段：Awake(内存/依赖) -> Start(环境配置) -> BootUp(状态机点火)。
    /// 3. 在 Update 循环中，按固定物理与逻辑顺序驱动各子系统更新。
    /// 4. 不包含具体游戏逻辑，仅负责组件整合、指令分发。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputReader))]
    [RequireComponent(typeof(AnimancerComponent))]
    [RequireComponent(typeof(AnimancerFacade))]
    public class PlayerController : MonoBehaviour
    {
        // ==========================================
        // 1. 配置字段 (Inspector)
        // ==========================================
        [Header("--- 核心配置 (Core Config) ---")]
        [Tooltip("玩家的配置文件（ScriptableObject）")]
        public PlayerSO Config;
        [Tooltip("玩家摄像机（可选，未指定时自动获取 MainCamera）")]
        public Transform PlayerCamera;

        [Header("--- 表现与挂点 (Visuals & Sockets) ---")]
        public PlayerIKSourceBase IKSource;
        public Transform WeaponContainer;
        public Transform RightHandBone; // 用于约束
        public Animator animator;       // 预留 Animator 引用，供特殊需求使用（如 IK）

        [Header("--- 调试选项 (Debug Options) ---")]
        [Tooltip("如果配置了此项，游戏开始时会自动装备这个物品")]
        public ItemDefinitionSO DefaultEquipment;
        public bool statedebug = false;


        // ==========================================
        // 2. 运行时核心引用 (Runtime References)
        // ==========================================
        public StateMachine StateMachine { get; private set; }
        public GlobalInterruptProcessor InterruptProcessor { get; private set; }
        public PlayerRuntimeData RuntimeData { get; private set; }
        public PlayerInventoryController InventoryController { get; private set; }
        public PlayerInputReader InputReader { get; private set; }

        // --- 驱动器与外观层 ---
        public AnimancerComponent Animancer { get; private set; }
        public IAnimationFacade AnimFacade { get; private set; }
        public CharacterController CharController { get; private set; }
        public MotionDriver MotionDriver { get; private set; }
        public EquipmentDriver EquipmentDriver { get; private set; }

        // --- 状态注册表与子控制器 ---
        public PlayerStateRegistry StateRegistry { get; private set; }
        public UpperBodyController UpperBodyCtrl { get; private set; } // 规范命名，公开供状态访问
        private FacialController _facialController;
        private IKController _ikController;
        private IntentProcessorPipeline _intentProcessorPipeline;
        private CharacterStatusDriver _characterStatusDriver;

        // --- 内部缓存 ---
        private PlayerBaseState _lastState;
        public event System.Action OnEquipmentChanged;


        // ==========================================
        // 阶段一：Awake (内存分配、找组件、依赖注入)
        // 绝对不执行任何状态机逻辑！
        // ==========================================
        private void Awake()
        {
            // 1. 获取 Unity 原生与桥接组件
            animator = GetComponent<Animator>();
            Animancer = GetComponent<AnimancerComponent>();
            CharController = GetComponent<CharacterController>();
            InputReader = GetComponent<PlayerInputReader>();
            AnimFacade = GetComponent<AnimancerFacade>();

            Animancer.Animator.applyRootMotion = false; // 由 MotionDriver 接管

            // 2. 实例化纯数据容器
            RuntimeData = new PlayerRuntimeData();
            if (Config != null) RuntimeData.CurrentStamina = Config.Core.MaxStamina;

            // 3. 实例化所有系统控制器与驱动器 (依赖注入 this)
            InventoryController = new PlayerInventoryController(this);
            StateMachine = new StateMachine();
            InterruptProcessor = new GlobalInterruptProcessor(this);
            MotionDriver = new MotionDriver(this);
            EquipmentDriver = new EquipmentDriver(this);
            _intentProcessorPipeline = new IntentProcessorPipeline(this);
            _characterStatusDriver = new CharacterStatusDriver(RuntimeData, Config);

            // 4. 实例化子分层控制器
            UpperBodyCtrl = new UpperBodyController(this); // 里面只做 new Registry，不启动
            _facialController = new FacialController(Animancer, Config);
            _ikController = new IKController(this);

            // 5. 装载状态字典 (反射或枚举映射，分配独立内存实例)
            StateRegistry = new PlayerStateRegistry();
            if (Config != null && Config.Brain != null)
            {
                StateRegistry.InitializeFromBrain(Config.Brain, this);
            }
            else
            {
                Debug.LogError("[PlayerController] 致命错误：未配置 PlayerSO 或 Brain！");
            }
        }

        // ==========================================
        // 阶段二：Start (环境预热 与 正式点火)
        // ==========================================
        private void Start()
        {
            // --- 预热环境 (Setup Environment) ---

            // 1. 初始化摄像机
            InitializeCamera();

            // 2. 初始化动画系统层级与遮罩（必须在状态机启动前设置好！）
            SetupAnimationLayers();

            // 3. 初始化初始装备
            InitializeEquipments();

            // --- 正式点火 (Boot Up) ---

            // 4. 启动状态机！引擎通电！
            BootUpStateMachines();
        }

        private void InitializeCamera()
        {
            if (PlayerCamera == null && Camera.main != null)
            {
                PlayerCamera = Camera.main.transform;
            }
            RuntimeData.CameraTransform = PlayerCamera;
        }

        private void SetupAnimationLayers()
        {
            // TODO: 未来如果你在 Config 里配置了 UpperBodyMask (AvatarMask)
            // 可以在这里调用 AnimFacade.SetLayerMask(1, Config.UpperBodyMask);

            // 预留：设置第 1 层（上半身）的初始权重为 1
            AnimFacade.SetLayerWeight(1, 1f);
        }

        private void InitializeEquipments()
        {
            if (DefaultEquipment != null)
            {
                // 将默认装备放入槽位 0 (对应按键 1)
                _intentProcessorPipeline.Equip.AssignItemToSlot(0, DefaultEquipment);
            }
        }

        private void BootUpStateMachines()
        {
            // 1. 先启动全身/下半身底盘
            if (StateRegistry.InitialState != null)
            {
                StateMachine.Initialize(StateRegistry.InitialState);
            }

            // 2. 再启动上半身 (调用 UpperBodyController 中我们新写的 Start 方法)
            // 如果你没有在 UpperBodyController 写 Start()，可以直接这样调用：
            if (UpperBodyCtrl.StateRegistry.InitialState != null)
            {
                UpperBodyCtrl.StateMachine.Initialize(UpperBodyCtrl.StateRegistry.InitialState);
            }
        }


        // ==========================================
        // 阶段三：Update (固定管线流转)
        // ==========================================
        private void Update()
        {
            _lastState = StateMachine.CurrentState as PlayerBaseState;

            // 1. 输入 -> 原始数据
            RuntimeData.MoveInput = InputReader.MoveInput;
            RuntimeData.LookInput = InputReader.LookInput;

            // 2. 原始数据 -> 逻辑意图 (含视角、装备、瞄准、运动)
            _intentProcessorPipeline.UpdateIntentProcessors();

            // 3. 被动状态更新：根据当前角色状态更新核心属性（体力/生命值等）
            _characterStatusDriver.Update();

            // 4. 物理更新：先于逻辑处理，让 grounded/vertical 等反映本帧真实物理结果
            StateMachine.CurrentState?.PhysicsUpdate();

            // 5. 逻辑意图 -> 表现层参数 (更新动画 Mixer 参数等)
            _intentProcessorPipeline.UpdateParameterProcessors();

            // 6. 状态逻辑更新 (包含全局打断检测、状态流转逻辑)
            StateMachine.CurrentState?.LogicUpdate();

            // 7. 更新上半身分层控制器（装备、瞄准、攻击等）
            UpperBodyCtrl.Update();

            // 8. 更新 IK 结算
            _ikController.Update();

            // 9. 清理帧尾标记 (极度重要：防止意图残留到下一帧)
            RuntimeData.ResetIntetnt();

            // --- 调试监控 ---
            if (statedebug && StateMachine.CurrentState != null && _lastState != null)
            {
                if (StateMachine.CurrentState.GetType().Name != _lastState.GetType().Name)
                {
                    Debug.Log($"[状态切换] {_lastState.GetType().Name} -> {StateMachine.CurrentState.GetType().Name}");
                }
            }
        }

        // ==========================================
        // 外部通讯 API
        // ==========================================
        public void PlayHurtExpression() => _facialController.PlayHurtExpression();

        public void NotifyEquipmentChanged()
        {
            OnEquipmentChanged?.Invoke();
        }
    }
}