namespace BBBNexus
{
    /// <summary>
    /// 汇总 Locomotion / Action / Status 三域的运行时控制权。
    /// 当前阶段先作为总线层输出统一语义，逐步替代直接查询 OverrideState / StatusEffectState。
    /// </summary>
    public class CharacterArbiter
    {
        private readonly BBBCharacterController _player;
        private readonly PlayerRuntimeData _data;

        public CharacterArbiter(BBBCharacterController player)
        {
            _player = player;
            _data = player.RuntimeData;
        }

        public void Arbitrate()
        {
            _data.CharacterControl.Clear();

            if (_data.IsDead || _data.Arbitration.IsDead)
            {
                _data.CharacterControl.ActiveDomain = CharacterControlDomain.Death;
                _data.CharacterControl.BlocksAction = true;
                _data.CharacterControl.BlocksLocomotion = true;
                _data.CharacterControl.BlocksInput = true;
                return;
            }

            if (_data.StatusControl.IsActive && _data.StatusControl.InterruptMode == StatusInterruptMode.Hard)
            {
                _data.CharacterControl.ActiveDomain = CharacterControlDomain.Status;
                _data.CharacterControl.BlocksAction = _data.StatusControl.BlocksAction;
                _data.CharacterControl.BlocksLocomotion = _data.StatusControl.BlocksLocomotion;
                _data.CharacterControl.BlocksInput = _data.StatusControl.BlocksInput;
                return;
            }

            if (_data.ActionControl.IsActive)
            {
                _data.CharacterControl.ActiveDomain = CharacterControlDomain.Action;
                _data.CharacterControl.BlocksAction = false;
                _data.CharacterControl.BlocksLocomotion = _data.ActionControl.BlocksLocomotion;
                _data.CharacterControl.BlocksInput = false;
                return;
            }

            _data.CharacterControl.ActiveDomain = CharacterControlDomain.Locomotion;
        }

        public bool IsActionBlocked()
        {
            return _data.StatusControl.BlocksAction ||
                   _data.CharacterControl.BlocksAction ||
                   _data.Arbitration.BlockAction;
        }

        public bool IsUnderStatusControl()
        {
            return (_data.StatusControl.IsActive && _data.StatusControl.InterruptMode == StatusInterruptMode.Hard) ||
                   _data.CharacterControl.ActiveDomain == CharacterControlDomain.Status;
        }

        public bool IsLocomotionBlocked()
        {
            return _data.StatusControl.BlocksLocomotion ||
                   _data.ActionControl.BlocksLocomotion ||
                   _data.CharacterControl.BlocksLocomotion;
        }
    }
}
