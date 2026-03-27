using Animancer;
using System.Collections.Generic;
using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 整个BBBNexus系统的 Root 节点唯一的 Monobehaviour 驱动源 
    /// 不包含任何具体游戏逻辑 仅负责组件整合、内存分配与严格的时序指令分发 
    /// 
    /// - Awake: 只做一次性分配/依赖注入（对象池复用时不会重复调用）
    /// - OnSpawned: 每次从池取出时做“帧状态复位 + 重启”
    /// - OnDespawned: 每次回收时做“回调/引用清理”
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(AnimancerComponent))]
    [RequireComponent(typeof(AnimancerFacade))]
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(AudioSource))]
    [DefaultExecutionOrder(-300)]
    public class BBBCharacterController : MonoBehaviour, IDamageable, IPoolable
    {
        [Header("--- 输入与表现源  ---")]
        [Tooltip("输入源 - 可拖拽赋值任何继承 IInputSourceBase 的组件")]
        public InputSourceBase InputSourceRef;
        [Tooltip("动画转接器 - 可拖拽赋值任何继承 AnimationFacadeBase 的组件")]
        public AnimationFacadeBase AnimationFacadeRef;
        [Tooltip("IK 目标源 - 可拖拽赋值任何继承 PlayerIKSourceBase 的组件")]
        public PlayerIKSourceBase IKSource;
        [Tooltip("用于播放角色音效的 AudioSource 建议关闭 Loop")]
        public AudioSource SfxSource;
        [Tooltip("注意: aniamncercomponet也记得要引用角色animator")]
        public Animator Animator;

        [Header("--- 核心配置 ---")]
        public PlayerSO Config;
        public Transform PlayerCamera;

        [Header("--- 表现与挂点 ---")]
        public Transform WeaponContainer;
        public Transform LeftHandBone { get; private set; }
        public Transform RightHandBone { get; private set; }
        public Transform LeftFootBone { get; private set; }
        public Transform RightFootBone { get; private set; }


        [Header("--- 调试选项 ---")]
        public EquippableItemSO DefaultEquipment1;
        public EquippableItemSO DefaultEquipment2;
        public EquippableItemSO DefaultEquipment3;
        public bool statedebug = false;


        // 运行时核心引用
        public StateMachine StateMachine { get; private set; }
        public GlobalInterruptProcessor InterruptProcessor { get; private set; }
        public PlayerRuntimeData RuntimeData { get; private set; }
        public InputData InputData { get; private set; }

        // 核心管线
        public InputPipeline InputPipeline { get; private set; }
        public MainProcessorPipeline MainProcessorPipeline { get; private set; }

        //子系统控制器
        public UpperBodyController UpperBodyCtrl { get; private set; }
        public FacialController FacialController { get; private set; }
        public IKController IkController { get; private set; }
        public PlayerInventoryController InventoryController { get; private set; }
        public ActionController ActionController { get; private set; }
        public AudioController AudioController { get; private set; }

        // 驱动器与外观层系统
        public AnimancerComponent Animancer { get; private set; }
        public CharacterController CharController { get; private set; }
        public MotionDriver MotionDriver { get; private set; }
        public EquipmentDriver EquipmentDriver { get; private set; }
        public AnimationFacadeBase AnimFacade { get; private set; }
        public AudioDriver AudioDriver { get; private set; }

        // 状态注册表
        public PlayerStateRegistry StateRegistry { get; private set; }

        //仲裁器(后期需要注册表化)
        public LODArbiter LodArbiter { get; private set; }
        public ArbiterPipeline ArbiterPipeline { get; private set; }

        //调试用缓存
        private PlayerBaseState _lastState;
        public event System.Action OnEquipmentChanged;

        private bool _booted;

        // Awake 负责内存分配、找组件、依赖注入 
        private void Awake()
        {
            Animator = GetComponent<Animator>();
            Animancer = GetComponent<AnimancerComponent>();
            CharController = GetComponent<CharacterController>();

            LeftHandBone=Animator.GetBoneTransform(HumanBodyBones.LeftHand);
            RightHandBone=Animator.GetBoneTransform(HumanBodyBones.RightHand);
            LeftFootBone=Animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            RightFootBone=Animator.GetBoneTransform(HumanBodyBones.RightFoot);

            // 统一的面板依赖注入检查 失败直接抛出异常
            try
            {
                if (InputSourceRef == null)
                {
                    throw new System.Exception("输入源未配置 请检查面板 InputSourceRef 赋值");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[PlayerController] 初始化警告 {ex.Message}");
            }

            try
            {
                AnimFacade = AnimationFacadeRef;
                if (AnimFacade == null)
                {
                    throw new System.Exception("动画源未配置 请检查面板 AnimationFacadeRef 赋值");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[PlayerController] 初始化警告 {ex.Message}");
            }

            try
            {
                if (IKSource == null)
                {
                    throw new System.Exception("IK目标源未配置 请检查面板 IKSource 赋值");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[PlayerController] 初始化警告 {ex.Message}");
            }

            Animancer.Animator.applyRootMotion = false;

            // 实例化纯数据容器
            RuntimeData = new PlayerRuntimeData(this);
            InputData = new InputData();

            // 实例化所有系统控制器与驱动器 
            StateMachine = new StateMachine();
            InterruptProcessor = new GlobalInterruptProcessor(this);
            MotionDriver = new MotionDriver(this);
            EquipmentDriver = new EquipmentDriver(this);
            LodArbiter = new LODArbiter(this);

            // 音频驱动器：如果没配 AudioSource 或模块，则 driver 仍可存在但会静默忽略播放请求。
            AudioDriver = new AudioDriver(transform, SfxSource, Config != null ? Config.Audio : null);

            // 实例化仲裁管线
            ArbiterPipeline = new ArbiterPipeline(this);

            // 建立管线并注入依赖
            InputPipeline = new InputPipeline(InputSourceRef);
            MainProcessorPipeline = new MainProcessorPipeline(this, InputPipeline);

            // 实例化子分层控制器
            InventoryController = new PlayerInventoryController(this);
            UpperBodyCtrl = new UpperBodyController(this);
            FacialController = new FacialController(this);
            IkController = new IKController(this);
            ActionController = new ActionController(this);
            AudioController = new AudioController(this);

            // 装载状态字典 分配独立内存实例
            StateRegistry = new PlayerStateRegistry();
            if (Config != null && Config.Brain != null)
            {
                StateRegistry.InitializeFromBrain(Config.Brain, this);
            }
            else
            {
                Debug.LogError("[PlayerController] 致命错误：未配置 PlayerSO 或 Brain");
            }
        }

        private void Start()
        {
            // 非池化使用触发一次Boot
            BootIfNeeded();
        }

        private void BootIfNeeded()
        {
            if (_booted) return;

            InitializeCamera();
            SetupAnimationLayers();
            InitializeEquipments();
            BootUpStateMachines();

            _booted = true;
        }

        public void OnSpawned()
        {
            // 对象池出池：确保启用状态下具备可运行的初始状态

            BootIfNeeded();

            // 复位帧级意图，防止复用时继承上一轮输入/仲裁结果。
            RuntimeData.ResetIntetnt();

            // 恢复 root motion 受控状态（某些 full-body override 可能改过它）
            if (Animancer != null && Animancer.Animator != null)
                Animancer.Animator.applyRootMotion = false;

            // 清理 IK 残留（目标可能在上次武器上）
            if (RuntimeData != null)
            {
                RuntimeData.CurrentAimReference = null;
                RuntimeData.WantsLookAtIK = false;
                RuntimeData.LeftHandGoal = null;
                RuntimeData.RightHandGoal = null;
                RuntimeData.WantsLeftHandIK = false;
                RuntimeData.WantsRightHandIK = false;
            }

            // 确保 ArbiterPipeline 本帧不会读到旧请求（例如 ActionOverride）
            // ActionArbitration/Override 结构若是 struct 直接归零
            if (RuntimeData != null)
            {
                RuntimeData.Override.IsActive = false;
            }
        }

        public void OnDespawned()
        {
            // 对象池回收：解除潜在引用 防止 callback/IK/装备对象继续持有该 PlayerController

            // 清空动画层回调 避免失活后仍触发逻辑
            AnimFacade?.ClearOverrideOnEndCallback();
            AnimFacade?.ClearOnEndCallback(0);
            AnimFacade?.ClearOnEndCallback(1);
            AnimFacade?.ClearOnEndCallback(2);

            // 让当前武器有机会停特效/解绑
            try { EquipmentDriver?.UnequipCurrentItem(); } catch { }

            if (RuntimeData != null)
            {
                RuntimeData.CurrentItem = null;
                RuntimeData.CurrentAimReference = null;
                RuntimeData.WantsLookAtIK = false;
                RuntimeData.WantsToFire = false;
                RuntimeData.ResetIntetnt();
            }
        }

        private void OnEnable()
        {
            // 对象池激活时 Start 不一定每次都会走（取决于场景/脚本执行顺序） 这里作为兜底
            if (Application.isPlaying)
                BootIfNeeded();
        }

        // 逻辑与意图更新 (在动画引擎运算之前)
        private void Update()
        {
            if (!_booted) return; 

            //Debug.Log(Animancer.Layers.Count);

            _lastState = StateMachine.CurrentState as PlayerBaseState;

            ArbiterPipeline.ProcessUpdateArbiters();

            InputPipeline.Update();

            MainProcessorPipeline.UpdateIntentProcessors();

            InventoryController.Update();

            MainProcessorPipeline.UpdateParameterProcessors();

            StateMachine.CurrentState.LogicUpdate();

            UpperBodyCtrl.Update();

            FacialController.Update();

            ActionController.Update();

            AudioController.Update();
            
            //古法状态调试 已经被drawxxldebuger代替 打包注释掉
            if (statedebug && StateMachine.CurrentState != null && _lastState != null)
            {
                if (StateMachine.CurrentState.GetType().Name != _lastState.GetType().Name)
                {
                    Debug.Log($"[State] {_lastState.GetType().Name} -> {StateMachine.CurrentState.GetType().Name}");
                }
            }
        }

        // 一些设计说明.......
        // 为什么角色的物理位移必须放在 LateUpdate？
        // 因为我们的位移是通过 MotionDriver 去读取动画片段的NormalizedTime计算出来的
        // Unity 的生命周期中 Animator 的骨骼结算发生在 Update 之后 LateUpdate 之前
        // 如果把 PhysicsUpdate 放在 Update 里 拿到的永远是上一帧的动画时间 导致角色的物理位置永远比动作快一帧 
        // 这会引发视觉上的抽搐问题 尤其是在低帧数的环境下 对那些带有转向的动画非常明显

        //物理与表现层的更新 (在动画引擎运算之后)
        private void LateUpdate()
        {
            if (!_booted) return; // pooling safety

            StateMachine.CurrentState?.PhysicsUpdate();

            IkController.Update();

            ArbiterPipeline?.ProcessLateUpdateArbiters();

            RuntimeData.ResetIntetnt();
        }

        public void NotifyEquipmentChanged()
        {
            OnEquipmentChanged?.Invoke();
        }

        public void RequestOverride(in ActionRequest request, bool flushImmediately = true)
        {
            RuntimeData.ActionArbitration.Submit(in request);

            // 如果要求立即刷新 则直接跑一次仲裁(一般情况下不用 如果有严格同步需求才请求)
            if (flushImmediately)
                ArbiterPipeline?.Action?.Arbitrate();
        }

        private void InitializeCamera()
        {
            if (PlayerCamera == null && Camera.main != null) PlayerCamera = Camera.main.transform;
            RuntimeData.CameraTransform = PlayerCamera;
        }

        private void SetupAnimationLayers()
        {
            if (AnimFacade != null && Config != null)
            {
                AnimFacade.SetLayerMask(1, Config.Core.UpperBodyMask);
                AnimFacade.SetLayerMask(2, Config.Core.FacialMask);
            }
        }

        private void InitializeEquipments()
        {
            InventoryController.Initialize();

            EquippableItemSO[] defaults = new EquippableItemSO[] { DefaultEquipment1, DefaultEquipment2, DefaultEquipment3 };
            ItemInstance firstToEquip = null;

            for (int i = 0; i < defaults.Length; i++)
            {
                if (defaults[i] != null)
                {
                    var instance = new ItemInstance(defaults[i], 1);
                    InventoryController.AssignItemToSlot(i, instance);
                    if (firstToEquip == null) firstToEquip = instance;
                }
            }

            if (firstToEquip != null) RuntimeData.CurrentItem = firstToEquip;
        }

        private void BootUpStateMachines()
        {
            if (StateRegistry.InitialState != null) StateMachine.Initialize(StateRegistry.InitialState);
            if (UpperBodyCtrl.StateRegistry.InitialState != null) UpperBodyCtrl.StateMachine.Initialize(UpperBodyCtrl.StateRegistry.InitialState);
        }

        #region IDamageable 接口实现
        public void RequestDamage(in DamageRequest request)
        {
            var health = ArbiterPipeline?.Health;
            if (health == null) return;

            health.Enqueue(in request);
        }

        #endregion
    }
}