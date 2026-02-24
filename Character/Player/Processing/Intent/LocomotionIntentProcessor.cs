using Characters.Player.Data;
using Characters.Player.Input;
using UnityEngine;

namespace Characters.Player.Processing
{
    /// <summary>
    /// 统一处理移动意图的处理器。
    /// 职责：
    /// 1. 计算世界空间的平滑移动方向 (DesiredWorldMoveDir)。
    /// 2. (新增) 计算量化的8方向意图 (QuantizedDirection)，供闪避等动作使用。
    /// 3. 根据输入、体力、状态判定当前的运动状态 (Idle/Walk/Jog/Sprint)。
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
        }

        public void Update()
        {
            ProcessMovementIntent();
            ProcessLocomotionStateAndStaminaIntent();
        }

        private void ProcessMovementIntent()
        {
            bool isMoving = _data.MoveInput.sqrMagnitude > 0.01f;

            if (isMoving)
            {
                // --- 1. 计算平滑的世界空间移动方向  ---
                Quaternion yawRot = Quaternion.Euler(0f, _data.AuthorityYaw, 0f);
                Vector3 basisForward = yawRot * Vector3.forward;
                Vector3 basisRight = yawRot * Vector3.right;
                _data.DesiredWorldMoveDir = (basisRight * _data.MoveInput.x + basisForward * _data.MoveInput.y).normalized;

                // --- 2.  计算量化的8方向意图 ---
                _data.QuantizedDirection = QuantizeInputDirection(_data.MoveInput);
            }
            else
            {
                _data.DesiredWorldMoveDir = Vector3.zero;
                _data.QuantizedDirection = DesiredDirection.None;
            }
        }

        /// <summary>
        /// 将连续的 Vector2 输入，量化为离散的8个方向。
        /// </summary>
        private DesiredDirection QuantizeInputDirection(Vector2 input)
        {
            // 使用一个简单的阈值来判断主方向
            float threshold = 0.5f;

            bool hasForward = input.y > threshold;
            bool hasBackward = input.y < -threshold;
            bool hasRight = input.x > threshold;
            bool hasLeft = input.x < -threshold;

            if (hasForward)
            {
                if (hasLeft) return DesiredDirection.ForwardLeft;
                if (hasRight) return DesiredDirection.ForwardRight;
                return DesiredDirection.Forward;
            }

            if (hasBackward)
            {
                if (hasLeft) return DesiredDirection.BackwardLeft;
                if (hasRight) return DesiredDirection.BackwardRight;
                return DesiredDirection.Backward;
            }

            // 如果没有前后输入，只判断左右
            if (hasLeft) return DesiredDirection.Left;
            if (hasRight) return DesiredDirection.Right;

            // 如果所有输入都在阈值内 (例如摇杆轻推)，则认为是无方向
            return DesiredDirection.None;
        }

        /// <summary>
        /// 处理运动状态与体力意图。
        /// </summary>
        private void ProcessLocomotionStateAndStaminaIntent()
        {
            if (_player.InputReader.IsRollPressed&&_data.IsGrounded) _data.WantsToRoll = true;
            if (_player.InputReader.IsDodgePressed && _data.IsGrounded) _data.WantsToDodge = true;


            bool isMoving = _data.MoveInput.sqrMagnitude > 0.01f;

            // Debug: 打印同一帧的关键值，方便追踪输入与状态决策
            Debug.Log($"[LocomotionIntent] Frame:{Time.frameCount} MoveInput:{_data.MoveInput} IsSprinting:{_input.IsSprinting} isMoving:{isMoving} CurrentLocomotionState:{_data.CurrentLocomotionState}");

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
