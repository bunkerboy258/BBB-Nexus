using UnityEngine;
using Characters.Player.Data;

namespace Characters.Player.Core
{
    /// <summary>
    /// MotionDriver：
    /// 把“输入/动画曲线”转换为 CharacterController.Move 的速度向量，并维护角色运动相关运行时数据。
    /// 
    /// 职责：
    /// - 根据 CurrentLocomotionState 获取基础速度（Walk/Jog/Sprint）
    /// - 根据 IsAiming 选择探索模式或瞄准模式的速度
    /// - 计算水平运动向量并应用到 CharacterController
    /// - 维护垂直速度和重力
    /// 
    /// 重要约定（当前工程的目标手感）：
    /// - AuthorityYaw/AuthorityPitch 仅由 ViewRotationProcessor（鼠标）维护；相机可自由旋转。
    /// - 曲线驱动阶段（Start/Land/Vault 等）只允许动画强制旋转角色，不允许越级修改 Authority 参考系。
    /// - 曲线截断点后，角色在输入驱动逻辑下自然追随 Authority（SmoothDamp/Orient-to-movement）。
    /// </summary>
    public class MotionDriver
    {
        private readonly PlayerController _player;
        private readonly CharacterController _cc;
        private readonly PlayerRuntimeData _data;
        private readonly PlayerSO _config;

        // 模式切换消耗性标记：用于在 FreeLook<->Aiming 切换时做一次性的 yaw 同步（防止角度跳变）
        private bool _wasAimingLastFrame;

        public MotionDriver(PlayerController player)
        {
            _player = player;
            _cc = player.CharController;
            _data = player.RuntimeData;
            _config = player.Config;

            _wasAimingLastFrame = _data.IsAiming;
        }

        /// <summary>
        /// 统一入口：
        /// - clipData != null：使用曲线/混合驱动（用于 Start/Land/Vault 等带烘焙位移的状态）。
        /// - clipData == null：使用输入驱动。
        /// </summary>
        public void UpdateMotion(MotionClipData clipData, float stateTime, float startYaw)
        {
            HandleAimModeTransitionIfNeeded();

            Vector3 horizontalVelocity = 
                clipData == null? CalculateMotionFromInput():
                CalculateMotionFromClip(clipData, stateTime, startYaw);

            Vector3 verticalVelocity = CalculateGravity();
            _cc.Move((horizontalVelocity + verticalVelocity) * Time.deltaTime);
        }

        /// <summary>
        /// 仅更新重力/接地，不进行水平移动。（Idle 等状态使用）
        /// </summary>
        public void UpdateMotion()
        {
            Vector3 verticalVelocity = CalculateGravity();
            _cc.Move(verticalVelocity * Time.deltaTime);
        }

        /// <summary>
        /// 输入驱动的移动（FreeLook/Aiming 自动分流）。
        /// </summary>
        public void UpdateLocomotionFromInput(float speedMult = 1f)
        {
            HandleAimModeTransitionIfNeeded();

            Vector3 horizontalVelocity = CalculateMotionFromInput(speedMult);
            Vector3 verticalVelocity = CalculateGravity();
            _cc.Move((horizontalVelocity + verticalVelocity) * Time.deltaTime);
        }

        private Vector3 CalculateMotionFromClip(MotionClipData clipData, float stateTime, float startYaw)
        {
            if (clipData.Type == MotionType.CurveDriven)
            {
                return CalculateCurveDrivenVelocity(clipData, stateTime, startYaw);
            }

            if (clipData.Type == MotionType.mixed)
            {
                // RotationFinishedTime 之前：曲线强制旋转/位移
                // RotationFinishedTime 之后：切回输入驱动（角色将自然追随 Authority）
                return stateTime < clipData.RotationFinishedTime
                    ? CalculateCurveDrivenVelocity(clipData, stateTime, startYaw)
                    : CalculateMotionFromInput();
            }

            // InputDriven
            return CalculateMotionFromInput();
        }

        private Vector3 CalculateMotionFromInput(float speedMult = 1f)
        {
            return _data.IsAiming
                ? CalculateAimDrivenVelocity(speedMult)
                : CalculateFreeLookDrivenVelocity(speedMult);
        }

        /// <summary>
        /// 探索模式（FreeLook）：
        /// - 参考系来自 AuthorityYaw（相机/鼠标权威）；
        /// - 角色朝向使用 Orient-to-movement（朝移动方向转）。
        /// - 根据 CurrentLocomotionState 选择三档速度。
        /// </summary>
        private Vector3 CalculateFreeLookDrivenVelocity(float speedMult = 1f)
        {
            float yaw = _data.AuthorityYaw;
            Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);

            Vector3 basisForward = yawRot * Vector3.forward;
            Vector3 basisRight = yawRot * Vector3.right;

            Vector2 input = _data.MoveInput;
            if (input.sqrMagnitude < 0.001f)
            {
                _data.CurrentYaw = _player.transform.eulerAngles.y;
                return Vector3.zero;
            }

            Vector3 moveDir = basisRight * input.x + basisForward * input.y;
            moveDir = moveDir.sqrMagnitude > 0.0001f ? moveDir.normalized : Vector3.zero;

            // 角色朝向：朝向移动方向（而不是强制朝 AuthorityYaw）
            float targetYaw = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
            float smoothedYaw = Mathf.SmoothDampAngle(
                _player.transform.eulerAngles.y,
                targetYaw,
                ref _data.RotationVelocity,
                _config.RotationSmoothTime
            );
            _player.transform.rotation = Quaternion.Euler(0f, smoothedYaw, 0f);
            _data.CurrentYaw = smoothedYaw;

            // 根据运动状态获取基础速度
            float baseSpeed = GetSpeedForLocomotionState(_data.CurrentLocomotionState, isAiming: false);
            if (!_data.IsGrounded) baseSpeed *= _config.AirControl;

            return moveDir * baseSpeed * speedMult;
        }

        /// <summary>
        /// 瞄准模式（Strafing）：
        /// - 旋转由 LookInput.x 驱动角色（本模式下由 MotionDriver 消费 LookInput）；
        /// - 移动严格使用角色自身 forward/right。
        /// - 根据 CurrentLocomotionState 选择三档速度。
        /// </summary>
        private Vector3 CalculateAimDrivenVelocity(float speedMult = 1f)
        {
            float currentYaw = _player.transform.eulerAngles.y;

            // 瞄准模式：写死为“角色平滑对齐 AuthorityYaw（相机权威方向）”。
            // 注意：这里不修改 AuthorityYaw/AuthorityRotation，权威永远由 ViewRotationProcessor（鼠标输入）维护。
            float targetYaw = _data.AuthorityYaw;

            float smoothedYaw = Mathf.SmoothDampAngle(
                currentYaw,
                targetYaw,
                ref _data.RotationVelocity,
                _config.AimRotationSmoothTime
            );

            _player.transform.rotation = Quaternion.Euler(0f, smoothedYaw, 0f);
            _data.CurrentYaw = smoothedYaw;

            Vector2 input = _data.MoveInput;
            if (input.sqrMagnitude < 0.001f) return Vector3.zero;

            Vector3 forward = _player.transform.forward;
            Vector3 right = _player.transform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 moveDir = (right * input.x + forward * input.y);
            moveDir = moveDir.sqrMagnitude > 0.0001f ? moveDir.normalized : Vector3.zero;

            // 根据运动状态获取基础速度（瞄准模式）
            float baseSpeed = GetSpeedForLocomotionState(_data.CurrentLocomotionState, isAiming: true);
            if (!_data.IsGrounded) baseSpeed *= _config.AirControl;

            return moveDir * baseSpeed * speedMult;
        }

        /// <summary>
        /// 根据运动状态获取基础速度。
        /// 
        /// 探索模式：
        /// - Walk:  WalkSpeed
        /// - Jog:   JogSpeed
        /// - Sprint: SprintSpeed
        /// - Idle:  0
        /// 
        /// 瞄准模式：
        /// - Walk:  AimWalkSpeed
        /// - Jog:   AimJogSpeed
        /// - Sprint: AimSprintSpeed
        /// - Idle:  0
        /// </summary>
        private float GetSpeedForLocomotionState(LocomotionState state, bool isAiming)
        {
            if (isAiming)
            {
                return state switch
                {
                    LocomotionState.Walk => _config.AimWalkSpeed,
                    LocomotionState.Jog => _config.AimJogSpeed,
                    LocomotionState.Sprint => _config.AimSprintSpeed,
                    _ => 0f
                };
            }
            else
            {
                return state switch
                {
                    LocomotionState.Walk => _config.WalkSpeed,
                    LocomotionState.Jog => _config.JogSpeed,
                    LocomotionState.Sprint => _config.SprintSpeed,
                    _ => 0f
                };
            }
        }

        private Vector3 CalculateCurveDrivenVelocity(MotionClipData data, float time, float startYaw)
        {
            float curveAngle = data.RotationCurve.Evaluate(time * data.PlaybackSpeed);

            Quaternion startRot = Quaternion.Euler(0f, startYaw, 0f);
            Quaternion offsetRot = Quaternion.Euler(0f, curveAngle, 0f);
            Quaternion targetRot = startRot * offsetRot;

            // 曲线阶段：动画强制转身（只影响角色）
            _player.transform.rotation = Quaternion.Slerp(_player.transform.rotation, targetRot, Time.deltaTime * 30f);

            _data.CurrentYaw = _player.transform.eulerAngles.y;

            float speed = data.SpeedCurve.Evaluate(time * data.PlaybackSpeed);

            // [新增] 支持自带局部方向的动画（向左跳/向后闪避等）：
            // TargetLocalDirection 非零时，曲线阶段的“前进方向”由该局部方向决定。
            // 注意：这里使用角色当前朝向将 localDir 转成 worldDir，使其随曲线旋转一起变化。
            Vector3 localDir = data.TargetLocalDirection;
            if (localDir.sqrMagnitude > 0.0001f)
            {
                localDir.y = 0f;
                localDir.Normalize();

                Vector3 worldDir = _player.transform.TransformDirection(localDir);
                worldDir.y = 0f;
                worldDir = worldDir.sqrMagnitude > 0.0001f ? worldDir.normalized : Vector3.zero;

                return worldDir * speed;
            }

            return _player.transform.forward * speed;
        }

        private void HandleAimModeTransitionIfNeeded()
        {
            bool isAiming = _data.IsAiming;
            if (isAiming == _wasAimingLastFrame) return;

            // 切换瞬间清理旋转速度，避免 SmoothDampAngle 过冲。
            _data.RotationVelocity = 0f;

            _wasAimingLastFrame = isAiming;
        }

        private Vector3 CalculateGravity()
        {
            _data.IsGrounded = _cc.isGrounded;

            if (_data.IsGrounded && _data.VerticalVelocity < 0)
            {
                _data.VerticalVelocity = -2f;
            }
            else
            {
                _data.VerticalVelocity += _config.Gravity * Time.deltaTime;
            }

            return new Vector3(0f, _data.VerticalVelocity, 0f);
        }
    }
}
