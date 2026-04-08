using UnityEngine;

namespace BBBNexus
{
    // 基础移动意图处理器
    public class LocomotionIntentProcessor
    {
        private readonly PlayerRuntimeData _data;
        private readonly PlayerSO _config;

        // 短按 Shift = 闪避，长按 Shift = 冲刺
        // 按下时间超过此阈值才视为"长按"进入冲刺
        private const float SprintTapThreshold = 0.25f;
        private float _sprintHeldTime;
        private bool _wasSprintHeld;

        public LocomotionIntentProcessor(PlayerRuntimeData data, PlayerSO config)
        {
            _data = data;
            _config = config;
        }

        public void Update(in ProcessedInputData input)
        {
            ProcessMovementIntent();
            ProcessLocomotionStateAndStaminaIntent(in input);
        }

        private void ProcessMovementIntent()
        {
            // 判断按键是否产生有效输入
            bool isMoving = _data.MoveInput.sqrMagnitude > 0.01f;

            if (isMoving)
            {
                // 将本地的二维输入 转化为基于摄像机朝向的绝对世界坐标意图方向
                Quaternion yawRot = Quaternion.Euler(0f, _data.AuthorityYaw, 0f);
                Vector3 basisForward = yawRot * Vector3.forward;
                Vector3 basisRight = yawRot * Vector3.right;
                _data.DesiredWorldMoveDir = (basisRight * _data.MoveInput.x + basisForward * _data.MoveInput.y).normalized;

                // 将摇杆输入量化为 8 个离散方向 供混合树 或 起步状态判定
                _data.QuantizedDirection = QuantizeInputDirection(_data.MoveInput);
            }
            else
            {
                _data.DesiredWorldMoveDir = Vector3.zero;
                _data.QuantizedDirection = DesiredDirection.None;
            }
        }

        private void ProcessLocomotionStateAndStaminaIntent(in ProcessedInputData input)
        {
            LocomotionState prestate = _data.CurrentLocomotionState;

            if (input.RollPressed && _data.IsGrounded)
            {
                _data.WantsToRoll = true;
            }

            // 独立按键闪避（保留原有 Dodge 键支持）
            if (input.DodgePressed && _data.IsGrounded)
            {
                _data.WantsToDodge = true;
            }

            bool sprintHeld = input.SprintHeld;
            if (sprintHeld)
            {
                _sprintHeldTime += Time.deltaTime;
            }
            else
            {
                // 刚松开：判断是否为短按
                if (_wasSprintHeld && _sprintHeldTime < SprintTapThreshold && _data.IsGrounded)
                    _data.WantsToDodge = true;
                _sprintHeldTime = 0f;
            }
            _wasSprintHeld = sprintHeld;

            // 持续性移动状态判定
            bool isMoving = _data.MoveInput.sqrMagnitude > 0.01f;
            bool sprintActive = sprintHeld &&
                _sprintHeldTime >= SprintTapThreshold &&
                !_data.IsStaminaDepleted &&
                _data.CurrentStamina > 0f;

            // Sprint 与 Aim/Tactical 明确互斥：Sprint 优先，直接打断 Aim，
            // 避免两条意图链同帧同时成立。
            if (sprintActive)
            {
                _data.IsTacticalStance = false;
                _data.WantsToSecondaryAction = false;
            }

            // 体力耗尽后的恢复阈值判定
            if (_data.IsStaminaDepleted && _data.CurrentStamina > _data.MaxStamina * _config.Core.StaminaRecoverThreshold)
            {
                _data.IsStaminaDepleted = false;
            }

            // 空中时冻结档位，CurrentLocomotionState 语义为"地面运动档位"，不应在空中被覆写
            if (!_data.IsGrounded)
            {
                _data.WantToRun = _data.CurrentLocomotionState == LocomotionState.Sprint;
                return;
            }

            // 基于输入与体力 推导当前运动档位
            // Shift 长按（超过阈值）才进入冲刺
            if (!isMoving)
            {
                _data.CurrentLocomotionState = LocomotionState.Idle;
                _data.WantToRun = false;
            }
            else if (sprintActive)
            {
                _data.CurrentLocomotionState = LocomotionState.Sprint;
                _data.WantToRun = true;
            }
            else if (input.WalkHeld)
            {
                _data.CurrentLocomotionState = LocomotionState.Walk;
                _data.WantToRun = false;
            }
            else
            {
                _data.CurrentLocomotionState = LocomotionState.Jog;
                _data.WantToRun = false;
            }

            // 记录历史档位状态 供起步等状态用于动量继承判定
            if (_data.CurrentLocomotionState != prestate)
            {
                _data.LastLocomotionState = prestate;
            }
        }

        // 输入方向量化器 将连续向量切分为 8 个逻辑朝向
        private DesiredDirection QuantizeInputDirection(Vector2 input)
        {
            float threshold = 0.5f;
            bool hasForward = input.y > threshold;
            bool hasBackward = input.y < -threshold;
            bool hasRight = input.x > threshold;
            bool hasLeft = input.x < -threshold;

            if (hasForward) { if (hasLeft) return DesiredDirection.ForwardLeft; if (hasRight) return DesiredDirection.ForwardRight; return DesiredDirection.Forward; }
            if (hasBackward) { if (hasLeft) return DesiredDirection.BackwardLeft; if (hasRight) return DesiredDirection.BackwardRight; return DesiredDirection.Backward; }
            if (hasLeft) return DesiredDirection.Left;
            if (hasRight) return DesiredDirection.Right;
            return DesiredDirection.None;
        }
    }
}
