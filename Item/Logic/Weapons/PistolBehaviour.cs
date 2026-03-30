using UnityEngine;

namespace BBBNexus
{
    // 手枪行为：装备后维持上半身枪姿；右键时申请 Aim 全身状态；Aim 时允许持续开火。
    public class PistolBehaviour : MonoBehaviour, IHoldableItem, IPoolable
    {
        private const float DefaultHitScanRange = 80f;
        private const float DefaultDamageAmount = 10f;
        private const float DefaultTracerDuration = 0.06f;

        [Header("--- 表现与挂点 ---")]
        [Tooltip("枪口火焰特效")]
        [SerializeField] private ParticleSystem _muzzleFlash;
        [Tooltip("枪口瞄准参考点")]
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

        public EquipmentSlot CurrentEquipSlot { get; set; }

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

            if (_muzzle != null && _player?.RuntimeData != null)
            {
                _player.RuntimeData.CurrentAimReference = _muzzle;
            }

            if (_config != null && _config.EquipAnim != null && _player != null)
            {
                _player.AnimFacade.PlayTransition(_config.EquipAnim, _config.EquipAnimPlayOptions);
            }
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
                return;
            }

            if (_isEquipping)
            {
                if (Time.time >= _equipEndTime)
                {
                    _isEquipping = false;
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
                _player.RuntimeData.IsAiming &&
                _player.RuntimeData.IsItemEquipped(_instance);

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

            if (isAiming && _player.RuntimeData.WantsToPrimaryAction)
            {
                TryFire();
            }
        }

        public void OnForceUnequip()
        {
            _isEquipping = false;
            _wasAiming = false;

            if (_muzzleFlash != null)
            {
                _muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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
            if (Time.time - _lastFireTime < _fireRate)
            {
                return;
            }

            _lastFireTime = Time.time;

            if (_muzzleFlash != null)
            {
                _muzzleFlash.Play();
            }

            if (_config.ShootSound != null && _muzzle != null)
            {
                AudioSource.PlayClipAtPoint(_config.ShootSound, _muzzle.position);
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
                    muzzleVfx = Object.Instantiate(_config.MuzzleVFXPrefab, _muzzle.position, _muzzle.rotation);
                    muzzleVfx.transform.parent = _muzzle;
                }
            }

            ApplyRecoil();

            FireHitScan();
        }

        private void ApplyRecoil()
        {
            if (_player?.RuntimeData == null || _config == null)
            {
                return;
            }

            float pitchNoise = Random.Range(-_config.RecoilPitchRandomRange, _config.RecoilPitchRandomRange);
            float yawNoise = Random.Range(-_config.RecoilYawRandomRange, _config.RecoilYawRandomRange);
            float finalPitch = _config.RecoilPitchAngle + pitchNoise;
            float finalYaw = _config.RecoilYawAngle + yawNoise;
            float yawSign = Random.value > 0.5f ? 1f : -1f;

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
            float damageAmount = _config.DamageAmount > 0f ? _config.DamageAmount : DefaultDamageAmount;

            Vector3 origin = _muzzle.position;
            Vector3 direction = _muzzle.forward;
            Vector3 endPoint = origin + direction * hitScanRange;

            if (Physics.Raycast(origin, direction, out RaycastHit hit, hitScanRange, ~0, QueryTriggerInteraction.Ignore))
            {
                endPoint = hit.point;

                var damageable = FindDamageable(hit.collider);
                if (damageable != null)
                {
                    var request = new DamageRequest(
                        damageAmount,
                        hit.point,
                        _player.gameObject,
                        _muzzle);
                    damageable.RequestDamage(in request);
                }

                if (_config.ProjectileHitSound != null)
                {
                    AudioSource.PlayClipAtPoint(_config.ProjectileHitSound, hit.point);
                }
            }

            SpawnTracer(origin, endPoint);
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

        public void OnSpawned()
        {
            _isEquipping = false;
            _wasAiming = false;
            _lastFireTime = 0f;

            if (_muzzleFlash != null)
            {
                _muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        public void OnDespawned()
        {
            if (_muzzleFlash != null)
            {
                _muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}
