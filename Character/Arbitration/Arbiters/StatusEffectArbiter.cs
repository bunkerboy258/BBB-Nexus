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
        private StatusEffectSO _hitStopOverlay;
        private float _hitStopOverlayRemainingTime;

        public StatusEffectSO Current => _current;
        public bool IsHitStopActive => _hitStopOverlay != null && (_hitStopOverlay.Duration <= 0f || _hitStopOverlayRemainingTime > 0f);
        public bool IsHitStopMotionFrozen => IsHitStopActive && _hitStopOverlay != null && _hitStopOverlay.FreezeMotion;
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

            if (effect.IsHitStop)
            {
                ApplyHitStopOverlay(effect);
                return;
            }

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

            if (effect.InterruptMode == StatusInterruptMode.Hard)
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
            if (_hitStopOverlay != null)
            {
                _player.AnimFacade?.ExitHitStop();
            }
            _player.AnimFacade?.ClearOverrideOnEndCallback();
            _player.AnimFacade?.StopFullBodyAction();

            _current = null;
            _remainingTime = 0f;
            _hitStopOverlay = null;
            _hitStopOverlayRemainingTime = 0f;
            _data.StatusEffect.Clear();
            _data.StatusControl.Clear();
        }

        public void Arbitrate()
        {
            if (_hitStopOverlay != null)
            {
                if (_hitStopOverlay.Duration > 0f)
                {
                    _hitStopOverlayRemainingTime -= Time.deltaTime;
                    if (_hitStopOverlayRemainingTime <= 0f)
                    {
                        _player.AnimFacade?.ExitHitStop();
                        _hitStopOverlay = null;
                        _hitStopOverlayRemainingTime = 0f;
                    }
                }

                if (_hitStopOverlay != null)
                    _player.AnimFacade?.EnterHitStop(_hitStopOverlay.HitStopAnimationSpeed);
            }

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

            if (_current.Duration <= 0f || _remainingTime <= 0f)
                Clear();
            else
                ApplyCurrentEffect(); // 还有剩余时间，重播动画（loop）
        }

        private void SyncStatusControl(StatusEffectSO effect, bool usesLegacyStatusState)
        {
            bool isHardInterrupt = effect != null && effect.InterruptMode == StatusInterruptMode.Hard;
            _data.StatusControl.IsActive = effect != null;
            _data.StatusControl.Priority = effect != null ? effect.Priority : 0;
            _data.StatusControl.InterruptMode = effect != null ? effect.InterruptMode : StatusInterruptMode.None;
            _data.StatusControl.BlocksAction = isHardInterrupt && effect.BlockAction;
            _data.StatusControl.BlocksLocomotion = isHardInterrupt;
            _data.StatusControl.BlocksInput = isHardInterrupt && effect.BlockInput;
            _data.StatusControl.UsesLegacyStatusState = usesLegacyStatusState;
        }

        private void ApplyHitStopOverlay(StatusEffectSO effect)
        {
            if (_hitStopOverlay != null && effect.Priority < _hitStopOverlay.Priority)
                return;

            _hitStopOverlay = effect;
            _hitStopOverlayRemainingTime = effect.Duration;
        }
    }
}
