using UnityEngine;

namespace BBBNexus
{
    [DisallowMultipleComponent]
    public class AIBlackboardInputAdapter : InputSourceBase
    {
        [Header("AI Modules")]
        public NavigatorSensorBase NavigatorSensor;

        [SubclassSelector]
        [SerializeReference]
        public IAITacticalBrain Brain;

        [Header("AI Configuration")]
        [Tooltip("AI 战术配置。由 Brain 读取，但 adapter 也会用它辅助决定追击时的 locomotion 档位。")]
        public AITacticalBrainConfigSO TacticalConfig;

        [Header("Blackboard Translation")]
        [Tooltip("把目标朝向误差转成玩家 LookAxis 时的额外倍率。1 表示严格按 LookSensitivity 反推。")]
        public float LookAxisGain = 1f;

        [Tooltip("小于该角度时不再持续注入 LookAxis，避免在目标附近抖动。")]
        public float YawDeadZoneDegrees = 2f;

        [Tooltip("追击时是否自动按住 Sprint，使现有 locomotion 档位系统进入 Sprint。")]
        public bool AutoSprintWhenFar = false;

        [Tooltip("当目标距离大于 AttackRange * 该倍率时，若 AutoSprintWhenFar 打开，则输出 SprintHeld。")]
        public float SprintDistanceMultiplier = 2f;

        private BBBCharacterController _player;
        private MainProcessorPipeline _mainProcessor;
        private bool _lastJumpIntent;
        private bool _lastDodgeIntent;
        private bool _lastRollIntent;
        private bool _lastReloadIntent;

        protected override void Awake()
        {
            base.Awake();
            EnsureDependencies();
            InitializeBrain();
        }

        public void ConfigureBrain(IAITacticalBrain brain)
        {
            Brain = brain;
            InitializeBrain();
        }

        public void ConfigureTacticalConfig(AITacticalBrainConfigSO config)
        {
            TacticalConfig = config;
            InitializeBrain();
        }

        public void ConfigureTarget(Transform target)
        {
            EnsureDependencies();
            if (NavigatorSensor != null)
            {
                NavigatorSensor.Target = target;
            }
        }

        private void EnsureDependencies()
        {
            if (NavigatorSensor == null)
                NavigatorSensor = GetComponent<NavigatorSensorBase>();

            _player = GetComponentInParent<BBBCharacterController>();
            _mainProcessor = _player?.MainProcessorPipeline;
        }

        private void InitializeBrain()
        {
            EnsureDependencies();

            if (Brain == null)
            {
                Debug.LogError("AI brain is null. Please assign a tactical brain in the inspector.", this);
                enabled = false;
                return;
            }

            enabled = true;
            Brain.Initialize(transform, TacticalConfig);
        }

        public override void FetchRawInput(ref RawInputData rawData)
        {
            if (NavigatorSensor == null || Brain == null)
            {
                ClearIntent(ref rawData);
                return;
            }

            ref readonly var context = ref NavigatorSensor.GetCurrentContext();
            ref readonly var intent = ref Brain.EvaluateTactics(in context);

            float currentYaw = GetAuthorityYaw();
            Vector3 facingWorldDir = ResolveFacingWorldDirection(in context);
            rawData.LookAxis = BuildLookAxis(facingWorldDir, currentYaw);

            float predictedYaw = currentYaw + rawData.LookAxis.x * GetLookSensitivityX();
            Vector3 moveWorldDir = ResolveMoveWorldDirection(in context, in intent, predictedYaw);
            rawData.MoveAxis = BuildMoveAxis(moveWorldDir, predictedYaw);

            rawData.WalkHeld = false;
            rawData.SprintHeld = ShouldSprint(in context, rawData.MoveAxis);

            rawData.AimHeld = intent.WantsToAim;

            bool wantsAttack = intent.WantsToAttack;
            rawData.PrimaryAttackHeld = wantsAttack;
            rawData.PrimaryAttackJustPressed = wantsAttack;

            bool wantsReload = TryPlanReload(in context, out int reloadTargetCount);
            rawData.ReloadJustPressed = wantsReload && !_lastReloadIntent;
            if (rawData.ReloadJustPressed && _player?.RuntimeData != null)
            {
                _player.RuntimeData.RequestedReloadTargetCount = reloadTargetCount;
            }

            bool wantsJump = intent.WantsToJump;
            rawData.JumpHeld = wantsJump;
            rawData.JumpJustPressed = wantsJump && !_lastJumpIntent;

            bool wantsDodge = intent.WantsToDodge;
            rawData.DodgeHeld = wantsDodge;
            rawData.DodgeJustPressed = wantsDodge && !_lastDodgeIntent;

            bool wantsRoll = intent.WantsToRoll;
            rawData.RollHeld = wantsRoll;
            rawData.RollJustPressed = wantsRoll && !_lastRollIntent;

            _lastJumpIntent = wantsJump;
            _lastDodgeIntent = wantsDodge;
            _lastRollIntent = wantsRoll;
            _lastReloadIntent = wantsReload;
        }

        private bool TryPlanReload(in NavigationContext context, out int targetCount)
        {
            targetCount = -1;
            if (_player?.EquipmentDriver?.CurrentItemDirector is not IAiReloadable reloadable)
            {
                return false;
            }

            if (!(_player.EquipmentDriver.CurrentItemDirector is IManualReloadable manualReloadable) ||
                !manualReloadable.CanManualReload ||
                reloadable.IsReloading ||
                reloadable.CurrentMagazine > 0)
            {
                return false;
            }

            targetCount = CalculateReloadTargetCount(reloadable.MagazineCapacity, context.DistanceToTarget);
            return targetCount > 0;
        }

        private int CalculateReloadTargetCount(int magazineCapacity, float distanceToTarget)
        {
            if (magazineCapacity <= 0)
            {
                return -1;
            }

            // 使用默认换弹参数喵~
            float attackRange = TacticalConfig != null ? Mathf.Max(0.01f, TacticalConfig.AttackRange) : 10f;
            float distance01 = Mathf.Clamp01(distanceToTarget / attackRange);
            float nearRatio = 0.4f;
            float farRatio = 1f;
            float variancePercent = 0.2f;

            float baseRatio = Mathf.Lerp(nearRatio, farRatio, distance01);
            float variance = magazineCapacity * variancePercent;
            float target = magazineCapacity * baseRatio + UnityEngine.Random.Range(-variance, variance);
            return Mathf.Clamp(Mathf.RoundToInt(target), 1, magazineCapacity);
        }

        private float GetAuthorityYaw()
        {
            if (_runtimeData != null)
            {
                if (!float.IsNaN(_runtimeData.AuthorityYaw))
                    return _runtimeData.AuthorityYaw;
            }

            return transform.eulerAngles.y;
        }

        private float GetLookSensitivityX()
        {
            if (_player != null && _player.Config != null)
                return Mathf.Max(1f, _player.Config.Core.LookSensitivity.x);

            return 150f;
        }

        private Vector3 ResolveFacingWorldDirection(in NavigationContext context)
        {
            var facing = Vector3.ProjectOnPlane(context.TargetWorldDirection, Vector3.up);
            if (facing.sqrMagnitude > 0.0001f)
                return facing.normalized;

            facing = Vector3.ProjectOnPlane(context.DesiredWorldDirection, Vector3.up);
            return facing.sqrMagnitude > 0.0001f ? facing.normalized : Vector3.zero;
        }

        private Vector2 BuildLookAxis(Vector3 desiredFacingWorldDir, float currentYaw)
        {
            if (desiredFacingWorldDir.sqrMagnitude < 0.0001f)
                return Vector2.zero;

            Vector3 currentForward = Quaternion.Euler(0f, currentYaw, 0f) * Vector3.forward;
            float yawAngle = Vector3.SignedAngle(currentForward, desiredFacingWorldDir, Vector3.up);
            if (Mathf.Abs(yawAngle) <= YawDeadZoneDegrees)
                return Vector2.zero;

            float yawInput = Mathf.Clamp((yawAngle / GetLookSensitivityX()) * LookAxisGain, -1f, 1f);
            return new Vector2(yawInput, 0f);
        }

        private Vector3 ResolveMoveWorldDirection(in NavigationContext context, in TacticalIntent intent, float predictedYaw)
        {
            if (intent.MovementInput.sqrMagnitude <= 0.0001f)
                return Vector3.zero;

            Vector3 desired = Vector3.ProjectOnPlane(context.DesiredWorldDirection, Vector3.up);
            if (desired.sqrMagnitude > 0.0001f)
                return desired.normalized;

            Quaternion yawRot = Quaternion.Euler(0f, predictedYaw, 0f);
            Vector3 fallback = yawRot * new Vector3(intent.MovementInput.x, 0f, intent.MovementInput.y);
            fallback.y = 0f;
            return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.zero;
        }

        private static Vector2 BuildMoveAxis(Vector3 desiredWorldDir, float predictedYaw)
        {
            if (desiredWorldDir.sqrMagnitude < 0.0001f)
                return Vector2.zero;

            Quaternion yawRot = Quaternion.Euler(0f, predictedYaw, 0f);
            Vector3 localDir = Quaternion.Inverse(yawRot) * desiredWorldDir.normalized;
            return new Vector2(localDir.x, localDir.z).normalized;
        }

        private bool ShouldSprint(in NavigationContext context, Vector2 moveAxis)
        {
            if (!AutoSprintWhenFar || TacticalConfig == null)
                return false;

            if (moveAxis.sqrMagnitude <= 0.0001f)
                return false;

            return context.DistanceToTarget > TacticalConfig.AttackRange * SprintDistanceMultiplier;
        }

        private void ClearIntent(ref RawInputData rawData)
        {
            rawData.MoveAxis = Vector2.zero;
            rawData.LookAxis = Vector2.zero;
            rawData.WalkHeld = false;
            rawData.SprintHeld = false;
            rawData.AimHeld = false;
            rawData.PrimaryAttackHeld = false;
            rawData.PrimaryAttackJustPressed = false;
            rawData.ReloadJustPressed = false;
            rawData.JumpHeld = false;
            rawData.JumpJustPressed = false;
            rawData.DodgeHeld = false;
            rawData.DodgeJustPressed = false;
            rawData.RollHeld = false;
            rawData.RollJustPressed = false;
            _lastJumpIntent = false;
            _lastDodgeIntent = false;
            _lastRollIntent = false;
            _lastReloadIntent = false;
        }
    }
}
