using System;
using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 盾哥 AI 大脑。行为模式：防御压制型。
    /// 逼近 → 保压区举盾等待 → 伺机出击 → 收盾冷却 → 循环。
    ///
    /// WantsToAim 复用为"举盾姿态"信号，Attack 相期间落下。
    /// 盾牌碰撞体是被动的，格挡判定不依赖此信号。
    /// </summary>
    [Serializable]
    public class ShielderBrain : AITacticalBrainBase
    {
        private enum ShielderPhase
        {
            Approach,   // 逼近接战范围
            Pressure,   // 保压区：举盾缓进，等待出手时机
            Attack,     // 出击：落盾挥砍
            Recovery,   // 收招：举盾冷却
        }

        private ShielderTacticalConfigSO _shielderConfig;

        private ShielderPhase _phase;
        private float _pressureTimer;
        private float _attackCommitTimer;
        private float _recoveryTimer;

        // 内置默认值
        private const float DefaultWindupMin     = 1.0f;
        private const float DefaultWindupMax     = 2.5f;
        private const float DefaultCommitTime    = 0.6f;
        private const float DefaultRecoveryMin   = 1.2f;
        private const float DefaultRecoveryMax   = 2.0f;
        private const float DefaultFacingAngle   = 25f;

        public override void Initialize(Transform selfTransform, AITacticalBrainConfigSO config)
        {
            base.Initialize(selfTransform, config);
            _shielderConfig = config as ShielderTacticalConfigSO;
            if (_shielderConfig == null)
                Debug.LogWarning("[ShielderBrain] 配置类型应为 ShielderTacticalConfigSO，将使用内置默认值。");

            _phase = ShielderPhase.Approach;
        }

        protected override void ProcessTactics(in NavigationContext context)
        {
            if (_config == null)
            {
                _currentIntent = new TacticalIntent(Vector2.zero, Vector2.zero, false, false, false, false, false);
                return;
            }

            float dt = Time.deltaTime;
            if (_pressureTimer > 0f)    _pressureTimer -= dt;
            if (_attackCommitTimer > 0f) _attackCommitTimer -= dt;
            if (_recoveryTimer > 0f)    _recoveryTimer -= dt;

            var targetDir    = context.DesiredWorldDirection;
            var distance     = context.DistanceToTarget;
            var facingAngle  = Vector3.Angle(
                Vector3.ProjectOnPlane(_selfTransform.forward, Vector3.up),
                Vector3.ProjectOnPlane(context.TargetWorldDirection, Vector3.up));

            var wantsToAttack = false;
            var wantsToAim   = true;   // 举盾默认开，Attack 相覆写为 false
            var moveWorldDir = Vector3.zero;

            switch (_phase)
            {
                case ShielderPhase.Approach:
                    moveWorldDir = targetDir;
                    if (distance <= _config.EngagementRange)
                    {
                        _phase = ShielderPhase.Pressure;
                        _pressureTimer = GetAttackWindup();
                    }
                    break;

                case ShielderPhase.Pressure:
                    // 缓慢逼近以保持朝向驱动（沿用 ZombieBrain 的 FreeLook 经验）
                    moveWorldDir = targetDir;

                    if (distance > _config.EngagementRange)
                    {
                        _phase = ShielderPhase.Approach;
                        break;
                    }

                    if (_pressureTimer <= 0f
                        && distance <= _config.AttackRange
                        && facingAngle < GetFacingAngle())
                    {
                        _phase = ShielderPhase.Attack;
                        _attackCommitTimer = GetAttackCommitTime();
                    }
                    break;

                case ShielderPhase.Attack:
                    wantsToAim   = false;
                    wantsToAttack = true;
                    moveWorldDir  = Vector3.zero;

                    if (_attackCommitTimer <= 0f)
                    {
                        _phase = ShielderPhase.Recovery;
                        _recoveryTimer = GetRecoveryDuration();
                    }
                    break;

                case ShielderPhase.Recovery:
                    moveWorldDir = Vector3.zero;

                    if (_recoveryTimer <= 0f)
                    {
                        if (distance <= _config.EngagementRange)
                        {
                            _phase = ShielderPhase.Pressure;
                            _pressureTimer = GetAttackWindup();
                        }
                        else
                        {
                            _phase = ShielderPhase.Approach;
                        }
                    }
                    break;
            }

            var moveInput = moveWorldDir != Vector3.zero ? Vector2.one : Vector2.zero;

            _currentIntent = new TacticalIntent(
                moveInput,
                Vector2.zero,
                wantsToAttack,
                wantsToAim,
                false,
                false,
                false);
        }

        private float GetAttackWindup()
        {
            float min = _shielderConfig != null ? _shielderConfig.AttackWindupMin : DefaultWindupMin;
            float max = _shielderConfig != null ? _shielderConfig.AttackWindupMax : DefaultWindupMax;
            return UnityEngine.Random.Range(min, max);
        }

        private float GetAttackCommitTime()
        {
            return _shielderConfig != null ? _shielderConfig.AttackCommitTime : DefaultCommitTime;
        }

        private float GetRecoveryDuration()
        {
            float min = _shielderConfig != null ? _shielderConfig.RecoveryDurationMin : DefaultRecoveryMin;
            float max = _shielderConfig != null ? _shielderConfig.RecoveryDurationMax : DefaultRecoveryMax;
            return UnityEngine.Random.Range(min, max);
        }

        private float GetFacingAngle()
        {
            return _shielderConfig != null ? _shielderConfig.AttackFacingAngle : DefaultFacingAngle;
        }
    }
}
