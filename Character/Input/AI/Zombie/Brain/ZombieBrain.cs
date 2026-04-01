using System;
using UnityEngine;

namespace BBBNexus
{
    [Serializable]
    public class ZombieBrain : AITacticalBrainBase
    {
        private const float AttackFacingAngle = 35f;

        // 无配置时的内置默认值
        private const float DefaultPerHitCommitTime = 0.7f;
        private const int DefaultComboMin = 1;
        private const int DefaultComboMax = 3;

        private float _attackCooldownTimer;
        private float _comboCommitTimer;
        private int _comboHitsRemaining;
        private ZombieTacticalConfigSO _zombieConfig;

        public override void Initialize(Transform selfTransform, AITacticalBrainConfigSO config)
        {
            base.Initialize(selfTransform, config);
            _zombieConfig = config as ZombieTacticalConfigSO;
            if (_zombieConfig == null)
                Debug.LogWarning("[ZombieBrain] 配置类型应为 ZombieTacticalConfigSO，将使用内置默认值。");
        }

        protected override void ProcessTactics(in NavigationContext context)
        {
            if (_config == null)
            {
                _currentIntent = new TacticalIntent(Vector2.zero, Vector2.zero, false, false, false, false, false);
                return;
            }

            float dt = Time.deltaTime;
            if (_attackCooldownTimer > 0f) _attackCooldownTimer -= dt;
            if (_comboCommitTimer > 0f) _comboCommitTimer -= dt;

            var targetDir = context.DesiredWorldDirection;
            var distance = context.DistanceToTarget;
            var facingAngle = Vector3.Angle(
                Vector3.ProjectOnPlane(_selfTransform.forward, Vector3.up),
                Vector3.ProjectOnPlane(context.TargetWorldDirection, Vector3.up));

            var wantsToAttack = false;
            var moveWorldDir = Vector3.zero;

            if (distance > _config.EngagementRange)
            {
                moveWorldDir = targetDir;
            }
            else if (distance > _config.AttackRange)
            {
                moveWorldDir = targetDir;
            }
            else
            {
                // In attack range.
                // FreeLook 模式下 MotionDriver 仅在有移动输入时才旋转角色，
                // 因此需要调整朝向或冷却恢复时必须保持缓慢逼近以驱动转向。
                if (facingAngle > AttackFacingAngle)
                {
                    moveWorldDir = targetDir;
                }
                else if (_comboCommitTimer > 0f && _comboHitsRemaining > 0)
                {
                    // 正在连段中：持续输出攻击意图。
                    // 每当 per-hit 时间窗口耗尽且还有剩余段数，刷新计时器。
                    wantsToAttack = true;
                    moveWorldDir = Vector3.zero;

                    if (_comboCommitTimer <= 0f)
                    {
                        // 不会走到这里（外层已判 > 0），仅作防御
                    }
                }
                else if (_attackCooldownTimer <= 0f)
                {
                    // 发起新一轮连段
                    int desiredHits = GetDesiredComboHits();
                    float perHit = GetPerHitCommitTime();
                    _comboHitsRemaining = desiredHits;
                    _comboCommitTimer = perHit;
                    wantsToAttack = true;
                    moveWorldDir = Vector3.zero;
                    _attackCooldownTimer = GetAttackRecoveryDuration();
                }
                else
                {
                    // Recovery: keep creeping toward target to maintain facing.
                    moveWorldDir = targetDir;
                }
            }

            // 连段推进：当一段的 commit 时间耗尽，自动刷新到下一段
            if (wantsToAttack && _comboCommitTimer <= 0f && _comboHitsRemaining > 1)
            {
                _comboHitsRemaining--;
                _comboCommitTimer = GetPerHitCommitTime();
            }
            else if (wantsToAttack && _comboCommitTimer <= 0f)
            {
                // 最后一段的 commit 也结束了，停止攻击
                _comboHitsRemaining = 0;
                wantsToAttack = false;
                moveWorldDir = targetDir;
            }

            var moveInput = moveWorldDir != Vector3.zero ? Vector2.one : Vector2.zero;

            _currentIntent = new TacticalIntent(
                moveInput,
                Vector2.zero,
                wantsToAttack,
                false,
                false,
                false,
                false);
        }

        private int GetDesiredComboHits()
        {
            int min = _zombieConfig != null ? _zombieConfig.ComboHitsMin : DefaultComboMin;
            int max = _zombieConfig != null ? _zombieConfig.ComboHitsMax : DefaultComboMax;
            return UnityEngine.Random.Range(min, max + 1);
        }

        private float GetPerHitCommitTime()
        {
            return _zombieConfig != null ? _zombieConfig.PerHitCommitTime : DefaultPerHitCommitTime;
        }

        private float GetAttackRecoveryDuration()
        {
            if (_zombieConfig != null)
            {
                return UnityEngine.Random.Range(_zombieConfig.AttackCooldownMin, _zombieConfig.AttackCooldownMax);
            }
            return UnityEngine.Random.Range(1f, 2.5f);
        }
    }
}
