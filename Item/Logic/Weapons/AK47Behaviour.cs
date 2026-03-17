using UnityEngine;

namespace BBBNexus
{
    // 步枪 AK47 的行为脚本 完整的射击逻辑与 IK 管理 
    // 负责装备 瞄准 开火 IK 延时启用 后坐力等全面流程 
    // 简单的逻辑说明:
    //  1) 装备时有硬直 期间禁止开火 
    //  2) 瞄准时激活枪口指向 IK 与头部查看 IK 
    //  3) 只有瞄准状态才能开火 
    //  4) 开火时生成特效 音效 投射物 
    //  5) 后坐力通过修改视角参数 而不是旋转 确保被权威系统整合 
    // 仅作为一个简单的演示物品行为的示例 真实项目中可以根据需要进行扩展和优化
    public class AK46Behaviour : MonoBehaviour, IHoldableItem
    {
        // UI 分区标记 方便编辑器检查配置 

        [Header("--- 表现与挂点 (Visual & IK) ---")]
        // 左手抓枪的目标位置 通常在枪管附近 
        [Tooltip("左手应该握在哪里？(将枪管上的空物体拖入)")]
        [SerializeField] private Transform _leftHandGoal;

        // 枪口火焰粒子特效 开火时播放 
        [Tooltip("枪口火焰特效")]
        [SerializeField] private ParticleSystem _muzzleFlash;

        // 枪口投射点与指向基准 用作 AimIK 的目标 让瞄准指向枪口而非头部 
        [Tooltip("枪口 / 瞄准参考点 (用作 AimIK 的目标)")]
        [SerializeField] private Transform _muzzle;

        // 宿主控制器 访问动画系统 运行时状态 输入等 
        private PlayerController _player;
        // 该枪的实例数据 包含弹药 耐久等运行时属性 
        private ItemInstance _instance;
        // 离线配置 包含所有动画参数 开火冷却 后坐力等 
        private AKSO _akconfig;
        // 射速 从配置中读取或用默认值 
        private float _fireRate = 0.1f;

        // 装备状态标志 
        private bool _isEquipping;
        // 装备动画结束时间 超过此时间才能开火 
        private float _equipEndTime;
        // 上一次开火的时间戳 用于冷却控制 
        private float _lastFireTime;

        // 上一帧的瞄准状态 用于检测瞄准模式切换 
        private bool _wasAiming;

        // IK 延时启用调度 
        private bool _ikEnableScheduled;
        private float _ikEnableTimePoint;
        // IK 延时关闭调度 
        private bool _ikDisableScheduled;
        private float _ikDisableTimePoint;
        // IK 当前是否激活 
        private bool _ikActive;

        // 灵魂注入 枪械获得实例与配置 
        public void Initialize(ItemInstance instanceData)
        {
            _instance = instanceData;
            // 强转为步枪配置 
            _akconfig = _instance.BaseData as AKSO;

            // 读取射速 优先使用显式间隔 否则使用配置的射速率 
            if (_akconfig != null)
            {
                float interval = _akconfig.ShootInterval > 0f ? _akconfig.ShootInterval : _akconfig.FireRate;
                _fireRate = Mathf.Max(0.001f, interval);
            }

            //Debug.Log($"<color=#00FF00>[AK46]</color> 实例注入成功！当前物品名: {_instance.BaseData.DisplayName}");
        }

        // 装备入场 开始拔枪流程 设置 IK 目标 
        public void OnEquipEnter(PlayerController player)
        {
            _player = player;
            // 开始装备硬直 期间禁止开火 
            _isEquipping = true;

            // 设置左手 IK 目标但延迟启用 确保拔枪动画完成后再接上 IK 
            if (_leftHandGoal != null && _player != null && _player.RuntimeData != null)
            {
                _player.RuntimeData.LeftHandGoal = _leftHandGoal;
                _player.RuntimeData.WantsLeftHandIK = false;

                // 如果配置了延迟时间则安排延迟启用 
                if (_akconfig != null)
                {
                    _ikEnableScheduled = true;
                    _ikEnableTimePoint = Time.time + _akconfig.EnableIKTime;
                }
                else
                {
                    // 没配置则立即启用 
                    _player.RuntimeData.WantsLeftHandIK = true;
                    _ikActive = true;
                }

                //Debug.Log($"<color=#00FF00>[AK46]</color> 左手 IK 目标已设置，计划在 {_ikEnableTimePoint - Time.time:0.00}s 后开启（若配置）。");
            }

            // 装备动画的时长 在此期间禁止开火 
            float equipAnimDuration = _akconfig.EquipEndTime;
            _equipEndTime = Time.time + equipAnimDuration;

            // 播放拔枪动画 
            if (_akconfig != null && _akconfig.EquipAnim != null && _player != null)
            {
                _player.AnimFacade.PlayTransition(_akconfig.EquipAnim, _akconfig.EquipAnimPlayOptions);
            }

            //Debug.Log($"<color=#FFFF00>[AK46]</color> 正在拔枪... {equipAnimDuration} 秒内禁止开火。");
        }

        // 逻辑更新 每帧处理 IK 调度 瞄准切换 开火输入 
        public void OnUpdateLogic()
        {
            // IK 延时启用检查 
            if (_ikEnableScheduled && Time.time >= _ikEnableTimePoint)
            {
                // 如果仍在装备则延迟到装备完成 
                if (_isEquipping)
                {
                    _ikEnableTimePoint = _equipEndTime + 0.001f;
                }
                else
                {
                    // 装备完成 启用 IK 
                    _ikEnableScheduled = false;
                    // 仅当该枪仍为当前装备时启用 IK 避免已切枪的枪仍在作用 IK 
                    if (_player != null && _player.RuntimeData != null && _player.RuntimeData.CurrentItem == _instance)
                    {
                        _player.RuntimeData.WantsLeftHandIK = true;
                        _ikActive = true;
                        //Debug.Log("<color=#00FF00>[AK46]</color> 延时开启左手 IK。");
                    }
                }
            }

            // IK 延时关闭检查 
            if (_ikDisableScheduled && Time.time >= _ikDisableTimePoint)
            {
                _ikDisableScheduled = false;
                // 只有当该枪应该关闭 IK 时才关闭 避免影响新装备的 IK 
                if (_player != null && _player.RuntimeData != null)
                {
                    var current = _player.RuntimeData.CurrentItem;
                    // 检查是否仍是该枪或已经卸载 
                    if (current == null || current.InstanceID == _instance.InstanceID)
                    {
                        _player.RuntimeData.WantsLeftHandIK = false;
                        _player.RuntimeData.LeftHandGoal = null;
                        _ikActive = false;
                        //Debug.Log("<color=#FF0000>[AK46]</color> 延时关闭左手 IK。");
                    }
                    else
                    {
                        //Debug.Log("<color=#FFFF00>[AK46]</color> 跳过延时关闭 IK，因为当前装备已更换。");
                    }
                }
            }

            // 装备硬直阶段 禁止一切操作 
            if (_isEquipping)
            {
                if (Time.time >= _equipEndTime)
                {
                    // 硬直结束 转入待机 
                    _isEquipping = false;
                    //Debug.Log("<color=#00FF00>[AK46]</color> 拔枪完毕！进入战备状态。");

                    // 播放装备后的待机动画 如果有配置的话 
                    if (_akconfig != null && _akconfig.EquipIdleAnim != null && _player != null)
                    {
                        _player.AnimFacade.PlayTransition(_akconfig.EquipIdleAnim, _akconfig.EquipIdleAnimOptions);
                    }
                }
                else
                {
                    // 依然在装备 直接返回 不响应任何输入 
                    return;
                }
            }

            // 业务逻辑执行阶段 

            // 检查当前是否处于瞄准状态 
            bool isAiming = _player != null && _player.RuntimeData != null && _player.RuntimeData.IsAiming;

            // 瞄准状态发生切换时 切换相应的动画与 IK 配置 
            if (!_isEquipping && _wasAiming != isAiming)
            {
                if (isAiming)
                {
                    // 进入瞄准状态 播放瞄准动画 
                    if (_akconfig != null && _akconfig.AimAnim != null && _player != null)
                    {
                        _player.AnimFacade.PlayTransition(_akconfig.AimAnim, _akconfig.AnimPlayOptions);
                    }

                    // 设置枪口为 AimIK 的基准点 这样瞄准指向会以枪口为中心 而不是头部 
                    if (_player != null && _player.RuntimeData != null && _muzzle != null)
                    {
                        _player.RuntimeData.CurrentAimReference = _muzzle;
                        // 启用头部指向 IK 让头部看向准星 
                        _player.RuntimeData.WantsLookAtIK = true;
                    }
                }
                else
                {
                    // 退出瞄准状态 回到待机动画 
                    if (_akconfig != null && _akconfig.EquipIdleAnim != null && _player != null)
                    {
                        _player.AnimFacade.PlayTransition(_akconfig.EquipIdleAnim, _akconfig.EquipIdleAnimOptions);
                    }

                    // 清除瞄准相关的 IK 配置 
                    if (_player != null && _player.RuntimeData != null)
                    {
                        // 仅当枪口仍是 AimReference 时才清除 避免清除其他来源的配置 
                        if (_player.RuntimeData.CurrentAimReference == _muzzle)
                            _player.RuntimeData.CurrentAimReference = null;

                        // 关闭头部指向 IK 
                        _player.RuntimeData.WantsLookAtIK = false;
                    }
                }

                // 同步瞄准状态标志 
                _wasAiming = isAiming;
            }

            // 检测射击按键 - 从运行时数据中读取开火意图
            bool isFiring = _player != null && _player.RuntimeData != null && 
                           _player.RuntimeData.CurrentItem == _instance && 
                           _player.RuntimeData.WantsToFire;
            
            // 仅在瞄准时才允许开火 确保瞄准的必要性 
            if (isAiming && isFiring)
            {
                TryFire();
            }
        }

        // 强制卸载 通常由状态机或装备管理器在切枪 翻滚等情况下调用 
        public void OnForceUnequip()
        {
            // 强制重置装备状态 
            _isEquipping = false;

            // 停止火焰特效 
            if (_muzzleFlash != null) _muzzleFlash.Stop();

            // 根据配置延迟关闭 IK 以配合收枪动画 
            if (_akconfig != null)
            {
                _ikDisableScheduled = true;
                _ikDisableTimePoint = Time.time + _akconfig.DisableIKTime;

                //Debug.Log($"<color=#FF8800>[AK46]</color> 计划在 {_akconfig.DisableIKTime:0.00}s 后关闭左手 IK（相对于收起动画开始）。");
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

            // 清理瞄准相关的 IK 
            if (_player != null && _player.RuntimeData != null)
            {
                if (_player.RuntimeData.CurrentAimReference == _muzzle)
                    _player.RuntimeData.CurrentAimReference = null;

                _player.RuntimeData.WantsLookAtIK = false;
            }

            // 播放收起动画 仅当当前装备已设为 null 时 
            if (_player != null && _player.RuntimeData != null && _player.RuntimeData.CurrentItem == null)
            {
                if (_akconfig != null && _akconfig.UnEquipAnim != null)
                {
                    _player.AnimFacade.PlayTransition(_akconfig.UnEquipAnim, _akconfig.UnEquipAnimPlayOptions);
                }
            }

            //Debug.Log($"<color=#FF0000>[AK46]</color> 已发起收枪流程，等待延时关闭 IK（若配置）。");
        }

        // 尝试开火 检查冷却与弹药 生成特效与投射物 
        private void TryFire()
        {
            // 射速冷却检查 未到冷却时间则不开火 
            if (Time.time - _lastFireTime < _fireRate) return;

            // 弹药检查 如果扩展了 ItemInstance 可以在这里检查弹药数量 
            // 暂时跳过 假设弹药充足 

            // 真正开火 更新上一次开火时间 
            _lastFireTime = Time.time;

            // 播放枪口火焰特效 
            if (_muzzleFlash != null) _muzzleFlash.Play();

            // 播放射击音效 
            if (_akconfig != null && _akconfig.ShootSound != null && _muzzle != null)
            {
                AudioSource.PlayClipAtPoint(_akconfig.ShootSound, _muzzle.position);
            }

            // 生成枪口 VFX 特效 
            if (_akconfig != null && _akconfig.MuzzleVFXPrefab != null && _muzzle != null)
            {
                var muzzleVFX = Object.Instantiate(_akconfig.MuzzleVFXPrefab, _muzzle.position, _muzzle.rotation);
                // 让特效跟随枪口 
                muzzleVFX.transform.parent = _muzzle;
            }

            // 应用后坐力 修改视角使其自然晃动 
            ApplyRecoil();

            // 生成投射物 
            if (_akconfig != null && _akconfig.ProjectilePrefab != null && _muzzle != null)
            {
                var proj = Object.Instantiate(_akconfig.ProjectilePrefab, _muzzle.position, _muzzle.rotation);
                // 投射物不应该成为枪的子物体 应该独立存在于世界 
                proj.transform.parent = null;

                // 设置投射物的初始速度 
                var rb = proj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.velocity = _muzzle.forward * _akconfig.ProjectileSpeed;
                }

                // 如果投射物有简单投射脚本 注入碰撞音效配置 
                var simple = proj.GetComponent<SimpleProjectile>();
                if (simple != null)
                {
                    simple.hitSound = _akconfig.ProjectileHitSound;
                }
            }

            //Debug.Log($"<color=#FF8800>[AK46]</color> 砰！检测到瞄准状态，成功开火！");
        }

        // 应用后坐力效果 通过修改视角参数而不是旋转确保被权威系统整合 
        // 这样后坐力会被 ViewRotationProcessor 纳入最终的权威旋转 不会被下一帧重置 
        private void ApplyRecoil()
        {
            if (_player == null || _player.RuntimeData == null || _akconfig == null) return;

            // 计算随机化的俯仰与偏航 增加射击的"手感" 
            float pitchNoise = Random.Range(-_akconfig.RecoilPitchRandomRange, _akconfig.RecoilPitchRandomRange);
            float yawNoise = Random.Range(-_akconfig.RecoilYawRandomRange, _akconfig.RecoilYawRandomRange);

            float finalPitch = _akconfig.RecoilPitchAngle + pitchNoise;
            float finalYaw = _akconfig.RecoilYawAngle + yawNoise;

            // 修改视角参数而不是权威旋转 这样后坐力会被纳入权威参考系计算 
            // 俯仰减小使视角向上晃 
            _player.RuntimeData.ViewPitch -= finalPitch;
            // 偏航左右随机 
            float yawSign = Random.value > 0.5f ? 1f : -1f;
            _player.RuntimeData.ViewYaw += yawSign * finalYaw;

            // 应用俯仰限制 确保视角不会超出配置的范围 
            _player.RuntimeData.ViewPitch = Mathf.Clamp(
                _player.RuntimeData.ViewPitch,
                _player.Config.Core.PitchLimits.x,
                _player.Config.Core.PitchLimits.y
            );

            //Debug.Log($"<color=#FF8800>[AK46]</color> 一次性后坐力已应用！俯仰: {finalPitch}°, 偏航: {finalYaw}° (yawSign: {yawSign})");
        }
    }
}