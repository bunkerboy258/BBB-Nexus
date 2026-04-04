using Animancer;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 通用武器行为。对应 WeaponSO。
    /// HasMelee == true 时开启连招近战逻辑（来自 FistsBehaviour）。
    /// HasRanged == true 时开启射击/换弹逻辑（来自 PistolBehaviour）。
    /// 两者均为 true 时为双模式武器：左键近战，右键瞄准/射击。
    /// </summary>
    public class WeaponBehaviour : MonoBehaviour, IHoldableItem, IPoolable, IManualReloadable, IAiReloadable
    {
        // ─────────────────────────────────────────────────────
        // 近战常量与辅助类型
        // ─────────────────────────────────────────────────────

        private const bool AttackTrace = false;
        private const int AutoTargetMaxOverlapCount = 32;
        private static readonly Collider[] AutoTargetOverlaps = new Collider[AutoTargetMaxOverlapCount];

        private sealed class MeleeAttackWindowContext
        {
            public readonly HashSet<IDamageable> SharedHitSet = new HashSet<IDamageable>();
            public BBBCharacterController Owner;
            public FistsAttackHand AttackHand;
            public int ComboIndex;
            public float AlignmentWindowStartTime;
            public float AlignmentWindowEndTime;

            // 多伤害窗口支持
            public float[] WindowStartTimes = System.Array.Empty<float>();
            public float[] WindowEndTimes = System.Array.Empty<float>();

            // 便捷属性：第一个窗口（向后兼容）
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

            public AutoTargetCandidate(Transform t, float cost, float dist, float yaw, float step)
            {
                TargetTransform = t; Cost = cost; Distance = dist; TargetYaw = yaw; StepDistance = step;
            }
        }

        // ─────────────────────────────────────────────────────
        // 远程常量
        // ─────────────────────────────────────────────────────

        private const float DefaultHitScanRange = 80f;
        private const float DefaultDamageAmount = 10f;
        private const float DefaultTracerDuration = 0.06f;

        // ─────────────────────────────────────────────────────
        // Inspector 挂点（远程用）
        // ─────────────────────────────────────────────────────

        [Header("--- 远程挂点 ---")]
        [Tooltip("枪口瞄准参考点（枪口空物体，Z 轴朝出弹方向）")]
        [SerializeField] private Transform _muzzle;

        [Tooltip("可选的曳光弹材质；为空则使用默认精灵材质")]
        [SerializeField] private Material _tracerMaterial;

        [Header("--- Debug ---")]
        [Tooltip("输出前摇对齐窗口与自动锁定求解日志。")]
        [SerializeField] private bool _debugAutoTarget;

        // ─────────────────────────────────────────────────────
        // 共享状态
        // ─────────────────────────────────────────────────────

        private BBBCharacterController _player;
        private WeaponSO _config;
        private ItemInstance _instance;

        public EquipmentSlot CurrentEquipSlot { get; set; }

        // ─────────────────────────────────────────────────────
        // 近战状态
        // ─────────────────────────────────────────────────────

        private FistHitbox _hitbox;
        private Action<object> _onAttackStart;
        private Action<object> _onAttackEnd;
        private float _ignoreAttackUntil;
        private MeleeAttackWindowContext _activeAttackContext;
        private bool _isHitWindowOpen;
        private Transform _autoTargetTransform;
        private string _lastAutoTargetReason;

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

        // ─────────────────────────────────────────────────────
        // 远程状态
        // ─────────────────────────────────────────────────────

        private float _fireRate = 0.18f;
        private bool _isEquipping;
        private float _equipEndTime;
        private float _lastFireTime;
        private bool _wasAiming;

        private AmmoStateData _cachedAmmoState;
        private ReloadStateData _cachedReloadState;
        private bool _hasCachedAmmo;
        private int _requestedReloadTargetMagazine;

        // IManualReloadable / IAiReloadable
        public bool HasCachedAmmo => _hasCachedAmmo;
        public int CurrentMagazine => _hasCachedAmmo && _cachedAmmoState != null ? _cachedAmmoState.CurrentMagazine : 0;
        public int MagazineCapacity => _config != null ? Mathf.Max(1, _config.MagazineSize) : 0;
        public int ReserveAmmo => ResolveReserveAmmo();
        public bool IsReloading => _cachedReloadState != null && _cachedReloadState.IsReloading;
        public float ReloadEndTime => _cachedReloadState != null ? _cachedReloadState.ReloadEndTime : 0f;
        public bool CanManualReload => CanStartReload();

        // ─────────────────────────────────────────────────────
        // IHoldableItem
        // ─────────────────────────────────────────────────────

        public void Initialize(ItemInstance instanceData)
        {
            _instance = instanceData;
            _config = instanceData?.GetSODataAs<WeaponSO>();

            if (_config != null)
            {
                CurrentEquipSlot = _config.EquipSlot;
                if (_config.HasRanged)
                    _fireRate = Mathf.Max(0.001f, _config.ShootInterval > 0f ? _config.ShootInterval : _config.FireRate);
            }
        }

        public void OnEquipEnter(BBBCharacterController player)
        {
            _player = player;

            if (_config != null && _config.HasMelee)
                EquipMelee();

            if (_config != null && _config.HasRanged)
                EquipRanged();
        }

        public void OnUpdateLogic()
        {
            if (_player == null || _config == null) return;

            bool statusBlocked = _player.CharacterArbiter != null && _player.CharacterArbiter.IsUnderStatusControl();
            bool actionBlocked = _player.CharacterArbiter != null && _player.CharacterArbiter.IsActionBlocked();

            if (_config.HasMelee)
                UpdateMelee(statusBlocked, actionBlocked);

            if (_config.HasRanged)
                UpdateRanged(statusBlocked || actionBlocked);
        }

        public void OnForceUnequip()
        {
            if (_config != null && _config.HasMelee)
                UnequipMelee();

            if (_config != null && _config.HasRanged)
                UnequipRanged();
        }

        // ─────────────────────────────────────────────────────
        // IPoolable
        // ─────────────────────────────────────────────────────

        public void OnSpawned()
        {
            ResetComboState();
            _ignoreAttackUntil = Time.time + 0.12f;
            _isEquipping = false;
            _wasAiming = false;
            _lastFireTime = 0f;
            _requestedReloadTargetMagazine = 0;
        }

        public void OnDespawned() { }

        // ═════════════════════════════════════════════════════
        // 近战 — 装备 / 卸载
        // ═════════════════════════════════════════════════════

        private void EquipMelee()
        {
            _hitbox = GetComponentInChildren<FistHitbox>();
            if (_hitbox != null)
            {
                _hitbox.SetOwner(_player);
                _hitbox.Deactivate();
                _hitbox.SetAttackGeometryId(_config.GetAttackGeometryId());
                _hitbox.HitRegistered -= OnHitRegistered;
                _hitbox.HitRegistered += OnHitRegistered;

                if (CurrentEquipSlot == EquipmentSlot.OffHand)
                {
                    _onAttackStart = OnRemoteAttackStart;
                    _onAttackEnd = _ => CloseHitWindow();
                    PostSystem.Instance.On("Weapon.AttackStart", _onAttackStart);
                    PostSystem.Instance.On("Weapon.AttackEnd", _onAttackEnd);
                }
            }
            else
            {
                Debug.LogWarning("[WeaponBehaviour] OnEquipEnter: missing FistHitbox on weapon prefab.");
            }

            _player?.InputPipeline?.ConsumePrimaryAttackPressed();
            _ignoreAttackUntil = Time.time + 0.12f;
            ResetComboState();
        }

        private void UnequipMelee()
        {
            if (_hitbox != null)
                _hitbox.HitRegistered -= OnHitRegistered;

            if (CurrentEquipSlot == EquipmentSlot.OffHand)
            {
                PostSystem.Instance.Off("Weapon.AttackStart", _onAttackStart);
                PostSystem.Instance.Off("Weapon.AttackEnd", _onAttackEnd);
            }

            ResetComboState();
        }

        // ═════════════════════════════════════════════════════
        // 近战 — 每帧更新
        // ═════════════════════════════════════════════════════

        private void UpdateMelee(bool statusBlocked, bool actionBlocked)
        {
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

            if (statusBlocked)
            {
                if (_isAttacking || _isEnteringStance || _isExitingStance || _activeAttackContext != null)
                    ResetComboState();
                TraceAttackGate("status-effect", attackInputObserved, in input);
                return;
            }

            if (actionBlocked)
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
                if (wantsAttack) { _inputBuffered = true; _player.InputPipeline.ConsumePrimaryAttackPressed(); }
                TryAdvanceStanceBridge();
                return;
            }

            if (_isExitingStance)
            {
                TraceAttackGate("exiting-stance", attackInputObserved, in input);
                if (wantsAttack) { _restartQueued = true; _player.InputPipeline.ConsumePrimaryAttackPressed(); }
                if (!_player.RuntimeData.Override.IsActive)
                {
                    _isExitingStance = false;
                    if (_restartQueued) { _restartQueued = false; TriggerEnterStanceOrAttack(); }
                }
                return;
            }

            if (_isAttacking)
            {
                TraceAttackGate("attacking", attackInputObserved, in input);
                float now = Time.time;

                if (wantsAttack && now < _comboWindowOpenTime)
                { _inputBuffered = true; _player.InputPipeline.ConsumePrimaryAttackPressed(); }

                bool inWindow = now >= _comboWindowOpenTime && now <= _comboWindowCloseTime;
                if (inWindow && (_inputBuffered || wantsAttack))
                {
                    _inputBuffered = false;
                    int nextComboIndex = _comboIndex < _config.ComboSequence.Length ? _comboIndex : 0;
                    QueueAttackTransition(nextComboIndex);
                    _player.InputPipeline.ConsumePrimaryAttackPressed();
                }

                if (TryExecuteQueuedAttack()) return;

                if (now > _comboWindowCloseTime) TriggerExitStance();
                return;
            }

            if (wantsAttack)
            {
                TraceAttackGate("trigger-enter-or-attack", true, in input);
                TriggerEnterStanceOrAttack();
            }
        }

        // ═════════════════════════════════════════════════════
        // 近战 — 连招状态机
        // ═════════════════════════════════════════════════════

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

        private void OnStanceEnd() => TryBridgeFromStance();

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

            if (!_player.RuntimeData.Override.IsActive &&
                _player.RuntimeData.CurrentLocomotionState != LocomotionState.Idle)
            {
                _isExitingStance = false;
                if (_restartQueued) { _restartQueued = false; TriggerEnterStanceOrAttack(); }
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
                _player.ArbiterPipeline?.Action?.CancelActiveAction(stopAnimation: false);
                _player.AnimFacade?.ClearOverrideOnEndCallback();
                _isExitingStance = false;
                PlayVisualExitIfAny();
                if (_restartQueued) { _restartQueued = false; TriggerEnterStanceOrAttack(); }
            }
        }

        private void TriggerAttack()
        {
            int idx = _comboIndex;
            var transition = idx < _config.ComboSequence.Length ? _config.ComboSequence[idx] : null;
            if (transition == null || transition.Clip == null)
            {
                Debug.LogWarning($"[WeaponBehaviour] TriggerAttack: combo segment {idx} has no clip.");
                ResetComboState();
                return;
            }

            var clip = transition.Clip;
            var attackHand = _config.GetAttackHand(idx);
            _currentAttackTransition = transition;
            _currentStanceTransition = null;
            _queuedAttackIndex = -1;
            _currentAttackStartTime = Time.time;
            _currentStanceStartTime = 0f;
            _lastAutoTargetReason = null;

            float speed = transition.Speed > 0f ? transition.Speed : 1f;
            float normStart = float.IsNaN(transition.NormalizedStartTime) ? 0f : transition.NormalizedStartTime;
            float normEnd = transition.Events.GetRealNormalizedEndTime(speed);
            float actualDuration = clip.length * (normEnd - normStart) / speed;

            PlaySwingSound(idx);

            _comboIndex = idx + 1;
            _isAttacking = true;
            _inputBuffered = false;
            _restartQueued = false;
            _comboWindowOpenTime = Time.time + actualDuration * _config.ComboWindowStart;
            _comboWindowCloseTime = Time.time + actualDuration + _config.ComboLateBuffer;

            CloseHitWindow();
            _activeAttackContext = new MeleeAttackWindowContext
            {
                Owner = _player,
                AttackHand = attackHand,
                ComboIndex = idx,
                AlignmentWindowStartTime = _currentAttackStartTime,
                AlignmentWindowEndTime = _currentAttackStartTime,
            };
            _activeAttackContext.SetWindowCount(1);
            _activeAttackContext.WindowStartTime = _currentAttackStartTime;
            _activeAttackContext.WindowEndTime = _currentAttackStartTime + actualDuration;

            ApplyDamageWindowTiming(_activeAttackContext, actualDuration, idx);
            ApplyAlignmentWindowTiming(_activeAttackContext, actualDuration, idx);

            if (CurrentEquipSlot == EquipmentSlot.MainHand)
                PostSystem.Instance.Send("Weapon.AttackStart", _activeAttackContext);

            _player.InputPipeline.ConsumePrimaryAttackPressed();
            var req = new ActionRequest(clip, _config.ComboPriority, applyGravity: true) { Transition = transition };
            _player.RequestOverride(in req, flushImmediately: true);
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
                PostSystem.Instance.Send("Weapon.AttackEnd", null);
        }

        private void CloseHitWindow()
        {
            _hitbox?.Deactivate();
            _isHitWindowOpen = false;
            _activeAttackContext = null;
            _autoTargetTransform = null;
        }

        private void OnHitRegistered(Collider other, IDamageable damageable, DamageRequest request)
        {
            if (_config == null) return;
            WeaponAudioUtil.PlayAt(_config.MeleeAudio.HitSounds, request.HitPoint);
        }

        private void OnRemoteAttackStart(object data)
        {
            var context = data as MeleeAttackWindowContext;
            if (context == null || context.Owner != _player) { CloseHitWindow(); return; }
            _activeAttackContext = context;
            if (!MatchesCurrentHand(context.AttackHand)) _hitbox?.Deactivate();
        }

        private void UpdateHitWindowState()
        {
            if (_activeAttackContext == null) return;
            if (!MatchesCurrentHand(_activeAttackContext.AttackHand))
            {
                if (_isHitWindowOpen) { _hitbox?.Deactivate(); _isHitWindowOpen = false; }
                return;
            }

            float now = Time.time;
            bool shouldBeActive = _activeAttackContext.IsInAnyDamageWindow(now);
            if (shouldBeActive == _isHitWindowOpen) return;

            _isHitWindowOpen = shouldBeActive;
            if (shouldBeActive) _hitbox?.Activate(_activeAttackContext.SharedHitSet, true);
            else _hitbox?.Deactivate();
        }

        private void UpdateAutoTargeting()
        {
            if (_player == null || _config == null || _activeAttackContext == null)
            {
                _autoTargetTransform = null;
                TraceAutoTarget("inactive-context");
                return;
            }

            float now = Time.time;
            if (now < _activeAttackContext.AlignmentWindowStartTime || now > _activeAttackContext.AlignmentWindowEndTime)
            {
                _autoTargetTransform = null;
                TraceAutoTarget(
                    "outside-alignment-window",
                    $"now={now:F3}, start={_activeAttackContext.AlignmentWindowStartTime:F3}, end={_activeAttackContext.AlignmentWindowEndTime:F3}");
                return;
            }

            float remainingTime = Mathf.Max(0f, _activeAttackContext.AlignmentWindowEndTime - now);
            if (remainingTime <= 0f || _config.AutoTargetTurnSpeed <= 0f)
            {
                _autoTargetTransform = null;
                TraceAutoTarget(
                    "alignment-disabled",
                    $"remaining={remainingTime:F3}, turnSpeed={_config.AutoTargetTurnSpeed:F1}");
                return;
            }

            var attackClipGeometry = GetCurrentAttackClipGeometry();
            float searchRange = ComputeAutoTargetSearchRange(attackClipGeometry);
            if (searchRange <= 0f)
            {
                _autoTargetTransform = null;
                TraceAutoTarget("invalid-search-range", $"searchRange={searchRange:F3}");
                return;
            }

            float maxStepDistance = ComputeMaxStepDistance(remainingTime);
            float candidateSearchRange = searchRange + maxStepDistance;
            if (!TryFindBestAutoTargetCandidate(_player, attackClipGeometry, candidateSearchRange, remainingTime, _config.AutoTargetTurnSpeed, maxStepDistance, out var candidate))
            {
                _autoTargetTransform = null;
                TraceAutoTarget(
                    "no-candidate",
                    $"searchRange={searchRange:F3}, candidateRange={candidateSearchRange:F3}, remaining={remainingTime:F3}, maxStep={maxStepDistance:F3}, hasGeo={attackClipGeometry != null}");
                return;
            }

            _autoTargetTransform = candidate.TargetTransform;
            float angleDelta = Mathf.Abs(Mathf.DeltaAngle(_player.transform.eulerAngles.y, candidate.TargetYaw));
            float smoothTime = _config.AutoTargetTurnSpeed > 0f
                ? Mathf.Clamp(angleDelta / _config.AutoTargetTurnSpeed, 0.01f, remainingTime)
                : 0.01f;
            _player.MotionDriver.RequestYaw(candidate.TargetYaw, smoothTime);
            ApplyStepCompensation(candidate, remainingTime);
            TraceAutoTarget(
                "candidate-applied",
                $"target={candidate.TargetTransform?.name ?? "null"}, yaw={candidate.TargetYaw:F1}, dist={candidate.Distance:F2}, step={candidate.StepDistance:F2}, cost={candidate.Cost:F3}, remaining={remainingTime:F3}");
        }

        private AttackClipGeometryClipDefinition GetCurrentAttackClipGeometry()
        {
            if (_config == null || _activeAttackContext == null) return null;
            var definition = AttackClipGeometryLibrary.LoadOrNull(_config.GetAttackGeometryId());
            return definition?.GetClip(_activeAttackContext.ComboIndex);
        }

        private float ComputeAutoTargetSearchRange(AttackClipGeometryClipDefinition geo)
        {
            if (geo != null)
            {
                float reach = AttackHitPredictionSolver.EstimateHorizontalReach(geo);
                float pad = _player.CharController != null ? Mathf.Max(_player.CharController.radius, 0.2f) : 0.2f;
                return Mathf.Max(reach + pad, 0.5f);
            }
            if (_hitbox != null && _hitbox.TryGetQueryBox(out Vector3 center, out Vector3 half, out _))
            {
                float ownerToCenter = Vector3.Distance(_player.transform.position, center);
                float pad = _player.CharController != null ? Mathf.Max(_player.CharController.radius, 0.2f) : 0.2f;
                return ownerToCenter + half.magnitude + pad;
            }
            return _player.CharController != null ? Mathf.Max(1f, _player.CharController.radius + 1f) : 1f;
        }

        private float ComputeMaxStepDistance(float remainingAlignmentTime)
        {
            if (_player == null || _config == null || remainingAlignmentTime <= 0f) return 0f;
            return Mathf.Max(0f, remainingAlignmentTime * ResolveCurrentStepSpeed());
        }

        private float ResolveCurrentStepSpeed()
        {
            if (_player?.Config?.Core == null) return 0f;
            bool isTactical = _player.RuntimeData != null && _player.RuntimeData.IsTacticalStance;
            LocomotionState loco = _player.RuntimeData != null ? _player.RuntimeData.CurrentLocomotionState : LocomotionState.Idle;
            if (isTactical && _player.Config.TacticalMotionBase != null)
                return loco switch
                {
                    LocomotionState.Walk => _player.Config.TacticalMotionBase.AimWalkSpeed,
                    LocomotionState.Jog => _player.Config.TacticalMotionBase.AimJogSpeed,
                    LocomotionState.Sprint => _player.Config.TacticalMotionBase.AimSprintSpeed,
                    _ => _player.Config.TacticalMotionBase.AimWalkSpeed,
                };
            return loco switch
            {
                LocomotionState.Walk => _player.Config.Core.WalkSpeed,
                LocomotionState.Jog => _player.Config.Core.JogSpeed,
                LocomotionState.Sprint => _player.Config.Core.SprintSpeed,
                _ => _player.Config.Core.JogSpeed,
            };
        }

        private void ApplyStepCompensation(in AutoTargetCandidate candidate, float remainingAlignmentTime)
        {
            if (_player?.MotionDriver == null || remainingAlignmentTime <= 0f || candidate.StepDistance <= 0f) return;
            float step = Mathf.Min(candidate.StepDistance, candidate.StepDistance * Mathf.Clamp01(Time.deltaTime / remainingAlignmentTime));
            if (step <= 0.0001f) return;
            _player.MotionDriver.RequestHorizontalDisplacement(Quaternion.Euler(0f, candidate.TargetYaw, 0f) * Vector3.forward * step);
        }

        private static bool TryFindBestAutoTargetCandidate(
            BBBCharacterController owner,
            AttackClipGeometryClipDefinition geo,
            float searchRange, float remainingTime, float turnSpeed, float maxStepDistance,
            out AutoTargetCandidate candidate)
        {
            candidate = default;
            if (owner == null) return false;

            Vector3 origin = owner.transform.position;
            int count = Physics.OverlapSphereNonAlloc(origin, searchRange, AutoTargetOverlaps, ~0, QueryTriggerInteraction.Collide);
            if (count <= 0) return false;

            var seenRoots = new HashSet<Transform>();
            bool found = false;
            float bestCost = float.MaxValue, bestDistance = float.MaxValue;
            AutoTargetCandidate best = default;

            for (int i = 0; i < count; i++)
            {
                Collider other = AutoTargetOverlaps[i];
                AutoTargetOverlaps[i] = null;
                if (other == null || other.transform.IsChildOf(owner.transform)) continue;
                var damageable = other.GetComponentInParent<IDamageable>();
                if (damageable == null) continue;
                Transform root = other.transform.root;
                if (root == null || !seenRoots.Add(root)) continue;

                Vector3 toTarget = other.bounds.center - origin;
                toTarget.y = 0f;
                float distance = toTarget.magnitude;
                if (distance <= 0.001f || distance > searchRange) continue;

                if (geo == null)
                {
                    Vector3 dir = toTarget / distance;
                    float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                    float cost = Mathf.Abs(Mathf.DeltaAngle(owner.transform.eulerAngles.y, yaw)) * 0.02f;
                    if (!found || cost < bestCost - 0.0001f || (Mathf.Abs(cost - bestCost) <= 0.0001f && distance < bestDistance))
                    { found = true; bestCost = cost; bestDistance = distance; best = new AutoTargetCandidate(root, cost, distance, yaw, 0f); }
                    continue;
                }

                var solveInput = new AttackHitPredictionSolver.SolveInput(geo, owner.transform, other.bounds, remainingTime, turnSpeed, maxStepDistance);
                if (!AttackHitPredictionSolver.TrySolve(in solveInput, out var result)) continue;
                if (!found || result.BestCost < bestCost - 0.0001f || (Mathf.Abs(result.BestCost - bestCost) <= 0.0001f && distance < bestDistance))
                { found = true; bestCost = result.BestCost; bestDistance = distance; best = new AutoTargetCandidate(root, result.BestCost, distance, result.BestWorldYaw, result.BestStepDistance); }
            }

            candidate = best;
            return found;
        }

        private void ApplyDamageWindowTiming(MeleeAttackWindowContext ctx, float actualDuration, int comboIndex)
        {
            var s = _config.GetDamageWindow(comboIndex);
            float domStart = 0f;
            float domEnd = ResolveDominantEndTime(actualDuration, comboIndex);
            if (!s.Enabled)
            {
                ctx.SetWindowCount(1);
                ctx.WindowStartTime = _currentAttackStartTime + domStart;
                ctx.WindowEndTime = _currentAttackStartTime + domEnd;
                return;
            }

            int windowCount = s.WindowCount;
            ctx.SetWindowCount(windowCount);

            for (int i = 0; i < windowCount; i++)
            {
                s.GetWindow(i, out float start, out float end);
                ctx.WindowStartTimes[i] = _currentAttackStartTime + Mathf.LerpUnclamped(domStart, domEnd, start);
                ctx.WindowEndTimes[i] = _currentAttackStartTime + Mathf.LerpUnclamped(domStart, domEnd, end);
            }
        }

        private void ApplyAlignmentWindowTiming(MeleeAttackWindowContext ctx, float actualDuration, int comboIndex)
        {
            var s = _config.GetAlignmentWindow(comboIndex);
            if (!s.Enabled) { ctx.AlignmentWindowStartTime = _currentAttackStartTime; ctx.AlignmentWindowEndTime = _currentAttackStartTime; return; }
            float domStart = 0f;
            float domEnd = ResolveDominantEndTime(actualDuration, comboIndex);
            float start = Mathf.Clamp01(s.StartNormalized), end = Mathf.Clamp01(s.EndNormalized);
            if (end < start) (start, end) = (end, start);
            ctx.AlignmentWindowStartTime = _currentAttackStartTime + Mathf.LerpUnclamped(domStart, domEnd, start);
            ctx.AlignmentWindowEndTime = _currentAttackStartTime + Mathf.LerpUnclamped(domStart, domEnd, end);

            if (_debugAutoTarget)
            {
                Debug.Log(
                    $"[WeaponAutoTarget] combo={comboIndex} domain=[{domStart:F3},{domEnd:F3}] actual={actualDuration:F3} " +
                    $"norm=[{start:F3},{end:F3}] window=[{ctx.AlignmentWindowStartTime:F3},{ctx.AlignmentWindowEndTime:F3}]",
                    this);
            }
        }

        private float ResolveDominantEndTime(float actualDuration, int comboIndex)
        {
            float duration = Mathf.Max(0f, actualDuration);
            float outgoingFade = ResolveOutgoingFadeDuration(comboIndex);
            return Mathf.Max(0f, duration - outgoingFade);
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

        private void PlaySwingSound(int comboIndex)
        {
            var clip = WeaponAudioUtil.PickCombo(_config.MeleeAudio.ComboSwingSounds, comboIndex);
            if (clip != null) { AudioSource.PlayClipAtPoint(clip, transform.position); return; }
            WeaponAudioUtil.PlayAt(_config.MeleeAudio.EnterStanceSounds, transform.position);
        }

        private bool HasEnterStance() => _config.EnterStanceAnim != null && _config.EnterStanceAnim.Clip != null;
        private ClipTransition GetExitTransition() => (_config?.ExitStanceAnim?.Clip != null) ? _config.ExitStanceAnim : null;
        private bool UsesLockExit() => _config != null && _config.ExitUsesLock;

        private bool TryBridgeFromStance()
        {
            if (!_isEnteringStance || _stanceAttackBridged) return false;
            _stanceAttackBridged = true; _isEnteringStance = false; _inputBuffered = false;
            TriggerAttack(); return true;
        }

        private void QueueAttackTransition(int nextIdx)
        {
            if (_config.ComboSequence == null || nextIdx < 0 || nextIdx >= _config.ComboSequence.Length) return;
            if (_queuedAttackIndex == nextIdx) return;
            _queuedAttackIndex = nextIdx;
        }

        private bool TryAdvanceStanceBridge()
        {
            if (!_isEnteringStance || _stanceAttackBridged || _config.ComboSequence == null || _config.ComboSequence.Length == 0) return false;
            var first = _config.ComboSequence[Mathf.Clamp(_comboIndex, 0, _config.ComboSequence.Length - 1)];
            if (!ShouldBridgeNow(_currentStanceTransition, first, _currentStanceStartTime)) return false;
            return TryBridgeFromStance();
        }

        private bool TryExecuteQueuedAttack()
        {
            if (!_isAttacking || _queuedAttackIndex < 0 || _config.ComboSequence == null || _queuedAttackIndex >= _config.ComboSequence.Length) return false;
            var next = _config.ComboSequence[_queuedAttackIndex];
            if (!ShouldBridgeNow(_currentAttackTransition, next, _currentAttackStartTime)) return false;
            _comboIndex = _queuedAttackIndex; _queuedAttackIndex = -1;
            TriggerAttack(); return true;
        }

        private bool ShouldBridgeNow(ClipTransition current, ClipTransition next, float segStart)
        {
            if (current?.Clip == null || next?.Clip == null) return false;
            float spd = current.Speed > 0f ? current.Speed : 1f;
            float normStart = float.IsNaN(current.NormalizedStartTime) ? 0f : current.NormalizedStartTime;
            float normEnd = current.Events.GetRealNormalizedEndTime(spd);
            float len = current.Clip.length;
            if (len <= 0f) return false;
            float dur = len * (normEnd - normStart) / spd;
            if (dur <= 0f) return false;
            float fade = next.FadeDuration > 0f ? next.FadeDuration : 0f;
            return Time.time >= segStart + Mathf.Max(0f, dur - fade) - 0.0001f;
        }

        private void PlayVisualExitIfAny()
        {
            var ve = GetExitTransition();
            if (ve == null) { _player?.AnimFacade?.StopFullBodyAction(); return; }
            _player.AnimFacade?.PlayFullBodyActionTransition(ve);
            _player.AnimFacade?.SetOnEndCallback(() =>
            {
                _player?.AnimFacade?.StopFullBodyAction();
                _player?.AnimFacade?.ClearOnEndCallback(0);
            }, 0);
        }

        private bool MatchesCurrentHand(FistsAttackHand hand)
        {
            if (hand == FistsAttackHand.BothHands) return true;
            var myHand = CurrentEquipSlot == EquipmentSlot.OffHand ? FistsAttackHand.OffHand : FistsAttackHand.MainHand;
            return hand == myHand;
        }

        private void TraceAttackGate(string reason, bool inputObserved, in ProcessedInputData input)
        {
            if (!AttackTrace || !inputObserved) return;
            Debug.Log($"[WeaponTrace] reason={reason} slot={CurrentEquipSlot} " +
                $"override={_player.RuntimeData.Override.IsActive} wantsAttack={_player.RuntimeData.WantsToPrimaryAction} " +
                $"entering={_isEnteringStance} exiting={_isExitingStance} attacking={_isAttacking}");
        }

        private void TraceAutoTarget(string reason, string detail = null)
        {
            if (!_debugAutoTarget)
                return;

            if (string.Equals(_lastAutoTargetReason, reason, StringComparison.Ordinal))
                return;

            _lastAutoTargetReason = reason;
            Debug.Log(
                $"[WeaponAutoTarget] reason={reason} combo={_activeAttackContext?.ComboIndex.ToString() ?? "-"} " +
                $"time={Time.time:F3}" + (string.IsNullOrWhiteSpace(detail) ? string.Empty : $" {detail}"),
                this);
        }

        // ═════════════════════════════════════════════════════
        // 远程 — 装备 / 卸载
        // ═════════════════════════════════════════════════════

        private void EquipRanged()
        {
            _isEquipping = true;
            _equipEndTime = Time.time + (_config != null ? _config.EquipEndTime : 0.35f);

            if (_player?.RuntimeData != null)
                _player.RuntimeData.CanEnterTacticalMotionBase = false;

            if (_muzzle != null && _player?.RuntimeData != null && _config.UseAimCorrection)
                _player.RuntimeData.CurrentAimReference = _muzzle;

            if (_config.EquipAnim != null && _player != null)
                _player.AnimFacade.PlayTransition(_config.EquipAnim, _config.EquipAnimPlayOptions);

            LoadAmmoState();
        }

        private void UnequipRanged()
        {
            CancelReload(false);
            SaveAmmoState();
            SaveReloadState();

            _isEquipping = false;
            _wasAiming = false;
            _hasCachedAmmo = false;
            _requestedReloadTargetMagazine = 0;

            if (_player?.RuntimeData != null)
            {
                _player.RuntimeData.CanEnterTacticalMotionBase = false;
                _player.RuntimeData.CurrentAimReference = null;
            }

            if (_player != null && _player.RuntimeData != null && _player.RuntimeData.CurrentItem == null)
            {
                _player.RuntimeData.WantsLookAtIK = false;
                if (_config?.UnEquipAnim != null)
                    _player.AnimFacade.PlayTransition(_config.UnEquipAnim, _config.UnEquipAnimPlayOptions);
            }
        }

        // ═════════════════════════════════════════════════════
        // 远程 — 每帧更新
        // ═════════════════════════════════════════════════════

        private void UpdateRanged(bool blocked)
        {
            if (_player == null || _config == null) return;

            if (blocked) { CancelReload(false); return; }

            if (_isEquipping)
            {
                if (Time.time >= _equipEndTime)
                {
                    _isEquipping = false;
                    _player.RuntimeData.CanEnterTacticalMotionBase = true;
                    if (_config.EquipIdleAnim != null)
                        _player.AnimFacade.PlayTransition(_config.EquipIdleAnim, _config.EquipIdleAnimOptions);
                }
                else return;
            }

            bool isAiming = _player.RuntimeData != null
                && _player.RuntimeData.IsTacticalStance
                && _player.RuntimeData.IsItemEquipped(_instance);

            if (_config.CameraPreset != null && !_config.HasMelee)
            {
                CameraExpressionSO preset = isAiming
                    ? (_config.AimingCameraPreset ?? _config.CameraPreset)
                    : (ResolveIsSprinting(_player) ? (_config.SprintCameraPreset ?? _config.CameraPreset) : _config.CameraPreset);
                _player.RuntimeData.CameraExpression = preset.ToExpression();
            }

            if (_wasAiming != isAiming)
            {
                if (isAiming)
                {
                    if (_config.AimAnim != null) _player.AnimFacade.PlayTransition(_config.AimAnim, _config.AimAnimPlayOptions);
                    _player.RuntimeData.WantsLookAtIK = true;
                }
                else
                {
                    if (_config.EquipIdleAnim != null) _player.AnimFacade.PlayTransition(_config.EquipIdleAnim, _config.EquipIdleAnimOptions);
                    _player.RuntimeData.WantsLookAtIK = false;
                }
                _wasAiming = isAiming;
            }

            if (isAiming)
            {
                bool wantsToFire = _config.IsFullAuto
                    ? _player.RuntimeData.IsPrimaryAttackHeld
                    : _player.RuntimeData.WantsToPrimaryAction;

                if (wantsToFire) { CancelReload(false); TryFire(); }
            }

            if (_hasCachedAmmo && _cachedReloadState.IsReloading && Time.time >= _cachedReloadState.ReloadEndTime)
                CompleteReloadCycle();
        }

        private void TryFire()
        {
            if (_cachedReloadState.IsReloading) return;
            if (!_hasCachedAmmo || _cachedAmmoState.CurrentMagazine <= 0) return;
            if (Time.time - _lastFireTime < _fireRate) return;

            _cachedAmmoState.CurrentMagazine--;
            _cachedAmmoState.ShotsFired++;
            SaveAmmoState();
            _lastFireTime = Time.time;

            if (_muzzle != null) WeaponAudioUtil.PlayAt(_config.RangedAudio.ShootSounds, _muzzle.position);

            if (_config.MuzzleVFXPrefab != null && _muzzle != null)
            {
                GameObject vfx = SimpleObjectPoolSystem.Shared != null
                    ? SimpleObjectPoolSystem.Shared.Spawn(_config.MuzzleVFXPrefab)
                    : UnityEngine.Object.Instantiate(_config.MuzzleVFXPrefab);
                vfx.transform.SetPositionAndRotation(_muzzle.position, _muzzle.rotation);
                vfx.transform.SetParent(_muzzle, true);
            }

            ApplyRecoil();
            FireHitScan();

            if (!_config.IsFullAuto)
                _player.InputPipeline.ConsumePrimaryAttackPressed();
        }

        private void ApplyRecoil()
        {
            if (_player?.RuntimeData == null || _config == null) return;
            float pitch = _config.RecoilPitchAngle + UnityEngine.Random.Range(-_config.RecoilPitchRandomRange, _config.RecoilPitchRandomRange);
            float yaw = _config.RecoilYawAngle + UnityEngine.Random.Range(-_config.RecoilYawRandomRange, _config.RecoilYawRandomRange);
            float yawSign = UnityEngine.Random.value > 0.5f ? 1f : -1f;
            _player.RuntimeData.ViewPitch -= pitch;
            _player.RuntimeData.ViewYaw += yawSign * yaw;
            _player.RuntimeData.ViewPitch = Mathf.Clamp(_player.RuntimeData.ViewPitch, _player.Config.Core.PitchLimits.x, _player.Config.Core.PitchLimits.y);
        }

        private void FireHitScan()
        {
            if (_player == null || _config == null || _muzzle == null) return;
            float range = _config.HitScanRange > 0f ? _config.HitScanRange : DefaultHitScanRange;
            float damage = _config.DamageAmount > 0f ? _config.DamageAmount : DefaultDamageAmount;

            Vector3 origin = _muzzle.position;
            Vector3 dir = (_player.RuntimeData.TargetAimPoint - origin).normalized;
            Vector3 end = origin + dir * range;

            if (Physics.Raycast(origin, dir, out RaycastHit hit, range, ~0, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                bool isSelf = hit.transform.IsChildOf(_player.transform) || hit.transform == _player.transform;
                if (!isSelf)
                {
                    var dmg = FindDamageable(hit.collider);
                    if (dmg != null) dmg.RequestDamage(new DamageRequest(damage, hit.point, _player.gameObject, _muzzle));
                    WeaponAudioUtil.PlayAt(_config.RangedAudio.ProjectileHitSounds, hit.point);
                }
            }

            SpawnTracer(origin, end);
        }

        private void SpawnTracer(Vector3 start, Vector3 end)
        {
            var go = new GameObject("WeaponTracer");
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true; line.positionCount = 2;
            line.SetPosition(0, start); line.SetPosition(1, end);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false; line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch; line.numCapVertices = 2;
            line.startWidth = 0.025f; line.endWidth = 0.01f;
            line.startColor = new Color(1f, 0.92f, 0.55f, 0.95f);
            line.endColor = new Color(1f, 0.55f, 0.2f, 0.1f);
            line.material = _tracerMaterial != null ? _tracerMaterial : new Material(Shader.Find("Sprites/Default"));
            float dur = _config.TracerDuration > 0f ? _config.TracerDuration : DefaultTracerDuration;
            Destroy(go, dur);
        }

        // ═════════════════════════════════════════════════════
        // 远程 — 换弹 (IManualReloadable / IAiReloadable)
        // ═════════════════════════════════════════════════════

        public bool RequestManualReload() => TryReloadInternal(MagazineCapacity);
        public bool RequestManualReload(int target) => TryReloadInternal(target);

        private bool TryReloadInternal(int target)
        {
            if (_player == null || _config == null || !_hasCachedAmmo) return false;
            int clamped = Mathf.Clamp(target, 1, MagazineCapacity);
            if (_cachedReloadState.IsReloading) return false;
            if (_cachedAmmoState.CurrentMagazine >= clamped) return false;
            if (ReserveAmmo <= 0) return false;
            _requestedReloadTargetMagazine = clamped;
            StartReloadCycle();
            return _cachedReloadState.IsReloading;
        }

        private void StartReloadCycle()
        {
            float t = _cachedAmmoState.CurrentMagazine > 0 ? _config.TacticalReloadTime : _config.ReloadTime;
            _cachedReloadState.IsReloading = true;
            _cachedReloadState.ReloadStartTime = Time.time;
            _cachedReloadState.ReloadEndTime = Time.time + t;
            SaveReloadState();
            if (_config.ReloadAnim?.Clip != null)
                _player.AnimFacade.PlayTransition(_config.ReloadAnim, _config.ReloadAnimOptions);
        }

        private void CompleteReloadCycle()
        {
            if (!_hasCachedAmmo) return;
            bool loaded = TryConsumeReserveAmmo(1);
            if (loaded) _cachedAmmoState.CurrentMagazine++;
            _cachedAmmoState.ReserveAmmo = ReserveAmmo;
            SaveAmmoState();
            if (loaded && CanContinueReload()) { StartReloadCycle(); return; }
            CancelReload(true);
        }

        private void CancelReload(bool restoreIdle)
        {
            if (_cachedReloadState == null || !_cachedReloadState.IsReloading) return;
            _cachedReloadState.IsReloading = false;
            _cachedReloadState.ReloadStartTime = 0f;
            _cachedReloadState.ReloadEndTime = 0f;
            _requestedReloadTargetMagazine = 0;
            SaveReloadState();
            if (restoreIdle && _config?.EquipIdleAnim != null && _player != null)
                _player.AnimFacade.PlayTransition(_config.EquipIdleAnim, _config.EquipIdleAnimOptions);
        }

        private bool CanStartReload()
        {
            if (_player == null || _config == null || !_hasCachedAmmo || _cachedReloadState == null) return false;
            if (_cachedReloadState.IsReloading) return false;
            if (_cachedAmmoState == null || _cachedAmmoState.CurrentMagazine >= _config.MagazineSize) return false;
            return ReserveAmmo > 0;
        }

        private bool CanContinueReload()
        {
            if (!_hasCachedAmmo || _cachedAmmoState == null || _config == null) return false;
            int targetMag = _requestedReloadTargetMagazine > 0 ? Mathf.Min(_requestedReloadTargetMagazine, MagazineCapacity) : MagazineCapacity;
            if (_cachedAmmoState.CurrentMagazine >= targetMag) return false;
            return ReserveAmmo > 0;
        }

        private int ResolveReserveAmmo()
        {
            if (_player == null || _config == null) return _hasCachedAmmo && _cachedAmmoState != null ? _cachedAmmoState.ReserveAmmo : 0;
            if (_config.AmmoItem == null || string.IsNullOrWhiteSpace(_config.AmmoItem.ItemID))
                return _hasCachedAmmo && _cachedAmmoState != null ? _cachedAmmoState.ReserveAmmo : 0;
            return ItemPackVfs.GetItemCount(_config.AmmoItem.ItemID, _player);
        }

        private bool TryConsumeReserveAmmo(int amount)
        {
            if (amount <= 0) return true;
            if (_player == null || _config == null) return false;
            if (_config.AmmoItem == null || string.IsNullOrWhiteSpace(_config.AmmoItem.ItemID))
            {
                if (_cachedAmmoState == null || _cachedAmmoState.ReserveAmmo < amount) return false;
                _cachedAmmoState.ReserveAmmo -= amount; return true;
            }
            return ItemPackVfs.TryConsumeItem(_config.AmmoItem.ItemID, amount, _player);
        }

        private void LoadAmmoState()
        {
            if (_instance == null) return;
            string name = _config.name;
            if (AmmoPackVfs.TryGetAmmoState(name, _instance.InstanceID, out var ammo, _player))
            { _cachedAmmoState = ammo; _hasCachedAmmo = true; }
            else
            { _cachedAmmoState = new AmmoStateData { CurrentMagazine = 0, ReserveAmmo = 0, ShotsFired = 0 }; _hasCachedAmmo = true; SaveAmmoState(); }

            if (AmmoPackVfs.TryGetReloadState(name, _instance.InstanceID, out var reload, _player))
                _cachedReloadState = reload;
            else
                _cachedReloadState = new ReloadStateData();
        }

        private void SaveAmmoState()
        {
            if (_instance == null || !_hasCachedAmmo) return;
            AmmoPackVfs.SetAmmoState(_config.name, _instance.InstanceID, _cachedAmmoState, _player);
        }

        private void SaveReloadState()
        {
            if (_instance == null || _cachedReloadState == null) return;
            AmmoPackVfs.SetReloadState(_config.name, _instance.InstanceID, _cachedReloadState, _player);
        }

        // ─────────────────────────────────────────────────────
        // 工具
        // ─────────────────────────────────────────────────────

        private static IDamageable FindDamageable(Collider col)
        {
            if (col == null) return null;
            var d = col.GetComponentInParent<IDamageable>();
            if (d != null) return d;
            var rb = col.attachedRigidbody;
            if (rb != null) { d = rb.GetComponentInParent<IDamageable>(); if (d != null) return d; }
            return col.transform.root?.GetComponent<IDamageable>();
        }

        private static bool ResolveIsSprinting(BBBCharacterController player)
            => player.RuntimeData.CurrentLocomotionState == LocomotionState.Sprint;
    }
}
