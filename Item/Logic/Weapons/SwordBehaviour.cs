using UnityEngine;
using Animancer;

namespace BBBNexus
{
    // 剑：简化的持有与单段攻击逻辑
    public class SwordBehaviour : MonoBehaviour, IHoldableItem, IPoolable
    {
        private enum SwordPhase { None, Equipping, Idle, Unequipping, Attacking }

        private BBBCharacterController _player;
        private ItemInstance _instance;
        private SwordSO _config;

        private SwordPhase _phase = SwordPhase.None;
        private float _equipEndTime;
        private float _unequipEndTime;
        private bool _lastFireInput;

        // 兜底时间
        private float _attackFallbackEndTime;
        private const float AttackFallbackExtraTime = 0.25f;

        private const int UpperBodyLayer = 1;
        private const float DefaultFadeOut = 0.15f;

        public void Initialize(ItemInstance instanceData)
        {
            _instance = instanceData;
            _config = _instance.BaseData as SwordSO;
        }

        // 装备
        public void OnEquipEnter(BBBCharacterController player)
        {
            _player = player;
            _player.RuntimeData.WantsLeftHandIK = false;
            _player.RuntimeData.LeftHandGoal = null;
            _player.RuntimeData.WantsRightHandIK= false;
            _player.RuntimeData.RightHandGoal = null;
            _phase = SwordPhase.Equipping;
            _equipEndTime = Time.time + (_config != null ? _config.EquipEndTime : 0f);
            _lastFireInput = false;

            if (_player?.AnimFacade != null)
                _player.AnimFacade.SetLayerWeight(UpperBodyLayer, 1f, 0.05f);

            if (_player?.AnimFacade != null && _config != null && _config.EquipAnim != null)
                _player.AnimFacade.PlayTransition(_config.EquipAnim, _config.EquipAnimPlayOptions);

            _player?.AnimFacade?.ClearOnEndCallback(UpperBodyLayer);
        }

        // 更新
        public void OnUpdateLogic()
        {
            if (_player == null || _player.AnimFacade == null || _config == null) return;

            if (_phase == SwordPhase.Attacking && Time.time >= _attackFallbackEndTime)
            {
                EndAttackLocally();
                return;
            }

            bool fire = _player.RuntimeData != null && _player.RuntimeData.WantsToFire;
            bool fireDown = fire && !_lastFireInput;
            _lastFireInput = fire;

            if (_phase == SwordPhase.Attacking && _player.RuntimeData != null)
            {
                var rd = _player.RuntimeData;
                if (rd.WantsToRoll || rd.WantsToDodge || rd.WantsToVault || rd.WantsToJump)
                {
                    CancelAttack();
                    return;
                }
            }

            switch (_phase)
            {
                case SwordPhase.Equipping:
                    if (Time.time >= _equipEndTime)
                    {
                        _phase = SwordPhase.Idle;
                        _player.AnimFacade.ClearOnEndCallback(UpperBodyLayer);
                        _player.AnimFacade.SetLayerWeight(UpperBodyLayer, 0f, DefaultFadeOut);
                    }
                    return;

                case SwordPhase.Unequipping:
                    if (Time.time >= _unequipEndTime) _phase = SwordPhase.None;
                    return;

                case SwordPhase.Idle:
                    if (fireDown) StartAttack();
                    return;

                case SwordPhase.Attacking:
                    return;

                default:
                    return;
            }
        }

        // 强制卸载
        public void OnForceUnequip()
        {
            if (_player == null || _player.AnimFacade == null) return;

            CancelAttack();

            _phase = SwordPhase.Unequipping;
            _unequipEndTime = Time.time + (_config != null ? _config.EquipEndTime : 0f);

            if (_player.RuntimeData != null && _player.RuntimeData.CurrentItem == null)
            {
                if (_config != null && _config.UnEquipAnim != null)
                {
                    _player.AnimFacade.SetLayerWeight(UpperBodyLayer, 1f, 0.05f);
                    _player.AnimFacade.PlayTransition(_config.UnEquipAnim, _config.UnEquipAnimPlayOptions);
                }
            }

            if (_config != null && _config.EquipIdleAnim != null)
                _player.AnimFacade.SetLayerWeight(UpperBodyLayer, 0f, DefaultFadeOut);
        }

        // 攻击
        private void StartAttack()
        {
            if (_config == null || _player == null) return;
            if (_phase == SwordPhase.Equipping || _phase == SwordPhase.Unequipping) return;

            var request = _config.AttackRequest;
            if (request.Clip == null) return;

            _phase = SwordPhase.Attacking;
            float clipLen = request.Clip.length;
            _attackFallbackEndTime = Time.time + Mathf.Max(0.05f, clipLen + AttackFallbackExtraTime);

            if (_config.SwingSound != null)
                AudioSource.PlayClipAtPoint(_config.SwingSound, transform.position);

            _player.RequestOverride(request, flushImmediately: true);
        }

        // 本地结束攻击
        private void EndAttackLocally()
        {
            if (_player == null || _player.AnimFacade == null) return;
            if (_phase != SwordPhase.Attacking) return;
            _phase = SwordPhase.Idle;
        }

        // 取消攻击
        private void CancelAttack()
        {
            if (_player == null) return;
            if (_phase == SwordPhase.Attacking) _phase = SwordPhase.Idle;
        }

        public void OnSpawned()
        {
            _phase = SwordPhase.None;
            _equipEndTime = 0f;
            _unequipEndTime = 0f;
            _lastFireInput = false;
            _attackFallbackEndTime = 0f;
        }

        public void OnDespawned()
        {
            _phase = SwordPhase.None;
            _lastFireInput = false;
        }
    }
}
