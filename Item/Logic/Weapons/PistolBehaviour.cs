using UnityEngine;
using System.Collections.Generic;

namespace BBBNexus
{
    // 手枪行为：装备后维持上半身枪姿；右键时申请 Aim 全身状态；Aim 时允许持续开火。
    public class PistolBehaviour : MonoBehaviour, IHoldableItem, IPoolable, IManualReloadable, IAiReloadable
    {
        private const float DefaultHitScanRange = 80f;
        private const float DefaultDamageAmount = 10f;
        private const float DefaultTracerDuration = 0.06f;
        private const bool DebugHitScan = true;
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[16];

        [Header("--- 表现与挂点 ---")]
        [Tooltip("枪口瞄准参考点（枪口空物体，Z 轴朝出弹方向）")]
        [SerializeField] private Transform _muzzle;
        [Tooltip("可选的曳光弹材质；为空则使用默认精灵材质。")]
        [SerializeField] private Material _tracerMaterial;

        private BBBCharacterController _player;
        private ItemInstance _instance;
        private PistolSO _config;
        private float _fireRate = 0.18f;
        private bool _isEquipping;
        private float _equipEndTime;
        private float _lastFireTime;
        private bool _wasAiming;

        // 弹药系统缓存喵~
        private AmmoStateData _cachedAmmoState;
        private ReloadStateData _cachedReloadState;
        private bool _hasCachedAmmo;
        private int _requestedReloadTargetMagazine;

        public EquipmentSlot CurrentEquipSlot { get; set; }
        public bool HasCachedAmmo => _hasCachedAmmo;
        public int CurrentMagazine => _hasCachedAmmo && _cachedAmmoState != null ? _cachedAmmoState.CurrentMagazine : 0;
        public int MagazineCapacity => _config != null ? Mathf.Max(1, _config.MagazineSize) : 0;
        public int ReserveAmmo => ResolveReserveAmmo();
        public bool IsReloading => _cachedReloadState != null && _cachedReloadState.IsReloading;
        public float ReloadEndTime => _cachedReloadState != null ? _cachedReloadState.ReloadEndTime : 0f;
        public bool CanManualReload => CanStartReload();

        public void Initialize(ItemInstance instanceData)
        {
            _instance = instanceData;
            _config = instanceData?.GetSODataAs<PistolSO>();
            if (_config != null)
            {
                _fireRate = Mathf.Max(0.001f, _config.ShootInterval > 0f ? _config.ShootInterval : _config.FireRate);
            }
        }

        public void OnEquipEnter(BBBCharacterController player)
        {
            _player = player;
            _isEquipping = true;
            _equipEndTime = Time.time + (_config != null ? _config.EquipEndTime : 0.35f);
            if (_player?.RuntimeData != null)
            {
                _player.RuntimeData.CanEnterTacticalMotionBase = false;
            }

            if (_muzzle != null && _player?.RuntimeData != null && _config != null && _config.UseAimCorrection)
            {
                _player.RuntimeData.CurrentAimReference = _muzzle;
            }

            if (_config != null && _config.EquipAnim != null && _player != null)
            {
                _player.AnimFacade.PlayTransition(_config.EquipAnim, _config.EquipAnimPlayOptions);
            }

            // 装备时从 AmmoPack 加载弹药状态喵~
            LoadAmmoState();
        }

        public void OnUpdateLogic()
        {
            if (_player == null || _config == null)
            {
                return;
            }

            if ((_player.CharacterArbiter != null && _player.CharacterArbiter.IsUnderStatusControl()) ||
                (_player.CharacterArbiter != null && _player.CharacterArbiter.IsActionBlocked()))
            {
                CancelReload(false);
                return;
            }

            if (_isEquipping)
            {
                if (Time.time >= _equipEndTime)
                {
                    _isEquipping = false;
                    _player.RuntimeData.CanEnterTacticalMotionBase = true;
                    if (_config.EquipIdleAnim != null)
                    {
                        _player.AnimFacade.PlayTransition(_config.EquipIdleAnim, _config.EquipIdleAnimOptions);
                    }
                }
                else
                {
                    return;
                }
            }

            bool isAiming =
                _player.RuntimeData != null &&
                _player.RuntimeData.IsTacticalStance &&
                _player.RuntimeData.IsItemEquipped(_instance);

            if (_config.CameraPreset != null)
            {
                CameraExpressionSO camPreset;
                if (isAiming)
                    camPreset = _config.AimingCameraPreset ?? _config.CameraPreset;
                else if (ResolveIsSprinting(_player))
                    camPreset = _config.SprintCameraPreset ?? _config.CameraPreset;
                else
                    camPreset = _config.CameraPreset;
                _player.RuntimeData.CameraExpression = camPreset.ToExpression();
            }

            if (_wasAiming != isAiming)
            {
                if (isAiming)
                {
                    if (_config.AimAnim != null)
                    {
                        _player.AnimFacade.PlayTransition(_config.AimAnim, _config.AnimPlayOptions);
                    }

                    _player.RuntimeData.WantsLookAtIK = true;
                }
                else
                {
                    if (_config.EquipIdleAnim != null)
                    {
                        _player.AnimFacade.PlayTransition(_config.EquipIdleAnim, _config.EquipIdleAnimOptions);
                    }

                    _player.RuntimeData.WantsLookAtIK = false;
                }

                _wasAiming = isAiming;
            }

            if (isAiming)
            {
                bool wantsToFire = _config.IsFullAuto
                    ? _player.RuntimeData.IsPrimaryAttackHeld
                    : _player.RuntimeData.WantsToPrimaryAction;

                if (wantsToFire)
                {
                    CancelReload(false);
                    TryFire();
                }
            }

            // 检查换弹是否完成喵~
            if (_hasCachedAmmo && _cachedReloadState.IsReloading && Time.time >= _cachedReloadState.ReloadEndTime)
            {
                CompleteReloadCycle();
            }
        }

        public void OnForceUnequip()
        {
            // 卸载时保存状态喵~
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
                if (_config != null && _config.UnEquipAnim != null)
                {
                    _player.AnimFacade.PlayTransition(_config.UnEquipAnim, _config.UnEquipAnimPlayOptions);
                }
            }
        }

        private void TryFire()
        {
            // 检查是否正在换弹喵~
            if (_cachedReloadState.IsReloading)
            {
                return;
            }

            // 检查弹药喵~
            if (!_hasCachedAmmo || _cachedAmmoState.CurrentMagazine <= 0)
            {
                return;
            }

            if (Time.time - _lastFireTime < _fireRate)
            {
                return;
            }

            // 消耗一发子弹喵~
            _cachedAmmoState.CurrentMagazine--;
            _cachedAmmoState.ShotsFired++;
            SaveAmmoState();

            _lastFireTime = Time.time;

            if (_muzzle != null)
            {
                WeaponAudioUtil.PlayAt(_config.RangedAudio.ShootSounds, _muzzle.position);
            }

            if (_config.MuzzleVFXPrefab != null && _muzzle != null)
            {
                GameObject muzzleVfx;
                if (SimpleObjectPoolSystem.Shared != null)
                {
                    muzzleVfx = SimpleObjectPoolSystem.Shared.Spawn(_config.MuzzleVFXPrefab);
                    muzzleVfx.transform.SetPositionAndRotation(_muzzle.position, _muzzle.rotation);
                    muzzleVfx.transform.SetParent(_muzzle, true);
                }
                else
                {
                    muzzleVfx = UnityEngine.Object.Instantiate(_config.MuzzleVFXPrefab, _muzzle.position, _muzzle.rotation);
                    muzzleVfx.transform.parent = _muzzle;
                }
            }

            ApplyRecoil();
            FireHitScan();

            // 半自动：开火后立即核销 Buffer，防止单次按键在 bufferTime > fireRate 时触发多发
            if (!_config.IsFullAuto)
                _player.InputPipeline.ConsumePrimaryAttackPressed();
        }

        private void ApplyRecoil()
        {
            if (_player?.RuntimeData == null || _config == null)
            {
                return;
            }

            float pitchNoise = UnityEngine.Random.Range(-_config.RecoilPitchRandomRange, _config.RecoilPitchRandomRange);
            float yawNoise = UnityEngine.Random.Range(-_config.RecoilYawRandomRange, _config.RecoilYawRandomRange);
            float finalPitch = _config.RecoilPitchAngle + pitchNoise;
            float finalYaw = _config.RecoilYawAngle + yawNoise;
            float yawSign = UnityEngine.Random.value > 0.5f ? 1f : -1f;

            _player.RuntimeData.ViewPitch -= finalPitch;
            _player.RuntimeData.ViewYaw += yawSign * finalYaw;
            _player.RuntimeData.ViewPitch = Mathf.Clamp(
                _player.RuntimeData.ViewPitch,
                _player.Config.Core.PitchLimits.x,
                _player.Config.Core.PitchLimits.y);
        }

        private void FireHitScan()
        {
            if (_player == null || _config == null || _muzzle == null)
            {
                return;
            }

            float hitScanRange = _config.HitScanRange > 0f ? _config.HitScanRange : DefaultHitScanRange;

            float baseDamage = _config.DamageAmount > 0f ? _config.DamageAmount : DefaultDamageAmount;

            Vector3 origin = _muzzle.position;
            Vector3 direction = ResolveFireDirection(origin);
            Vector3 endPoint = origin + direction * hitScanRange;

            int hitCount = Physics.RaycastNonAlloc(origin, direction, HitBuffer, hitScanRange, ~0, QueryTriggerInteraction.Ignore);
            if (hitCount > 0)
            {
                System.Array.Sort(HitBuffer, 0, hitCount, RaycastHitDistanceComparer.Instance);

                bool foundValidHit = false;
                for (int i = 0; i < hitCount; i++)
                {
                    RaycastHit hit = HitBuffer[i];
                    if (hit.collider == null)
                    {
                        continue;
                    }

                    bool isSelf = hit.transform.IsChildOf(_player.transform) || hit.transform == _player.transform;
                    var damageable = isSelf ? null : FindDamageable(hit.collider);

                    if (DebugHitScan)
                    {
                        Debug.Log(
                            $"[PistolHitScan] shooter={_player.name} hit={hit.collider.name} layer={LayerMask.LayerToName(hit.collider.gameObject.layer)} " +
                            $"distance={hit.distance:F2} isSelf={isSelf} damageable={(damageable != null ? damageable.GetType().Name : "null")} " +
                            $"origin={origin} dir={direction} point={hit.point}",
                            hit.collider);
                    }

                    if (isSelf)
                    {
                        continue;
                    }

                    endPoint = hit.point;
                    foundValidHit = true;

                    bool applied = true;
                    if (damageable != null)
                    {
                        DamageHitZoneType hitZone = RangedHitZoneUtility.ResolveHitZone(hit.collider);
                        float damageAmount = baseDamage * _config.ResolveHitZoneDamageMultiplier(hitZone);
                        var request = new DamageRequest(
                            damageAmount,
                            hit.point,
                            _player.gameObject,
                            _muzzle);
                        applied = damageable.RequestDamage(in request);
                    }

                    if (applied)
                    {
                        WeaponAudioUtil.PlayAt(_config.RangedAudio.ProjectileHitSounds, hit.point);
                    }

                    break;
                }

                if (!foundValidHit && DebugHitScan)
                {
                    Debug.Log(
                        $"[PistolHitScan] shooter={_player.name} miss hit=self-only origin={origin} dir={direction} range={hitScanRange:F2}",
                        _player);
                }
            }
            else if (DebugHitScan)
            {
                Debug.Log(
                    $"[PistolHitScan] shooter={_player.name} miss hit=none origin={origin} dir={direction} range={hitScanRange:F2}",
                    _player);
            }

            SpawnTracer(origin, endPoint);
        }

        private Vector3 ResolveFireDirection(Vector3 origin)
        {
            if (_player?.RuntimeData == null)
            {
                return transform.forward;
            }

            if (TryResolveAiHorizontalDirection(out var horizontalDir))
            {
                return horizontalDir;
            }

            if (_player.RuntimeData.CameraTransform == null)
            {
                Vector3 aiAimDelta = _player.RuntimeData.AIAimTargetPoint - origin;
                aiAimDelta.y = 0f;
                if (aiAimDelta.sqrMagnitude > 0.0001f)
                {
                    return aiAimDelta.normalized;
                }
            }
            else
            {
                Vector3 toAimPoint = _player.RuntimeData.TargetAimPoint - origin;
                if (toAimPoint.sqrMagnitude > 0.0001f)
                {
                    return toAimPoint.normalized;
                }
            }

            return transform.forward;
        }

        private bool TryResolveAiHorizontalDirection(out Vector3 direction)
        {
            direction = Vector3.zero;

            var sensor = _player != null ? _player.GetComponentInChildren<NavigatorSensorBase>() : null;
            if (sensor == null)
            {
                return false;
            }

            ref readonly var context = ref sensor.GetCurrentContext();
            Vector3 flatDir = Vector3.ProjectOnPlane(context.TargetWorldDirection, Vector3.up);
            if (flatDir.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            direction = flatDir.normalized;
            return true;
        }

        private sealed class RaycastHitDistanceComparer : IComparer<RaycastHit>
        {
            internal static readonly RaycastHitDistanceComparer Instance = new RaycastHitDistanceComparer();

            public int Compare(RaycastHit x, RaycastHit y)
            {
                return x.distance.CompareTo(y.distance);
            }
        }

        private void SpawnTracer(Vector3 start, Vector3 end)
        {
            var tracer = new GameObject("PistolTracer");
            var line = tracer.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 2;
            line.startWidth = 0.025f;
            line.endWidth = 0.01f;
            line.startColor = new Color(1f, 0.92f, 0.55f, 0.95f);
            line.endColor = new Color(1f, 0.55f, 0.2f, 0.1f);
            line.material = _tracerMaterial != null
                ? _tracerMaterial
                : new Material(Shader.Find("Sprites/Default"));

            float tracerDuration = _config.TracerDuration > 0f ? _config.TracerDuration : DefaultTracerDuration;
            Destroy(tracer, tracerDuration);
        }

        private static IDamageable FindDamageable(Collider col)
        {
            if (col == null)
            {
                return null;
            }

            var damageable = col.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                return damageable;
            }

            var rb = col.attachedRigidbody;
            if (rb != null)
            {
                damageable = rb.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    return damageable;
                }
            }

            var root = col.transform.root;
            return root != null ? root.GetComponent<IDamageable>() : null;
        }

        private static bool ResolveIsSprinting(BBBCharacterController player)
            => player.RuntimeData.CurrentLocomotionState == LocomotionState.Sprint;

        /// <summary>
        /// 从 AmmoPack 加载弹药状态喵~
        /// </summary>
        private void LoadAmmoState()
        {
            if (_instance == null) return;

            // 获取武器 SO 名称作为目录名喵~
            string weaponSoName = _config.name;

            // 尝试读取弹药状态
            if (AmmoPackVfs.TryGetAmmoState(weaponSoName, _instance.InstanceID, out var ammoState, _player))
            {
                _cachedAmmoState = ammoState;
                _hasCachedAmmo = true;
            }
            else
            {
                // 首次装备，初始化弹药状态
                _cachedAmmoState = new AmmoStateData
                {
                    CurrentMagazine = 0,
                    ReserveAmmo = 0,
                    ShotsFired = 0
                };
                _hasCachedAmmo = true;
                SaveAmmoState();
            }

            // 尝试读取换弹状态
            if (AmmoPackVfs.TryGetReloadState(weaponSoName, _instance.InstanceID, out var reloadState, _player))
            {
                _cachedReloadState = reloadState;
            }
            else
            {
                _cachedReloadState = new ReloadStateData();
            }
        }

        /// <summary>
        /// 保存弹药状态到 AmmoPack喵~
        /// </summary>
        private void SaveAmmoState()
        {
            if (_instance == null || !_hasCachedAmmo) return;
            string weaponSoName = _config.name;
            AmmoPackVfs.SetAmmoState(weaponSoName, _instance.InstanceID, _cachedAmmoState, _player);
        }

        /// <summary>
        /// 保存换弹状态到 AmmoPack喵~
        /// </summary>
        private void SaveReloadState()
        {
            if (_instance == null || _cachedReloadState == null) return;
            string weaponSoName = _config.name;
            AmmoPackVfs.SetReloadState(weaponSoName, _instance.InstanceID, _cachedReloadState, _player);
        }

        /// <summary>
        /// 尝试换弹喵~
        /// </summary>
        public bool RequestManualReload()
        {
            return TryReloadInternal(MagazineCapacity);
        }

        public bool RequestManualReload(int targetMagazineCount)
        {
            return TryReloadInternal(targetMagazineCount);
        }

        private bool TryReloadInternal(int targetMagazineCount)
        {
            if (_player == null || _config == null || !_hasCachedAmmo)
                return false;

            int clampedTarget = Mathf.Clamp(targetMagazineCount, 1, MagazineCapacity);

            // 检查是否可以换弹
            if (_cachedReloadState.IsReloading)
                return false;

            // 检查弹匣是否已满
            if (_cachedAmmoState.CurrentMagazine >= clampedTarget)
                return false;

            // 背包里没有对应弹药时不允许换弹
            if (ReserveAmmo <= 0)
                return false;

            _requestedReloadTargetMagazine = clampedTarget;
            StartReloadCycle();
            return _cachedReloadState.IsReloading;
        }

        /// <summary>
        /// 完成单发装填，并在需要时继续下一段换弹喵~
        /// </summary>
        private void CompleteReloadCycle()
        {
            if (!_hasCachedAmmo) return;

            bool loadedOneRound = TryConsumeReserveAmmo(1);
            if (loadedOneRound)
            {
                _cachedAmmoState.CurrentMagazine += 1;
            }

            _cachedAmmoState.ReserveAmmo = ReserveAmmo;
            SaveAmmoState();
            if (loadedOneRound && CanContinueReload())
            {
                StartReloadCycle();
                return;
            }

            CancelReload(true);
        }

        private int ResolveReserveAmmo()
        {
            if (_player == null || _config == null)
            {
                return _hasCachedAmmo && _cachedAmmoState != null ? _cachedAmmoState.ReserveAmmo : 0;
            }

            if (_config.AmmoItem == null || string.IsNullOrWhiteSpace(_config.AmmoItem.ItemID))
            {
                return _hasCachedAmmo && _cachedAmmoState != null ? _cachedAmmoState.ReserveAmmo : 0;
            }

            return ItemPackVfs.GetItemCount(_config.AmmoItem.ItemID, _player);
        }

        private bool TryConsumeReserveAmmo(int amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            if (_player == null || _config == null)
            {
                return false;
            }

            if (_config.AmmoItem == null || string.IsNullOrWhiteSpace(_config.AmmoItem.ItemID))
            {
                if (_cachedAmmoState == null || _cachedAmmoState.ReserveAmmo < amount)
                {
                    return false;
                }

                _cachedAmmoState.ReserveAmmo -= amount;
                return true;
            }

            return ItemPackVfs.TryConsumeItem(_config.AmmoItem.ItemID, amount, _player);
        }

        public void OnSpawned()
        {
            _isEquipping = false;
            _wasAiming = false;
            _lastFireTime = 0f;
            _requestedReloadTargetMagazine = 0;
        }

        private bool CanStartReload()
        {
            if (_player == null || _config == null || !_hasCachedAmmo || _cachedReloadState == null)
            {
                return false;
            }

            if (_cachedReloadState.IsReloading)
            {
                return false;
            }

            if (_cachedAmmoState == null || _cachedAmmoState.CurrentMagazine >= _config.MagazineSize)
            {
                return false;
            }

            return ReserveAmmo > 0;
        }

        private bool CanContinueReload()
        {
            if (!_hasCachedAmmo || _cachedAmmoState == null || _config == null)
            {
                return false;
            }

            int targetMagazineCount = _requestedReloadTargetMagazine > 0
                ? Mathf.Min(_requestedReloadTargetMagazine, MagazineCapacity)
                : MagazineCapacity;

            if (_cachedAmmoState.CurrentMagazine >= targetMagazineCount)
            {
                return false;
            }

            return ReserveAmmo > 0;
        }

        private void StartReloadCycle()
        {
            float reloadTime = _cachedAmmoState.CurrentMagazine > 0
                ? _config.TacticalReloadTime
                : _config.ReloadTime;

            _cachedReloadState.IsReloading = true;
            _cachedReloadState.ReloadStartTime = Time.time;
            _cachedReloadState.ReloadEndTime = Time.time + reloadTime;
            SaveReloadState();

            if (_config.ReloadAnim != null && _config.ReloadAnim.Clip != null)
            {
                _player.AnimFacade.PlayTransition(_config.ReloadAnim, _config.ReloadAnimOptions);
            }
        }

        private void CancelReload(bool restoreIdle)
        {
            if (_cachedReloadState == null || !_cachedReloadState.IsReloading)
            {
                return;
            }

            _cachedReloadState.IsReloading = false;
            _cachedReloadState.ReloadStartTime = 0f;
            _cachedReloadState.ReloadEndTime = 0f;
            _requestedReloadTargetMagazine = 0;
            SaveReloadState();

            if (restoreIdle && _config != null && _config.EquipIdleAnim != null && _player != null)
            {
                _player.AnimFacade.PlayTransition(_config.EquipIdleAnim, _config.EquipIdleAnimOptions);
            }
        }

        public void OnDespawned() { }
    }
}
