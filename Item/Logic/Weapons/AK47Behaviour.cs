using UnityEngine;

namespace BBBNexus
{
    // 步枪AK47行为 负责装备瞄准开火IK后坐力等
    public class AK46Behaviour : MonoBehaviour, IHoldableItem, IPoolable
    {
        [Header("--- 表现与挂点 ---")]
        // 左手握点
        [Tooltip("左手应该握在哪里")]
        [SerializeField] private Transform _leftHandGoal;
        // 枪口火焰
        [Tooltip("枪口火焰特效")]
        [SerializeField] private ParticleSystem _muzzleFlash;
        // 枪口投射点
        [Tooltip("枪口瞄准参考点")]
        [SerializeField] private Transform _muzzle;
        // 玩家引用
        private BBBCharacterController _player;
        // 实例数据
        private ItemInstance _instance;
        // 配置
        private AKSO _akconfig;
        // 射速
        private float _fireRate = 0.1f;
        // 装备状态
        private bool _isEquipping;
        private float _equipEndTime;
        private float _lastFireTime;
        // 上一帧瞄准状态
        private bool _wasAiming;
        // IK调度
        private bool _ikEnableScheduled;
        private float _ikEnableTimePoint;
        private bool _ikDisableScheduled;
        private float _ikDisableTimePoint;

        // 初始化实例和配置
        public void Initialize(ItemInstance instanceData)
        {
            _instance = instanceData;
            _akconfig = _instance.BaseData as AKSO;
            if (_akconfig != null)
            {
                float interval = _akconfig.ShootInterval > 0f ? _akconfig.ShootInterval : _akconfig.FireRate;
                _fireRate = Mathf.Max(0.001f, interval);
            }
        }

        // 装备并设置IK
        public void OnEquipEnter(BBBCharacterController player)
        {
            _player = player;
            _isEquipping = true;
            if (_leftHandGoal != null && _player != null && _player.RuntimeData != null)
            {
                _player.RuntimeData.LeftHandGoal = _leftHandGoal;
                _player.RuntimeData.WantsLeftHandIK = false;
                if (_akconfig != null)
                {
                    _ikEnableScheduled = true;
                    _ikEnableTimePoint = Time.time + _akconfig.EnableIKTime;
                }
                else
                {
                    _player.RuntimeData.WantsLeftHandIK = true;
                }
            }
            
            // 🆕 立刻设置 muzzle，不等瞄准时再设置
            // 这样 FinalIK 始终有有效的引用，避免切装备时 NullRef
            if (_muzzle != null && _player != null && _player.RuntimeData != null)
            {
                _player.RuntimeData.CurrentAimReference = _muzzle;
            }
            
            float equipAnimDuration = _akconfig.EquipEndTime;
            _equipEndTime = Time.time + equipAnimDuration;
            if (_akconfig != null && _akconfig.EquipAnim != null && _player != null)
            {
                _player.AnimFacade.PlayTransition(_akconfig.EquipAnim, _akconfig.EquipAnimPlayOptions);
            }
        }

        // 每帧更新逻辑
        public void OnUpdateLogic()
        {
            if (_ikEnableScheduled && Time.time >= _ikEnableTimePoint)
            {
                if (_isEquipping)
                {
                    _ikEnableTimePoint = _equipEndTime + 0.001f;
                }
                else
                {
                    _ikEnableScheduled = false;
                    if (_player != null && _player.RuntimeData != null && _player.RuntimeData.CurrentItem == _instance)
                    {
                        _player.RuntimeData.WantsLeftHandIK = true;
                    }
                }
            }
            if (_ikDisableScheduled && Time.time >= _ikDisableTimePoint)
            {
                _ikDisableScheduled = false;
                if (_player != null && _player.RuntimeData != null)
                {
                    var current = _player.RuntimeData.CurrentItem;
                    if (current == null || current.InstanceID == _instance.InstanceID)
                    {
                        _player.RuntimeData.WantsLeftHandIK = false;
                        _player.RuntimeData.LeftHandGoal = null;
                    }
                }
            }
            if (_isEquipping)
            {
                if (Time.time >= _equipEndTime)
                {
                    _isEquipping = false;
                    if (_akconfig != null && _akconfig.EquipIdleAnim != null && _player != null)
                    {
                        _player.AnimFacade.PlayTransition(_akconfig.EquipIdleAnim, _akconfig.EquipIdleAnimOptions);
                    }
                }
                else
                {
                    return;
                }
            }
            bool isAiming = _player != null && _player.RuntimeData != null && _player.RuntimeData.IsAiming;
            if (!_isEquipping && _wasAiming != isAiming)
            {
                if (isAiming)
                {
                    if (_akconfig != null && _akconfig.AimAnim != null && _player != null)
                    {
                        _player.AnimFacade.PlayTransition(_akconfig.AimAnim, _akconfig.AnimPlayOptions);
                    }
                    if (_player != null && _player.RuntimeData != null)
                    {
                        // 🔄 瞄准时只改意图，muzzle 已在装备时设置
                        _player.RuntimeData.WantsLookAtIK = true;
                    }
                }
                else
                {
                    if (_akconfig != null && _akconfig.EquipIdleAnim != null && _player != null)
                    {
                        _player.AnimFacade.PlayTransition(_akconfig.EquipIdleAnim, _akconfig.EquipIdleAnimOptions);
                    }
                    if (_player != null && _player.RuntimeData != null)
                    {
                        // 🔄 仅改意图，不清理 CurrentAimReference
                        _player.RuntimeData.WantsLookAtIK = false;
                    }
                }
                _wasAiming = isAiming;
            }
            bool isFiring = _player != null && _player.RuntimeData != null && 
                           _player.RuntimeData.CurrentItem == _instance && 
                           _player.RuntimeData.WantsToFire;
            if (isAiming && isFiring)
            {
                TryFire();
            }
        }

        // 强制卸载
        public void OnForceUnequip()
        {
            _isEquipping = false;
            if (_muzzleFlash != null) _muzzleFlash.Stop();

            if (_akconfig != null)
            {
                _ikDisableScheduled = true;
                _ikDisableTimePoint = Time.time + _akconfig.DisableIKTime;
            }

            if (_player != null && _player.RuntimeData != null && _player.RuntimeData.CurrentItem == null)
            {
                if (_akconfig != null && _akconfig.UnEquipAnim != null)
                {
                    _player.AnimFacade.PlayTransition(_akconfig.UnEquipAnim, _akconfig.UnEquipAnimPlayOptions);
                }
            }
        }

        // 检查冷却并开火
        private void TryFire()
        {
            if (Time.time - _lastFireTime < _fireRate) return;
            _lastFireTime = Time.time;
            if (_muzzleFlash != null) _muzzleFlash.Play();
            if (_akconfig != null && _akconfig.ShootSound != null && _muzzle != null)
            {
                AudioSource.PlayClipAtPoint(_akconfig.ShootSound, _muzzle.position);
            }

            if (_akconfig != null && _akconfig.MuzzleVFXPrefab != null && _muzzle != null)
            {
                GameObject muzzleVFX;
                if (BBBNexus.SimpleObjectPoolSystem.Shared != null)
                {
                    muzzleVFX = BBBNexus.SimpleObjectPoolSystem.Shared.Spawn(_akconfig.MuzzleVFXPrefab);
                    muzzleVFX.transform.SetPositionAndRotation(_muzzle.position, _muzzle.rotation);
                    muzzleVFX.transform.SetParent(_muzzle, true);
                }
                else
                {
                    muzzleVFX = Object.Instantiate(_akconfig.MuzzleVFXPrefab, _muzzle.position, _muzzle.rotation);
                    muzzleVFX.transform.parent = _muzzle;
                }
            }

            ApplyRecoil();

            if (_akconfig != null && _akconfig.ProjectilePrefab != null && _muzzle != null)
            {
                GameObject proj;
                if (BBBNexus.SimpleObjectPoolSystem.Shared != null)
                {
                    proj = BBBNexus.SimpleObjectPoolSystem.Shared.Spawn(_akconfig.ProjectilePrefab);
                    proj.transform.SetPositionAndRotation(_muzzle.position, _muzzle.rotation);
                    proj.transform.SetParent(null, true);
                }
                else
                {
                    proj = Object.Instantiate(_akconfig.ProjectilePrefab, _muzzle.position, _muzzle.rotation);
                    proj.transform.parent = null;
                }

                var rb = proj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.velocity = _muzzle.forward * _akconfig.ProjectileSpeed;
                }
                var simple = proj.GetComponent<SimpleProjectile>();
                if (simple != null)
                {
                    simple.hitSound = _akconfig.ProjectileHitSound;
                }
            }
        }

        // 应用后坐力
        private void ApplyRecoil()
        {
            if (_player == null || _player.RuntimeData == null || _akconfig == null) return;
            float pitchNoise = Random.Range(-_akconfig.RecoilPitchRandomRange, _akconfig.RecoilPitchRandomRange);
            float yawNoise = Random.Range(-_akconfig.RecoilYawRandomRange, _akconfig.RecoilYawRandomRange);
            float finalPitch = _akconfig.RecoilPitchAngle + pitchNoise;
            float finalYaw = _akconfig.RecoilYawAngle + yawNoise;
            float yawSign = Random.value > 0.5f ? 1f : -1f;
            _player.RuntimeData.ViewPitch -= finalPitch;
            _player.RuntimeData.ViewYaw += yawSign * finalYaw;
            _player.RuntimeData.ViewPitch = Mathf.Clamp(
                _player.RuntimeData.ViewPitch,
                _player.Config.Core.PitchLimits.x,
                _player.Config.Core.PitchLimits.y
            );
        }

        public void OnSpawned()
        {
            // 保守复位（避免复用武器时残留）
            _isEquipping = false;
            _wasAiming = false;
            _ikEnableScheduled = false;
            _ikDisableScheduled = false;
            _lastFireTime = 0f;

            if (_muzzleFlash != null) _muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        }

        public void OnDespawned()
        {
            if (_muzzleFlash != null) _muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        }
    }
}