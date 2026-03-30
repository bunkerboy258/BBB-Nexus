using Animancer;
using System.Collections.Generic;
using UnityEngine;
using NekoGraph;

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
        public EquippableItemSO DebugOffhandEquipment;
        public bool UseLocalEquipmentBackend = false;
        [HideInInspector]
        [System.Obsolete("Use DebugMainhandEquipment1~5/DebugOffhandEquipment instead.")]
        public EquippableItemSO DefaultEquipment1;
        [HideInInspector]
        [System.Obsolete("Use DebugMainhandEquipment1~5/DebugOffhandEquipment instead.")]
        public EquippableItemSO DefaultEquipment2;
        [HideInInspector]
        [System.Obsolete("Use DebugMainhandEquipment1~5/DebugOffhandEquipment instead.")]
        public EquippableItemSO DefaultEquipment3;
        public bool statedebug = false;
        public bool DebugShowCurrentClipOverlay = false;
        public Vector3 DebugClipOverlayWorldOffset = new Vector3(0f, 0.35f, 0f);


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
        public ExtraActionController ExtraActionController { get; private set; }

        // 驱动器与外观层系统
        public AnimancerComponent Animancer { get; private set; }
        public CharacterController CharController { get; private set; }
        public MotionDriver MotionDriver { get; private set; }
        public EquipmentDriver EquipmentDriver { get; private set; }
        public AnimationFacadeBase AnimFacade { get; private set; }
        public AudioDriver AudioDriver { get; private set; }
        public LocalGraphHub LocalGraphHub { get; private set; }
        public Dictionary<string, BasePackData> LocalPackDataDict { get; private set; }

        // 状态注册表
        public PlayerStateRegistry StateRegistry { get; private set; }

        //仲裁器(后期需要注册表化)
        public LODArbiter LodArbiter { get; private set; }
        public ArbiterPipeline ArbiterPipeline { get; private set; }

        /// <summary>异常状态仲裁器快捷访问</summary>
        public StatusEffectArbiter StatusEffects => ArbiterPipeline.StatusEffect;
        public CharacterArbiter CharacterArbiter => ArbiterPipeline.Character;

        //调试用缓存
        private PlayerBaseState _lastState;
        public event System.Action OnEquipmentChanged;

        private bool _booted;
        private bool _wasLocomotionBlocked;
        private GUIStyle _debugClipOverlayStyle;

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
            HeadBone=Animator.GetBoneTransform(HumanBodyBones.Head);

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

            LocalGraphHub = new LocalGraphHub(GraphInstanceSlot.System);
            LocalPackDataDict = new Dictionary<string, BasePackData>();
            LocalGraphHub.SetPackDataDict(LocalPackDataDict);

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

            // 额外动作控制器（需要 EyesClosedSystemManager 引用）
            var eyesClosedManager = FindObjectOfType<EyesClosedSystemManager>();
            ExtraActionController = new ExtraActionController(RuntimeData, eyesClosedManager);

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
                RuntimeData.StatusEffect.Clear();
                RuntimeData.ActionControl.Clear();
                RuntimeData.StatusControl.Clear();
                RuntimeData.CharacterControl.Clear();
            }
            StatusEffects?.Clear();
            _wasLocomotionBlocked = false;
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
                RuntimeData.StatusEffect.Clear();
                RuntimeData.ActionControl.Clear();
                RuntimeData.StatusControl.Clear();
                RuntimeData.CharacterControl.Clear();
                RuntimeData.WantsLookAtIK = false;
                RuntimeData.ResetIntetnt();
            }
            StatusEffects?.Clear();
            _wasLocomotionBlocked = false;
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

            LocalGraphHub?.Tick();

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

        private void OnGUI()
        {
            if (!Application.isPlaying || !DebugShowCurrentClipOverlay || Animancer == null)
                return;

            var cameraTransform = PlayerCamera != null ? PlayerCamera : Camera.main != null ? Camera.main.transform : null;
            var camera = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : Camera.main;
            if (camera == null)
                return;

            Vector3 anchor = GetDebugClipOverlayAnchor();
            Vector3 screenPos = camera.WorldToScreenPoint(anchor);
            if (screenPos.z <= 0f)
                return;

            string text = BuildDebugClipOverlayText();
            if (string.IsNullOrWhiteSpace(text))
                return;

            var style = GetDebugClipOverlayStyle();
            var content = new GUIContent(text);
            Vector2 size = style.CalcSize(content);
            float x = screenPos.x - size.x * 0.5f;
            float y = Screen.height - screenPos.y - size.y;
            GUI.Label(new Rect(x, y, size.x, size.y), content, style);
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
            if (HeadBone != null)
                return HeadBone.position + DebugClipOverlayWorldOffset;

            return transform.position + Vector3.up * (CharController != null ? CharController.height + 0.2f : 2f) + DebugClipOverlayWorldOffset;
        }

        private string BuildDebugClipOverlayText()
        {
            string fullBodyState = StateMachine?.CurrentState?.GetType().Name ?? "null";
            string upperBodyState = UpperBodyCtrl?.StateMachine?.CurrentState?.GetType().Name ?? "null";
            string layer0Clip = GetLayerClipLabel(0);
            string layer1Clip = GetLayerClipLabel(1);
            string controlDomain = RuntimeData != null ? RuntimeData.CharacterControl.ActiveDomain.ToString() : "Unknown";

            return $"Full:{fullBodyState}\nUpper:{upperBodyState}\nOwner:{controlDomain}\nL0:{layer0Clip}\nL1:{layer1Clip}";
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
            InitializeEquipmentPackDefaults();
            RestoreEquippedItemsFromPack();
        }

        private void InitializeEquipmentPackDefaults()
        {
            EquipmentPackVfs.EnsureLayout(this);
            SeedDebugMainSlotIfMissing(DebugMainhandEquipment1, 1);
            SeedDebugMainSlotIfMissing(DebugMainhandEquipment2, 2);
            SeedDebugMainSlotIfMissing(DebugMainhandEquipment3, 3);
            SeedDebugMainSlotIfMissing(DebugMainhandEquipment4, 4);
            SeedDebugMainSlotIfMissing(DebugMainhandEquipment5, 5);
            SeedDebugEquipmentIfMissing(DebugOffhandEquipment, EquipmentSlot.OffHand);
        }

        private void SeedDebugMainSlotIfMissing(EquippableItemSO equipment, int mainSlotIndex)
        {
            if (equipment == null)
            {
                return;
            }

            if (EquipmentPackVfs.TryGetMainSlotItemId(mainSlotIndex, out _, this))
            {
                return;
            }

            EquipmentPackVfs.SetMainSlotItem(mainSlotIndex, equipment.name, this);
        }

        private void SeedDebugEquipmentIfMissing(EquippableItemSO equipment, EquipmentSlot slot)
        {
            if (equipment == null)
            {
                return;
            }

            if (EquipmentPackVfs.TryGetOtherSlotItemId(slot, out _, this))
            {
                return;
            }

            string itemId = equipment.name;
            EquipmentPackVfs.SetOtherSlot(slot, itemId, this);
        }

        private void RestoreEquippedItemsFromPack()
        {
            if (!EquipmentPackVfs.TryGetOtherSlotItemId(EquipmentSlot.MainHand, out var mainhandItemId, this))
            {
                if (TryGetFirstAvailableMainSlotIndex(out var defaultMainSlotIndex) &&
                    EquipmentPackVfs.SwapMainHandWithMainSlot(defaultMainSlotIndex, this) &&
                    EquipmentPackVfs.TryGetOtherSlotItemId(EquipmentSlot.MainHand, out var swappedMainhandItemId, this))
                {
                    mainhandItemId = swappedMainhandItemId;
                }
            }

            if (!string.IsNullOrWhiteSpace(mainhandItemId))
            {
                EquipmentManager.EquipById(this, mainhandItemId, EquipmentSlot.MainHand);
            }

            if (EquipmentPackVfs.TryGetOtherSlotItemId(EquipmentSlot.OffHand, out var offhandItemId, this))
            {
                EquipmentManager.EquipById(this, offhandItemId, EquipmentSlot.OffHand);
            }
        }

        private bool TryGetFirstAvailableMainSlotIndex(out int index)
        {
            index = -1;
            for (int i = 1; i <= 5; i++)
            {
                if (EquipmentPackVfs.TryGetMainSlotItemId(i, out var itemId, this) &&
                    !string.IsNullOrWhiteSpace(itemId) &&
                    !string.Equals(itemId, EquipmentPackVfs.MainSlotOccupierId, System.StringComparison.Ordinal))
                {
                    index = i;
                    return true;
                }
            }

            return false;
        }

        private void BootUpStateMachines()
        {
            if (StateRegistry.InitialState != null) StateMachine.Initialize(StateRegistry.InitialState);
            if (UpperBodyCtrl.StateRegistry.InitialState != null) UpperBodyCtrl.StateMachine.Initialize(UpperBodyCtrl.StateRegistry.InitialState);
        }

        public void EnsureLocalEquipmentPackReady()
        {
            if (!UseLocalEquipmentBackend)
            {
                return;
            }

            LocalGraphHub ??= new LocalGraphHub(GraphInstanceSlot.System);
            LocalPackDataDict ??= new Dictionary<string, BasePackData>();
            LocalGraphHub.SetPackDataDict(LocalPackDataDict);

            foreach (var pair in LocalPackDataDict)
            {
                if (pair.Value != null && pair.Value.PackID == EquipmentPackVfs.EquipmentPackId)
                {
                    return;
                }
            }

            var pack = MetaLib.GetPack<BasePackData>(EquipmentPackVfs.EquipmentPackId);
            if (pack == null)
            {
                throw new System.InvalidOperationException($"Failed to create local pack '{EquipmentPackVfs.EquipmentPackId}'.");
            }

            LocalGraphHub.Analyser.LoadVFSFromPack(pack);
            LocalGraphHub.Analyser.RebuildIndex();
            LocalGraphHub.Runner.OnPackDataDictLoaded(LocalPackDataDict);
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
