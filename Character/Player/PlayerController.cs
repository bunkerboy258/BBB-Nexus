using Animancer;
using Characters.Player.Animation;
using Characters.Player.Core;
using Characters.Player.Data;
using Characters.Player.Input;
using Characters.Player.Expression;
using Characters.Player.Processing;
using Characters.Player.States;
using Core.StateMachine;
using Items.Data;
using System.Collections.Generic;
using UnityEngine;
using Items.Core;

namespace Characters.Player
{
    // 玩家角色的核心控制器 它是整个玩家系统的根节点 
    // 采用 Update(逻辑) -> LateUpdate(物理与表现) 的错峰管线设计 
    // 不包含具体游戏逻辑 仅负责组件整合与指令分发 
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputReader))]
    [RequireComponent(typeof(AnimancerComponent))]
    [RequireComponent(typeof(AnimancerFacade))]
    [DefaultExecutionOrder(-300)]
    public class PlayerController : MonoBehaviour
    {
        [Header("--- 核心配置 ---")]
        [Tooltip("玩家的配置文件")]
        public PlayerSO Config;
        [Tooltip("玩家摄像机 可选 未指定时自动获取 MainCamera")]
        public Transform PlayerCamera;

        [Header("--- 表现与挂点 ---")]
        public PlayerIKSourceBase IKSource;
        public Transform WeaponContainer;
        public Transform RightHandBone;
        public Animator animator;

        [Header("--- 调试选项 Debug Options ---")]
        [Tooltip("如果配置了此项 游戏开始时会自动装备这个物品 最多 3 个 用于调试")]
        public EquippableItemSO DefaultEquipment1;
        public EquippableItemSO DefaultEquipment2;
        public EquippableItemSO DefaultEquipment3;
        [Tooltip("开启状态切换日志 输出每次状态机切换的状态名称")]
        public bool statedebug = false;

        // 运行时核心引用
        public StateMachine StateMachine { get; private set; }
        public GlobalInterruptProcessor InterruptProcessor { get; private set; }
        public PlayerRuntimeData RuntimeData { get; private set; }
        public Characters.Player.Expression.PlayerInventoryController InventoryController { get; private set; }
        public PlayerInputReader InputReader { get; private set; }

        // 驱动器与外观层系统
        public AnimancerComponent Animancer { get; private set; }
        public IAnimationFacade AnimFacade { get; private set; }
        public CharacterController CharController { get; private set; }
        public MotionDriver MotionDriver { get; private set; }
        public EquipmentDriver EquipmentDriver { get; private set; }

        // 状态注册表与子控制器
        public PlayerStateRegistry StateRegistry { get; private set; }
        public UpperBodyController UpperBodyCtrl { get; private set; }
        private FacialController _facialController;
        private IKController _ikController;
        private IntentProcessorPipeline _intentProcessorPipeline;
        private CharacterStatusDriver _characterStatusDriver;

        // 内部缓存
        private PlayerBaseState _lastState;
        public event System.Action OnEquipmentChanged;

        // Awake 负责内存分配 找组件 依赖注入 
        // 所有初始化都在这里完成 不依赖运行时数据 
        private void Awake()
        {
            // 1. 获取 Unity 原生与桥接组件
            animator = GetComponent<Animator>();
            Animancer = GetComponent<AnimancerComponent>();
            CharController = GetComponent<CharacterController>();
            InputReader = GetComponent<PlayerInputReader>();
            AnimFacade = GetComponent<AnimancerFacade>();

            Animancer.Animator.applyRootMotion = false;

            // 2. 实例化纯数据容器
            RuntimeData = new PlayerRuntimeData();
            if (Config != null) RuntimeData.CurrentStamina = Config.Core.MaxStamina;

            // 3. 实例化所有系统控制器与驱动器 
            StateMachine = new StateMachine();
            InterruptProcessor = new GlobalInterruptProcessor(this);
            MotionDriver = new MotionDriver(this);
            EquipmentDriver = new EquipmentDriver(this);
            _intentProcessorPipeline = new IntentProcessorPipeline(this);
            _characterStatusDriver = new CharacterStatusDriver(RuntimeData, Config);

            // 4. 实例化子分层控制器
            InventoryController = new PlayerInventoryController(this);
            UpperBodyCtrl = new UpperBodyController(this);
            _facialController = new FacialController(Animancer, Config, RuntimeData);
            _ikController = new IKController(this);

            // 5. 装载状态字典 反射或枚举映射 分配独立内存实例
            StateRegistry = new PlayerStateRegistry();
            if (Config != null && Config.Brain != null)
            {
                StateRegistry.InitializeFromBrain(Config.Brain, this);
            }
            else
            {
                Debug.LogError("[PlayerController] 致命错误 未配置 PlayerSO 或 Brain");
            }
        }

        private void Start()
        {
            // 运行环境准备 这时所有脚本都已经 Awake 了

            // 1. 初始化摄像机
            InitializeCamera();

            // 2. 初始化动画系统层级与遮罩 必须在状态机启动前设置好
            SetupAnimationLayers();

            // 初始化物品栏控制器 绑定数字键等输入
            InventoryController.Initialize();

            // 3. 初始化初始装备
            InitializeEquipments();

            // 正式运行
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
            AnimFacade.SetLayerMask(1, Config.Core.UpperBodyMask);
            AnimFacade.SetLayerMask(2, Config.Core.FacialMask);
        }

        private void InitializeEquipments()
        {
            // 支持最多三个调试装备 会放入快捷栏的前 3 格并自动装备第一个非空项
            EquippableItemSO[] defaults = new EquippableItemSO[] { DefaultEquipment1, DefaultEquipment2, DefaultEquipment3 };
            ItemInstance firstToEquip = null;

            for (int i = 0; i < defaults.Length; i++)
            {
                var def = defaults[i];
                if (def != null)
                {
                    var instance = new ItemInstance(def, 1);
                    InventoryController.AssignItemToSlot(i, instance);

                    if (firstToEquip == null)
                    {
                        firstToEquip = instance;
                    }
                }
            }

            // 自动装备第一个非空调试装备
            if (firstToEquip != null)
            {
                RuntimeData.CurrentItem = firstToEquip;
            }
        }

        private void BootUpStateMachines()
        {
            if (StateRegistry.InitialState != null)
            {
                StateMachine.Initialize(StateRegistry.InitialState);
            }

            if (UpperBodyCtrl.StateRegistry.InitialState != null)
                UpperBodyCtrl.StateMachine.Initialize(UpperBodyCtrl.StateRegistry.InitialState);
        }

        // 逻辑与意图更新 (先于动画系统执行)
        private void Update()
        {
            _lastState = StateMachine.CurrentState as PlayerBaseState;

            // 1. 输入 原始数据 直接读取 PlayerInputFrame
            var inputFrame = InputReader.Current;
            RuntimeData.MoveInput = inputFrame.Move;
            RuntimeData.LookInput = inputFrame.Look;

            // 2. 原始数据 逻辑意图 含视角 装备 瞄准 运动
            _intentProcessorPipeline.UpdateIntentProcessors();

            // 3. 被动状态更新 根据当前角色状态更新核心属性 体力 生命值等
            _characterStatusDriver.Update();

            // 4. 逻辑意图 表现层参数 更新动画 Mixer 参数等
            _intentProcessorPipeline.UpdateParameterProcessors();

            // 5. 状态逻辑更新 包含全局打断检测 状态流转逻辑
            StateMachine.CurrentState?.LogicUpdate();

            // 6. 更新上半身分层控制器 装备 瞄准 攻击等
            UpperBodyCtrl.Update();

            // 7. 更新表情 读取黑板意图并播放瞬时表情
            _facialController?.Update();

            // 调试状态切换记录
            if (statedebug && StateMachine.CurrentState != null && _lastState != null)
            {
                if (StateMachine.CurrentState.GetType().Name != _lastState.GetType().Name)
                {
                    Debug.Log($"[状态切换] {_lastState.GetType().Name} -> {StateMachine.CurrentState.GetType().Name}");
                }
            }
        }

        // 物理与表现对齐 (在动画引擎结算后执行)
        private void LateUpdate()
        {
            // 注意：当代码执行到这里时，Unity内部已经自动推进了动画时间并结算了最新骨骼姿态。

            // 8. 物理更新 先于逻辑处理 让 grounded vertical 等反映本帧真实物理结果
            // （此时 MotionDriver 去读取时间，拿到的就是引擎刚刚推进好的最新时间戳）
            StateMachine.CurrentState?.PhysicsUpdate();

            // 9. 更新 IK 结算
            // （必须在 PhysicsUpdate 之后执行，因为胶囊体移动完、骨骼摆放完，IK 贴合才能绝对精准不打滑）
            _ikController.Update();

            // 10. 清理帧尾标记 防止意图残留到下一帧的防御性编程
            RuntimeData.ResetIntetnt();
        }

        public void NotifyEquipmentChanged()
        {
            OnEquipmentChanged?.Invoke();
        }
        //一些设计说明：
        //为什么动画层的更新是反直觉的在物理更新之前？因为 我们的物理系统是死死咬合动画系统的归一化时间的
        //之前的方案中 物理更新在前 导致骨骼的位置永远比物理位置慢一帧 造成了轻微的视觉问题
        //尤其是在带有转向的动画 在低帧数的环境下 抽搐会更明显
        //然鹅 unity并没有提供手动更新动画的选项 如果去修改时钟 可能会引发更多问题 故使用lateupdate
    }
}