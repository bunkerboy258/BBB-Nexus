using Characters.Player;
using Items.Core;
using Items.Data.Weapons;
using UnityEngine;

namespace Items.Logic.Weapons
{
    /// <summary>
    /// AK47 实体表现脚本：挂载在 AK47 的 Prefab 上。
    /// 它的生杀大权由 UpperBodyHoldItemState 掌控，但具体怎么开枪自己说了算。
    /// </summary>
    public class AK47Behaviour : MonoBehaviour, IHoldableItem
    {
        [Header("--- 表现与挂点配置 ---")]
        [Tooltip("左手应该握在哪？(把子物体拖进来)")]
        [SerializeField] private Transform _leftHandGrip;

        [Tooltip("枪口火花特效")]
        [SerializeField] private ParticleSystem _muzzleFlash;

        // --- 运行时的宿主与灵魂 ---
        private PlayerController _player;
        private ItemInstance _mySoul;       // 纯逻辑实例 (记录当前真实子弹数)
        private RangedWeaponSO _weaponData; // 只读静态图纸 (射速、动画)

        // --- 内部状态 ---
        private bool _isEquipping;
        private float _equipEndTime; // 拔枪硬直结束时间
        private float _lastFireTime;

        // ==========================================
        // 1. 灵魂注入 (EquipmentDriver 生成模型时立刻调用)
        // ==========================================
        public void Initialize(ItemInstance instanceData)
        {
            _mySoul = instanceData;
            _weaponData = _mySoul.GetSODataAs<RangedWeaponSO>();

            // 如果刚拿到枪（没用过），填满弹药
            // 注意：真实项目中这个逻辑可以放在 Inventory 生成 Instance 时
            if (_mySoul.CurrentAmount == 0) _mySoul.CurrentAmount = _weaponData.MaxAmmo;
        }

        // ==========================================
        // 2. 状态机赋权：刚切出武器时调用
        // ==========================================
        public void OnEquipEnter(PlayerController player)
        {
            _player = player;
            _isEquipping = true;

            // ? 【核心】：直接向黑板宣告主权，开启左手 IK！
            if (_leftHandGrip != null)
            {
                _player.RuntimeData.WantsLeftHandIK = true;
                _player.RuntimeData.LeftHandGoal = _leftHandGrip;
            }

            // ? 【核心】：播放拔枪动画，并精准获取动画时长作为硬直时间
            if (_weaponData != null && _weaponData.EquipAnim.Clip != null)
            {
                _player.AnimFacade.PlayTransition(_weaponData.EquipAnim, _weaponData.GetUpperBodyPlayOptions());

                // 算出动画什么时候播完（当前时间 + 动画长度）
                _equipEndTime = Time.time + _weaponData.EquipAnim.Clip.length;
            }
            else
            {
                _equipEndTime = Time.time; // 没动画，瞬间拔枪
            }
        }

        // ==========================================
        // 3. 武器舞台：状态机每帧无脑转发
        // ==========================================
        public void OnUpdateLogic()
        {
            // --- 阶段 A：硬直拦截 ---
            if (_isEquipping)
            {
                if (Time.time >= _equipEndTime)
                {
                    _isEquipping = false;
                    Debug.Log("<color=yellow>[AK47]</color> 拔枪动作完成，可以执行操作！");
                }
                else
                {
                    return; // 还在拔枪，强行 return，不理会任何按键
                }
            }

            // --- 阶段 B：业务逻辑执行 ---

            // 1. 从黑板读取瞄准意图 (你的 AimIntentProcessor 已经帮你算好了)
            bool isAiming = _player.RuntimeData.IsAiming;

            // 2. 从输入汇总类直接读取开火按键
            // 【注意】：请把 .FireInput 换成你 PlayerInputReader 里实际的开火 bool 变量名！
            bool isFiring = _player.InputReader.FireInput;

            // 你的需求：可以进行瞄准，瞄准时可以开火
            if (isAiming)
            {
                // 如果需要，这里可以调用 _player.AnimFacade.PlayTransition(...) 播放持枪瞄准的待机动画

                if (isFiring)
                {
                    TryFire();
                }
            }
            else
            {
                // 没瞄准时，可以播放持枪腰射待机动画 (EquipIdleAnim)
            }
        }

        // ==========================================
        // 4. 被迫下线：受击、翻滚、切枪时调用
        // ==========================================
        public void OnForceUnequip()
        {
            _isEquipping = false;

            if (_muzzleFlash != null) _muzzleFlash.Stop();

            // ? 【核心】：擦干屁股，把黑板里的 IK 请求撤销！
            if (_player != null && _player.RuntimeData != null)
            {
                _player.RuntimeData.WantsLeftHandIK = false;
                _player.RuntimeData.LeftHandGoal = null;
            }
        }

        // ==========================================
        // 私有方法：开火判定
        // ==========================================
        private void TryFire()
        {
            // 1. 射速限制校验
            if (Time.time - _lastFireTime < _weaponData.FireRate) return;

            // 2. 弹药校验 (扣的是内存里灵魂的数据！)
            if (_mySoul.CurrentAmount <= 0)
            {
                Debug.Log("<color=red>[AK47]</color> 咔咔咔！没子弹了！");
                return;
            }

            // 3. 成功开火
            _lastFireTime = Time.time;
            _mySoul.CurrentAmount--;

            if (_muzzleFlash != null) _muzzleFlash.Play();

            Debug.Log($"<color=orange>[AK47]</color> 砰！正在瞄准开火！剩余弹药: {_mySoul.CurrentAmount}");
        }
    }
}