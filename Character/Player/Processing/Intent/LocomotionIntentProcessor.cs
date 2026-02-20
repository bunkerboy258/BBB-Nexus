using Characters.Player.Data;
using Characters.Player.Input;
using UnityEngine;

namespace Characters.Player.Processing
{
    /// <summary>
    /// 统一处理移动意图的处理器。
    /// 职责：根据输入、体力、移动状态判定当前的运动状态（Idle/Walk/Jog/Sprint）。
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

            // Jump handling moved to JumpOrVaultIntentProcessor
        }

        public void Update()
        {
            // 处理移动意图
            ProcessMovementIntent();
            // 处理运动状态与体力意图判定
            ProcessLocomotionStateAndStaminaIntent();
        }

        private void ProcessMovementIntent()
        {
            // 判定是否有有效移动输入
            bool isMoving = _data.MoveInput.sqrMagnitude > 0.01f;

            // 计算世界空间移动方向（供参数处理器使用）
            if (isMoving)
            {
                // 使用权威朝向作为参考系
                Quaternion yawRot = Quaternion.Euler(0f, _data.AuthorityYaw, 0f);
                Vector3 basisForward = yawRot * Vector3.forward;
                Vector3 basisRight = yawRot * Vector3.right;

                _data.DesiredWorldMoveDir = (basisRight * _data.MoveInput.x + basisForward * _data.MoveInput.y).normalized;
            }
            else
            {
                _data.DesiredWorldMoveDir = Vector3.zero;
            }
        }

        /// <summary>
        /// 处理运动状态与体力意图。
        /// </summary>
        private void ProcessLocomotionStateAndStaminaIntent()
        {
            bool isMoving = _data.MoveInput.sqrMagnitude > 0.01f;

            // 首先检查体力耗尽恢复条件
            if (_data.IsStaminaDepleted && _data.CurrentStamina > _config.MaxStamina * _config.StaminaRecoverThreshold)
            {
                _data.IsStaminaDepleted = false;
            }

            // 根据优先级判定最终的运动状态
            if (!isMoving)
            {
                _data.CurrentLocomotionState = LocomotionState.Idle;
                _data.WantToRun = false;
            }
            else if (_input.IsSprinting && !_data.IsStaminaDepleted && _data.CurrentStamina > 0)
            {
                _data.CurrentLocomotionState = LocomotionState.Sprint;
                _data.WantToRun = true;
            }
            else if (_input.IsWalking)
            {
                _data.CurrentLocomotionState = LocomotionState.Walk;
                _data.WantToRun = false;
            }
            else
            {
                _data.CurrentLocomotionState = LocomotionState.Jog;
                _data.WantToRun = false;
            }
        }
    }
}
