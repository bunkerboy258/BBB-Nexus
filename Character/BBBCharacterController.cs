using Animancer;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
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
    public class BBBCharacterController : MonoBehaviour, IPoolable, IHealthBarTarget
    {
        public static BBBCharacterController PlayerInstance { get; private set; }

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
        public HealingItemSO QuickHealItem;

        [Header("--- 数据服务 ---")]
        [Tooltip("Hub 实现类（如 NekoGraphHub）— 装备/库存/角色状态共用一个 Hub")]
        [ServiceTypeField(typeof(IHub))]
        public string HubTypeName;
        [Tooltip("装备槽位服务实现类（如 NekoEquipmentService）")]
        [ServiceTypeField(typeof(IEquipmentService))]
        public string EquipmentServiceTypeName;
        [Tooltip("背包物品服务实现类（如 NekoInventoryService）")]
        [ServiceTypeField(typeof(IInventoryService))]
        public string InventoryServiceTypeName;
        [Tooltip("状态存储服务实现类（如 NekoStateStoreService）")]
        [FormerlySerializedAs("CharStateServiceTypeName")]
        [ServiceTypeField(typeof(IStateStoreService))]
        public string StateStoreServiceTypeName;
        [Tooltip("状态模板配置（StateProfileSO）")]
        public StateProfileSO StateProfile;
        [Tooltip("角色状态改写服务实现类（如 NekoStateModifyService）")]
        [ServiceTypeField(typeof(IStateModifyService))]
        public string StateModifyServiceTypeName;
        [Tooltip("额外动作服务实现类（如 LigteSoulExtraActionService）")]
        [ServiceTypeField(typeof(IExtraActionService))]
        public string ExtraActionServiceTypeName;

        [Header("--- 表现与挂点 ---")]
        public Transform MainhandWeaponContainer;   // 主手（右手）武器容器
        public Transform OffhandWeaponContainer;    // 副手（左手）武器容器
        public Transform LeftHandBone { get; private set; }
        public Transform RightHandBone { get; private set; }
        public Transform LeftFootBone { get; private set; }
        public Transform RightFootBone { get; private set; }
        public Transform HeadBone { get; private set; }


        [Header("--- 调试选项 ---")]
        public EquippableItemSO DebugMainhandEquipment1;
        public EquippableItemSO DebugMainhandEquipment2;
        public EquippableItemSO DebugMainhandEquipment3;
        public EquippableItemSO DebugMainhandEquipment4;
        public EquippableItemSO DebugMainhandEquipment5;
        public bool DebugParryTrace = true;
        [HideInInspector]
        [System.Obsolete("Use DebugMainhandEquipment1~5 instead.")]
        public EquippableItemSO DefaultEquipment1;
        [HideInInspector]
        [System.Obsolete("Use DebugMainhandEquipment1~5 instead.")]
        public EquippableItemSO DefaultEquipment2;
        [HideInInspector]
        [System.Obsolete("Use DebugMainhandEquipment1~5 instead.")]
        public EquippableItemSO DefaultEquipment3;
        public bool statedebug = false;
        public bool DebugShowCurrentClipOverlay = false;
        public bool DebugFullBodyRootMotion = false;
        public Vector3 DebugClipOverlayWorldOffset = new Vector3(0f, 0.12f, 0f);


        // 数据服务运行时实例（非序列化，由 InstantiateServices() 反射创建）
        public IHub Hub { get; private set; }
        public IEquipmentService EquipmentService { get; private set; }
        public IInventoryService InventoryService { get; private set; }
        public IStateStoreService StateStoreService { get; private set; }
        public IStateModifyService StateModifyService { get; private set; }
        public IExtraActionService ExtraActionService { get; private set; }

        // BBB 刚需状态钩子（由 IStateStoreService 决定何时触发）
        public event Action OnDead;
        public event Action OnRevive;
        public event Action<string, double> OnStateChanged;

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
        public PlayerInventoryOverlay InventoryOverlay { get; private set; }
        public QuickHealOverlay QuickHealOverlay { get; private set; }
        public ActionController ActionController { get; private set; }
        public AudioController AudioController { get; private set; }
        public ExtraActionController ExtraActionController { get; private set; }
        public ParryHandler ParryHandler { get; private set; }
        public PlayerInteractionSensor InteractionSensor { get; private set; }
        public ReadingMessageOverlay ReadingOverlay { get; private set; }

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

        /// <summary>异常状态仲裁器快捷访问</summary>
        public StatusEffectArbiter StatusEffects => ArbiterPipeline.StatusEffect;
        public CharacterArbiter CharacterArbiter => ArbiterPipeline.Character;
        public float CurrentMaxHealth => 0f;
        public float CurrentMaxSanity => 0f;
        public float CurrentSanity => 0f;
        public float CurrentSanityNormalized => 0f;
        public Transform HealthBarTransform => transform;
        public float CurrentHealthForBar => 0f;
        public float MaxHealthForBar => 0f;
        public bool RootMotionHandledVerticalThisFrame { get; private set; }

        private int _shieldBlockedFrame = -1;
        private int _lastIncomingDamageFrame = -1;
        private int _lastIncomingAttackerId;
        private int _lastIncomingWeaponId;
        /// <summary>盾牌被击中时调用，标记本帧已拦截，HealthArbiter 将跳过伤害队列</summary>
        public void NotifyShieldBlocked() => _shieldBlockedFrame = UnityEngine.Time.frameCount;
        public bool IsShieldBlockedThisFrame => _shieldBlockedFrame == UnityEngine.Time.frameCount;

        //调试用缓存
        private PlayerBaseState _lastState;
        public event System.Action OnEquipmentChanged;

        private bool _booted;
        private bool _wasLocomotionBlocked;
        private int _lastRootMotionDebugFrame = -1;
        private int _capturedRootMotionFrame = -1;
        private Vector3 _capturedRootMotionDeltaPosition = Vector3.zero;
        private Quaternion _capturedRootMotionDeltaRotation = Quaternion.identity;
        private GUIStyle _debugClipOverlayStyle;
        private GUIStyle _debugClipOverlayPanelStyle;
        private Renderer[] _debugOverlayRenderers;
        private AvatarMask _runtimeUpperBodyMask;
        private AvatarMask _runtimeFacialMask;

        // Awake 负责内存分配、找组件、依赖注入 
        private void Awake()
        {
            RegisterAsPlayerSingletonIfNeeded();
            Animator = GetComponent<Animator>();
            Animancer = GetComponent<AnimancerComponent>();
            CharController = GetComponent<CharacterController>();

            LeftHandBone=Animator.GetBoneTransform(HumanBodyBones.LeftHand);
            RightHandBone=Animator.GetBoneTransform(HumanBodyBones.RightHand);
            LeftFootBone=Animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            RightFootBone=Animator.GetBoneTransform(HumanBodyBones.RightFoot);
            HeadBone=Animator.GetBoneTransform(HumanBodyBones.Head);
            _debugOverlayRenderers = GetComponentsInChildren<Renderer>(true);
            InteractionSensor = GetComponentInChildren<PlayerInteractionSensor>(true);
            ReadingOverlay = GetComponentInChildren<ReadingMessageOverlay>(true);

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
                Debug.LogWarning($"[PlayerController] 初始化警告 {ex.Message}");
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
            InventoryOverlay = GetComponent<PlayerInventoryOverlay>();
            if (InventoryOverlay == null && CompareTag("Player"))
            {
                InventoryOverlay = gameObject.AddComponent<PlayerInventoryOverlay>();
                Debug.Log("[InventoryTrace] PlayerInventoryOverlay auto-added to player root.", this);
            }
            else if (InventoryOverlay == null)
            {
                Debug.LogWarning($"[InventoryTrace] InventoryOverlay not created because tag is '{tag}', expected 'Player'.", this);
            }
            InventoryOverlay?.Initialize(this);
            if (InventoryOverlay != null)
            {
                Debug.Log("[InventoryTrace] InventoryOverlay initialized.", this);
            }

            QuickHealOverlay = GetComponent<QuickHealOverlay>();
            if (QuickHealOverlay == null && CompareTag("Player"))
            {
                QuickHealOverlay = gameObject.AddComponent<QuickHealOverlay>();
            }
            QuickHealOverlay?.Initialize(this);

            UpperBodyCtrl = new UpperBodyController(this);
            FacialController = new FacialController(this);
            IkController = new IKController(this);
            ActionController = new ActionController(this);
            AudioController = new AudioController(this);

            ExtraActionController = new ExtraActionController(this, RuntimeData);

            // 替身格挡处理器（可选，优先同物体，其次回退到子物体）
            ParryHandler = GetComponent<ParryHandler>();
            if (ParryHandler == null)
                ParryHandler = GetComponentInChildren<ParryHandler>(true);

            // 装载状态字典 分配独立内存实例
            StateRegistry = new PlayerStateRegistry();
            if (Config != null && Config.LocomotionBrain != null)
            {
                StateRegistry.InitializeFromBrain(Config.LocomotionBrain, this);
            }
            else
            {
                Debug.LogError("[PlayerController] 致命错误：未配置 PlayerSO 或 Brain");
            }

            WireStateStoreCallbacksToDownstream();
        }

        private void Start()
        {
            // 非池化使用触发一次Boot
            BootIfNeeded();
            if (CompareTag("Player") && PlayerRespawnService.Instance != null)
            {
                PlayerRespawnService.Instance.TryPlaceAtInitialSpawn(this);
            }
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
                RuntimeData.StatusEffect.Clear();
                RuntimeData.ActionControl.Clear();
                RuntimeData.StatusControl.Clear();
                RuntimeData.CharacterControl.Clear();
                RuntimeData.IsTacticalStance = false;
                RuntimeData.CanEnterTacticalMotionBase = false;
            }
            StatusEffects?.Clear();
            _wasLocomotionBlocked = false;
            RootMotionHandledVerticalThisFrame = false;
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
                RuntimeData.IsInventoryOpen = false;
                RuntimeData.StatusEffect.Clear();
                RuntimeData.ActionControl.Clear();
                RuntimeData.StatusControl.Clear();
                RuntimeData.CharacterControl.Clear();
                RuntimeData.IsTacticalStance = false;
                RuntimeData.CanEnterTacticalMotionBase = false;
                RuntimeData.WantsLookAtIK = false;
                RuntimeData.ResetIntetnt();
            }
            StatusEffects?.Clear();
            _wasLocomotionBlocked = false;
            RootMotionHandledVerticalThisFrame = false;
        }

        private void OnEnable()
        {
            RegisterAsPlayerSingletonIfNeeded();
            RegisterExtraActionServiceIfNeeded();
            // 对象池激活时 Start 不一定每次都会走（取决于场景/脚本执行顺序） 这里作为兜底
            if (Application.isPlaying)
                BootIfNeeded();
        }

        private void OnDisable()
        {
            InventoryOverlay?.Close();
            UnregisterExtraActionServiceIfNeeded();
            if (ReferenceEquals(PlayerInstance, this))
            {
                PlayerInstance = null;
            }
        }

        private void RegisterAsPlayerSingletonIfNeeded()
        {
            if (CompareTag("Player"))
            {
                PlayerInstance = this;
            }
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

            InteractionSensor?.Tick();

            InventoryController.Update();

            MainProcessorPipeline.UpdateParameterProcessors();

            bool locomotionBlocked = CharacterArbiter != null && CharacterArbiter.IsLocomotionBlocked();
            if (!locomotionBlocked)
            {
                if (_wasLocomotionBlocked)
                {
                    RestoreLocomotionPresentation();
                }

                StateMachine.CurrentState.LogicUpdate();
            }
            else if (RuntimeData.IsDead)
            {
                // 死亡状态下仍然需要更新状态逻辑（等待复活倒计时）
                StateMachine.CurrentState.LogicUpdate();
            }

            _wasLocomotionBlocked = locomotionBlocked;

            UpperBodyCtrl.Update();

            FacialController.Update();

            ActionController.Update();

            AudioController.Update();

            ExtraActionController.Update();

            //古法状态调试 已经被 drawxxldebuger 代替 打包注释掉
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

            RootMotionHandledVerticalThisFrame = false;
            ConsumeFullBodyRootMotionIfNeeded();

            bool locomotionBlocked = CharacterArbiter != null && CharacterArbiter.IsLocomotionBlocked();
            if (locomotionBlocked)
            {
                bool applyGravityOnly = RuntimeData.StatusControl.IsActive ||
                                        (RuntimeData.ActionControl.IsActive && RuntimeData.Override.Request.ApplyGravity);

                if (applyGravityOnly)
                    MotionDriver.UpdateGravityOnly();
            }
            else
            {
                StateMachine.CurrentState?.PhysicsUpdate();
            }

            IkController.Update();

            ArbiterPipeline?.ProcessLateUpdateArbiters();

            RuntimeData.ResetIntetnt();
        }

        private void OnAnimatorMove()
        {
            if (!_booted || Animator == null || AnimFacade == null)
            {
                return;
            }

            if (!AnimFacade.IsFullBodyRootMotionEnabled)
            {
                return;
            }

            _capturedRootMotionDeltaPosition = Animator.deltaPosition;
            _capturedRootMotionDeltaRotation = Animator.deltaRotation;
            _capturedRootMotionFrame = Time.frameCount;
        }

        private void ConsumeFullBodyRootMotionIfNeeded()
        {
            if (AnimFacade == null || !AnimFacade.IsFullBodyRootMotionEnabled || Animator == null || MotionDriver == null)
            {
                return;
            }

            if (_capturedRootMotionFrame != Time.frameCount)
            {
                return;
            }

            Vector3 deltaPosition = _capturedRootMotionDeltaPosition;
            Vector3 filteredHorizontalDisplacement = Vector3.zero;
            bool hardStop = RuntimeData != null && RuntimeData.Override.IsActive && RuntimeData.Override.Request.HardStopOnBlock;
            if (deltaPosition.sqrMagnitude > 0.0000001f)
            {
                filteredHorizontalDisplacement = MotionDriver.ApplyRootMotionHorizontal(
                    new Vector3(deltaPosition.x, 0f, deltaPosition.z),
                    hardStop);
                if (filteredHorizontalDisplacement.sqrMagnitude > 0.0000001f)
                {
                    MotionDriver.RequestHorizontalDisplacement(filteredHorizontalDisplacement);
                }

            }

            Quaternion deltaRotation = _capturedRootMotionDeltaRotation;
            float deltaYaw = deltaRotation.eulerAngles.y;
            if (deltaYaw > 180f)
            {
                deltaYaw -= 360f;
            }

            if (Mathf.Abs(deltaYaw) > 0.001f)
            {
                MotionDriver.RequestYaw(transform.eulerAngles.y + deltaYaw);
            }

            TraceFullBodyRootMotion(deltaPosition, filteredHorizontalDisplacement, deltaYaw, hardStop);
        }

        public void ClearAttackRootMotionPlayback()
        {
            RuntimeData?.AttackRootMotionPlayback.Clear();
        }

        private void TraceFullBodyRootMotion(
            Vector3 deltaPosition,
            Vector3 filteredHorizontalDisplacement,
            float deltaYaw,
            bool hardStop)
        {
            if (!DebugFullBodyRootMotion || !Application.isPlaying)
            {
                return;
            }

            int frame = Time.frameCount;
            if (_lastRootMotionDebugFrame == frame || frame % 5 != 0)
            {
                return;
            }

            _lastRootMotionDebugFrame = frame;

            string clipName = "none";
            if (Animancer != null && Animancer.Layers.Count > 0)
            {
                var state = Animancer.Layers[0].CurrentState;
                var clip = state?.Clip != null ? state.Clip : state?.MainObject as AnimationClip;
                if (clip != null)
                {
                    clipName = clip.name;
                }
            }

            Vector3 rawHorizontal = new Vector3(deltaPosition.x, 0f, deltaPosition.z);
            bool horizontalSuppressed = rawHorizontal.sqrMagnitude > 0.0000001f &&
                                        filteredHorizontalDisplacement.sqrMagnitude <= 0.0000001f;

            Debug.Log(
                $"[FullBodyRootMotion] frame={frame} clip={clipName} active={AnimFacade.IsFullBodyRootMotionEnabled} " +
                $"rawDelta={deltaPosition:F4} rawXZ={rawHorizontal:F4} filteredXZ={filteredHorizontalDisplacement:F4} " +
                $"deltaYaw={deltaYaw:F3} hardStop={hardStop} blockedToZero={horizontalSuppressed} " +
                $"applyRootMotion={Animator.applyRootMotion}",
                this);
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || !DebugShowCurrentClipOverlay || Animancer == null)
                return;

            var camera = Camera.main;
            if (camera == null)
                return;

            Vector3 anchor = GetDebugClipOverlayAnchor();
            Vector3 screenPos = camera.WorldToScreenPoint(anchor);
            if (screenPos.z <= 0f)
            {
                if (!TryGetVisibleBoundsAnchor(camera, out anchor, out screenPos))
                    return;
            }

            string text = BuildDebugClipOverlayText();
            if (string.IsNullOrWhiteSpace(text))
                return;

            var style = GetDebugClipOverlayStyle();
            var panelStyle = GetDebugClipOverlayPanelStyle();
            var content = new GUIContent(text);
            Vector2 size = style.CalcSize(content);
            size.y = Mathf.Max(size.y, style.lineHeight * 6.2f);
            float clampedX = Mathf.Clamp(screenPos.x, size.x * 0.5f + 8f, Screen.width - size.x * 0.5f - 8f);
            float clampedY = Mathf.Clamp(Screen.height - screenPos.y - size.y, 8f, Screen.height - size.y - 8f);
            float x = clampedX - size.x * 0.5f;
            float y = clampedY;
            GUI.Box(new Rect(x - 6f, y - 4f, size.x + 12f, size.y + 8f), GUIContent.none, panelStyle);
            GUI.Label(new Rect(x, y, size.x, size.y), content, style);
            GUI.Label(new Rect(clampedX - 10f, y + size.y - 2f, 20f, 16f), "\u25BC", style);

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

        private void RestoreLocomotionPresentation()
        {
            if (StateMachine?.CurrentState == null)
                return;

            // 控制域释放时，layer0 仍可能停留在旧的 full-body action/status clip。
            // 这里强制重进当前 locomotion state，让基础姿态立刻重新接管表现层。
            StateMachine.ChangeState(StateMachine.CurrentState);
        }

        private Vector3 GetDebugClipOverlayAnchor()
        {
            float controllerHeight = CharController != null ? CharController.height : 2f;
            Vector3 bodyAnchor = transform.position + Vector3.up * (controllerHeight * 0.78f);

            if (HeadBone != null)
            {
                Vector3 headAnchor = Vector3.Lerp(bodyAnchor, HeadBone.position, 0.45f);
                return headAnchor + DebugClipOverlayWorldOffset;
            }

            return bodyAnchor + DebugClipOverlayWorldOffset;
        }

        private bool TryGetVisibleBoundsAnchor(Camera camera, out Vector3 anchor, out Vector3 screenPos)
        {
            anchor = transform.position + Vector3.up;
            screenPos = default;

            if (_debugOverlayRenderers == null || _debugOverlayRenderers.Length == 0)
                return false;

            bool hasBounds = false;
            Bounds combinedBounds = default;
            for (int i = 0; i < _debugOverlayRenderers.Length; i++)
            {
                var renderer = _debugOverlayRenderers[i];
                if (renderer == null || !renderer.enabled)
                    continue;

                if (!hasBounds)
                {
                    combinedBounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds)
                return false;

            anchor = combinedBounds.center + Vector3.up * (combinedBounds.extents.y + 0.1f) + DebugClipOverlayWorldOffset;
            screenPos = camera.WorldToScreenPoint(anchor);
            return screenPos.z > 0f;
        }

        private string BuildDebugClipOverlayText()
        {
            string fullBodyState = StateMachine?.CurrentState?.GetType().Name ?? "null";
            string upperBodyState = UpperBodyCtrl?.StateMachine?.CurrentState?.GetType().Name ?? "null";
            string layer0Clip = GetLayerClipLabel(0);
            string layer1Clip = GetLayerClipLabel(1);
            string controlDomain = RuntimeData != null ? RuntimeData.CharacterControl.ActiveDomain.ToString() : "Unknown";
            string locomotionState = RuntimeData != null ? RuntimeData.CurrentLocomotionState.ToString() : "Unknown";
            string tactical = RuntimeData != null ? RuntimeData.IsTacticalStance.ToString() : "Unknown";
            string tacticalReady = RuntimeData != null ? RuntimeData.CanEnterTacticalMotionBase.ToString() : "Unknown";
            string blend = RuntimeData != null ? $"{RuntimeData.CurrentAnimBlendX:0.00},{RuntimeData.CurrentAnimBlendY:0.00}" : "Unknown";

            return $"Full:{fullBodyState}\nUpper:{upperBodyState}\nOwner:{controlDomain}\nLoco:{locomotionState} Tactical:{tactical} Ready:{tacticalReady}\nBlend:{blend}\nL0:{layer0Clip}\nL1:{layer1Clip}";
        }

        private string GetLayerClipLabel(int layerIndex)
        {
            if (Animancer == null || Animancer.Layers == null)
                return "null";

            AnimancerState state = Animancer.Layers[layerIndex].CurrentState;
            if (state == null)
                return "null";

            var clip = state.Clip != null ? state.Clip : state.MainObject as AnimationClip;
            string clipName = clip != null ? clip.name : state.ToString();
            return $"{clipName} @ {state.NormalizedTime:0.00}";
        }

        private GUIStyle GetDebugClipOverlayStyle()
        {
            if (_debugClipOverlayStyle != null)
                return _debugClipOverlayStyle;

            _debugClipOverlayStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                normal =
                {
                    textColor = Color.white
                }
            };

            return _debugClipOverlayStyle;
        }

        private GUIStyle GetDebugClipOverlayPanelStyle()
        {
            if (_debugClipOverlayPanelStyle != null)
                return _debugClipOverlayPanelStyle;

            Texture2D background = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.72f));
            background.Apply();

            _debugClipOverlayPanelStyle = new GUIStyle(GUI.skin.box)
            {
                normal =
                {
                    background = background
                }
            };

            return _debugClipOverlayPanelStyle;
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
                var upperBodyMask = Config.Core != null ? Config.Core.UpperBodyMask : null;
                var facialMask = Config.Core != null ? Config.Core.FacialMask : null;

                if (upperBodyMask == null)
                {
                    _runtimeUpperBodyMask ??= CreateRuntimeUpperBodyMask();
                    upperBodyMask = _runtimeUpperBodyMask;
                }

                if (facialMask == null)
                {
                    _runtimeFacialMask ??= CreateRuntimeFacialMask();
                    facialMask = _runtimeFacialMask;
                }

                AnimFacade.SetLayerMask(1, upperBodyMask);
                AnimFacade.SetLayerMask(2, facialMask);
            }
        }

        private static AvatarMask CreateRuntimeUpperBodyMask()
        {
            var mask = new AvatarMask();
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            mask.name = "RuntimeUpperBodyMask";
            return mask;
        }

        private static AvatarMask CreateRuntimeFacialMask()
        {
            var mask = new AvatarMask();
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, false);
            mask.name = "RuntimeFacialMask";
            return mask;
        }

        private void InitializeEquipments()
        {
            InstantiateServices();
            InitializeMaxCoreState();
            InventoryController.Initialize();
            InitializeInventoryService();
            InitializeStateModifyService();
            InitializeEquipmentServiceDefaults();
            RestoreEquippedItemsFromService();
        }

        /// <summary>
        /// 根据 Inspector 中存储的类型全名，通过反射创建各服务实例
        /// Hub 先创建，各 Service 通过无参构造函数创建，然后通过 Initialize(IHub) 初始化
        /// </summary>
        private void InstantiateServices()
        {
            Hub = InstantiateHubByTypeName(HubTypeName);
            EquipmentService = InstantiateServiceByTypeName<IEquipmentService>(EquipmentServiceTypeName);
            InventoryService = InstantiateServiceByTypeName<IInventoryService>(InventoryServiceTypeName);
            StateStoreService = InstantiateServiceByTypeName<IStateStoreService>(StateStoreServiceTypeName);
            StateModifyService = InstantiateServiceByTypeName<IStateModifyService>(StateModifyServiceTypeName);
            ExtraActionService = InstantiateServiceByTypeName<IExtraActionService>(ExtraActionServiceTypeName);

            // 使用 IHubService.Initialize(IHub) 初始化服务
            (EquipmentService as IHubService)?.Initialize(Hub);
            (InventoryService as IHubService)?.Initialize(Hub);
            (StateStoreService as IHubService)?.Initialize(Hub);
            (StateModifyService as IHubService)?.Initialize(Hub);
            (ExtraActionService as IHubService)?.Initialize(Hub);
            RegisterExtraActionServiceIfNeeded();
        }

        private void RegisterExtraActionServiceIfNeeded()
        {
            if (!CompareTag("Player"))
            {
                return;
            }

            if (ExtraActionService != null)
            {
                ExtraActionServiceRegistry.Current = ExtraActionService;
            }
        }

        private void UnregisterExtraActionServiceIfNeeded()
        {
            if (ReferenceEquals(ExtraActionServiceRegistry.Current, ExtraActionService))
            {
                ExtraActionServiceRegistry.Current = null;
            }
        }

        private static IHub InstantiateHubByTypeName(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)) return null;
            var type = ResolveServiceType(typeName);
            if (type == null)
            {
                Debug.LogError($"[BBBCharacterController] Hub 类型找不到: {typeName}");
                return null;
            }
            try
            {
                return (IHub)Activator.CreateInstance(type);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BBBCharacterController] Hub 实例化失败 ({typeName}): {ex.Message}");
                return null;
            }
        }

        private static T InstantiateServiceByTypeName<T>(string typeName) where T : class
        {
            if (string.IsNullOrWhiteSpace(typeName)) return null;
            var type = ResolveServiceType(typeName);
            if (type == null)
            {
                Debug.LogError($"[BBBCharacterController] 服务类型找不到: {typeName}");
                return null;
            }
            try
            {
                // 使用无参构造函数创建服务实例
                return (T)Activator.CreateInstance(type);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BBBCharacterController] 服务实例化失败 ({typeName}): {ex.Message}");
                return null;
            }
        }

        private static Type ResolveServiceType(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var t = assembly.GetType(typeName);
                    if (t != null) return t;
                }
                catch { }
            }
            return null;
        }

        private void InitializeInventoryService()
        {
            if (InventoryService == null)
            {
                Debug.LogWarning("[BBBCharacterController] InventoryService 未配置，跳过背包初始化");
                return;
            }
            InventoryService.Initialize();
        }

        private void InitializeStateModifyService()
        {
            if (StateModifyService == null)
            {
                return;
            }

            StateModifyService.Initialize();
        }

        private void WireStateStoreCallbacksToDownstream()
        {
            OnDead -= HandleDeadFromStore;
            OnDead += HandleDeadFromStore;

            OnRevive -= HandleReviveFromStore;
            OnRevive += HandleReviveFromStore;
        }

        private void HandleDeadFromStore()
        {
            if (RuntimeData == null)
            {
                return;
            }

            RuntimeData.IsDead = true;
            RuntimeData.Arbitration.IsDead = true;
            RuntimeData.Arbitration.BlockInput = true;
            RuntimeData.Arbitration.BlockUpperBody = true;
            RuntimeData.Arbitration.BlockFacial = true;
            RuntimeData.Arbitration.BlockIK = true;
            RuntimeData.Arbitration.BlockInventory = true;
            StatusEffects?.Clear();

            if (!_booted || StateRegistry == null || StateMachine == null || StateMachine.CurrentState is PlayerDeathState)
            {
                return;
            }

            var death = StateRegistry.GetState<PlayerDeathState>();
            if (death != null)
            {
                StateMachine.ChangeState(death);
            }
        }

        private void HandleReviveFromStore()
        {
            if (RuntimeData == null)
            {
                return;
            }

            RuntimeData.IsDead = false;
            RuntimeData.Arbitration.Clear();

            if (!_booted || StateRegistry == null || StateMachine == null)
            {
                return;
            }

            var idle = StateRegistry.GetState<PlayerIdleState>();
            if (idle != null && StateMachine.CurrentState != idle)
            {
                StateMachine.ChangeState(idle);
            }
        }

        private void InitializeMaxCoreState()
        {
            if (StateStoreService == null)
            {
                Debug.LogWarning("[BBBCharacterController] StateStoreService 未配置，跳过状态初始化");
                return;
            }

            StateStoreService.SetProfile(StateProfile);
            StateStoreService.Initialize();
            StateStoreService.BindOnDead(() =>
            {
                OnDead?.Invoke();
            });
            StateStoreService.BindOnRevive(() =>
            {
                OnRevive?.Invoke();
            });
            StateStoreService.BindOnStateChanged((key, value) =>
            {
                OnStateChanged?.Invoke(key, value);
            });
        }

        private void InitializeEquipmentServiceDefaults()
        {
            if (EquipmentService == null)
            {
                Debug.LogWarning("[BBBCharacterController] EquipmentService 未配置，跳过装备初始化");
                return;
            }

            // 初始化 EquipmentService（确保 Pack 和目录结构存在）
            EquipmentService.Initialize();

            var registry = Config?.SlotRegistry;
            if (registry == null)
            {
                Debug.LogWarning("[BBBCharacterController] SlotRegistry 未配置，使用默认全部启用");
            }

            // 初始化配置槽位（只初始化启用的槽位）
            if (IsConfigSlotEnabled("config:weapon1")) SeedDebugConfigSlot(DebugMainhandEquipment1, "config:weapon1");
            if (IsConfigSlotEnabled("config:weapon2")) SeedDebugConfigSlot(DebugMainhandEquipment2, "config:weapon2");
            if (IsConfigSlotEnabled("config:weapon3")) SeedDebugConfigSlot(DebugMainhandEquipment3, "config:weapon3");
            if (IsConfigSlotEnabled("config:weapon4")) SeedDebugConfigSlot(DebugMainhandEquipment4, "config:weapon4");
            if (IsConfigSlotEnabled("config:weapon5")) SeedDebugConfigSlot(DebugMainhandEquipment5, "config:weapon5");
        }

        private bool IsConfigSlotEnabled(string key)
        {
            return Config?.SlotRegistry?.IsConfigSlotEnabled(key) ?? true; // 默认启用
        }

        private bool IsInstanceSlotEnabled(string key)
        {
            return Config?.SlotRegistry?.IsInstanceSlotEnabled(key) ?? true; // 默认启用
        }

        private void SeedDebugConfigSlot(EquippableItemSO equipment, string configSlotKey)
        {
            if (equipment == null) return;
            if (EquipmentService.HasEquipped(configSlotKey)) return;

            EquipmentService.TrySetEquipSO(configSlotKey, equipment);
        }

        private void RestoreEquippedItemsFromService()
        {
            if (EquipmentService == null)
            {
                Debug.LogWarning("[BBBCharacterController] EquipmentService 未配置，跳过后续装备恢复");
                return;
            }

            // 恢复主手装备（如果槽位启用）
            if (IsInstanceSlotEnabled("instance:mainhand"))
            {
                var itemSO = EquipmentService.GetEquippedSO("instance:mainhand");
                if (itemSO == null)
                {
                    // 尝试从启用的配置槽位找一个默认装备
                    for (int i = 1; i <= 5; i++)
                    {
                        var configKey = $"config:weapon{i}";
                        if (!IsConfigSlotEnabled(configKey)) continue;

                        var configSO = EquipmentService.GetEquippedSO(configKey);
                        if (configSO != null)
                        {
                            // 复制模式：配置槽位保留，实例槽位设置
                            EquipmentService.TrySetEquipSO("instance:mainhand", configSO);
                            itemSO = configSO;
                            break;
                        }
                    }
                }

                if (itemSO != null)
                {
                    // 直接通过 EquipmentDriver 装备
                    var instance = new ItemInstance(itemSO, null, 1);
                    EquipmentDriver.EquipItemToSlot(instance, EquipmentSlot.MainHand);
                    RuntimeData.CurrentItem = instance;

                    // 处理 VirtualOtherSlot 联动（开局恢复）
                    InventoryController?.HandleVirtualOtherSlotOnEquip(itemSO);
                }
            }

            // 恢复副手装备（如果槽位启用）
            if (IsInstanceSlotEnabled("instance:offhand"))
            {
                var offhandSO = EquipmentService.GetEquippedSO("instance:offhand");
                if (offhandSO != null)
                {
                    var instance = new ItemInstance(offhandSO, null, 1);
                    EquipmentDriver.EquipItemToSlot(instance, EquipmentSlot.OffHand);
                }
            }
        }

        private void BootUpStateMachines()
        {
            if (StateRegistry.InitialState != null) StateMachine.Initialize(StateRegistry.InitialState);
            if (UpperBodyCtrl.StateRegistry.InitialState != null) UpperBodyCtrl.StateMachine.Initialize(UpperBodyCtrl.StateRegistry.InitialState);
        }

        public MaxCoreStateData CreateDefaultMaxCoreStateData() => null;

        public void ApplyMaxCoreState(MaxCoreStateData data, bool refillCurrent = true) { }

        public void SetCurrentSanity(float sanity) { }

        public void SetSanityNormalized(float normalizedValue) { }

        public void AddSanityDelta(float delta) { }

        /// <summary>
        /// 直接将伤害加入 HealthArbiter 队列，跳过闭眼格挡拦截。
        /// 供 EyesClosedSystemManager 在格挡反应窗口超时时调用。
        /// </summary>
        public void EnqueueDamageDirectly(in DamageRequest request) { }

        public bool TryHeal(float amount) => false;

        public void SetMaxSanityValue(float maxSanity, bool refillCurrent = true) { }

        #region IDamageable 接口实现
        public bool RequestDamage(in DamageRequest request)
        {
            return false;
        }

        #endregion

        private bool TryInterceptByShield(in DamageRequest request)
        {
            // 远程攻击不允许在角色总入口被“代格挡”。
            // 只有真正命中盾牌碰撞体时，才由 ShieldBehaviour.RequestDamage 负责拦截。
            if (request.IsRanged || !request.UsesShieldBlockArc)
                return false;

            var equippedItems = EquipmentDriver?.AllEquippedItems;
            if (equippedItems == null)
                return false;

            for (int i = 0; i < equippedItems.Count; i++)
            {
                if (equippedItems[i] is ShieldBehaviour shield && shield.TryBlock(in request))
                    return true;
            }

            return false;
        }

        private bool ShouldIgnoreFriendlyFire(in DamageRequest request)
        {
            var attacker = request.ResolveAttackerController();
            if (attacker == null || attacker == this)
                return false;

            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer < 0)
                return false;

            return gameObject.layer == enemyLayer && attacker.gameObject.layer == enemyLayer;
        }

        private bool IsDuplicateIncomingDamage(in DamageRequest request)
        {
            if (_lastIncomingDamageFrame != Time.frameCount)
                return false;

            int attackerId = request.Attacker != null ? request.Attacker.GetInstanceID() : 0;
            int weaponId = request.WeaponTransform != null ? request.WeaponTransform.GetInstanceID() : 0;
            return _lastIncomingAttackerId == attackerId && _lastIncomingWeaponId == weaponId;
        }

        private void RememberIncomingDamage(in DamageRequest request)
        {
            _lastIncomingDamageFrame = Time.frameCount;
            _lastIncomingAttackerId = request.Attacker != null ? request.Attacker.GetInstanceID() : 0;
            _lastIncomingWeaponId = request.WeaponTransform != null ? request.WeaponTransform.GetInstanceID() : 0;
        }
    }
}
