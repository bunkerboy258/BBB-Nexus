using UnityEngine;
using Characters.Player.Data;

namespace Characters.Player.Core
{
    /// <summary>
    /// 职责：处理瞄准相关的输入意图。
    /// 逻辑：按住右键进入瞄准，松开后保持一小段时间再退出。
    /// </summary>
    public class AimIntentProcessor
    {
        private PlayerController _player;
        private PlayerRuntimeData _data;
        private PlayerSO _config; // 假设 SO 里有 AimHoldTime

        // --- 内部状态 ---
        private bool _isAimInputHeld;      // 玩家是否按着瞄准键
        private float _timeSinceAimCanceled; // 从松开瞄准键开始计时

        public AimIntentProcessor(PlayerController player)
        {
            _player = player;
            _data = player.RuntimeData;
            _config = player.Config;

            // 订阅输入事件
            _player.InputReader.OnAimStarted += HandleAimStarted;  
            _player.InputReader.OnAimCanceled += HandleAimCanceled;
        }

        // 别忘了在 PlayerController.OnDestroy 中解绑
        ~AimIntentProcessor()
        {
            if (_player != null && _player.InputReader != null)
            {
                _player.InputReader.OnAimStarted -= HandleAimStarted;
                _player.InputReader.OnAimCanceled -= HandleAimCanceled;
            }
        }

        /// <summary>
        /// 由 PlayerController 每帧调用。
        /// </summary>
        public void Update()
        {
            // 如果玩家按着键，强制进入瞄准状态
            if (_isAimInputHeld)
            {
                _data.IsAiming = true;
                return;
            }
            else
            {
                _data.IsAiming = false;
            }

            // 如果玩家没按键，且当前处于瞄准状态 (意味着是松手后的延迟期)
            if (!_isAimInputHeld && _data.IsAiming)
            {
                // 检查延迟是否结束
                if (Time.time - _timeSinceAimCanceled >= _config.AimHoldDuration) 
                {
                    _data.IsAiming = false;
                }
            }
        }

        // --- 事件回调 ---

        private void HandleAimStarted()
        {
            _isAimInputHeld = true;
        }

        private void HandleAimCanceled()
        {
            _isAimInputHeld = false;

            // 记录松手的瞬间
            _timeSinceAimCanceled = Time.time;
        }
    }
}
