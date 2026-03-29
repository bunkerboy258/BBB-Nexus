namespace BBBNexus
{
    // 瞄准与副键意图处理器。
    // 右键不再全局劫持 Aim 状态，只有当前主手武器显式支持时才会进入 Aim。
    public class AimIntentProcessor
    {
        private readonly PlayerRuntimeData _data;
        private readonly InputPipeline _input;
        private readonly BBBCharacterController _player;

        public AimIntentProcessor(PlayerRuntimeData data, InputPipeline input, BBBCharacterController player)
        {
            _data = data;
            _input = input;
            _player = player;
        }

        public void Update(in ProcessedInputData input)
        {
            bool wantsSecondary = input.AimHeld || input.SecondaryAttackHeld;
            _data.WantsToSecondaryAction = wantsSecondary;
            _data.IsAiming = wantsSecondary && CanCurrentMainhandEnterAimState();

            if (_data.Arbitration.BlockAction)
            {
                if (input.PrimaryAttackPressed)
                    _input?.ConsumePrimaryAttackPressed();
                return;
            }

            if (input.PrimaryAttackHeld && _data.IsAiming)
            {
                _data.WantsToPrimaryAction = true;
            }
        }

        private bool CanCurrentMainhandEnterAimState()
        {
            var ranged = _player?.EquipmentDriver?.MainhandItemData as RangedWeaponSO;
            return ranged != null && ranged.EnablesAimState;
        }
    }
}
