namespace BBBNexus
{
    // 瞄准与开火意图处理器
    public class AimIntentProcessor
    {
        private readonly PlayerRuntimeData _data;
        private bool _isAimHeld;
        private bool _wasAimHeld;

        public AimIntentProcessor(PlayerRuntimeData data)
        {
            _data = data;
        }

        public void Update(in ProcessedInputData input)
        {
            // 右键按住 = 瞄准
            bool isAimHeldNow = input.AimHeld || input.SecondaryAttackHeld;
            _data.IsAiming = isAimHeldNow;
            _wasAimHeld = _isAimHeld;
            _isAimHeld = isAimHeldNow;

            // 瞄准时左键 = 射击（使用 PrimaryAction）
            if (input.PrimaryAttackHeld && _data.IsAiming)
            {
                _data.WantsToPrimaryAction = true;
            }
        }
    }
}