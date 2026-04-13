namespace BBBNexus
{
    /// <summary>
    /// 体力仲裁器（管线节点）
    /// </summary>
    public sealed class StaminaArbiter
    {
        public StaminaArbiter(BBBCharacterController player)
        {
            // RESET-NOTE: runtime stamina/sanity fields were removed from PlayerRuntimeData.
            // Keep this pipeline node and preserve legacy logic below as commented reference.
        }

        public void Arbitrate()
        {
            // RESET-TODO:
            // Rebuild stamina arbitration on top of IStateStore/IStateModify.
            //
            // Legacy implementation kept intentionally for reset reference:
            //
            // if (_data == null || _config == null || _config.Core == null) return;
            //
            // float drainRate = GetStaminaDrainRateForState(_data.CurrentLocomotionState);
            //
            // if (drainRate > 0f)
            // {
            //     _data.CurrentStamina -= drainRate * Time.deltaTime;
            //
            //     if (_data.CurrentStamina <= 0f)
            //     {
            //         _data.CurrentStamina = 0f;
            //         _data.IsStaminaDepleted = true;
            //     }
            // }
            // else if (drainRate < 0f)
            // {
            //     _data.CurrentStamina += (-drainRate) * Time.deltaTime;
            //
            //     if (_data.CurrentStamina > _data.MaxStamina * _config.Core.StaminaRecoverThreshold)
            //     {
            //         _data.IsStaminaDepleted = false;
            //     }
            // }
            //
            // _data.CurrentStamina = Mathf.Clamp(_data.CurrentStamina, 0f, _data.MaxStamina);
        }

        // Legacy helper (commented):
        // private float GetStaminaDrainRateForState(BBBNexus.LocomotionState state)
        // {
        //     return state switch
        //     {
        //         BBBNexus.LocomotionState.Sprint => _config.Core.StaminaDrainRate,
        //         BBBNexus.LocomotionState.Walk => -_config.Core.StaminaRegenRate * _config.Core.WalkStaminaRegenMult,
        //         BBBNexus.LocomotionState.Jog => -_config.Core.StaminaRegenRate,
        //         BBBNexus.LocomotionState.Idle => -_config.Core.StaminaRegenRate,
        //         _ => 0f
        //     };
        // }
    }
}
