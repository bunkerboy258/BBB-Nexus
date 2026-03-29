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

        private StatusEffectSO _current;
        private float _remainingTime;

        public StatusEffectSO Current => _current;
        public bool IsActive => _current != null && (_current.Duration <= 0f || _remainingTime > 0f);

        public StatusEffectArbiter(BBBCharacterController player)
        {
            _player = player;
            _data = player.RuntimeData;
        }

        public void Apply(StatusEffectSO effect, float hitAngle = float.NaN)
        {
            if (effect == null || _data.IsDead)
                return;

            if (_current != null && effect.Priority < _current.Priority)
                return;

            bool alreadyInState = _player.StateMachine.CurrentState is StatusEffectState;
            var state = _player.StateRegistry?.GetState<StatusEffectState>();
            if (state == null)
                return;

            if (_current == effect)
            {
                if (!effect.CanBeRefreshed)
                    return;

                _remainingTime = effect.Duration;
                _data.StatusEffect.HitAngle = hitAngle;
                if (alreadyInState)
                    state.ForceReapply();
                return;
            }

            _current = effect;
            _remainingTime = effect.Duration;

            _data.StatusEffect.IsActive = true;
            _data.StatusEffect.Effect = effect;
            _data.StatusEffect.HitAngle = hitAngle;
            if (!alreadyInState)
                _data.StatusEffect.ReturnState = ResolveSafeReturnState();

            if (alreadyInState)
            {
                state.ForceReapply();
                return;
            }

            _player.StateMachine.ChangeState(state);
        }

        private BaseState ResolveSafeReturnState()
        {
            var currentState = _player.StateMachine.CurrentState;
            if (currentState is OverrideState)
            {
                // StatusEffect 会触发 OverrideState.Exit，而 Exit 会清掉 Override 上下文。
                // 受击结束后再返回同一个 OverrideState 会进入一个没有请求可回放的空壳状态。
                if (_data.Override.ReturnState != null &&
                    _data.Override.ReturnState is not OverrideState &&
                    _data.Override.ReturnState is not StatusEffectState)
                {
                    return _data.Override.ReturnState;
                }

                return _data.CurrentLocomotionState != LocomotionState.Idle
                    ? _player.StateRegistry.GetState<PlayerMoveLoopState>()
                    : _player.StateRegistry.GetState<PlayerIdleState>();
            }

            return currentState;
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
                        {
                            var state = _player.StateRegistry?.GetState<StatusEffectState>();
                            state?.ReturnToPreviousState();
                        }
                    }

                    return;
                }
            }

            _current.ApplyBlockFlagsTo(ref _data.Arbitration);
        }
    }
}
