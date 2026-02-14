using Characters.Player.Data;
using Characters.Player.Input;
using UnityEngine;

namespace Characters.Player.Processing
{
    /// <summary>
    /// 统一处理移动与跳跃意图的处理器。
    /// 职责：根据输入、体力、移动状态判定当前的运动状态（Idle/Walk/Jog/Sprint）。
    /// 
    /// 优先级判定（从高到低）：
    /// 1. Sprint（Shift 按住 + 体力充足 + 有移动输入）
    /// 2. Walk（Ctrl 按住 + 有移动输入）
    /// 3. Jog（无特殊按键 + 有移动输入）
    /// 4. Idle（无移动输入）
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
        /// 
        /// 逻辑流程：
        /// 1. 判定用户输入意图（Ctrl/Shift 按键 + 移动输入）
        /// 2. 检查体力耗尽限制
        /// 3. 根据优先级决定最终的运动状态
        /// 4. 维护 WantToRun 意图标记（用于参数计算、UI 反馈）
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
            // 优先级：Sprint > Walk > Jog > Idle
            if (!isMoving)
            {
                // 无移动输入 → Idle
                _data.CurrentLocomotionState = LocomotionState.Idle;
                _data.WantToRun = false;
            }
            else if (_input.IsSprinting && !_data.IsStaminaDepleted && _data.CurrentStamina > 0)
            {
                // Shift 按住 + 体力充足 + 有移动输入 → Sprint
                _data.CurrentLocomotionState = LocomotionState.Sprint;
                _data.WantToRun = true;
            }
            else if (_input.IsWalking)
            {
                // Ctrl 按住 + 有移动输入 → Walk
                _data.CurrentLocomotionState = LocomotionState.Walk;
                _data.WantToRun = false;
            }
            else
            {
                // 默认情况：有移动输入 + 无特殊键 → Jog
                _data.CurrentLocomotionState = LocomotionState.Jog;
                _data.WantToRun = false;
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
