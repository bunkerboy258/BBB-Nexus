using UnityEngine;
using Characters.Player.Data;

namespace Characters.Player.Core
{
    // 角色运动驱动器 负责解析黑板意图 并利用烘焙器数据执行物理位移 
    public class MotionDriver
    {
        #region Dependencies & References

        // 宿主引用 
        private readonly PlayerController _player;
        // 角色控制器 处理物理碰撞与斜坡交互 
        private readonly CharacterController _cc;
        // 运行时黑板 存储所有动态状态与物理参数 
        private readonly PlayerRuntimeData _data;
        // 离线配置注入 访问移动速率与重力系数 
        private readonly PlayerSO _config;
        // 缓存变换组件 
        private readonly Transform _transform;

        #endregion

        #region State Caches

        // 瞄准模式切换状态记录 
        private bool _wasAimingLastFrame;

        // 平滑速度缓冲区 
        private float _currentSmoothSpeed;
        private float _smoothSpeedVelocity;
        
        // 瞄准模式方向反转检测 用于处理速度平滑过渡
        private Vector3 _lastAimMoveDir = Vector3.zero;

        // 曲线驱动模式偏航角增量缓存 
        private float _lastCurveDrivenAngle;
        private bool _isCurveAngleInitialized;

        // 运动扭曲系统运行时缓存 记录特征点目标与补偿速度 
        private WarpedMotionData _warpData;
        private Vector3[] _warpTargets;
        private int _currentWarpIndex;
        private float _segmentStartTime;
        private Vector3 _segmentStartPosition;
        private Vector3 _currentCompensationVel;

        // 驱动模式记录 用于判定状态切换瞬间 
        private MotionType? _lastClipMotionType;

        // 混合模式对齐标记 
        private bool _didAlignOnMixedToInput;

        #endregion

        // 构造驱动器 注入黑板引用与配置 
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

        // 统一运动入口 自动协调黑板意图与烘焙好的物理曲线 
        public void UpdateMotion(MotionClipData clipData, float stateTime)
        {
            // 处理瞄准模式切换瞬间的物理修正 
            HandleAimModeTransition();

            // 自动检测并初始化曲线驱动所需的增量缓存 
            AutoHandleCurveDrivenEnter(clipData, stateTime);

            // 根据数据配置选择输入驱动或曲线驱动 
            Vector3 horizontalVelocity = clipData == null
                ? CalculateInputDrivenVelocity()
                : CalculateClipDrivenVelocity(clipData, stateTime);

            // 执行最终移动指令 
            ExecuteMovement(horizontalVelocity);
        }

        // 纯输入驱动入口 适用于不含物理曲线的基础移动 
        public void UpdateLocomotionFromInput(float speedMult = 1f)
        {
            HandleAimModeTransition();
            Vector3 horizontalVelocity = CalculateInputDrivenVelocity(speedMult);
            ExecuteMovement(horizontalVelocity);
        }

        // 物理怠机更新重载 仅应用重力与黑板属性 
        public void UpdateMotion()
        {
            ExecuteMovement(Vector3.zero);
        }

        // 强制重置增量旋转缓存 避免状态机切换导致瞬间跳变 
        public void InterruptClipDrivenMotion()
        {
            _lastClipMotionType = null;
            _didAlignOnMixedToInput = false;
            ResetCurveDrivenState();
        }

        #endregion

        #region Public API: Motion Warping

        // 初始化运动扭曲系统 在烘焙数据基础上计算特征点物理补偿 
        public void InitializeWarpData(WarpedMotionData data, Vector3[] targets)
        {
            if (data == null || data.WarpPoints.Count == 0 || targets == null || targets.Length != data.WarpPoints.Count)
            {
                Debug.LogError("运动扭曲数据初始化失败 参数不匹配");
                return;
            }

            _warpData = data;
            _warpTargets = new Vector3[targets.Length];

            // 将离线的目标点偏移转换为当前世界的对齐目标 
            for (int i = 0; i < targets.Length; i++)
            {
                Vector3 worldOffset = _transform.TransformDirection(_warpData.WarpPoints[i].TargetPositionOffset);
                _warpTargets[i] = targets[i] + worldOffset;
            }

            _currentWarpIndex = 0;
            _segmentStartTime = 0f;
            _segmentStartPosition = _transform.position;

            // 计算首个特征点阶段的补偿速度 
            RecalculateWarpCompensation();
        }

        // 基于离线烘焙局部位移初始化扭曲 适用于固定距离的预设动作 
        public void InitializeWarpData(WarpedMotionData data)
        {
            if (data?.WarpPoints == null || data.WarpPoints.Count == 0)
            {
                Debug.LogError("运动扭曲数据为空 无法初始化");
                return;
            }

            Vector3[] targets = new Vector3[data.WarpPoints.Count];
            for (int i = 0; i < data.WarpPoints.Count; i++)
            {
                targets[i] = _transform.position + _transform.TransformVector(data.WarpPoints[i].BakedLocalOffset);
            }

            InitializeWarpData(data, targets);
        }

        // 驱动运动扭曲更新 精准对齐意图目标并叠加烘焙轨迹 
        public void UpdateWarpMotion(float normalizedTime)
        {
            if (_warpData == null) return;

            // 同步黑板速度 保证动作切换时的速度连贯性 
            _currentSmoothSpeed = new Vector3(_cc.velocity.x, 0f, _cc.velocity.z).magnitude;
            _smoothSpeedVelocity = 0f;

            // 推进扭曲特征点分段 
            CheckAndAdvanceWarpSegment(normalizedTime);

            // 解析烘焙器生成的局部速度 转换为世界物理位移 
            Vector3 localVel = new Vector3(
                _warpData.LocalVelocityX.Evaluate(normalizedTime),
                _warpData.LocalVelocityY.Evaluate(normalizedTime),
                _warpData.LocalVelocityZ.Evaluate(normalizedTime)
            );
            Vector3 baseWorldVel = _transform.TransformDirection(localVel);

            // 合并基础速度与物理补偿速度 
            float rotVelY = _warpData.LocalRotationY.Evaluate(normalizedTime);
            Vector3 finalVelocity = baseWorldVel + _currentCompensationVel;

            Vector3 gravityVelocity = Vector3.zero;
            if (_warpData.ApplyGravity)
            {
                // 离线数据若开启重力 则由物理引擎解算垂直位移 
                gravityVelocity = CalculateGravity();
            }
            else
            {
                // 仅更新黑板的着地状态 保持数据同步 
                _data.IsGrounded = _cc.isGrounded;
            }

            finalVelocity += gravityVelocity;

            // 驱动底层控制器并同步偏航旋转 
            _cc.Move(finalVelocity * Time.deltaTime);
            _transform.Rotate(0f, rotVelY * Time.deltaTime, 0f, Space.World);
            _data.CurrentSpeed = _cc.velocity.magnitude;
        }

        // 清理扭曲缓存 释放物理资源 
        public void ClearWarpData()
        {
            _warpData = null;
            _warpTargets = null;
            _currentCompensationVel = Vector3.zero;
        }

        #endregion

        #region Internal Logic: Velocity Calculation

        // 统一物理执行 合并水平速度与重力 最终写入黑板状态 
        private void ExecuteMovement(Vector3 horizontalVelocity)
        {
            Vector3 verticalVelocity = CalculateGravity();
            _cc.Move((horizontalVelocity + verticalVelocity) * Time.deltaTime);
            _data.CurrentSpeed = _cc.velocity.magnitude;
        }

        // 决定使用纯曲线数据还是混合输入逻辑 
        private Vector3 CalculateClipDrivenVelocity(MotionClipData clipData, float stateTime)
        {
            if (clipData.Type == MotionType.CurveDriven)
                return CalculateCurveVelocity(clipData, stateTime);

            if (clipData.Type == MotionType.Mixed)
            {
                // 混合模式 在旋转结束前由曲线主导 结束后移交意图管线 
                bool isCurvePhase = stateTime < clipData.RotationFinishedTime;

                if (isCurvePhase)
                {
                    _didAlignOnMixedToInput = false;
                    return CalculateCurveVelocity(clipData, stateTime);
                }

                // 移交管线瞬间执行对齐 消除旋转惯性引发的跳变 
                if (!_didAlignOnMixedToInput)
                {
                    AlignAndResetForInputTransition();
                    _didAlignOnMixedToInput = true;
                }

                return CalculateInputDrivenVelocity();
            }

            return CalculateInputDrivenVelocity();
        }

        // 输入驱动速度计算 根据黑板瞄准标记选择计算逻辑 
        private Vector3 CalculateInputDrivenVelocity(float speedMult = 1f)
        {
            return _data.IsAiming
                ? CalculateAimVelocity(speedMult)
                : CalculateFreeLookVelocity(speedMult);
        }

        #endregion

        #region Internal Logic: Specific Movement Modes

        // 探索模式 角色朝向意图移动方向 基于相机权威系计算 
        private Vector3 CalculateFreeLookVelocity(float speedMult)
        {
            Vector3 moveDir = _data.DesiredWorldMoveDir;

            if (moveDir.sqrMagnitude < 0.0001f)
            {
                ResetMovementCaches();
                return Vector3.zero;
            }

            // 平滑旋转至移动方向 访问离线配置的平滑时间 
            float targetYaw = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
            ApplySmoothRotation(targetYaw, _config.Core.RotationSmoothTime);

            return CalculateSmoothedVelocity(moveDir, false, speedMult);
        }

        // 瞄准模式 角色朝向相机权威系 采用平移运动逻辑 
        private Vector3 CalculateAimVelocity(float speedMult = 1f)
        {
            // 强行平滑对齐相机偏航角 
            ApplySmoothRotation(_data.AuthorityYaw, _config.Aiming.AimRotationSmoothTime);

            Vector2 input = _data.MoveInput;
            if (input.sqrMagnitude < 0.001f)
            {
                _currentSmoothSpeed = 0f;
                _lastAimMoveDir = Vector3.zero;
                return Vector3.zero;
            }

            // 在角色局部空间内合成移动矢量 
            Vector3 forward = _transform.forward.SetY(0).normalized;
            Vector3 right = _transform.right.SetY(0).normalized;
            Vector3 moveDir = (right * input.x + forward * input.y).normalized;

            // 检测方向是否发生反向 如果反向则强制速度先归零
            if (_lastAimMoveDir.sqrMagnitude > 0.1f && Vector3.Dot(moveDir, _lastAimMoveDir) < 0f)
            {
                // 方向反向了 强制速度归零 然后再平滑过渡到新方向的目标速度
                _currentSmoothSpeed = 0f;
                _smoothSpeedVelocity = 0f;
            }

            _lastAimMoveDir = moveDir;
            return CalculateSmoothedVelocity(moveDir, true, speedMult);
        }

        // 曲线驱动逻辑 解析烘焙器物理曲线并执行增量旋转 
        private Vector3 CalculateCurveVelocity(MotionClipData data, float time)
        {
            // 采样离线旋转曲线 计算当帧偏航增量 
            float curveAngle = data.RotationCurve.Evaluate(time * data.PlaybackSpeed);

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

            // 采样离线速度曲线 根据烘焙好的本地方向执行位移 
            float speed = data.SpeedCurve.Evaluate(time * data.PlaybackSpeed);
            Vector3 localDir = data.TargetLocalDirection;

            if (localDir.sqrMagnitude > 0.0001f)
            {
                Vector3 worldDir = _transform.TransformDirection(localDir.SetY(0)).normalized;
                return worldDir * speed;
            }

            return _transform.forward * speed;
        }

        #endregion

        #region Internal Helpers

        // 执行平滑旋转 结果实时反馈至黑板 
        private void ApplySmoothRotation(float targetYaw, float smoothTime)
        {
            float smoothedYaw = Mathf.SmoothDampAngle(_transform.eulerAngles.y, targetYaw, ref _data.RotationVelocity, smoothTime);
            _transform.rotation = Quaternion.Euler(0f, smoothedYaw, 0f);
            _data.CurrentYaw = smoothedYaw;
        }

        // 获取当前状态速度上限并应用平滑过渡 包含空中控制惩罚 
        private Vector3 CalculateSmoothedVelocity(Vector3 moveDir, bool isAiming, float speedMult)
        {
            float baseSpeed = GetBaseSpeed(_data.CurrentLocomotionState, isAiming);
            if (!_data.IsGrounded) baseSpeed *= _config.Core.AirControl;

            _currentSmoothSpeed = Mathf.SmoothDamp(_currentSmoothSpeed, baseSpeed * speedMult, ref _smoothSpeedVelocity, _config.Core.MoveSpeedSmoothTime);
            return moveDir * _currentSmoothSpeed;
        }

        // 从离线配置注入中 检索当前运动状态对应的物理速率 
        private float GetBaseSpeed(LocomotionState state, bool isAiming)
        {
            return state switch
            {
                LocomotionState.Walk => isAiming ? _config.Aiming.AimWalkSpeed : _config.Core.WalkSpeed,
                LocomotionState.Jog => isAiming ? _config.Aiming.AimJogSpeed : _config.Core.JogSpeed,
                LocomotionState.Sprint => isAiming ? _config.Aiming.AimSprintSpeed : _config.Core.SprintSpeed,
                _ => 0f
            };
        }

        // 物理重力解算 处理落地反弹力并更新垂直速度 
        private Vector3 CalculateGravity()
        {
            _data.IsGrounded = _cc.isGrounded;

            _data.VerticalVelocity = (_data.IsGrounded && _data.VerticalVelocity < 0)
                ? _config.Core.ReboundForce
                : _data.VerticalVelocity + _config.Core.Gravity * Time.deltaTime;

            return new Vector3(0f, _data.VerticalVelocity, 0f);
        }

        // 瞄准状态切换时重置旋转惯性 防止镜头超调 
        private void HandleAimModeTransition()
        {
            if (_data.IsAiming == _wasAimingLastFrame) return;

            _data.RotationVelocity = 0f;
            _lastAimMoveDir = Vector3.zero;
            _wasAimingLastFrame = _data.IsAiming;
        }

        // 停止移动时重置黑板缓存 
        private void ResetMovementCaches()
        {
            _data.CurrentYaw = _transform.eulerAngles.y;
            _currentSmoothSpeed = 0f;
        }

        // 进入曲线驱动瞬间执行自动重置 清除上一状态残留的旋转速度 
        private void AutoHandleCurveDrivenEnter(MotionClipData clipData, float stateTime)
        {
            MotionType? current = clipData?.Type;

            bool isCurveDriven = current == MotionType.CurveDriven;
            bool isMixedCurvePhase = current == MotionType.Mixed && stateTime < (clipData?.RotationFinishedTime ?? 0f);

            bool isInCurveLogicThisFrame = isCurveDriven || isMixedCurvePhase;
            bool wasInCurveLogicLastFrame = _lastClipMotionType == MotionType.CurveDriven || _lastClipMotionType == MotionType.Mixed;

            if (isInCurveLogicThisFrame && (!wasInCurveLogicLastFrame || !_isCurveAngleInitialized))
            {
                ResetCurveDrivenState();
                _data.RotationVelocity = 0f;
                _didAlignOnMixedToInput = false;
            }

            _lastClipMotionType = current;
        }

        private void ResetCurveDrivenState()
        {
            _isCurveAngleInitialized = false;
            _lastCurveDrivenAngle = 0f;
        }

        // 从曲线驱动平滑切回意图驱动 执行物理对齐 
        private void AlignAndResetForInputTransition()
        {
            _data.RotationVelocity = 0f;
            _data.CurrentYaw = _transform.eulerAngles.y;
            _isCurveAngleInitialized = false;
        }

        #endregion

        #region Motion Warping Helpers

        // 检查并推进运动扭曲的特征点分段 
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

        // 重新计算当前分段的物理补偿速度 确保对齐意图目标 
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

    // 向量操作扩展工具  
    public static class Vector3Extensions
    {
        public static Vector3 SetY(this Vector3 vector, float y)
        {
            vector.y = y;
            return vector;
        }
    }
}