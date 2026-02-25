using UnityEngine;
using Characters.Player.Data;

namespace Characters.Player.Core
{
    /// <summary>
    /// 角色运动驱动器 (Motion Driver)
    /// 
    /// 核心职责：
    /// 1. 解析玩家输入与动画曲线，计算最终的位移与旋转。
    /// 2. 处理三种运动模式：输入驱动 (FreeLook/Aiming)、曲线驱动 (Curve/Mixed)、以及运动扭曲 (Motion Warping)。
    /// 3. 管理重力与落地状态，并统一通过 CharacterController 执行移动。
    /// </summary>
    public class MotionDriver
    {
        #region Dependencies & References

        private readonly PlayerController _player;
        private readonly CharacterController _cc;
        private readonly PlayerRuntimeData _data;
        private readonly PlayerSO _config;
        private readonly Transform _transform; // 缓存 Transform 提升性能

        #endregion

        #region State Caches

        // --- 模式切换缓存 ---
        private bool _wasAimingLastFrame;

        // --- 平滑速度缓存 ---
        private float _currentSmoothSpeed;
        private float _smoothSpeedVelocity;

        // --- Curve Driven (曲线驱动) 状态缓存 ---
        private float _lastCurveDrivenAngle;
        private bool _isCurveAngleInitialized;

        // --- Motion Warping (运动扭曲) 状态缓存 ---
        private WarpedMotionData _warpData;
        private Vector3[] _warpTargets;
        private int _currentWarpIndex;
        private float _segmentStartTime;
        private Vector3 _segmentStartPosition;
        private Vector3 _currentCompensationVel;

        // --- Clip 类型缓存 ---
        private MotionType? _lastClipMotionType;

        // Mixed 从 Curve -> Input 的当帧，只执行一次对齐/重置
        private bool _didAlignOnMixedToInput;

        #endregion

        public MotionDriver(PlayerController player)
        {
            _player = player;
            _cc = player.CharController;
            _data = player.RuntimeData;
            _config = player.Config;
            _transform = player.transform;

            _wasAimingLastFrame = _data.IsAiming;
        }

        #region Public API: Core Motion Updates

        /// <summary>
        /// 统一运动更新入口（包含动画剪辑数据）。
        /// 根据 ClipData 的类型，自动选择输入驱动或曲线驱动。
        /// </summary>
        public void UpdateMotion(MotionClipData clipData, float stateTime)
        {
            HandleAimModeTransition();

            // 自动处理曲线驱动缓存初始化/重置（避免依赖状态机在 Enter 调用）
            AutoHandleCurveDrivenEnter(clipData, stateTime);

            Vector3 horizontalVelocity = clipData == null
                ? CalculateInputDrivenVelocity()
                : CalculateClipDrivenVelocity(clipData, stateTime);

            ExecuteMovement(horizontalVelocity);
        }

        /// <summary>
        /// 纯输入驱动更新（无动画曲线干预）。
        /// </summary>
        public void UpdateLocomotionFromInput(float speedMult = 1f)
        {
            HandleAimModeTransition();
            Vector3 horizontalVelocity = CalculateInputDrivenVelocity(speedMult);
            ExecuteMovement(horizontalVelocity);
        }

        /// <summary>
        /// 纯物理更新（例如在 Idle 状态下只应用重力）。
        /// </summary>
        public void UpdateMotion()
        {
            ExecuteMovement(Vector3.zero);
        }

        #endregion

        #region Public API: Motion Warping

        /// <summary>
        /// 初始化 Motion Warping 数据（带目标点）。
        /// </summary>
        public void InitializeWarpData(WarpedMotionData data, Vector3[] targets)
        {
            if (data == null || data.WarpPoints.Count == 0 || targets == null || targets.Length != data.WarpPoints.Count)
            {
                Debug.LogError("[MotionDriver] Warp 数据初始化失败：参数为空或数量不匹配。");
                return;
            }

            _warpData = data;
            _warpTargets = new Vector3[targets.Length];

            for (int i = 0; i < targets.Length; i++)
            {
                Vector3 worldOffset = _transform.TransformDirection(_warpData.WarpPoints[i].TargetPositionOffset);
                _warpTargets[i] = targets[i] + worldOffset;
            }

            _currentWarpIndex = 0;
            _segmentStartTime = 0f;
            _segmentStartPosition = _transform.position;

            RecalculateWarpCompensation();
        }

        /// <summary>
        /// 初始化 Motion Warping 数据（仅基于烘焙局部位移）。
        /// </summary>
        public void InitializeWarpData(WarpedMotionData data)
        {
            if (data?.WarpPoints == null || data.WarpPoints.Count == 0)
            {
                Debug.LogError($"[MotionDriver] Warp 数据为空。Clip: {data?.Clip?.Name}");
                return;
            }

            Vector3[] targets = new Vector3[data.WarpPoints.Count];
            for (int i = 0; i < data.WarpPoints.Count; i++)
            {
                targets[i] = _transform.position + _transform.TransformVector(data.WarpPoints[i].BakedLocalOffset);
            }

            InitializeWarpData(data, targets);
        }

        /// <summary>
        /// 执行主动的 Motion Warping 更新。
        /// </summary>
        /// <param name="normalizedTime">动画归一化时间 (0~1)</param>
        public void UpdateWarpMotion(float normalizedTime)
        {
            if (_warpData == null) return;

            // 同步平滑速度系统，防止 Warp 结束后切回正常移动时速度骤降
            _currentSmoothSpeed = new Vector3(_cc.velocity.x, 0f, _cc.velocity.z).magnitude;
            _smoothSpeedVelocity = 0f;

            CheckAndAdvanceWarpSegment(normalizedTime);

            // 1. 获取并转换基础局部速度
            Vector3 localVel = new Vector3(
                _warpData.LocalVelocityX.Evaluate(normalizedTime),
                _warpData.LocalVelocityY.Evaluate(normalizedTime),
                _warpData.LocalVelocityZ.Evaluate(normalizedTime)
            );
            Vector3 baseWorldVel = _transform.TransformDirection(localVel);

            // 2. 获取旋转并执行物理移动
            float rotVelY = _warpData.LocalRotationY.Evaluate(normalizedTime);
            Vector3 finalVelocity = baseWorldVel + _currentCompensationVel;

            _cc.Move(finalVelocity * Time.deltaTime);
            _transform.Rotate(0f, rotVelY * Time.deltaTime, 0f, Space.World);
            _data.CurrentSpeed = _cc.velocity.magnitude;
        }

        public void ClearWarpData()
        {
            _warpData = null;
            _warpTargets = null;
            _currentCompensationVel = Vector3.zero;
        }

        #endregion

        #region Internal Logic: Velocity Calculation

        /// <summary>
        /// 统合物理执行：合并水平速度与重力。
        /// </summary>
        private void ExecuteMovement(Vector3 horizontalVelocity)
        {
            Vector3 verticalVelocity = CalculateGravity();
            _cc.Move((horizontalVelocity + verticalVelocity) * Time.deltaTime);
            _data.CurrentSpeed = _cc.velocity.magnitude;
        }

        private Vector3 CalculateClipDrivenVelocity(MotionClipData clipData, float stateTime)
        {
            if (clipData.Type == MotionType.CurveDriven)
                return CalculateCurveVelocity(clipData, stateTime);

            if (clipData.Type == MotionType.mixed)
            {
                bool isCurvePhase = stateTime < clipData.RotationFinishedTime;

                if (isCurvePhase)
                {
                    _didAlignOnMixedToInput = false;
                    return CalculateCurveVelocity(clipData, stateTime);
                }

                // Mixed 切换到 Input 的当帧：做一次性对齐/重置，避免 SmoothDamp 的 RotationVelocity 残留导致抽搐。
                if (!_didAlignOnMixedToInput)
                {
                    AlignAndResetForInputTransition();
                    _didAlignOnMixedToInput = true;
                }

                return CalculateInputDrivenVelocity();
            }

            return CalculateInputDrivenVelocity();
        }

        private Vector3 CalculateInputDrivenVelocity(float speedMult = 1f)
        {
            return _data.IsAiming
                ? CalculateAimVelocity(speedMult)
                : CalculateFreeLookVelocity(speedMult);
        }

        #endregion

        #region Internal Logic: Specific Movement Modes

        /// <summary>
        /// 探索模式：角色朝向实际移动方向，相对于相机权威系。
        /// </summary>
        private Vector3 CalculateFreeLookVelocity(float speedMult)
        {
            Vector3 moveDir = _data.DesiredWorldMoveDir;

            if (moveDir.sqrMagnitude < 0.0001f)
            {
                ResetMovementCaches();
                return Vector3.zero;
            }

            // 平滑旋转至移动方向
            float targetYaw = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
            ApplySmoothRotation(targetYaw, _config.RotationSmoothTime);

            return CalculateSmoothedVelocity(moveDir, false, speedMult);
        }

        /// <summary>
        /// 瞄准模式：角色朝向相机权威系 (Strafing)，根据角色局部坐标移动。
        /// </summary>
        private Vector3 CalculateAimVelocity(float speedMult)
        {
            // 平滑旋转至相机朝向
            ApplySmoothRotation(_data.AuthorityYaw, _config.AimRotationSmoothTime);

            Vector2 input = _data.MoveInput;
            if (input.sqrMagnitude < 0.001f)
            {
                _currentSmoothSpeed = 0f;
                return Vector3.zero;
            }

            // 基于角色当前朝向计算本地移动向量
            Vector3 forward = _transform.forward.SetY(0).normalized;
            Vector3 right = _transform.right.SetY(0).normalized;
            Vector3 moveDir = (right * input.x + forward * input.y).normalized;

            return CalculateSmoothedVelocity(moveDir, true, speedMult);
        }

        /// <summary>
        /// 曲线驱动模式：由动画曲线完全接管角色的旋转与位移。
        /// </summary>
        private Vector3 CalculateCurveVelocity(MotionClipData data, float time)
        {
            // --- 1. 旋转计算 (增量法) ---
            float curveAngle = data.RotationCurve.Evaluate(time * data.PlaybackSpeed);
            // Debug.Log(curveAngle);

            if (!_isCurveAngleInitialized)
            {
                _lastCurveDrivenAngle = curveAngle;
                _isCurveAngleInitialized = true;
            }

            float deltaAngle = curveAngle - _lastCurveDrivenAngle;
            _lastCurveDrivenAngle = curveAngle;

            if (Mathf.Abs(deltaAngle) > 0.0001f)
            {
                _transform.Rotate(0f, deltaAngle, 0f, Space.World);
            }
            _data.CurrentYaw = _transform.eulerAngles.y;

            // --- 2. 位移计算 ---
            float speed = data.SpeedCurve.Evaluate(time * data.PlaybackSpeed);
            Vector3 localDir = data.TargetLocalDirection;

            // 如果动画自带强制局部方向（如后侧闪避），则以此方向移动
            if (localDir.sqrMagnitude > 0.0001f)
            {
                Vector3 worldDir = _transform.TransformDirection(localDir.SetY(0)).normalized;
                return worldDir * speed;
            }

            return _transform.forward * speed;
        }

        #endregion

        #region Internal Helpers

        /// <summary>
        /// 应用平滑旋转并更新数据缓存。
        /// </summary>
        private void ApplySmoothRotation(float targetYaw, float smoothTime)
        {
            float smoothedYaw = Mathf.SmoothDampAngle(_transform.eulerAngles.y, targetYaw, ref _data.RotationVelocity, smoothTime);
            _transform.rotation = Quaternion.Euler(0f, smoothedYaw, 0f);
            _data.CurrentYaw = smoothedYaw;
        }

        /// <summary>
        /// 获取当前状态的基础速度并应用平滑和空中惩罚。
        /// </summary>
        private Vector3 CalculateSmoothedVelocity(Vector3 moveDir, bool isAiming, float speedMult)
        {
            float baseSpeed = GetBaseSpeed(_data.CurrentLocomotionState, isAiming);
            if (!_data.IsGrounded) baseSpeed *= _config.AirControl;

            _currentSmoothSpeed = Mathf.SmoothDamp(_currentSmoothSpeed, baseSpeed * speedMult, ref _smoothSpeedVelocity, _config.MoveSpeedSmoothTime);
            return moveDir * _currentSmoothSpeed;
        }

        private float GetBaseSpeed(LocomotionState state, bool isAiming)
        {
            return state switch
            {
                LocomotionState.Walk => isAiming ? _config.AimWalkSpeed : _config.WalkSpeed,
                LocomotionState.Jog => isAiming ? _config.AimJogSpeed : _config.JogSpeed,
                LocomotionState.Sprint => isAiming ? _config.AimSprintSpeed : _config.SprintSpeed,
                _ => 0f
            };
        }

        private Vector3 CalculateGravity()
        {
            _data.IsGrounded = _cc.isGrounded;

            // 落地时施加一个微小的持续向下的力，防止在斜坡上弹跳
            _data.VerticalVelocity = (_data.IsGrounded && _data.VerticalVelocity < 0)
                ? -2f
                : _data.VerticalVelocity + _config.Gravity * Time.deltaTime;

            return new Vector3(0f, _data.VerticalVelocity, 0f);
        }

        private void HandleAimModeTransition()
        {
            if (_data.IsAiming == _wasAimingLastFrame) return;

            // 模式切换时清空旋转速度惯性，防止转身越界
            _data.RotationVelocity = 0f;
            _wasAimingLastFrame = _data.IsAiming;
        }

        private void ResetMovementCaches()
        {
            _data.CurrentYaw = _transform.eulerAngles.y;
            _currentSmoothSpeed = 0f;
        }

        /// <summary>
        /// 自动检测并处理进入曲线驱动的瞬间：
        /// - 从 Input->Curve/Mixed（curve 阶段）时重置增量缓存
        /// - 同时清空 RotationVelocity，避免上一状态的 SmoothDamp 惯性影响曲线驱动第一帧
        /// </summary>
        private void AutoHandleCurveDrivenEnter(MotionClipData clipData, float stateTime)
        {
            MotionType? current = clipData?.Type;

            bool isCurveDriven = current == MotionType.CurveDriven;
            bool isMixedCurvePhase = current == MotionType.mixed && stateTime < (clipData?.RotationFinishedTime ?? 0f);

            bool isInCurveLogicThisFrame = isCurveDriven || isMixedCurvePhase;
            bool wasInCurveLogicLastFrame = _lastClipMotionType == MotionType.CurveDriven || _lastClipMotionType == MotionType.mixed;

            // 进入曲线驱动的第一帧（从 null / InputDriven / mixed(input阶段) 进入到 curve 逻辑）
            if (isInCurveLogicThisFrame && (!wasInCurveLogicLastFrame || !_isCurveAngleInitialized))
            {
                ResetCurveDrivenState();

                // 关键：清空 SmoothDampAngle 的惯性，避免曲线驱动第一帧被残留速度影响
                _data.RotationVelocity = 0f;

                _didAlignOnMixedToInput = false;
            }

            _lastClipMotionType = current;
        }

        // 曲线驱动增量旋转的缓存重置：由 MotionDriver 内部自动调用
        private void ResetCurveDrivenState()
        {
            _isCurveAngleInitialized = false;
            _lastCurveDrivenAngle = 0f;
        }

        /// <summary>
        /// Mixed 从 Curve -> Input 的当帧对齐：
        /// - 清空 RotationVelocity，避免 SmoothDampAngle 继承曲线阶段之前的惯性
        /// - 同步 CurrentYaw
        /// - 可选：重置曲线增量缓存，避免后续再次进入 curve 时污染
        /// </summary>
        private void AlignAndResetForInputTransition()
        {
            _data.RotationVelocity = 0f;
            _data.CurrentYaw = _transform.eulerAngles.y;

            // 进入 input 后曲线增量缓存不再使用，但清掉更安全
            _isCurveAngleInitialized = false;
        }

        #endregion

        #region Motion Warping Helpers

        private void CheckAndAdvanceWarpSegment(float normalizedTime)
        {
            if (_currentWarpIndex >= _warpData.WarpPoints.Count) return;

            float targetTime = _warpData.WarpPoints[_currentWarpIndex].NormalizedTime;
            if (normalizedTime >= targetTime)
            {
                _currentWarpIndex++;
                _segmentStartTime = targetTime;
                _segmentStartPosition = _transform.position;
                RecalculateWarpCompensation();
            }
        }

        private void RecalculateWarpCompensation()
        {
            if (_currentWarpIndex >= _warpData.WarpPoints.Count)
            {
                _currentCompensationVel = Vector3.zero;
                return;
            }

            var warpPoint = _warpData.WarpPoints[_currentWarpIndex];
            float segmentSeconds = (warpPoint.NormalizedTime - _segmentStartTime) * _warpData.BakedDuration;

            if (segmentSeconds < 0.01f)
            {
                _currentCompensationVel = Vector3.zero;
                return;
            }

            Vector3 realDelta = _warpTargets[_currentWarpIndex] - _segmentStartPosition;
            Vector3 animDelta = _transform.TransformVector(warpPoint.BakedLocalOffset);

            _currentCompensationVel = (realDelta - animDelta) / segmentSeconds;
        }

        #endregion
    }

    /// <summary>
    /// 扩展方法类：用于简化 Vector3 的操作，提升代码可读性
    /// 可以放在单独的文件中，或者放在同一个命名空间下。
    /// </summary>
    public static class Vector3Extensions
    {
        public static Vector3 SetY(this Vector3 vector, float y)
        {
            vector.y = y;
            return vector;
        }
    }
}
