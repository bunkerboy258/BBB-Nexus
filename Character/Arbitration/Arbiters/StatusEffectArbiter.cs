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

            if (_current == effect)
            {
                if (!effect.CanBeRefreshed)
                    return;

                _remainingTime = effect.Duration;
                _data.StatusEffect.HitAngle = hitAngle;
                SyncStatusControl(effect, usesLegacyStatusState: false);
                ApplyCurrentEffect();
                return;
            }

            _player.ArbiterPipeline?.Action?.CancelActiveAction(stopAnimation: false);

            _current = effect;
            _remainingTime = effect.Duration;

            _data.StatusEffect.IsActive = true;
            _data.StatusEffect.Effect = effect;
            _data.StatusEffect.HitAngle = hitAngle;
            _data.StatusEffect.ReturnState = _player.StateMachine.CurrentState;
            SyncStatusControl(effect, usesLegacyStatusState: false);
            ApplyCurrentEffect();
        }

        public void Clear()
        {
            _player.AnimFacade?.ClearOverrideOnEndCallback();
            _player.AnimFacade?.StopFullBodyAction();
            _current = null;
            _remainingTime = 0f;
            _data.StatusEffect.Clear();
            _data.StatusControl.Clear();
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
                    Clear();
                    return;
                }
            }

            _current.ApplyBlockFlagsTo(ref _data.Arbitration);
        }

        private void ApplyCurrentEffect()
        {
            if (!_data.StatusEffect.IsActive || _current == null)
                return;

            var transition = _current.SelectHitClip(_data.StatusEffect.HitAngle);
            if (transition?.Clip == null)
            {
                Clear();
                return;
            }

            _player.AnimFacade?.ClearOverrideOnEndCallback();
            _player.AnimFacade?.PlayFullBodyActionTransition(transition);
            _player.AnimFacade?.SetOverrideOnEndCallback(OnStatusClipEnd);
        }

        private void OnStatusClipEnd()
        {
            if (_current == null)
                return;

            Clear();
        }

        private void SyncStatusControl(StatusEffectSO effect, bool usesLegacyStatusState)
        {
            _data.StatusControl.IsActive = effect != null;
            _data.StatusControl.Priority = effect != null ? effect.Priority : 0;
            _data.StatusControl.BlocksAction = effect != null && effect.BlockAction;
            _data.StatusControl.BlocksLocomotion = effect != null;
            _data.StatusControl.BlocksInput = effect != null && effect.BlockInput;
            _data.StatusControl.UsesLegacyStatusState = usesLegacyStatusState;
        }
    }
}
