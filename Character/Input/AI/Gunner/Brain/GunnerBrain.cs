using System;
using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 枪手 AI 大脑。行为模式类似黑魂射箭活尸：
    /// 在射程外逼近 → 进入射程后停下举枪瞄准 → 稳定后开火 → 冷却 → 循环。
    /// </summary>
    [Serializable]
    public class GunnerBrain : AITacticalBrainBase
    {
        private const float AimFacingAngle = 20f;
        private const float DefaultMinAccuracy = 0.33f;
        private const float DefaultMaxAccuracy = 1f;
        private const float DefaultMinAccuracyDistance = 5f;
        private const float DefaultMaxAccuracyDistance = 15f;

        private GunnerTacticalConfigSO _gunnerConfig;
        private BBBCharacterController _owner;

        // 状态机
        private GunnerPhase _phase;
        private float _phaseTimer;

        // 冷却
        private float _burstShotsRemaining;
        private float _fireCooldownTimer;

        private enum GunnerPhase
        {
            Approach,   // 逼近射程
            AimSettle,  // 举枪瞄准，等待稳定
            Firing,     // 连射/点射
            Recovery,   // 射击后冷却
        }

        public override void Initialize(Transform selfTransform, AITacticalBrainConfigSO config)
        {
            base.Initialize(selfTransform, config);
            _gunnerConfig = config as GunnerTacticalConfigSO;
            if (_gunnerConfig == null)
                Debug.LogWarning("[GunnerBrain] 配置类型应为 GunnerTacticalConfigSO，将使用内置默认值。");

            _phase = GunnerPhase.Approach;
            _owner = selfTransform.GetComponentInParent<BBBCharacterController>();
        }

        private static Vector3 ResolveTargetAimPoint(NavigatorSensorBase navigatorSensor)
        {
            var target = navigatorSensor != null ? navigatorSensor.Target : null;
            if (target == null)
                return Vector3.zero;

            var controller = target.GetComponent<BBBCharacterController>();
            if (controller != null)
            {
                if (controller.HeadBone != null)
                    return controller.HeadBone.position;

                return controller.transform.position + Vector3.up * 1.3f;
            }

            var collider = target.GetComponent<Collider>();
            if (collider != null)
                return collider.bounds.center;

            return target.position + Vector3.up * 1.3f;
        }

        protected override void ProcessTactics(in NavigationContext context)
        {
            if (_config == null)
            {
                _currentIntent = new TacticalIntent(Vector2.zero, Vector2.zero, false, false, false, false, false);
                return;
            }

            float dt = Time.deltaTime;
            if (_fireCooldownTimer > 0f) _fireCooldownTimer -= dt;
            if (_phaseTimer > 0f) _phaseTimer -= dt;

            var targetDir = context.DesiredWorldDirection;
            var distance = context.DistanceToTarget;
            var lookInput = CalculateLookInput(context.TargetWorldDirection);
            var facingAngle = Vector3.Angle(
                Vector3.ProjectOnPlane(_selfTransform.forward, Vector3.up),
                Vector3.ProjectOnPlane(context.TargetWorldDirection, Vector3.up));

            float firingRange = _config.AttackRange;
            float engagementRange = _config.EngagementRange;

            var wantsToAttack = false;
            var wantsToAim = false;
            var moveWorldDir = Vector3.zero;

            UpdateAiAimSolution(context);

            switch (_phase)
            {
                case GunnerPhase.Approach:
                    if (distance <= firingRange && facingAngle <= AimFacingAngle)
                    {
                        // 进入射程且面朝目标，切换到举枪
                        _phase = GunnerPhase.AimSettle;
                        _phaseTimer = GetAimSettleTime();
                    }
                    else if (distance <= firingRange)
                    {
                        // 在射程内但没面朝，缓慢逼近以驱动转向
                        moveWorldDir = targetDir;
                    }
                    else
                    {
                        moveWorldDir = targetDir;
                    }
                    break;

                case GunnerPhase.AimSettle:
                    wantsToAim = true;

                    // 如果目标跑出射程，回到逼近
                    if (distance > firingRange * 1.2f)
                    {
                        _phase = GunnerPhase.Approach;
                        break;
                    }

                    // 等瞄准稳定时间结束后开火
                    if (_phaseTimer <= 0f)
                    {
                        _phase = GunnerPhase.Firing;
                        _burstShotsRemaining = GetBurstCount();
                        _fireCooldownTimer = 0f;
                    }
                    break;

                case GunnerPhase.Firing:
                    wantsToAim = true;

                    // 目标跑远了，放弃射击
                    if (distance > firingRange * 1.3f)
                    {
                        _phase = GunnerPhase.Approach;
                        break;
                    }

                    if (_burstShotsRemaining > 0 && _fireCooldownTimer <= 0f)
                    {
                        wantsToAttack = true;
                        _burstShotsRemaining--;
                        _fireCooldownTimer = GetShotInterval();
                    }

                    // 连射完毕，进入冷却
                    if (_burstShotsRemaining <= 0 && _fireCooldownTimer <= 0f)
                    {
                        _phase = GunnerPhase.Recovery;
                        _phaseTimer = GetRecoveryDuration();
                    }
                    break;

                case GunnerPhase.Recovery:
                    // 冷却期间保持瞄准但不开火
                    wantsToAim = true;

                    // 目标靠太近，脱离瞄准转为逼近（可以被近战僵尸逻辑接管或保持退让）
                    if (distance > firingRange * 1.2f)
                    {
                        _phase = GunnerPhase.Approach;
                        break;
                    }

                    if (_phaseTimer <= 0f)
                    {
                        _phase = GunnerPhase.AimSettle;
                        _phaseTimer = GetAimSettleTime();
                    }
                    break;
            }

            var moveInput = moveWorldDir.sqrMagnitude > 0.0001f
                ? ConvertWorldDirToJoystick(moveWorldDir)
                : Vector2.zero;

            _currentIntent = new TacticalIntent(
                moveInput,
                lookInput,
                wantsToAttack,
                wantsToAim,
                false,
                false,
                false);
        }

        private float GetAimSettleTime()
        {
            return _gunnerConfig != null ? _gunnerConfig.AimSettleTime : 0.6f;
        }

        private int GetBurstCount()
        {
            if (_gunnerConfig != null)
                return UnityEngine.Random.Range(_gunnerConfig.BurstCountMin, _gunnerConfig.BurstCountMax + 1);
            return UnityEngine.Random.Range(1, 4);
        }

        private float GetShotInterval()
        {
            return _gunnerConfig != null ? _gunnerConfig.BurstShotInterval : 0.3f;
        }

        private float GetRecoveryDuration()
        {
            if (_gunnerConfig != null)
                return UnityEngine.Random.Range(_gunnerConfig.RecoveryDurationMin, _gunnerConfig.RecoveryDurationMax);
            return UnityEngine.Random.Range(1.5f, 3f);
        }

        private void UpdateAiAimSolution(in NavigationContext context)
        {
            if (_owner?.RuntimeData == null)
            {
                return;
            }

            if (!context.HasValidTarget)
            {
                _owner.RuntimeData.CurrentAIAccuracy = 0f;
                _owner.RuntimeData.AIAimTargetPoint = Vector3.zero;
                _owner.RuntimeData.IsAIAimStabilized = false;
                return;
            }

            var navigatorSensor = _owner.GetComponent<NavigatorSensorBase>();
            Vector3 targetPoint = ResolveTargetAimPoint(navigatorSensor);
            if (targetPoint == Vector3.zero)
            {
                _owner.RuntimeData.CurrentAIAccuracy = 0f;
                _owner.RuntimeData.AIAimTargetPoint = Vector3.zero;
                _owner.RuntimeData.IsAIAimStabilized = false;
                return;
            }

            float t = Mathf.InverseLerp(DefaultMinAccuracyDistance, DefaultMaxAccuracyDistance, context.DistanceToTarget);
            float accuracy = Mathf.Lerp(DefaultMaxAccuracy, DefaultMinAccuracy, t);
            _owner.RuntimeData.CurrentAIAccuracy = accuracy;
            _owner.RuntimeData.AIAimTargetPoint = targetPoint;
            _owner.RuntimeData.IsAIAimStabilized = accuracy >= 0.95f;
        }
    }
}
