using UnityEngine;

namespace BBBNexus
{
    [DisallowMultipleComponent]
    [System.Obsolete("Use AIBlackboardInputAdapter instead. This adapter writes AI intent into player raw-input semantics too naively and is kept only for backward compatibility.")]
    public class AICombatInputAdapter : InputSourceBase
    {
        [Header("AI Modules")]
        public NavigatorSensorBase _navigatorSensor;

        // 多态序列化纯 C# 接口
        [SubclassSelector]
        [SerializeReference]
        public IAITacticalBrain _brain;

        [Header("AI Configuration")]
        [Tooltip("AI 战术配置 - 所有 AI 行为参数都在这里")]
        public AITacticalBrainConfigSO TacticalConfig;

        private bool _lastAttackIntent;
        private bool _lastAimIntent;
        private bool _lastJumpIntent;
        private bool _lastDodgeIntent;
        private bool _lastRollIntent;
        private bool _lastReloadIntent;

        public NavigatorSensorBase NavigatorSensor => _navigatorSensor;
        public IAITacticalBrain Brain => _brain;

        protected override void Awake()
        {
            if (_navigatorSensor == null)
                _navigatorSensor = GetComponent<NavigatorSensorBase>();

            if (_brain == null)
            {
                Debug.LogError($"{_brain} 为空！请在 Inspector 中使用下拉菜单选择战术大脑！", this);
                enabled = false;
                return;
            }

            // 纯 C# 类不知道自己长在哪 必须由挂载点把 Transform 喂给它
            _brain.Initialize(this.transform, TacticalConfig);

            if (TacticalConfig != null)
                InjectConfigIfSupported(_brain, TacticalConfig);
        }

        private static void InjectConfigIfSupported(IAITacticalBrain brain, AITacticalBrainConfigSO config)
        {
            if (brain is MeleeRusherBrain melee)
            {
                var field = typeof(MeleeRusherBrain).GetField("_config",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (field == null)
                {
                    Debug.LogWarning("MeleeRusherBrain _config field missing");
                    return;
                }

                field.SetValue(melee, config);
            }
        }

        public override void FetchRawInput(ref RawInputData rawData)
        {
            if (_navigatorSensor == null || _brain == null)
            {
                ClearIntent(ref rawData);
                return;
            }

            ref readonly var context = ref _navigatorSensor.GetCurrentContext();
            ref readonly var intent = ref _brain.EvaluateTactics(in context);

            rawData.MoveAxis = intent.MovementInput;
            rawData.LookAxis = intent.LookInput;

            bool currentAimIntent = intent.WantsToAim;
            rawData.AimHeld = currentAimIntent;

            bool currentAttackIntent = intent.WantsToAttack;
            rawData.PrimaryAttackHeld = currentAttackIntent;
            // AI intent is semantic, not a hardware edge. Keep emitting press pulses while attack intent is active
            // so combo-capable behaviours can continue buffering follow-up attacks.
            rawData.PrimaryAttackJustPressed = currentAttackIntent;

            bool currentReloadIntent = TryPlanReload(in context, out int reloadTargetCount);
            rawData.ReloadJustPressed = currentReloadIntent && !_lastReloadIntent;
            var player = GetComponentInParent<BBBCharacterController>();
            if (rawData.ReloadJustPressed && player?.RuntimeData != null)
            {
                player.RuntimeData.RequestedReloadTargetCount = reloadTargetCount;
            }

            // --- 跳跃信号映射 ---
            bool currentJumpIntent = intent.WantsToJump;
            rawData.JumpHeld = currentJumpIntent;
            rawData.JumpJustPressed = currentJumpIntent && !_lastJumpIntent;

            // --- 闪避信号映射 ---
            bool currentDodgeIntent = intent.WantsToDodge;
            rawData.DodgeHeld = currentDodgeIntent;
            rawData.DodgeJustPressed = currentDodgeIntent && !_lastDodgeIntent;

            // --- 翻滚信号映射 ---
            bool currentRollIntent = intent.WantsToRoll;
            rawData.RollHeld = currentRollIntent;
            rawData.RollJustPressed = currentRollIntent && !_lastRollIntent;

            _lastAttackIntent = currentAttackIntent;
            _lastAimIntent = currentAimIntent;
            _lastJumpIntent = currentJumpIntent;
            _lastDodgeIntent = currentDodgeIntent;
            _lastRollIntent = currentRollIntent;
            _lastReloadIntent = currentReloadIntent;

            // 不需要手动清理，这些在意图管线中会被处理
        }

        private void ClearIntent(ref RawInputData rawData)
        {
            rawData.MoveAxis = Vector2.zero;
            rawData.LookAxis = Vector2.zero;
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
            _lastAttackIntent = false;
            _lastAimIntent = false;
            _lastJumpIntent = false;
            _lastDodgeIntent = false;
            _lastRollIntent = false;
            _lastReloadIntent = false;
        }

        private bool TryPlanReload(in NavigationContext context, out int targetCount)
        {
            targetCount = -1;
            var player = GetComponentInParent<BBBCharacterController>();
            if (player?.EquipmentDriver?.CurrentItemDirector is not IAiReloadable reloadable)
            {
                return false;
            }

            if (!(player.EquipmentDriver.CurrentItemDirector is IManualReloadable manualReloadable) ||
                !manualReloadable.CanManualReload ||
                reloadable.IsReloading ||
                reloadable.CurrentMagazine > 0)
            {
                return false;
            }

            float attackRange = TacticalConfig != null ? Mathf.Max(0.01f, TacticalConfig.AttackRange) : 10f;
            float distance01 = Mathf.Clamp01(context.DistanceToTarget / attackRange);
            float nearRatio = 0.4f;
            float farRatio = 1f;
            float variancePercent = 0.2f;
            if (TacticalConfig is GunnerTacticalConfigSO gunnerConfig)
            {
                nearRatio = gunnerConfig.ReloadNearRatio;
                farRatio = gunnerConfig.ReloadFarRatio;
                variancePercent = gunnerConfig.ReloadVariancePercent;
            }

            float baseRatio = Mathf.Lerp(nearRatio, farRatio, distance01);
            float variance = reloadable.MagazineCapacity * variancePercent;
            float target = reloadable.MagazineCapacity * baseRatio + UnityEngine.Random.Range(-variance, variance);
            targetCount = Mathf.Clamp(Mathf.RoundToInt(target), 1, reloadable.MagazineCapacity);
            return targetCount > 0;
        }
    }
}
