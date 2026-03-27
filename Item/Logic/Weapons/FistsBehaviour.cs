using Animancer;
using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 拳头行为：无 Mesh，纯逻辑。
    /// 读黑板 WantsToAction → 驱动全身覆盖层播连招动画。
    /// </summary>
    public class FistsBehaviour : MonoBehaviour, IHoldableItem, IPoolable
    {
        private BBBCharacterController _player;
        private FistsSO _config;
        private ItemInstance _instance;
        private FistHitbox _hitbox;

        private int _comboIndex;

        // 前摇 / 收招状态
        private bool _isEnteringStance;      // 正在播前摇，等待结束后出第一拳
        private bool _isExitingStance;       // 正在播收招
        private Quaternion _rotationOnEnter; // 进入前摇时的旋转备份，用于被打断时恢复

        // 攻击计时
        private bool _isAttacking;
        private float _comboWindowOpenTime;  // 甜蜜期开始时间
        private float _comboWindowCloseTime; // 甜蜜期结束时间（含宽限）
        private bool _inputBuffered;         // 甜蜜期前提前按键的缓冲

        // ── IHoldableItem ──────────────────────────────────────────

        public void Initialize(ItemInstance instanceData)
        {
            _instance = instanceData;
            _config = instanceData?.GetSODataAs<FistsSO>();
            Debug.Log($"[Fists] Initialize: instance={instanceData != null}, config={_config != null}");
        }

        public void OnEquipEnter(BBBCharacterController player)
        {
            _player = player;
            _hitbox = GetComponentInChildren<FistHitbox>();
            if (_hitbox != null) _hitbox.SetOwner(player);
            ResetComboState();
            Debug.Log($"[Fists] OnEquipEnter: player={player != null}, config={_config != null}, comboClips={_config?.ComboSequence?.Length ?? 0}");
        }

        public void OnUpdateLogic()
        {
            if (_player == null || _config == null) return;
            if (_config.ComboSequence == null || _config.ComboSequence.Length == 0) return;
            if (_player.RuntimeData.Arbitration.BlockAction) return;

            bool wantsAttack = _player.RuntimeData.WantsToAction;

            // 前摇期间：缓冲输入，等 OnStanceEnd 再出招
            if (_isEnteringStance)
            {
                if (wantsAttack)
                {
                    _inputBuffered = true;
                    _player.InputPipeline.ConsumePrimaryAttackPressed();
                }
                return;
            }

            // 收招期间：等 Override 结束后才允许新一轮，不接受输入
            if (_isExitingStance)
            {
                if (!_player.RuntimeData.Override.IsActive)
                    _isExitingStance = false;
                return;
            }

            if (_isAttacking)
            {
                float now = Time.time;

                // 甜蜜期前：提前按键就缓冲起来，消费掉避免重复
                if (wantsAttack && now < _comboWindowOpenTime)
                {
                    _inputBuffered = true;
                    _player.InputPipeline.ConsumePrimaryAttackPressed();
                }

                // 甜蜜期内：有输入（缓冲或实时）则续招
                bool inWindow = now >= _comboWindowOpenTime && now <= _comboWindowCloseTime;
                if (inWindow && (_inputBuffered || wantsAttack))
                {
                    _inputBuffered = false;
                    TriggerAttack();
                    return;
                }

                // 甜蜜期结束还没续招：播收招
                if (now > _comboWindowCloseTime)
                    TriggerExitStance();

                return;
            }

            // 空闲状态：接受新一轮攻击
            if (wantsAttack)
                TriggerEnterStanceOrAttack();
        }

        public void OnForceUnequip()
        {
            // 被打断时（受击/闪避），收招没播完，手动把旋转转回去
            if (_isEnteringStance || _isAttacking || _isExitingStance)
                _player.transform.rotation = _rotationOnEnter;

            ResetComboState();
            Debug.Log("[Fists] OnForceUnequip");
        }

        // ── 私有方法 ───────────────────────────────────────────────

        private void TriggerEnterStanceOrAttack()
        {
            _rotationOnEnter = _player.transform.rotation;

            var stance = _config.EnterStanceAnim;
            if (stance != null && stance.Clip != null)
            {
                _isEnteringStance = true;
                _player.InputPipeline.ConsumePrimaryAttackPressed();
                var req = new ActionRequest(stance.Clip, _config.ComboPriority, stance.FadeDuration, true);
                _player.RequestOverride(in req, flushImmediately: true);
                _player.AnimFacade.SetOverrideOnEndCallback(OnStanceEnd);
            }
            else
            {
                TriggerAttack();
            }
        }

        private void OnStanceEnd()
        {
            _isEnteringStance = false;
            _inputBuffered = false;
            TriggerAttack();
        }

        private void TriggerExitStance()
        {
            // 连招结束，重置除 _isExitingStance 外的所有状态
            _comboIndex = 0;
            _isAttacking = false;
            _inputBuffered = false;
            _comboWindowOpenTime = 0f;
            _comboWindowCloseTime = 0f;

            var exit = _config.ExitStanceAnim;
            if (exit != null && exit.Clip != null)
            {
                _isExitingStance = true;
                var req = new ActionRequest(exit.Clip, _config.ComboPriority, exit.FadeDuration, true);
                _player.RequestOverride(in req, flushImmediately: true);
                // 不替换 OverrideState 的回调，让它自然把状态机切回移动
            }
            else
            {
                _isExitingStance = false;
            }
        }

        private void TriggerAttack()
        {
            var transition = _comboIndex < _config.ComboSequence.Length
                ? _config.ComboSequence[_comboIndex]
                : null;

            if (transition == null || transition.Clip == null)
            {
                ResetComboState();
                return;
            }

            var clip = transition.Clip;
            Debug.Log($"[Fists] 出招第 {_comboIndex + 1} 段：{clip.name}");

            _comboIndex = (_comboIndex + 1) % _config.ComboSequence.Length;
            _isAttacking = true;
            _inputBuffered = false;
            _comboWindowOpenTime = Time.time + clip.length * _config.ComboWindowStart;
            _comboWindowCloseTime = Time.time + clip.length + _config.ComboLateBuffer;

            _player.InputPipeline.ConsumePrimaryAttackPressed();
            _hitbox?.Activate();

            var req = new ActionRequest(clip, _config.ComboPriority, transition.FadeDuration, true);
            _player.RequestOverride(in req, flushImmediately: true);
        }

        private void ResetComboState()
        {
            _comboIndex = 0;
            _isAttacking = false;
            _isEnteringStance = false;
            _isExitingStance = false;
            _inputBuffered = false;
            _comboWindowOpenTime = 0f;
            _comboWindowCloseTime = 0f;
            _hitbox?.Deactivate();
        }

        // ── IPoolable ──────────────────────────────────────────────

        public void OnSpawned()
        {
            ResetComboState();
        }

        public void OnDespawned() { }
    }
}
