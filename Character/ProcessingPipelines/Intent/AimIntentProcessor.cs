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
            bool isAimHeldNow = input.AimHeld;
            // 检查按住状态或瞬间按下 用于支持连续射击与精确射击
            bool isFireHeldOrPressed = input.LeftMouseHeld || input.LeftMousePressed;

            _data.IsAiming = isAimHeldNow;
            _wasAimHeld = _isAimHeld;
            _isAimHeld = isAimHeldNow;

            if (isFireHeldOrPressed)
            {
                _data.WantsToFire = true;
            }
        }
    }
}