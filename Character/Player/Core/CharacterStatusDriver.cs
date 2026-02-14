using Characters.Player.Core;
using Characters.Player.Data;
using UnityEngine;

namespace Characters.Player.Core
{
    /// <summary>
    /// 角色状态 Driver（被动）。
    /// 职责：根据 PlayerRuntimeData 的"当前状态"被动驱动数值型属性变化。
    /// 例如：根据 CurrentLocomotionState 消耗/恢复体力；后续可在此扩展生命值、护盾、饥饿等。
    /// 
    /// 体力管理逻辑：
    /// - Sprint：快速消耗（StaminaDrainRate）
    /// - Jog：正常恢复（StaminaRegenRate）
    /// - Walk：加速恢复（StaminaRegenRate * WalkStaminaRegenMult）
    /// - Idle：加速恢复（StaminaRegenRate * WalkStaminaRegenMult）
    /// </summary>
    public class CharacterStatusDriver
    {
        private readonly PlayerRuntimeData _data;
        private readonly PlayerSO _config;

        // 构造改为接收数据与配置，避免直接依赖 PlayerController 类型，减少耦合。
        public CharacterStatusDriver(PlayerRuntimeData data, PlayerSO config)
        {
            _data = data;
            _config = config;
        }

        public void Update()
        {
            UpdateStamina();
            // TODO: UpdateHealth();
        }

        /// <summary>
        /// 被动更新体力。根据当前的移动状态决定消耗或恢复速率。
        /// </summary>
        private void UpdateStamina()
        {
            float drainRate = GetStaminaDrainRateForState(_data.CurrentLocomotionState);
            
            if (drainRate > 0)
            {
                // 消耗模式：体力减少
                _data.CurrentStamina -= drainRate * Time.deltaTime;

                if (_data.CurrentStamina <= 0f)
                {
                    _data.CurrentStamina = 0f;
                    _data.IsStaminaDepleted = true;
                }
            }
            else if (drainRate < 0)
            {
                // 恢复模式：体力增加
                _data.CurrentStamina += (-drainRate) * Time.deltaTime;

                // 恢复到阈值以上解除耗尽标记
                if (_data.CurrentStamina > _config.MaxStamina * _config.StaminaRecoverThreshold)
                {
                    _data.IsStaminaDepleted = false;
                }
            }
            // 如果 drainRate == 0，体力保持不变（静止状态）

            // 限制体力值范围
            _data.CurrentStamina = Mathf.Clamp(_data.CurrentStamina, 0f, _config.MaxStamina);
        }

        /// <summary>
        /// 根据运动状态获取体力消耗速率。
        /// 正值表示消耗，负值表示恢复，0 表示不变。
        /// </summary>
        private float GetStaminaDrainRateForState(LocomotionState state)
        {
            return state switch
            {
                // Sprint：快速消耗体力
                LocomotionState.Sprint => _config.StaminaDrainRate,

                // Walk：加速恢复（恢复速率为负，在 UpdateStamina 中取反使用）
                LocomotionState.Walk => -_config.StaminaRegenRate * _config.WalkStaminaRegenMult,

                // Jog 和 Idle：正常恢复
                LocomotionState.Jog => -_config.StaminaRegenRate,
                LocomotionState.Idle => -_config.StaminaRegenRate,

                _ => 0f // 未知状态，体力不变
            };
        }
    }
}
