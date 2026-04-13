namespace BBBNexus
{
    // 战术姿态与副键意图处理器。
    // 只有当前主手武器显式支持，且装备阶段已完成时，才会进入 TacticalMotionBase。
    public class TacticalStanceIntentProcessor
    {
        private readonly PlayerRuntimeData _data;
        private readonly InputPipeline _input;
        private readonly BBBCharacterController _player;

        public TacticalStanceIntentProcessor(PlayerRuntimeData data, InputPipeline input, BBBCharacterController player)
        {
            _data = data;
            _input = input;
            _player = player;
        }

        public void Update(in ProcessedInputData input)
        {
            bool sprintActive = input.SprintHeld &&
                _data.MoveInput.sqrMagnitude > 0.01f;/*&&
                !_data.IsStaminaDepleted &&
                _data.CurrentStamina > 0f;*/
            bool wantsSecondary = input.AimHeld || input.SecondaryAttackHeld;
            _data.WantsToSecondaryAction = !sprintActive && wantsSecondary;
            _data.IsTacticalStance = !sprintActive && wantsSecondary && CanCurrentMainhandEnterTacticalMotionBase();

            if (_data.Arbitration.BlockAction)
            {
                if (input.PrimaryAttackPressed)
                    _input?.ConsumePrimaryAttackPressed();
                return;
            }

            if (input.PrimaryAttackHeld && _data.IsTacticalStance)
            {
                _data.WantsToPrimaryAction = true;
            }
        }

        private bool CanCurrentMainhandEnterTacticalMotionBase()
        {
            var ranged = _player?.EquipmentDriver?.MainhandItemData as RangedWeaponSO;
            return ranged != null && ranged.EnablesAimState && _data.CanEnterTacticalMotionBase;
        }
    }
}
