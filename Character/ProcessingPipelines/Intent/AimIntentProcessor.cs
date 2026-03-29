namespace BBBNexus
{
    // 瞄准与开火意图处理器
    public class AimIntentProcessor
    {
        private readonly PlayerRuntimeData _data;
        private readonly InputPipeline _input;
        private bool _isAimHeld;
        private bool _wasAimHeld;

        public AimIntentProcessor(PlayerRuntimeData data, InputPipeline input)
        {
            _data = data;
            _input = input;
        }

        public void Update(in ProcessedInputData input)
        {
            bool isAimHeldNow = input.AimHeld || input.SecondaryAttackHeld;
            _data.IsAiming = isAimHeldNow;
            _wasAimHeld = _isAimHeld;
            _isAimHeld = isAimHeldNow;

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
    }
}
