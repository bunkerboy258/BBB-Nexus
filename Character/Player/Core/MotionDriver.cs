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

        // ==========================================
        // Motion Warping (特殊扭曲驱动模块)
        // ==========================================
        private WarpedMotionData _warpData;
        private Vector3[] _warpTargets;
        private int _currentWarpIndex;
        private float _segmentStartTime;
        private Vector3 _segmentStartPosition;
        private Vector3 _currentCompensationVel;

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
            _data.CurrentSpeed = _cc.velocity.magnitude;
        }

        /// <summary>
        /// 仅更新重力/接地，不进行水平移动。（Idle 等状态使用）
        /// </summary>
        public void UpdateMotion()
        {
            Vector3 verticalVelocity = CalculateGravity();
            _cc.Move(verticalVelocity * Time.deltaTime);
            _data.CurrentSpeed = _cc.velocity.magnitude;
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
            _data.CurrentSpeed = _cc.velocity.magnitude;
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
            // 单一来源：DesiredWorldMoveDir 由 LocomotionIntentProcessor 计算。
            Vector3 moveDir = _data.DesiredWorldMoveDir;

            if (moveDir.sqrMagnitude < 0.0001f)
            {
                _data.CurrentYaw = _player.transform.eulerAngles.y;
                return Vector3.zero;
            }

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

        public void InitializeWarpData(WarpedMotionData data, Vector3[] targets)
        {
            if (data == null || data.WarpPoints.Count == 0 || targets == null || targets.Length != data.WarpPoints.Count)
            {
                Debug.LogError("[MotionDriver] 初始化 Warp 数据失败：参数为空或数量不匹配。");
                return;
            }

            _warpData = data;

            //在初始化时，根据配置计算出包含偏移的最终目标点数组
            _warpTargets = new Vector3[targets.Length];

            for (int i = 0; i < targets.Length; i++)
            {
                // 1. 获取传入的原始物理目标点
                Vector3 rawPos = targets[i];

                // 2. 获取该特征点配置的局部偏移量
                Vector3 configuredOffset = _warpData.WarpPoints[i].TargetPositionOffset;

                // 3. 将局部偏移量转换到当前世界方向上 (假设翻越是沿着角色 forward 方向的)
                // 注意：这里用 transform.TransformDirection 是假设角色在翻越前已经对准了墙面
                // 如果您的 VaultIntentProcessor 里算出了精确的 WallNormal，最好用 WallNormal 来转换
                Vector3 worldOffset = _player.transform.TransformDirection(configuredOffset);

                // 4. 计算出最终角色 Root 应该到达的绝对位置
                _warpTargets[i] = rawPos + worldOffset;
            }

            _currentWarpIndex = 0;
            _segmentStartTime = 0f;
            _segmentStartPosition = _player.transform.position;

            // 预计算第一段的补偿速度
            RecalculateCompensation();
        }

        /// <summary>
        /// 由特殊状态在每帧的 UpdateStateLogic() 中【主动调用】。
        /// 替代原本的普通 UpdateMotion 逻辑。
        /// </summary>
        /// <param name="normalizedTime">当前动画的归一化播放进度 (0.0~1.0)</param>
        public void UpdateWarpMotion(float normalizedTime)
        {
            if (_warpData == null) return;

            // 1. 检查是否跨越特征点，更新阶段
            CheckAndAdvanceWarpSegment(normalizedTime);

            // 2. 基础速度：读取烘焙的 XYZ 曲线
            Vector3 localVel = new Vector3(
                _warpData.LocalVelocityX.Evaluate(normalizedTime),
                _warpData.LocalVelocityY.Evaluate(normalizedTime),
                _warpData.LocalVelocityZ.Evaluate(normalizedTime)
            );
            Vector3 baseWorldVel = _player.transform.TransformDirection(localVel);

            // 3. 合成最终速度：基础速度 + 补偿速度
            Vector3 finalVelocity = baseWorldVel + _currentCompensationVel;

            // 4. 旋转：读取 Yaw 曲线
            float rotVelY = _warpData.LocalRotationY.Evaluate(normalizedTime);

            // 5. 执行物理移动
            _cc.Move(finalVelocity * Time.deltaTime);
            _player.transform.Rotate(0f, rotVelY * Time.deltaTime, 0f, Space.World);
            _data.CurrentSpeed = _cc.velocity.magnitude;
        }

        /// <summary>
        /// 检查时间进度，决定是否进入下一个特征点区间
        /// </summary>
        private void CheckAndAdvanceWarpSegment(float normalizedTime)
        {
            if (_currentWarpIndex < _warpData.WarpPoints.Count)
            {
                float targetTime = _warpData.WarpPoints[_currentWarpIndex].NormalizedTime;

                if (normalizedTime >= targetTime)
                {
                    // 跨越了节点！更新基准数据
                    _currentWarpIndex++;
                    _segmentStartTime = targetTime;
                    _segmentStartPosition = _player.transform.position;

                    // 重新计算下一段的补偿速度
                    RecalculateCompensation();
                }
            }
        }

        /// <summary>
        /// 重新计算当前区间的补偿速度 (Delta Error / Time)
        /// </summary>
        private void RecalculateCompensation()
        {
            if (_currentWarpIndex >= _warpData.WarpPoints.Count)
            {
                _currentCompensationVel = Vector3.zero;
                return;
            }

            var warpPoint = _warpData.WarpPoints[_currentWarpIndex];

            // 计算本段动画时长
            float segmentNormDuration = warpPoint.NormalizedTime - _segmentStartTime;
            float segmentSeconds = segmentNormDuration * _warpData.BakedDuration;

            if (segmentSeconds < 0.01f)
            {
                _currentCompensationVel = Vector3.zero;
                return;
            }

            // 预期现实位移
            Vector3 realDelta = _warpTargets[_currentWarpIndex] - _segmentStartPosition;

            // 动画原始位移 (Local 转 World)
            Vector3 animDelta = _player.transform.TransformVector(warpPoint.BakedLocalOffset);

            // 算出误差，平摊到每秒
            _currentCompensationVel = (realDelta - animDelta) / segmentSeconds;
        }

        /// <summary>
        /// 由特殊状态在 Exit() 时调用，清理数据。
        /// </summary>
        public void ClearWarpData()
        {
            _warpData = null;
            _warpTargets = null;
            _currentCompensationVel = Vector3.zero;
        }
    }
}
