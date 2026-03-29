using System;
using UnityEngine;

namespace BBBNexus
{
    [Serializable]
    public class ZombieBrain : AITacticalBrainBase
    {
        private const float AttackFacingAngle = 35f;
        private const float JumpMinDistanceFromTarget = 2.5f;
        private const float AttackCommitDuration = 0.45f;

        private float _attackCooldownTimer;
        private float _attackCommitTimer;
        private float _jumpCooldownTimer;

        public override void Initialize(Transform selfTransform, AITacticalBrainConfigSO config)
        {
            base.Initialize(selfTransform, config);
        }

        protected override void ProcessTactics(in NavigationContext context)
        {
            if (_config == null)
            {
                _currentIntent = new TacticalIntent(Vector2.zero, Vector2.zero, false, false, false, false, false);
                return;
            }

            if (_attackCooldownTimer > 0f)
            {
                _attackCooldownTimer -= Time.deltaTime;
            }

            if (_attackCommitTimer > 0f)
            {
                _attackCommitTimer -= Time.deltaTime;
            }

            if (_jumpCooldownTimer > 0f)
            {
                _jumpCooldownTimer -= Time.deltaTime;
            }

            var lookInput = CalculateLookInput(context.TargetWorldDirection);
            var targetDir = context.DesiredWorldDirection;
            var distance = context.DistanceToTarget;
            var facingAngle = Vector3.Angle(
                Vector3.ProjectOnPlane(_selfTransform.forward, Vector3.up),
                Vector3.ProjectOnPlane(context.TargetWorldDirection, Vector3.up));

            var wantsToAttack = false;
            var wantsToJump = false;
            var moveWorldDir = Vector3.zero;

            if (distance > _config.EngagementRange)
            {
                // Outside engagement: just shamble straight at the target.
                moveWorldDir = targetDir;
            }
            else if (distance > _config.AttackRange)
            {
                // Inside engagement but not yet in range: keep pressing forward.
                moveWorldDir = targetDir;
            }
            else
            {
                // In attack range: face the target first, then attack on cooldown.
                if (facingAngle > AttackFacingAngle)
                {
                    moveWorldDir = Vector3.zero;
                }
                else if (_attackCommitTimer > 0f)
                {
                    wantsToAttack = true;
                    moveWorldDir = Vector3.zero;
                }
                else if (_attackCooldownTimer <= 0f)
                {
                    wantsToAttack = true;
                    moveWorldDir = Vector3.zero;
                    _attackCommitTimer = AttackCommitDuration;
                    _attackCooldownTimer = GetAttackRecoveryDuration();
                }
                else
                {
                    // Recovery window after an attack: stop pushing and just keep facing.
                    moveWorldDir = Vector3.zero;
                }
            }

            var shouldJumpToTraverse =
                context.NeedsJump &&
                _jumpCooldownTimer <= 0f &&
                !wantsToAttack &&
                distance > Mathf.Max(_config.EngagementRange, JumpMinDistanceFromTarget) &&
                moveWorldDir != Vector3.zero;

            if (shouldJumpToTraverse)
            {
                wantsToJump = true;
                _jumpCooldownTimer = _config.JumpCooldown;
            }

            var moveInput = moveWorldDir == Vector3.zero
                ? Vector2.zero
                : ConvertWorldDirToJoystick(moveWorldDir.normalized);

            _currentIntent = new TacticalIntent(
                moveInput,
                lookInput,
                wantsToAttack,
                false,
                wantsToJump,
                false,
                false);
        }

        private float GetAttackRecoveryDuration()
        {
            float min = Mathf.Max(0.45f, _config.StrafeCooldownMin);
            float max = Mathf.Max(min, _config.StrafeCooldownMax);
            return UnityEngine.Random.Range(min, max);
        }
    }
}
