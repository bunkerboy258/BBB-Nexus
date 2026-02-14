using Characters.Player.Data;
using Characters.Player.Input;
using UnityEngine;

namespace Characters.Player.Processing
{
    /// <summary>
    /// 统一处理移动与跳跃意图的处理器。
    /// </summary>
    public class LocomotionIntentProcessor
    {
        private PlayerController _player;
        private PlayerInputReader _input;
        private PlayerRuntimeData _data;
        private PlayerSO _config;

        public LocomotionIntentProcessor(PlayerController player)
        {
            _player = player;
            _input = player.InputReader;
            _data = player.RuntimeData;
            _config = player.Config;

            // 监听跳跃键
            _input.OnJumpPressed += HandleJumpInput;
        }

        ~LocomotionIntentProcessor()
        {
            if (_input != null) _input.OnJumpPressed -= HandleJumpInput;
        }

        public void Update()
        {
            // 处理移动意图
            ProcessMovementIntent();
            // 体力与奔跑意图判定
            ProcessStaminaAndRunIntent();
        }

        private void ProcessMovementIntent()
        {
            // 判定是否有有效移动输入
            bool isMoving = _data.MoveInput.sqrMagnitude > 0.01f;

            // 更新移动相关意图（可根据需要扩展）
            if (isMoving)
            {
                _data.DesiredWorldMoveDir = _player.transform.forward;
            }
            else
            {
                _data.DesiredWorldMoveDir = Vector3.zero;
            }
        }

        private void ProcessStaminaAndRunIntent()
        {
            bool isMoving = _data.MoveInput.sqrMagnitude > 0.01f;
            bool wantsToRun = _input.IsSprinting && isMoving;

            // 体力耗尽后需恢复至阈值才能重新奔跑
            if (_data.IsStaminaDepleted)
            {
                if (_data.CurrentStamina > _config.MaxStamina * _config.StaminaRecoverThreshold)
                {
                    _data.IsStaminaDepleted = false;
                }
                else
                {
                    wantsToRun = false;
                }
            }

            // 根据意图和体力状态决定是否奔跑
            if (wantsToRun && !_data.IsStaminaDepleted && _data.CurrentStamina > 0)
            {
                _data.IsRunning = true;
                _data.WantToRun = true;
            }
            else
            {
                _data.IsRunning = false;
                _data.WantToRun = _input.IsSprinting && !_data.IsStaminaDepleted;
            }
        }

        private void HandleJumpInput()
        {
            // 优先级判定逻辑
            if (CheckObstacle())
            {
                _data.WantsToVault = true;
                return;
            }

            if (_data.IsGrounded)
            {
                _data.WantsToJump = true;
            }
        }

        private bool CheckObstacle()
        {
            // [调试开关] 临时逻辑
            return false; // 强行禁用翻越，测试跳跃

            // 真正的检测逻辑 (可扩展)
            // Vector3 origin = _player.transform.position;
            // Vector3 dir = _player.transform.forward;
            // ...
        }
    }
}
