using Animancer;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BBBNexus
{
    public class FistsBehaviour : MonoBehaviour, IHoldableItem, IPoolable
    {
        private const bool AttackTrace = false;
        private const int AutoTargetMaxOverlapCount = 32;
        private static readonly Collider[] AutoTargetOverlaps = new Collider[AutoTargetMaxOverlapCount];

        private sealed class FistsAttackWindowContext
        {
            public readonly HashSet<IDamageable> SharedHitSet = new HashSet<IDamageable>();
            public BBBCharacterController Owner;
            public FistsAttackHand AttackHand;
            public int ComboIndex;
            public float AlignmentWindowStartTime;
            public float AlignmentWindowEndTime;

            public float[] WindowStartTimes = System.Array.Empty<float>();
            public float[] WindowEndTimes = System.Array.Empty<float>();

            public float WindowStartTime
            {
                get => WindowStartTimes.Length > 0 ? WindowStartTimes[0] : 0f;
                set { if (WindowStartTimes.Length > 0) WindowStartTimes[0] = value; }
            }
            public float WindowEndTime
            {
                get => WindowEndTimes.Length > 0 ? WindowEndTimes[0] : 0f;
                set { if (WindowEndTimes.Length > 0) WindowEndTimes[0] = value; }
            }

            public void SetWindowCount(int count)
            {
                count = Mathf.Max(1, count);
                WindowStartTimes = new float[count];
                WindowEndTimes = new float[count];
            }

            public bool IsInAnyDamageWindow(float time)
            {
                for (int i = 0; i < WindowStartTimes.Length; i++)
                {
                    if (time >= WindowStartTimes[i] && time <= WindowEndTimes[i])
                        return true;
                }
                return false;
            }
        }

        private readonly struct AutoTargetCandidate
        {
            public readonly Transform TargetTransform;
            public readonly float Cost;
            public readonly float Distance;
            public readonly float TargetYaw;
            public readonly float StepDistance;

            public AutoTargetCandidate(Transform targetTransform, float cost, float distance, float targetYaw, float stepDistance)
            {
                TargetTransform = targetTransform;
                Cost = cost;
                Distance = distance;
                TargetYaw = targetYaw;
                StepDistance = stepDistance;
            }
        }

        private BBBCharacterController _player;
        private WeaponSO _config;
        private ItemInstance _instance;
        private FistHitbox _hitbox;

        public EquipmentSlot CurrentEquipSlot { get; set; }

        private Action<object> _onAttackStart;
        private Action<object> _onAttackEnd;
        private float _ignoreAttackUntil;
        private FistsAttackWindowContext _activeAttackContext;
        private bool _isHitWindowOpen;
        private Transform _autoTargetTransform;

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
            _config = instanceData?.GetSODataAs<WeaponSO>();

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
                _hitbox.SetAttackGeometryDefinition(_config?.AttackGeometry);
                _hitbox.HitRegistered -= OnHitRegistered;
                _hitbox.HitRegistered += OnHitRegistered;

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

            if (CurrentEquipSlot == EquipmentSlot.MainHand && _config.CameraPreset != null)
            {
                var camPreset = (ResolveIsSprinting(_player) ? _config.SprintCameraPreset : null) ?? _config.CameraPreset;
                _player.RuntimeData.CameraExpression = camPreset.ToExpression();
            }

            UpdateHitWindowState();
            UpdateAutoTargeting();

            ref readonly ProcessedInputData input = ref _player.InputPipeline.Current.currentFrameData.Processed;
            bool wantsAttack = _player.RuntimeData.WantsToPrimaryAction;
            bool attackInputObserved = wantsAttack || input.PrimaryAttackHeld || input.PrimaryAttackPressed;

            if (_player.CharacterArbiter != null && _player.CharacterArbiter.IsUnderStatusControl())
            {
                if (_isAttacking || _isEnteringStance || _isExitingStance || _activeAttackContext != null)
                    ResetComboState();

                TraceAttackGate("status-effect", attackInputObserved, in input);
                return;
            }

            if (_player.CharacterArbiter != null && _player.CharacterArbiter.IsActionBlocked())
            {
                if (_isAttacking || _isEnteringStance || _isExitingStance || _activeAttackContext != null)
                    ResetComboState();

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
            if (_hitbox != null)
            {
                _hitbox.HitRegistered -= OnHitRegistered;
            }

            if (CurrentEquipSlot == EquipmentSlot.OffHand)
            {
                PostSystem.Instance.Off("Fists.AttackStart", _onAttackStart);
                PostSystem.Instance.Off("Fists.AttackEnd", _onAttackEnd);
            }

            ResetComboState();
        }

        private void TriggerEnterStanceOrAttack()
        {
            var stance = HasEnterStance() ? _config.EnterStanceAnim : null;
            if (stance != null)
            {
                WeaponAudioUtil.PlayAt(_config.MeleeAudio.EnterStanceSounds, transform.position);
                _isEnteringStance = true;
                _stanceAttackBridged = false;
                _currentStanceTransition = stance;
                _currentStanceStartTime = Time.time;
                _player.InputPipeline.ConsumePrimaryAttackPressed();
                var req = new ActionRequest(stance.Clip, _config.ComboPriority, applyGravity: true) { Transition = stance };
                _player.RequestOverride(in req, flushImmediately: true);
                _player.AnimFacade?.SetOverrideOnEndCallback(OnStanceEnd);
                return;
            }

            _player.AnimFacade?.ClearOverrideOnEndCallback();
            _isEnteringStance = false;
            _stanceAttackBridged = true;
            _currentStanceTransition = null;
            _currentStanceStartTime = 0f;
            TriggerAttack();
        }

        private void OnStanceEnd()
        {
            TryBridgeFromStance();
        }

        private void TriggerExitStance()
        {
            WeaponAudioUtil.PlayAt(_config.MeleeAudio.ExitStanceSounds, transform.position);
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

            var exit = GetExitTransition();
            if (exit != null && UsesLockExit())
            {
                _isExitingStance = true;
                var req = new ActionRequest(exit.Clip, _config.ComboPriority, applyGravity: true) { Transition = exit };
                _player.RequestOverride(in req, flushImmediately: true);
            }
            else
            {
                // VisualExit 的语义是“纯视觉收尾，不保留动作锁”。
                // 所以进入 visual exit 的瞬间就应该释放 ActionControl / Override，
                // 而不是等 visual clip 播完才释放，否则它本质上还是 lock exit。
                _player.ArbiterPipeline?.Action?.CancelActiveAction(stopAnimation: false);
                _player.AnimFacade?.ClearOverrideOnEndCallback();
                _isExitingStance = false;
                PlayVisualExitIfAny();
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
            float actualDuration = clip.length * (transition.Events.GetRealNormalizedEndTime(speed) - normStart) / speed;

            PlaySwingSound(currentComboIndex);

            _comboIndex = currentComboIndex + 1;
            _isAttacking = true;
            _inputBuffered = false;
            _restartQueued = false;
            _comboWindowOpenTime = Time.time + actualDuration * _config.ComboWindowStart;
            _comboWindowCloseTime = Time.time + actualDuration + _config.ComboLateBuffer;

            CloseHitWindow();
            _activeAttackContext = new FistsAttackWindowContext
            {
                Owner = _player,
                AttackHand = attackHand,
                ComboIndex = currentComboIndex,
                AlignmentWindowStartTime = _currentAttackStartTime,
                AlignmentWindowEndTime = _currentAttackStartTime,
            };
            _activeAttackContext.SetWindowCount(1);
            _activeAttackContext.WindowStartTime = _currentAttackStartTime;
            _activeAttackContext.WindowEndTime = _currentAttackStartTime + actualDuration;

            float dominantEnd = ResolveDominantEndTime(actualDuration, currentComboIndex);
            ApplyDamageWindowTiming(_activeAttackContext, actualDuration, currentComboIndex);
            ApplyAlignmentWindowTiming(_activeAttackContext, actualDuration, currentComboIndex);

            // 注册调试信息供 Gizmo 使用
            AttackWindowDebugService.RegisterWindow(
                _currentAttackStartTime,
                _currentAttackStartTime + actualDuration,
                _activeAttackContext.WindowStartTimes,
                _activeAttackContext.WindowEndTimes,
                _activeAttackContext.AlignmentWindowStartTime,
                _activeAttackContext.AlignmentWindowEndTime,
                currentComboIndex,
                actualDuration,
                dominantEnd);

            if (CurrentEquipSlot == EquipmentSlot.MainHand)
            {
                PostSystem.Instance.Send("Fists.AttackStart", _activeAttackContext);
            }

            _player.InputPipeline.ConsumePrimaryAttackPressed();
            var req = new ActionRequest(clip, _config.ComboPriority, applyGravity: true) { Transition = transition };
            _player.RequestOverride(in req, flushImmediately: true);
        }

        private void PlaySwingSound(int comboIndex)
        {
            if (_config == null)
            {
                return;
            }

            var swingClip = WeaponAudioUtil.PickCombo(_config.MeleeAudio.ComboSwingSounds, comboIndex);
            if (swingClip != null)
            {
                WeaponAudioUtil.PlayAt(swingClip, transform.position);
                return;
            }

            // 资源还没补齐时，先退回到已有的起手音，避免挥空完全静音。
            WeaponAudioUtil.PlayAt(_config.MeleeAudio.EnterStanceSounds, transform.position);
        }

        private void OnRemoteAttackStart(object data)
        {
            var context = data as FistsAttackWindowContext;
            if (context == null || context.Owner != _player)
            {
                CloseHitWindow();
                return;
            }

            _activeAttackContext = context;
            if (!MatchesCurrentHand(context.AttackHand))
            {
                _hitbox?.Deactivate();
                return;
            }
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
            _autoTargetTransform = null;
            CloseHitWindow();

            if (CurrentEquipSlot == EquipmentSlot.MainHand)
            {
                PostSystem.Instance.Send("Fists.AttackEnd", null);
            }
        }

        private void CloseHitWindow()
        {
            _hitbox?.Deactivate();
            _isHitWindowOpen = false;
            _activeAttackContext = null;
            _autoTargetTransform = null;
            AttackWindowDebugService.ClearWindow();
        }

        private void OnHitRegistered(Collider other, IDamageable damageable, DamageRequest request)
        {
            if (_config == null)
            {
                return;
            }

            WeaponAudioUtil.PlayAt(_config.MeleeAudio.HitSounds, request.HitPoint);

            var targetController = damageable as BBBCharacterController ?? other.GetComponentInParent<BBBCharacterController>();
            if (targetController != null && targetController != _player)
            {
                if (_config.AttackerHitStopDuration > 0f)
                {
                    HitStopService.Instance?.Request(new HitStopRequest(_player, _config.AttackerHitStopDuration, targetController));
                }
            }
        }

        private void UpdateHitWindowState()
        {
            if (_activeAttackContext == null)
                return;

            if (!MatchesCurrentHand(_activeAttackContext.AttackHand))
            {
                if (_isHitWindowOpen)
                {
                    _hitbox?.Deactivate();
                    _isHitWindowOpen = false;
                }

                return;
            }

            float now = Time.time;
            bool shouldBeActive = _activeAttackContext.IsInAnyDamageWindow(now);
            if (shouldBeActive == _isHitWindowOpen)
                return;

            _isHitWindowOpen = shouldBeActive;
            if (shouldBeActive)
                _hitbox?.Activate(_activeAttackContext.SharedHitSet, true);
            else
                _hitbox?.Deactivate();
        }

        private void UpdateAutoTargeting()
        {
            if (_player == null || _config == null || _activeAttackContext == null)
            {
                _autoTargetTransform = null;
                return;
            }

            float now = Time.time;
            if (now < _activeAttackContext.AlignmentWindowStartTime || now > _activeAttackContext.AlignmentWindowEndTime)
            {
                _autoTargetTransform = null;
                return;
            }

            float remainingTime = Mathf.Max(0f, _activeAttackContext.AlignmentWindowEndTime - now);
            if (remainingTime <= 0f || _config.AutoTargetTurnSpeed <= 0f)
            {
                _autoTargetTransform = null;
                return;
            }

            AttackClipGeometryClipDefinition attackClipGeometry = GetCurrentAttackClipGeometry();
            float searchRange = ComputeAutoTargetSearchRange(attackClipGeometry);
            if (searchRange <= 0f)
            {
                _autoTargetTransform = null;
                return;
            }

            float maxStepDistance = ComputeMaxStepDistance(remainingTime);
            float candidateSearchRange = searchRange + maxStepDistance;
            if (!TryFindBestAutoTargetCandidate(_player, attackClipGeometry, candidateSearchRange, remainingTime, _config.AutoTargetTurnSpeed, maxStepDistance, out var candidate))
            {
                _autoTargetTransform = null;
                return;
            }

            _autoTargetTransform = candidate.TargetTransform;
            float angleDelta = Mathf.Abs(Mathf.DeltaAngle(_player.transform.eulerAngles.y, candidate.TargetYaw));
            float smoothTime = _config.AutoTargetTurnSpeed > 0f
                ? Mathf.Clamp(angleDelta / _config.AutoTargetTurnSpeed, 0.01f, remainingTime)
                : 0.01f;
            _player.MotionDriver.RequestYaw(candidate.TargetYaw, smoothTime);
            ApplyStepCompensation(candidate, remainingTime);
        }

        private AttackClipGeometryClipDefinition GetCurrentAttackClipGeometry()
        {
            if (_config == null || _activeAttackContext == null)
            {
                return null;
            }

            AttackClipGeometryDefinition definition = _config.AttackGeometry;
            return definition?.GetClip(_activeAttackContext.ComboIndex);
        }

        private float ComputeAutoTargetSearchRange(AttackClipGeometryClipDefinition attackClipGeometry)
        {
            if (attackClipGeometry != null)
            {
                float geometryReach = AttackHitPredictionSolver.EstimateHorizontalReach(attackClipGeometry);
                float controllerPadding = _player.CharController != null ? Mathf.Max(_player.CharController.radius, 0.2f) : 0.2f;
                return Mathf.Max(geometryReach + controllerPadding, 0.5f);
            }

            if (_hitbox != null && _hitbox.TryGetQueryBox(out Vector3 center, out Vector3 halfExtents, out _))
            {
                float ownerToCenter = Vector3.Distance(_player.transform.position, center);
                float hitboxReach = halfExtents.magnitude;
                float controllerPadding = _player.CharController != null ? Mathf.Max(_player.CharController.radius, 0.2f) : 0.2f;
                return ownerToCenter + hitboxReach + controllerPadding;
            }

            return _player.CharController != null
                ? Mathf.Max(1f, _player.CharController.radius + 1f)
                : 1f;
        }

        private float ComputeMaxStepDistance(float remainingAlignmentTime)
        {
            if (_player == null || _config == null || remainingAlignmentTime <= 0f)
            {
                return 0f;
            }

            float stepSpeed = ResolveCurrentStepSpeed();
            return Mathf.Max(0f, remainingAlignmentTime * stepSpeed);
        }

        private float ResolveCurrentStepSpeed()
        {
            if (_player == null || _player.Config == null || _player.Config.Core == null)
            {
                return 0f;
            }

            bool isTactical = _player.RuntimeData != null && _player.RuntimeData.IsTacticalStance;
            LocomotionState locomotionState = _player.RuntimeData != null
                ? _player.RuntimeData.CurrentLocomotionState
                : LocomotionState.Idle;

            if (isTactical && _player.Config.TacticalMotionBase != null)
            {
                return locomotionState switch
                {
                    LocomotionState.Walk => _player.Config.TacticalMotionBase.AimWalkSpeed,
                    LocomotionState.Jog => _player.Config.TacticalMotionBase.AimJogSpeed,
                    LocomotionState.Sprint => _player.Config.TacticalMotionBase.AimSprintSpeed,
                    _ => _player.Config.TacticalMotionBase.AimWalkSpeed,
                };
            }

            return locomotionState switch
            {
                LocomotionState.Walk => _player.Config.Core.WalkSpeed,
                LocomotionState.Jog => _player.Config.Core.JogSpeed,
                LocomotionState.Sprint => _player.Config.Core.SprintSpeed,
                _ => _player.Config.Core.JogSpeed,
            };
        }

        private void ApplyStepCompensation(in AutoTargetCandidate candidate, float remainingAlignmentTime)
        {
            if (_player?.MotionDriver == null || remainingAlignmentTime <= 0f || candidate.StepDistance <= 0f)
            {
                return;
            }

            float stepThisFrame = Mathf.Min(
                candidate.StepDistance,
                candidate.StepDistance * Mathf.Clamp01(Time.deltaTime / remainingAlignmentTime));

            if (stepThisFrame <= 0.0001f)
            {
                return;
            }

            Vector3 displacement = Quaternion.Euler(0f, candidate.TargetYaw, 0f) * Vector3.forward * stepThisFrame;
            _player.MotionDriver.RequestHorizontalDisplacement(displacement);
        }

        private static bool TryFindBestAutoTargetCandidate(
            BBBCharacterController owner,
            AttackClipGeometryClipDefinition attackClipGeometry,
            float searchRange,
            float remainingAlignmentTime,
            float turnSpeed,
            float maxStepDistance,
            out AutoTargetCandidate candidate)
        {
            candidate = default;
            if (owner == null)
            {
                return false;
            }

            Vector3 origin = owner.transform.position;
            int count = Physics.OverlapSphereNonAlloc(origin, searchRange, AutoTargetOverlaps, ~0, QueryTriggerInteraction.Collide);
            if (count <= 0)
            {
                return false;
            }

            var seenRoots = new HashSet<Transform>();
            bool found = false;
            float bestCost = float.MaxValue;
            float bestDistance = float.MaxValue;
            AutoTargetCandidate best = default;

            for (int i = 0; i < count; i++)
            {
                Collider other = AutoTargetOverlaps[i];
                AutoTargetOverlaps[i] = null;
                if (other == null || other.transform.IsChildOf(owner.transform))
                {
                    continue;
                }

                var damageable = other.GetComponentInParent<IDamageable>();
                if (damageable == null)
                {
                    continue;
                }

                Transform root = other.transform.root;
                if (root == null || !seenRoots.Add(root))
                {
                    continue;
                }

                Vector3 targetPoint = other.bounds.center;
                Vector3 toTarget = targetPoint - origin;
                toTarget.y = 0f;
                float distance = toTarget.magnitude;
                if (distance <= 0.001f || distance > searchRange)
                {
                    continue;
                }

                if (attackClipGeometry == null)
                {
                    Vector3 direction = toTarget / distance;
                    float targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                    float angleCost = Mathf.Abs(Mathf.DeltaAngle(owner.transform.eulerAngles.y, targetYaw)) * 0.02f;
                    if (!found || angleCost < bestCost - 0.0001f || (Mathf.Abs(angleCost - bestCost) <= 0.0001f && distance < bestDistance))
                    {
                        found = true;
                        bestCost = angleCost;
                        bestDistance = distance;
                        best = new AutoTargetCandidate(root, angleCost, distance, targetYaw, 0f);
                    }

                    continue;
                }

                var solveInput = new AttackHitPredictionSolver.SolveInput(
                    attackClipGeometry,
                    owner.transform,
                    other.bounds,
                    remainingAlignmentTime,
                    turnSpeed,
                    maxStepDistance);

                if (!AttackHitPredictionSolver.TrySolve(in solveInput, out var solveResult))
                {
                    continue;
                }

                if (!found || solveResult.BestCost < bestCost - 0.0001f || (Mathf.Abs(solveResult.BestCost - bestCost) <= 0.0001f && distance < bestDistance))
                {
                    found = true;
                    bestCost = solveResult.BestCost;
                    bestDistance = distance;
                    best = new AutoTargetCandidate(root, solveResult.BestCost, distance, solveResult.BestWorldYaw, solveResult.BestStepDistance);
                }
            }

            candidate = best;
            return found;
        }

        private void ApplyDamageWindowTiming(
            FistsAttackWindowContext context,
            float actualDuration,
            int comboIndex)
        {
            FistsDamageWindowSidecar sidecar = _config.GetDamageWindow(comboIndex);
            float dominantStart = 0f;
            float dominantEnd = ResolveDominantEndTime(actualDuration, comboIndex);
            if (!sidecar.Enabled)
            {
                context.SetWindowCount(1);
                context.WindowStartTime = _currentAttackStartTime + dominantStart;
                context.WindowEndTime = _currentAttackStartTime + dominantEnd;
                return;
            }

            int windowCount = sidecar.WindowCount;
            context.SetWindowCount(windowCount);

            for (int i = 0; i < windowCount; i++)
            {
                sidecar.GetWindow(i, out float start, out float end);
                context.WindowStartTimes[i] = _currentAttackStartTime + Mathf.LerpUnclamped(dominantStart, dominantEnd, start);
                context.WindowEndTimes[i] = _currentAttackStartTime + Mathf.LerpUnclamped(dominantStart, dominantEnd, end);
            }
        }

        private void ApplyAlignmentWindowTiming(
            FistsAttackWindowContext context,
            float actualDuration,
            int comboIndex)
        {
            FistsAlignmentWindowSidecar sidecar = _config.GetAlignmentWindow(comboIndex);
            if (!sidecar.Enabled)
            {
                context.AlignmentWindowStartTime = _currentAttackStartTime;
                context.AlignmentWindowEndTime = _currentAttackStartTime;
                return;
            }

            float dominantStart = 0f;
            float dominantEnd = ResolveDominantEndTime(actualDuration, comboIndex);
            float startNormalized = Mathf.Clamp01(sidecar.StartNormalized);
            float endNormalized = Mathf.Clamp01(sidecar.EndNormalized);
            if (endNormalized < startNormalized)
            {
                (startNormalized, endNormalized) = (endNormalized, startNormalized);
            }

            float localStart = Mathf.LerpUnclamped(dominantStart, dominantEnd, startNormalized);
            float localEnd = Mathf.LerpUnclamped(dominantStart, dominantEnd, endNormalized);
            context.AlignmentWindowStartTime = _currentAttackStartTime + localStart;
            context.AlignmentWindowEndTime = _currentAttackStartTime + localEnd;
        }

        private float ResolveDominantEndTime(float actualDuration, int _)
        {
            // Damage/alignment sidecars are authored against the transition's effective
            // playback span (start->end), not "start->(end-nextFade)".
            // Keep bridge timing fade-aware elsewhere, but keep window timing aligned with preview.
            return Mathf.Max(0f, actualDuration);
        }

        private float ResolveOutgoingFadeDuration(int comboIndex)
        {
            if (_config?.ComboSequence == null || _config.ComboSequence.Length == 0)
                return 0f;

            int nextIndex = comboIndex + 1;
            if (nextIndex >= _config.ComboSequence.Length)
                nextIndex = 0;

            ClipTransition next = _config.ComboSequence[nextIndex];
            return next != null ? Mathf.Max(0f, next.FadeDuration) : 0f;
        }

        private FistsAttackHand GetCurrentAttackHand()
        {
            return CurrentEquipSlot == EquipmentSlot.OffHand
                ? FistsAttackHand.OffHand
                : FistsAttackHand.MainHand;
        }

        private bool MatchesCurrentHand(FistsAttackHand attackHand)
        {
            if (attackHand == FistsAttackHand.BothHands)
                return true;

            return attackHand == GetCurrentAttackHand();
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

        private bool HasEnterStance()
        {
            return _config != null && _config.EnterStanceAnim != null && _config.EnterStanceAnim.Clip != null;
        }

        private bool HasExitStance()
        {
            return GetExitTransition() != null;
        }

        private ClipTransition GetExitTransition()
        {
            if (_config == null)
            {
                return null;
            }

            if (_config.ExitStanceAnim != null && _config.ExitStanceAnim.Clip != null)
            {
                return _config.ExitStanceAnim;
            }

            return null;
        }

        private bool UsesLockExit()
        {
            return _config != null && _config.ExitUsesLock;
        }

        private void PlayVisualExitIfAny()
        {
            var visualExit = GetExitTransition();
            if (visualExit == null)
            {
                _player?.AnimFacade?.StopFullBodyAction();
                return;
            }

            _player.AnimFacade?.PlayFullBodyActionTransition(visualExit);
            _player.AnimFacade?.SetOnEndCallback(() =>
            {
                _player?.AnimFacade?.StopFullBodyAction();
                _player?.AnimFacade?.ClearOnEndCallback(0);
            }, 0);
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

        private static bool ResolveIsSprinting(BBBCharacterController player)
            => player.RuntimeData.CurrentLocomotionState == LocomotionState.Sprint;
    }
}
