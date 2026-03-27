using System;
using UnityEngine;

namespace BBBNexus
{
    [Serializable]
    public class MeleeRusherBrain : AITacticalBrainBase
    {

        private float _strafeTimer;
        private float _strafeDirection = 1f;

        private float _jumpCooldownTimer;
        private float _doubleJumpDelayTimer;
        private int _jumpPhase;

        private float _dodgeCooldownTimer;
        private int _dodgeAttemptCount;

        private float _rollCooldownTimer;
        private int _rollAttemptCount;

        public override void Initialize(Transform selfTransform,AITacticalBrainConfigSO config)
        {
            base.Initialize(selfTransform, config);
            
            if (_config == null)
            {
                Debug.LogWarning("[BBNexus] MeleeRusherBrainÅäÖÃ SO Î´¸³Öµ");
            }
        }

        protected override void ProcessTactics(in NavigationContext context)
        {
            if (_config == null)
            {
                _currentIntent = new TacticalIntent(Vector2.zero, Vector2.zero, false, false, false, false, false);
                return;
            }

            float dist = context.DistanceToTarget;
            Vector3 worldDir = context.DesiredWorldDirection;
            Vector2 lookInput = CalculateLookInput(context.TargetWorldDirection);

            bool wantsToJump = false;
            if (_jumpCooldownTimer > 0) _jumpCooldownTimer -= Time.deltaTime;
            if (_doubleJumpDelayTimer > 0) _doubleJumpDelayTimer -= Time.deltaTime;

            if (context.NeedsJump && _jumpCooldownTimer <= 0)
            {
                wantsToJump = true;
                _jumpPhase = 1;
                _jumpCooldownTimer = _config.JumpCooldown;
                _doubleJumpDelayTimer = _config.DoubleJumpDelay;
            }
            else if (_jumpPhase == 1 && context.NeedsJump && _doubleJumpDelayTimer <= 0)
            {
                wantsToJump = true;
                _jumpPhase = 2;
                _doubleJumpDelayTimer = 1.0f;
            }

            if (!context.NeedsJump && _jumpCooldownTimer <= 0)
            {
                _jumpPhase = 0;
            }

            bool wantsToDodge = false;

            if (_dodgeCooldownTimer > 0) _dodgeCooldownTimer -= Time.deltaTime;

            if (dist < _config.DodgeTriggerRange && _dodgeCooldownTimer <= 0 && _dodgeAttemptCount < _config.DodgeMaxAttempts)
            {
                if (UnityEngine.Random.value < _config.DodgeChance)
                {
                    wantsToDodge = true;
                    _dodgeCooldownTimer = _config.DodgeCooldown;
                    _dodgeAttemptCount++;
                }
            }

            if (dist > _config.DodgeTriggerRange * 1.5f)
            {
                _dodgeAttemptCount = 0;
            }

            bool wantsToRoll = false;

            if (_rollCooldownTimer > 0) _rollCooldownTimer -= Time.deltaTime;

            if (dist < _config.RollTriggerRange && _rollCooldownTimer <= 0 && _rollAttemptCount < _config.RollMaxAttempts)
            {
                if (UnityEngine.Random.value < _config.RollChance)
                {
                    wantsToRoll = true;
                    _rollCooldownTimer = _config.RollCooldown;
                    _rollAttemptCount++;
                }
            }

            if (dist > _config.RollTriggerRange * 1.5f)
            {
                _rollAttemptCount = 0;
            }

            if (dist > _config.EngagementRange)
            {
                _currentIntent = new TacticalIntent(
                    ConvertWorldDirToJoystick(worldDir), 
                    lookInput, 
                    false, 
                    false,      
                    wantsToJump,
                    wantsToDodge,
                    wantsToRoll);
            }
            else if (dist > _config.AttackRange)
            {
                _strafeTimer -= Time.deltaTime;
                if (_strafeTimer <= 0)
                {
                    _strafeDirection = UnityEngine.Random.value > 0.5f ? 1f : -1f;
                    _strafeTimer = UnityEngine.Random.Range(_config.StrafeCooldownMin, _config.StrafeCooldownMax);
                }

                Vector3 rightDir = Vector3.Cross(Vector3.up, worldDir).normalized;
                Vector3 tacticalDir = (worldDir * 0.4f) + (rightDir * _strafeDirection * 0.8f);

                _currentIntent = new TacticalIntent(
                    ConvertWorldDirToJoystick(tacticalDir.normalized), 
                    lookInput, 
                    false, 
                    true,      
                    wantsToJump,
                    wantsToDodge,
                    wantsToRoll);
            }
            else
            {
                _currentIntent = new TacticalIntent(
                    Vector2.zero, 
                    lookInput, 
                    true,  
                    false,    
                    wantsToJump,
                    wantsToDodge,
                    wantsToRoll);
            }
        }
    }
}