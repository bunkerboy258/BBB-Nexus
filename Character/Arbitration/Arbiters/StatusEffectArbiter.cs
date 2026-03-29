using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 管理角色身上的被动状态效果。
    /// 被动状态与主动动作 Override 分离，避免污染攻击状态机。
    /// </summary>
    public class StatusEffectArbiter
    {
        private readonly BBBCharacterController _player;
        private readonly PlayerRuntimeData _data;
        private readonly StatusEffectState _state;

        private StatusEffectSO _current;
        private float _remainingTime;

        public StatusEffectSO Current => _current;
        public bool IsActive => _current != null && (_current.Duration <= 0f || _remainingTime > 0f);

        public StatusEffectArbiter(BBBCharacterController player)
        {
            _player = player;
            _data = player.RuntimeData;
            _state = new StatusEffectState(player);
        }

        public void Apply(StatusEffectSO effect, float hitAngle = float.NaN)
        {
            if (effect == null || _data.IsDead)
                return;

            if (_current != null && effect.Priority < _current.Priority)
                return;

            bool alreadyInState = _player.StateMachine.CurrentState is StatusEffectState;
            if (_current == effect)
            {
                if (!effect.CanBeRefreshed)
                    return;

                _remainingTime = effect.Duration;
                _data.StatusEffect.HitAngle = hitAngle;
                if (alreadyInState)
                    _state.ForceReapply();
                return;
            }

            _current = effect;
            _remainingTime = effect.Duration;

            _data.StatusEffect.IsActive = true;
            _data.StatusEffect.Effect = effect;
            _data.StatusEffect.HitAngle = hitAngle;
            if (!alreadyInState)
                _data.StatusEffect.ReturnState = _player.StateMachine.CurrentState;

            if (alreadyInState)
            {
                _state.ForceReapply();
                return;
            }

            _player.StateMachine.ChangeState(_state);
        }

        public void Clear()
        {
            _current = null;
            _remainingTime = 0f;
            _data.StatusEffect.Clear();
        }

        public void Arbitrate()
        {
            if (_current == null)
                return;

            if (_current.Duration > 0f)
            {
                _remainingTime -= Time.deltaTime;
                if (_remainingTime <= 0f)
                {
                    _current = null;
                    if (_data.StatusEffect.IsActive)
                    {
                        _data.StatusEffect.IsActive = false;
                        if (_player.StateMachine.CurrentState is StatusEffectState)
                            _state.ReturnToPreviousState();
                    }

                    return;
                }
            }

            _current.ApplyBlockFlagsTo(ref _data.Arbitration);
        }
    }
}
