using Animancer;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BBBNexus
{
    public class FistsBehaviour : MonoBehaviour, IHoldableItem, IPoolable
    {
        private const bool AttackTrace = true;

        private sealed class FistsAttackWindowContext
        {
            public readonly HashSet<IDamageable> SharedHitSet = new HashSet<IDamageable>();
            public FistsAttackHand AttackHand;
            public int ComboIndex;
        }

        private BBBCharacterController _player;
        private FistsSO _config;
        private ItemInstance _instance;
        private FistHitbox _hitbox;

        public EquipmentSlot CurrentEquipSlot { get; set; }

        private Action<object> _onAttackStart;
        private Action<object> _onAttackEnd;
        private float _ignoreAttackUntil;
        private FistsAttackWindowContext _activeAttackContext;

        private int _comboIndex;
        private bool _isEnteringStance;
        private bool _isExitingStance;
        private bool _isAttacking;
        private float _comboWindowOpenTime;
        private float _comboWindowCloseTime;
        private bool _inputBuffered;
        private bool _restartQueued;
        private bool _stanceAttackBridged;
        private int _queuedAttackIndex = -1;
        private ClipTransition _currentAttackTransition;
        private ClipTransition _currentStanceTransition;
        private float _currentAttackStartTime;
        private float _currentStanceStartTime;


        public void Initialize(ItemInstance instanceData)
        {
            _instance = instanceData;
            _config = instanceData?.GetSODataAs<FistsSO>();

            if (_config != null)
            {
                CurrentEquipSlot = _config.EquipSlot;
            }
        }

        public void OnEquipEnter(BBBCharacterController player)
        {
            _player = player;
            _hitbox = GetComponentInChildren<FistHitbox>();
            if (_hitbox != null)
            {
                _hitbox.SetOwner(player);
                _hitbox.Deactivate();

                if (CurrentEquipSlot == EquipmentSlot.OffHand)
                {
                    _onAttackStart = OnRemoteAttackStart;
                    _onAttackEnd = _ => CloseHitWindow();
                    PostSystem.Instance.On("Fists.AttackStart", _onAttackStart);
                    PostSystem.Instance.On("Fists.AttackEnd", _onAttackEnd);
                }
            }
            else
            {
                Debug.LogWarning("[Fists] OnEquipEnter: missing FistHitbox on fists prefab.");
            }

            _player?.InputPipeline?.ConsumePrimaryAttackPressed();
            _ignoreAttackUntil = Time.time + 0.12f;
            ResetComboState();
        }

        public void OnUpdateLogic()
        {
            if (_player == null || _config == null) return;
            if (_config.ComboSequence == null || _config.ComboSequence.Length == 0) return;

            ref readonly ProcessedInputData input = ref _player.InputPipeline.Current.currentFrameData.Processed;
            bool wantsAttack = _player.RuntimeData.WantsToPrimaryAction;
            bool attackInputObserved = wantsAttack || input.PrimaryAttackHeld || input.PrimaryAttackPressed;

            if (_player.RuntimeData.Arbitration.BlockAction)
            {
                TraceAttackGate("block-action", attackInputObserved, in input);
                return;
            }

            if (CurrentEquipSlot != EquipmentSlot.MainHand)
            {
                TraceAttackGate("not-main-hand", attackInputObserved, in input);
                return;
            }

            if (Time.time < _ignoreAttackUntil)
            {
                TraceAttackGate("ignore-window", attackInputObserved, in input);
                return;
            }

            if (_isEnteringStance)
            {
                TraceAttackGate("entering-stance", attackInputObserved, in input);
                if (wantsAttack)
                {
                    _inputBuffered = true;
                    _player.InputPipeline.ConsumePrimaryAttackPressed();
                }

                TryAdvanceStanceBridge();
                return;
            }

            if (_isExitingStance)
            {
                TraceAttackGate("exiting-stance", attackInputObserved, in input);
                if (wantsAttack)
                {
                    _restartQueued = true;
                    _player.InputPipeline.ConsumePrimaryAttackPressed();
                }

                if (!_player.RuntimeData.Override.IsActive)
                {
                    _isExitingStance = false;
                    if (_restartQueued)
                    {
                        _restartQueued = false;
                        TriggerEnterStanceOrAttack();
                    }
                }
                return;
            }

            if (_isAttacking)
            {
                TraceAttackGate("attacking", attackInputObserved, in input);
                float now = Time.time;

                if (wantsAttack && now < _comboWindowOpenTime)
                {
                    _inputBuffered = true;
                    _player.InputPipeline.ConsumePrimaryAttackPressed();
                }

                bool inWindow = now >= _comboWindowOpenTime && now <= _comboWindowCloseTime;
                if (inWindow && (_inputBuffered || wantsAttack))
                {
                    _inputBuffered = false;
                    int nextComboIndex = _comboIndex < _config.ComboSequence.Length ? _comboIndex : 0;
                    QueueAttackTransition(nextComboIndex);
                    _player.InputPipeline.ConsumePrimaryAttackPressed();
                }

                if (TryExecuteQueuedAttack())
                {
                    return;
                }

                if (now > _comboWindowCloseTime)
                {
                    TriggerExitStance();
                }

                return;
            }

            if (wantsAttack)
            {
                TraceAttackGate("trigger-enter-or-attack", true, in input);
                TriggerEnterStanceOrAttack();
            }
        }

        public void OnForceUnequip()
        {
            if (CurrentEquipSlot == EquipmentSlot.OffHand)
            {
                PostSystem.Instance.Off("Fists.AttackStart", _onAttackStart);
                PostSystem.Instance.Off("Fists.AttackEnd", _onAttackEnd);
            }

            ResetComboState();
        }

        private void TriggerEnterStanceOrAttack()
        {
            var stance = _config.EnterStanceAnim;
            if (stance != null && stance.Clip != null)
            {
                _isEnteringStance = true;
                _stanceAttackBridged = false;
                _currentStanceTransition = stance;
                _currentStanceStartTime = Time.time;
                _player.InputPipeline.ConsumePrimaryAttackPressed();
                var req = new ActionRequest(stance.Clip, _config.ComboPriority, applyGravity: true) { Transition = stance };
                _player.RequestOverride(in req, flushImmediately: true);
                _player.AnimFacade.SetOverrideOnEndCallback(OnStanceEnd);
                return;
            }

            TriggerAttack();
        }

        private void OnStanceEnd()
        {
            TryBridgeFromStance();
        }

        private void TriggerExitStance()
        {
            _comboIndex = 0;
            _isAttacking = false;
            _inputBuffered = false;
            _comboWindowOpenTime = 0f;
            _comboWindowCloseTime = 0f;
            _queuedAttackIndex = -1;
            _currentAttackTransition = null;
            _currentStanceTransition = null;
            _currentAttackStartTime = 0f;
            _currentStanceStartTime = 0f;
            CloseHitWindow();

            // If the full-body override has already ended and locomotion has resumed,
            // playing an extra exit stance here causes a delayed hitch after movement
            // has already continued.
            if (!_player.RuntimeData.Override.IsActive &&
                _player.RuntimeData.CurrentLocomotionState != LocomotionState.Idle)
            {
                _isExitingStance = false;
                if (_restartQueued)
                {
                    _restartQueued = false;
                    TriggerEnterStanceOrAttack();
                }
                return;
            }

            var exit = _config.ExitStanceAnim;
            if (exit != null && exit.Clip != null)
            {
                _isExitingStance = true;
                var req = new ActionRequest(exit.Clip, _config.ComboPriority, applyGravity: true) { Transition = exit };
                _player.RequestOverride(in req, flushImmediately: true);
            }
            else
            {
                _isExitingStance = false;
                if (_restartQueued)
                {
                    _restartQueued = false;
                    TriggerEnterStanceOrAttack();
                }
            }
        }

        private void TriggerAttack()
        {
            int currentComboIndex = _comboIndex;
            var transition = currentComboIndex < _config.ComboSequence.Length
                ? _config.ComboSequence[currentComboIndex]
                : null;

            if (transition == null || transition.Clip == null)
            {
                Debug.LogWarning($"[Fists] TriggerAttack: combo segment {currentComboIndex} has no clip.");
                ResetComboState();
                return;
            }

            var clip = transition.Clip;
            var attackHand = _config.GetAttackHand(currentComboIndex);
            _currentAttackTransition = transition;
            _currentStanceTransition = null;
            _queuedAttackIndex = -1;
            _currentAttackStartTime = Time.time;
            _currentStanceStartTime = 0f;

            float speed = transition.Speed > 0f ? transition.Speed : 1f;
            float normStart = float.IsNaN(transition.NormalizedStartTime) ? 0f : transition.NormalizedStartTime;
            float normEnd = transition.Events.GetRealNormalizedEndTime(speed);
            float actualDuration = clip.length * (normEnd - normStart) / speed;

            _comboIndex = currentComboIndex + 1;
            _isAttacking = true;
            _inputBuffered = false;
            _restartQueued = false;
            _comboWindowOpenTime = Time.time + actualDuration * _config.ComboWindowStart;
            _comboWindowCloseTime = Time.time + actualDuration + _config.ComboLateBuffer;

            CloseHitWindow();
            _activeAttackContext = new FistsAttackWindowContext
            {
                AttackHand = attackHand,
                ComboIndex = currentComboIndex,
            };

            if (CurrentEquipSlot == EquipmentSlot.MainHand)
            {
                PostSystem.Instance.Send("Fists.AttackStart", _activeAttackContext);
            }

            if (attackHand == GetCurrentAttackHand())
            {
                _hitbox?.Activate(_activeAttackContext.SharedHitSet, true);
                Debug.Log($"[Fists] Activate hit window slot={CurrentEquipSlot} combo={currentComboIndex} hand={attackHand} object={name}");
            }

            _player.InputPipeline.ConsumePrimaryAttackPressed();
            var req = new ActionRequest(clip, _config.ComboPriority, applyGravity: true) { Transition = transition };
            _player.RequestOverride(in req, flushImmediately: true);
        }

        private void OnRemoteAttackStart(object data)
        {
            var context = data as FistsAttackWindowContext;
            if (context == null)
            {
                CloseHitWindow();
                return;
            }

            _activeAttackContext = context;
            if (context.AttackHand != GetCurrentAttackHand())
            {
                CloseHitWindow();
                return;
            }

            _hitbox?.Activate(context.SharedHitSet, true);
            Debug.Log($"[Fists] Activate hit window slot={CurrentEquipSlot} combo={context.ComboIndex} hand={context.AttackHand} object={name}");
        }

        private void ResetComboState()
        {
            _comboIndex = 0;
            _isAttacking = false;
            _isEnteringStance = false;
            _isExitingStance = false;
            _inputBuffered = false;
            _restartQueued = false;
            _stanceAttackBridged = false;
            _queuedAttackIndex = -1;
            _currentAttackTransition = null;
            _currentStanceTransition = null;
            _currentAttackStartTime = 0f;
            _currentStanceStartTime = 0f;
            _comboWindowOpenTime = 0f;
            _comboWindowCloseTime = 0f;
            CloseHitWindow();

            if (CurrentEquipSlot == EquipmentSlot.MainHand)
            {
                PostSystem.Instance.Send("Fists.AttackEnd", null);
            }
        }

        private void CloseHitWindow()
        {
            _hitbox?.Deactivate();
            _activeAttackContext = null;
            Debug.Log($"[Fists] Deactivate hit window slot={CurrentEquipSlot} object={name}");
        }

        private FistsAttackHand GetCurrentAttackHand()
        {
            return CurrentEquipSlot == EquipmentSlot.OffHand
                ? FistsAttackHand.OffHand
                : FistsAttackHand.MainHand;
        }

        private void TraceAttackGate(string reason, bool attackInputObserved, in ProcessedInputData input)
        {
            if (!AttackTrace || !attackInputObserved)
            {
                return;
            }

            string fullBodyState = _player?.StateMachine?.CurrentState?.GetType().Name ?? "null";
            string upperBodyState = _player?.UpperBodyCtrl?.StateMachine?.CurrentState?.GetType().Name ?? "null";
            Debug.Log(
                $"[FistsTrace] reason={reason} slot={CurrentEquipSlot} state={fullBodyState}/{upperBodyState} " +
                $"loco={_player.RuntimeData.CurrentLocomotionState} grounded={_player.RuntimeData.IsGrounded} " +
                $"override={_player.RuntimeData.Override.IsActive} wantsAttack={_player.RuntimeData.WantsToPrimaryAction} " +
                $"pressed={input.PrimaryAttackPressed} held={input.PrimaryAttackHeld} sprintHeld={input.SprintHeld} " +
                $"jumpHeld={input.JumpHeld} entering={_isEnteringStance} exiting={_isExitingStance} attacking={_isAttacking} restartQueued={_restartQueued} " +
                $"ignoreUntil={_ignoreAttackUntil:F3} now={Time.time:F3}");
        }

        public void OnSpawned()
        {
            ResetComboState();
            _ignoreAttackUntil = Time.time + 0.12f;
        }

        private static float GetEffectiveDuration(ClipTransition transition)
        {
            if (transition == null || transition.Clip == null)
            {
                return 0f;
            }

            float speed = transition.Speed > 0f ? transition.Speed : 1f;
            float normStart = float.IsNaN(transition.NormalizedStartTime) ? 0f : transition.NormalizedStartTime;
            float normEnd = transition.Events.GetRealNormalizedEndTime(speed);
            return transition.Clip.length * Mathf.Max(0f, normEnd - normStart) / speed;
        }

        private bool TryBridgeFromStance()
        {
            if (!_isEnteringStance || _stanceAttackBridged)
            {
                return false;
            }

            _stanceAttackBridged = true;
            _isEnteringStance = false;
            _inputBuffered = false;
            TriggerAttack();
            return true;
        }

        private void QueueAttackTransition(int nextComboIndex)
        {
            if (_config.ComboSequence == null || nextComboIndex < 0 || nextComboIndex >= _config.ComboSequence.Length)
            {
                return;
            }

            if (_queuedAttackIndex == nextComboIndex)
            {
                return;
            }

            _queuedAttackIndex = nextComboIndex;
        }

        private bool TryAdvanceStanceBridge()
        {
            if (!_isEnteringStance || _stanceAttackBridged || _config.ComboSequence == null || _config.ComboSequence.Length == 0)
            {
                return false;
            }

            ClipTransition firstAttack = _config.ComboSequence[Mathf.Clamp(_comboIndex, 0, _config.ComboSequence.Length - 1)];
            if (!ShouldBridgeNow(_currentStanceTransition, firstAttack, _currentStanceStartTime))
            {
                return false;
            }

            return TryBridgeFromStance();
        }

        private bool TryExecuteQueuedAttack()
        {
            if (!_isAttacking || _queuedAttackIndex < 0 || _config.ComboSequence == null || _queuedAttackIndex >= _config.ComboSequence.Length)
            {
                return false;
            }

            ClipTransition nextTransition = _config.ComboSequence[_queuedAttackIndex];
            if (!ShouldBridgeNow(_currentAttackTransition, nextTransition, _currentAttackStartTime))
            {
                return false;
            }

            int nextComboIndex = _queuedAttackIndex;
            _comboIndex = nextComboIndex;
            _queuedAttackIndex = -1;
            TriggerAttack();
            return true;
        }

        private bool ShouldBridgeNow(ClipTransition current, ClipTransition next, float segmentStartTime)
        {
            if (current == null || current.Clip == null || next == null || next.Clip == null)
            {
                return false;
            }

            float currentSpeed = current.Speed > 0f ? current.Speed : 1f;
            float currentNormStart = float.IsNaN(current.NormalizedStartTime) ? 0f : current.NormalizedStartTime;
            float currentNormEnd = current.Events.GetRealNormalizedEndTime(currentSpeed);
            float clipLength = current.Clip.length;
            if (clipLength <= 0f)
            {
                return false;
            }

            float currentDuration = clipLength * (currentNormEnd - currentNormStart) / currentSpeed;
            if (currentDuration <= 0f)
            {
                return false;
            }

            float nextFade = next.FadeDuration > 0f ? next.FadeDuration : 0f;
            float bridgeTime = segmentStartTime + Mathf.Max(0f, currentDuration - nextFade);
            return Time.time >= bridgeTime - 0.0001f;
        }

        public void OnDespawned() { }
    }
}
