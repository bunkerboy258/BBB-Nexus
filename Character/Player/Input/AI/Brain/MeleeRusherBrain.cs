using System;
using UnityEngine;

namespace BBBNexus
{
    [Serializable]
    public class MeleeRusherBrain : AITacticalBrainBase
    {

        private float _strafeTimer;
        private float _strafeDirection = 1f;

        // 【跳跃状态机】
        private float _jumpCooldownTimer;
        private float _doubleJumpDelayTimer;
        private int _jumpPhase; // 0=空闲, 1=一段跳已触发, 2=二段跳已触发

        // 【闪避状态机】：基于概率与冷却的低开销触发
        private float _dodgeCooldownTimer;
        private int _dodgeAttemptCount;

        // 【翻滚状态机】：基于概率与冷却的低开销触发
        private float _rollCooldownTimer;
        private int _rollAttemptCount;

        public override void Initialize(Transform selfTransform,AITacticalBrainConfigSO config)
        {
            base.Initialize(selfTransform, config);
            
            // 如果没有配置，创建默认配置
            if (_config == null)
            {
                Debug.LogWarning("[MeleeRusherBrain] 配置 SO 未赋值，使用默认参数");
                // 可选：从 Resources 或 Addressables 加载默认配置
            }
        }

        protected override void ProcessTactics(in NavigationContext context)
        {
            // 安全保护：如果没有配置就返回空意图
            if (_config == null)
            {
                _currentIntent = new TacticalIntent(Vector2.zero, Vector2.zero, false, false, false, false, false);
                return;
            }

            float dist = context.DistanceToTarget;
            Vector3 worldDir = context.DesiredWorldDirection;
            Vector2 lookInput = CalculateLookInput(context.TargetWorldDirection);

            // --- 【跳跃逻辑】---
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
            // --- 跳跃逻辑结束 ---

            // --- 【闪避逻辑】：距离与概率驱动的躲闪行为 ---
            bool wantsToDodge = false;

            if (_dodgeCooldownTimer > 0) _dodgeCooldownTimer -= Time.deltaTime;

            // 仅在敌人靠近且冷却已过时，以一定概率触发闪避
            if (dist < _config.DodgeTriggerRange && _dodgeCooldownTimer <= 0 && _dodgeAttemptCount < _config.DodgeMaxAttempts)
            {
                if (UnityEngine.Random.value < _config.DodgeChance)
                {
                    wantsToDodge = true;
                    _dodgeCooldownTimer = _config.DodgeCooldown;
                    _dodgeAttemptCount++;
                }
            }

            // 距离足够远时，重置闪避尝试计数
            if (dist > _config.DodgeTriggerRange * 1.5f)
            {
                _dodgeAttemptCount = 0;
            }
            // --- 闪避逻辑结束 ---

            // --- 【翻滚逻辑】：比闪避更谨慎、更罕见的防御动作 ---
            bool wantsToRoll = false;

            if (_rollCooldownTimer > 0) _rollCooldownTimer -= Time.deltaTime;

            // 翻滚触发条件更严格：更接近 + 更低概率 + 冷却更长
            if (dist < _config.RollTriggerRange && _rollCooldownTimer <= 0 && _rollAttemptCount < _config.RollMaxAttempts)
            {
                if (UnityEngine.Random.value < _config.RollChance)
                {
                    wantsToRoll = true;
                    _rollCooldownTimer = _config.RollCooldown;
                    _rollAttemptCount++;
                }
            }

            // 距离足够远时，重置翻滚尝试计数
            if (dist > _config.RollTriggerRange * 1.5f)
            {
                _rollAttemptCount = 0;
            }
            // --- 翻滚逻辑结束 ---

            if (dist > _config.EngagementRange)
            {
                // 远距离：直线冲向目标
                _currentIntent = new TacticalIntent(
                    ConvertWorldDirToJoystick(worldDir), 
                    lookInput, 
                    false,      // 不攻击
                    false,      // 不瞄准
                    wantsToJump,
                    wantsToDodge,
                    wantsToRoll);
            }
            else if (dist > _config.AttackRange)
            {
                // 中距离：迂回战术
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
                    false,      // 不攻击
                    true,       // 瞄准（准备挨近时的反击）
                    wantsToJump,
                    wantsToDodge,
                    wantsToRoll);
            }
            else
            {
                // 近距离：贴脸输出
                _currentIntent = new TacticalIntent(
                    Vector2.zero, 
                    lookInput, 
                    true,       // 攻击
                    false,      // 不瞄准
                    wantsToJump,
                    wantsToDodge,
                    wantsToRoll);
            }
        }
    }
}