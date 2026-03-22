using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 体力仲裁器：被动读取黑板中的运动状态 并更新体力消耗/恢复
    /// </summary>
    public sealed class StaminaArbiter
    {
        private readonly PlayerRuntimeData _data;
        private readonly PlayerSO _config;

        public StaminaArbiter(PlayerController player)
        {
            _data = player.RuntimeData;
            _config = player.Config;
        }

        public void Arbitrate()
        {
            if (_data == null || _config == null || _config.Core == null) return;

            float drainRate = GetStaminaDrainRateForState(_data.CurrentLocomotionState);

            if (drainRate > 0f)
            {
                _data.CurrentStamina -= drainRate * Time.deltaTime;

                if (_data.CurrentStamina <= 0f)
                {
                    _data.CurrentStamina = 0f;
                    _data.IsStaminaDepleted = true;
                }
            }
            else if (drainRate < 0f)
            {
                _data.CurrentStamina += (-drainRate) * Time.deltaTime;

                if (_data.CurrentStamina > _config.Core.MaxStamina * _config.Core.StaminaRecoverThreshold)
                {
                    _data.IsStaminaDepleted = false;
                }
            }

            _data.CurrentStamina = Mathf.Clamp(_data.CurrentStamina, 0f, _config.Core.MaxStamina);
        }

        private float GetStaminaDrainRateForState(BBBNexus.LocomotionState state)
        {
            return state switch
            {
                BBBNexus.LocomotionState.Sprint => _config.Core.StaminaDrainRate,
                BBBNexus.LocomotionState.Walk => -_config.Core.StaminaRegenRate * _config.Core.WalkStaminaRegenMult,
                BBBNexus.LocomotionState.Jog => -_config.Core.StaminaRegenRate,
                BBBNexus.LocomotionState.Idle => -_config.Core.StaminaRegenRate,
                _ => 0f
            };
        }
    }
}
