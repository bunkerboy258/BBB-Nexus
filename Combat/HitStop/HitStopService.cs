using UnityEngine;

namespace BBBNexus
{
    public enum HitStopKind
    {
        Light,
        Medium,
        Heavy,
        PerfectParry,
    }

    public readonly struct HitStopRequest
    {
        public readonly BBBCharacterController Target;
        public readonly HitStopKind Kind;
        public readonly BBBCharacterController Source;
        public readonly float DurationSeconds;
        public readonly bool HasCustomDuration;

        public HitStopRequest(BBBCharacterController target, HitStopKind kind, BBBCharacterController source = null)
        {
            Target = target;
            Kind = kind;
            Source = source;
            DurationSeconds = 0f;
            HasCustomDuration = false;
        }

        public HitStopRequest(BBBCharacterController target, float durationSeconds, BBBCharacterController source = null)
        {
            Target = target;
            Kind = HitStopKind.Light;
            Source = source;
            DurationSeconds = durationSeconds;
            HasCustomDuration = true;
        }
    }

    /// <summary>
    /// 卡肉服务：只负责把运行时生成的空 HitStop 状态塞进目标角色的 StatusEffect。
    /// 可选挂到场景对象上，用于开启 DebugLog。
    /// </summary>
    public sealed class HitStopService : SingletonMono<HitStopService>
    {
        [SerializeField] private bool _enabled = false;
        [SerializeField] private bool _debugLog = false;

        private static StatusEffectSO _light;
        private static StatusEffectSO _medium;
        private static StatusEffectSO _heavy;
        private static StatusEffectSO _perfectParry;
        private static StatusEffectSO _customDuration;

        protected override void Awake()
        {
            base.Awake();
        }

        public void Request(in HitStopRequest request)
        {
            if (!_enabled)
                return;

            if (request.Target == null || request.Target.RuntimeData == null || request.Target.RuntimeData.IsDead)
                return;

            var effect = request.HasCustomDuration
                ? ResolveCustomDurationEffect(request.DurationSeconds)
                : ResolveEffect(request.Kind);
            request.Target.StatusEffects?.Apply(effect);

            if (_debugLog)
            {
                Debug.Log(
                    $"[HitStop] kind={request.Kind} target={request.Target.name} source={request.Source?.name ?? "null"} " +
                    $"effect={effect.DisplayName} duration={effect.Duration:F3} animSpeed={effect.HitStopAnimationSpeed:F3}",
                    request.Target);
            }
        }

        private static StatusEffectSO ResolveEffect(HitStopKind kind)
        {
            EnsureInitialized();
            return kind switch
            {
                HitStopKind.Medium => _medium,
                HitStopKind.Heavy => _heavy,
                HitStopKind.PerfectParry => _perfectParry,
                _ => _light,
            };
        }

        private static void EnsureInitialized()
        {
            _light ??= CreateEffect("HitStop_Light", 0.035f, 5, 0.05f);
            _medium ??= CreateEffect("HitStop_Medium", 0.05f, 8, 0.03f);
            _heavy ??= CreateEffect("HitStop_Heavy", 0.065f, 12, 0.02f);
            _perfectParry ??= CreateEffect("HitStop_PerfectParry", 0.08f, 18, 0f);
            _customDuration ??= CreateEffect("HitStop_Custom", 0.035f, 5, 0.05f);
        }

        private static StatusEffectSO ResolveCustomDurationEffect(float durationSeconds)
        {
            EnsureInitialized();
            _customDuration.Duration = Mathf.Clamp(durationSeconds, 0f, 0.2f);
            return _customDuration;
        }

        private static StatusEffectSO CreateEffect(string displayName, float duration, int priority, float animationSpeed)
        {
            var effect = ScriptableObject.CreateInstance<StatusEffectSO>();
            effect.name = displayName;
            effect.DisplayName = displayName;
            effect.Duration = duration;
            effect.CanBeRefreshed = true;
            effect.Priority = priority;
            effect.InterruptMode = StatusInterruptMode.None;
            effect.BlockInput = false;
            effect.BlockAction = false;
            effect.IsHitStop = true;
            effect.HitStopAnimationSpeed = animationSpeed;
            effect.FreezeMotion = true;
            return effect;
        }
    }
}
