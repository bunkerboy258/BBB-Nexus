using UnityEngine;

namespace BBBNexus
{
    // 类星体加农炮的行为脚本 与步枪类似但具有更强的后坐力与冷却时间 
    // 负责装备 瞄准 开火 IK 管理等完整流程 
    public class CannonBehaviour : MonoBehaviour, IHoldableItem
    {
        [Header("--- 表现与挂点 (Visual & IK) ---")]
        // 左手握点 通常在枪管附近 
        [Tooltip("左手应该握在哪里？(将枪管上的空物体拖入)")]
        [SerializeField] private Transform _leftHandGoal;

        // 枪口火焰粒子 
        [Tooltip("枪口火焰特效")]
        [SerializeField] private ParticleSystem _muzzleFlash;

        // 枪口投射点 用于 AimIK 与投射物生成 
        [Tooltip("枪口 / 瞄准参考点 (用作 AimIK 的目标)")]
        [SerializeField] private Transform _muzzle;

        // 宿主控制器 
        private PlayerController _player;
        // 炮的实例数据 
        private ItemInstance _instance;
        // 炮的离线配置 
        private CannonSO _cannonConfig;
        // 射速间隔 炮通常比步枪慢 
        private float _fireRate = 0.1f;

        // 装备状态与时长 
        private bool _isEquipping;
        private float _equipEndTime;
        // 上一次开火时间 用于冷却控制 
        private float _lastFireTime;

        // 瞄准状态检测 
        private bool _wasAiming;

        // IK 延时启用与关闭调度 
        private bool _ikEnableScheduled;
        private float _ikEnableTimePoint;
        private bool _ikDisableScheduled;
        private float _ikDisableTimePoint;
        private bool _ikActive;

        // 灵魂注入 获得实例与配置 
        public void Initialize(ItemInstance instanceData)
        {
            _instance = instanceData;
            // 强转为炮的配置 
            _cannonConfig = _instance.BaseData as CannonSO;

            // 读取射速 炮通常有较长的冷却时间 
            if (_cannonConfig != null)
            {
                float interval = _cannonConfig.ShootInterval > 0f ? _cannonConfig.ShootInterval : _cannonConfig.FireRate;
                _fireRate = Mathf.Max(0.001f, interval);
            }

            //Debug.Log($"<color=#00FF00>[CANNON]</color> 注入成功！当前物品名: {_instance.BaseData.DisplayName}");
        }

        // 装备入场 拔枪流程 
        public void OnEquipEnter(PlayerController player)
        {
            _player = player;
            // 开始装备硬直 
            _isEquipping = true;

            // 设置左手 IK 目标 延迟启用 
            if (_leftHandGoal != null && _player != null && _player.RuntimeData != null)
            {
                _player.RuntimeData.LeftHandGoal = _leftHandGoal;
                _player.RuntimeData.WantsLeftHandIK = false;

                // 安排延迟启用 IK 
                if (_cannonConfig != null)
                {
                    _ikEnableScheduled = true;
                    _ikEnableTimePoint = Time.time + _cannonConfig.EnableIKTime;
                }
                else
                {
                    // 没配置则立即启用 
                    _player.RuntimeData.WantsLeftHandIK = true;
                    _ikActive = true;
                }

                //Debug.Log($"<color=#00FF00>[CANNON]</color> 左手 IK 目标已设置，计划在 {_ikEnableTimePoint - Time.time:0.00}s 后开启（若配置）。");
            }

            // 装备动画时长 
            float equipAnimDuration = _cannonConfig != null ? _cannonConfig.EquipEndTime : 0.5f;
            _equipEndTime = Time.time + equipAnimDuration;

            // 播放拔枪动画 
            if (_cannonConfig != null && _cannonConfig.EquipAnim != null && _player != null)
            {
                _player.AnimFacade.PlayTransition(_cannonConfig.EquipAnim, _cannonConfig.EquipAnimPlayOptions);
            }

            //Debug.Log($"<color=#FFFF00>[CANNON]</color> 正在拔枪... {equipAnimDuration} 秒内禁止开火。");
        }

        // 逻辑更新 每帧处理 IK 调度与输入 
        public void OnUpdateLogic()
        {
            // IK 延时启用 
            if (_ikEnableScheduled && Time.time >= _ikEnableTimePoint)
            {
                // 如果仍在装备则继续延迟 
                if (_isEquipping)
                {
                    _ikEnableTimePoint = _equipEndTime + 0.001f;
                }
                else
                {
                    // 装备完成 启用 IK 
                    _ikEnableScheduled = false;
                    if (_player != null && _player.RuntimeData != null && _player.RuntimeData.CurrentItem == _instance)
                    {
                        _player.RuntimeData.WantsLeftHandIK = true;
                        _ikActive = true;
                        //Debug.Log("<color=#00FF00>[CANNON]</color> 延时开启左手 IK。");
                    }
                }
            }

            // IK 延时关闭 
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
                        _ikActive = false;
                        //Debug.Log("<color=#FF0000>[CANNON]</color> 延时关闭左手 IK。");
                    }
                    else
                    {
                        //Debug.Log("<color=#FFFF00>[CANNON]</color> 跳过延时关闭 IK，因为当前装备已更换。");
                    }
                }
            }

            // 装备硬直阶段 禁止一切操作 
            if (_isEquipping)
            {
                if (Time.time >= _equipEndTime)
                {
                    // 硬直结束 
                    _isEquipping = false;
                    //Debug.Log("<color=#00FF00>[CANNON]</color> 拔枪完毕！进入战备状态。");

                    // 播放待机动画 
                    if (_cannonConfig != null && _cannonConfig.EquipIdleAnim != null && _player != null)
                    {
                        _player.AnimFacade.PlayTransition(_cannonConfig.EquipIdleAnim, _cannonConfig.EquipIdleAnimOptions);
                    }
                }
                else
                {
                    // 仍在装备 直接返回 
                    return;
                }
            }

            // 业务逻辑 

            // 检查瞄准状态 
            bool isAiming = _player != null && _player.RuntimeData != null && _player.RuntimeData.IsAiming;

            // 瞄准状态切换 
            if (!_isEquipping && _wasAiming != isAiming)
            {
                if (isAiming)
                {
                    // 进入瞄准 
                    if (_cannonConfig != null && _cannonConfig.AimAnim != null && _player != null)
                    {
                        _player.AnimFacade.PlayTransition(_cannonConfig.AimAnim, _cannonConfig.AnimPlayOptions);
                    }

                    // 设置枪口为 AimIK 基准点 
                    if (_player != null && _player.RuntimeData != null && _muzzle != null)
                    {
                        _player.RuntimeData.CurrentAimReference = _muzzle;
                        _player.RuntimeData.WantsLookAtIK = true;
                    }
                }
                else
                {
                    // 退出瞄准 
                    if (_cannonConfig != null && _cannonConfig.EquipIdleAnim != null && _player != null)
                    {
                        _player.AnimFacade.PlayTransition(_cannonConfig.EquipIdleAnim, _cannonConfig.EquipIdleAnimOptions);
                    }

                    // 清除瞄准 IK 
                    if (_player != null && _player.RuntimeData != null)
                    {
                        if (_player.RuntimeData.CurrentAimReference == _muzzle)
                            _player.RuntimeData.CurrentAimReference = null;

                        _player.RuntimeData.WantsLookAtIK = false;
                    }
                }

                _wasAiming = isAiming;
            }

            // 检测射击输入 - 从运行时数据中读取开火意图
            bool isFiring = _player != null && _player.RuntimeData != null && 
                           _player.RuntimeData.CurrentItem == _instance && 
                           _player.RuntimeData.WantsToFire;

            // 仅在瞄准时允许开火 
            if (isAiming && isFiring)
            {
                TryFire();
            }
        }

        // 强制卸载 
        public void OnForceUnequip()
        {
            // 强制重置状态 
            _isEquipping = false;

            // 停止火焰特效 
            if (_muzzleFlash != null) _muzzleFlash.Stop();

            // 安排延迟关闭 IK 
            if (_cannonConfig != null)
            {
                _ikDisableScheduled = true;
                _ikDisableTimePoint = Time.time + _cannonConfig.DisableIKTime;

                //Debug.Log($"<color=#FF8800>[CANNON]</color> 计划在 {_cannonConfig.DisableIKTime:0.00}s 后关闭左手 IK（相对于收起动画开始）。");
            }
            else
            {
                // 没配置则立即关闭 
                if (_player != null && _player.RuntimeData != null)
                {
                    _player.RuntimeData.WantsLeftHandIK = false;
                    _player.RuntimeData.LeftHandGoal = null;
                    _ikActive = false;
                }
            }

            // 清除瞄准 IK 
            if (_player != null && _player.RuntimeData != null)
            {
                if (_player.RuntimeData.CurrentAimReference == _muzzle)
                    _player.RuntimeData.CurrentAimReference = null;

                _player.RuntimeData.WantsLookAtIK = false;
            }

            // 播放收起动画 
            if (_player != null && _player.RuntimeData != null && _player.RuntimeData.CurrentItem == null)
            {
                if (_cannonConfig != null && _cannonConfig.UnEquipAnim != null)
                {
                    _player.AnimFacade.PlayTransition(_cannonConfig.UnEquipAnim, _cannonConfig.UnEquipAnimPlayOptions);
                }
            }

            //Debug.Log("<color=#FF0000>[CANNON]</color> 已发起收枪流程，等待延时关闭 IK（若配置）。");
        }

        // 尝试开火 检查冷却 生成特效与投射物 
        private void TryFire()
        {
            // 冷却检查 炮的冷却时间通常较长 
            if (Time.time - _lastFireTime < _fireRate) return;

            // 更新开火时间 
            _lastFireTime = Time.time;

            // 播放火焰特效 
            if (_muzzleFlash != null) _muzzleFlash.Play();

            // 播放开火音效 
            if (_cannonConfig != null && _cannonConfig.ShootSound != null && _muzzle != null)
            {
                AudioSource.PlayClipAtPoint(_cannonConfig.ShootSound, _muzzle.position);
            }

            // 生成枪口 VFX 
            if (_cannonConfig != null && _cannonConfig.MuzzleVFXPrefab != null && _muzzle != null)
            {
                var muzzleVFX = Object.Instantiate(_cannonConfig.MuzzleVFXPrefab, _muzzle.position, _muzzle.rotation);
                muzzleVFX.transform.parent = _muzzle;
            }

            // 应用后坐力 炮的后坐力通常比步枪更强 
            ApplyRecoil();

            // 生成投射物 
            if (_cannonConfig != null && _cannonConfig.ProjectilePrefab != null && _muzzle != null)
            {
                var proj = Object.Instantiate(_cannonConfig.ProjectilePrefab, _muzzle.position, _muzzle.rotation);
                proj.transform.parent = null;

                // 设置投射物速度 
                var rb = proj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.velocity = _muzzle.forward * _cannonConfig.ProjectileSpeed;
                }

                // 注入碰撞音效 
                var simple = proj.GetComponent<SimpleProjectile>();
                if (simple != null)
                {
                    simple.hitSound = _cannonConfig.ProjectileHitSound;
                }
            }

            //Debug.Log("<color=#FF8800>[CANNON]</color> 砰！检测到瞄准状态，成功开火！");
        }

        // 应用后坐力 炮的后坐力更大 
        private void ApplyRecoil()
        {
            if (_player == null || _player.RuntimeData == null || _cannonConfig == null) return;

            // 计算随机化的后坐力 
            float pitchNoise = Random.Range(-_cannonConfig.RecoilPitchRandomRange, _cannonConfig.RecoilPitchRandomRange);
            float yawNoise = Random.Range(-_cannonConfig.RecoilYawRandomRange, _cannonConfig.RecoilYawRandomRange);

            float finalPitch = _cannonConfig.RecoilPitchAngle + pitchNoise;
            float finalYaw = _cannonConfig.RecoilYawAngle + yawNoise;

            // 随机偏向左或右 
            float yawSign = Random.value > 0.5f ? 1f : -1f;

            // 修改视角参数 
            _player.RuntimeData.ViewPitch -= finalPitch;
            _player.RuntimeData.ViewYaw += yawSign * finalYaw;

            // 应用俯仰限制 
            _player.RuntimeData.ViewPitch = Mathf.Clamp(
                _player.RuntimeData.ViewPitch,
                _player.Config.Core.PitchLimits.x,
                _player.Config.Core.PitchLimits.y
            );

            //Debug.Log($"<color=#FF8800>[CANNON]</color> 一次性后坐力已应用！俯仰: {finalPitch}°, 偏航: {finalYaw}° (yawSign: {yawSign})");
        }
    }
}
