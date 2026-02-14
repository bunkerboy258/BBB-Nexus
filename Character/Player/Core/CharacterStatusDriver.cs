using Characters.Player.Core;
using Characters.Player.Data;
using UnityEngine;

namespace Characters.Player.Core
{
    /// <summary>
    /// 角色状态 Driver（被动）。
    /// 职责：根据 PlayerRuntimeData 的“当前状态”被动驱动数值型属性变化。
    /// 例如：根据 IsRunning 消耗/恢复体力；后续可在此扩展生命值、护盾、饥饿等。
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

        private void UpdateStamina()
        {
            if (_data.IsRunning)
            {
                _data.CurrentStamina -= _config.StaminaDrainRate * Time.deltaTime;

                if (_data.CurrentStamina <= 0f)
                {
                    _data.CurrentStamina = 0f;
                    _data.IsStaminaDepleted = true;
                }
            }
            else
            {
                _data.CurrentStamina += _config.StaminaRegenRate * Time.deltaTime;

                if (_data.CurrentStamina > _config.MaxStamina * _config.StaminaRecoverThreshold)
                {
                    _data.IsStaminaDepleted = false;
                }
            }

            _data.CurrentStamina = Mathf.Clamp(_data.CurrentStamina, 0f, _config.MaxStamina);
        }
    }
}
