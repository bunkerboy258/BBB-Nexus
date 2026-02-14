using UnityEngine;
using Characters.Player.Data;
using Characters.Player.Input;

namespace Characters.Player.Processing
{
    /// <summary>
    /// 职责: 处理跳跃键相关的交互意图分发 (翻越优先 > 跳跃)。
    /// </summary>
    public class JumpInteractionProcessor
    {
        private PlayerController _player;
        private PlayerInputReader _input;
        private PlayerRuntimeData _data;
        private PlayerSO _config;

        public JumpInteractionProcessor(PlayerController player)
        {
            _player = player;
            _input = player.InputReader;
            _data = player.RuntimeData;
            _config = player.Config;

            // 监听跳跃键
            _input.OnJumpPressed += HandleJumpInput;
        }

        // 析构提供清理方法，虽然 Controller 生命周期通常伴随整个游戏
        ~JumpInteractionProcessor()
        {
            if (_input != null) _input.OnJumpPressed -= HandleJumpInput;
        }

        public void Update()
        {
            // 意图重置逻辑在 PlayerController.LateUpdate/EndFrame 中统一处理
        }

        private void HandleJumpInput()
        {
            // 优先级判定逻辑

            // 1. 尝试检测翻越
            if (CheckObstacle())
            {
                _data.WantsToVault = true;
                // 如果翻越成功，就不跳跃了
                return;
            }

            // 2. 如果没环境交互，则默认为普通跳跃
            // (可以在这里加 IsGrounded 检查，防止空中二段跳，除非你想做二段跳)
            if (_data.IsGrounded)
            {
                _data.WantsToJump = true;
            }
        }

        private bool CheckObstacle()
        {
            // [调试开关] 临时逻辑
            return false; // 强行禁用翻越，测试跳跃

            // 真正的检测逻辑 (保留你的拓展接口)
            Vector3 origin = _player.transform.position;
            Vector3 dir = _player.transform.forward;

            // 1. 膝盖射线 (必须有墙)
            if (!Physics.Raycast(origin + Vector3.up * _config.VaultMinHeight, dir, _config.VaultCheckDistance, _config.ObstacleLayers))
                return false;

            // 2. 头顶射线 (必须无顶，或者是矮墙)
            if (Physics.Raycast(origin + Vector3.up * (_config.VaultMaxHeight + 0.1f), dir, _config.VaultCheckDistance, _config.ObstacleLayers))
                return false;

            return true;
        }
    }
}
