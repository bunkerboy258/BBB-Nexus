using UnityEngine;
using Characters.Player.Data;

namespace Characters.Player.Core
{
    // 角色运动驱动器 负责解析黑板意图 并利用烘焙器数据执行物理位移 
    // 这是一个精简后的版本 之前由于长期打补丁和维护 已经变成了屎山
    // 如果还要加新的驱动方式 请保持模块化和清晰的逻辑分层 
    public class MotionDriver
    {
        #region Dependencies
        private readonly PlayerController _player;
        private readonly CharacterController _cc;
        private readonly PlayerRuntimeData _data;
        private readonly PlayerSO _config;
        private readonly Transform _transform;
        #endregion

        #region Isolated Contexts (状态隔离容器)

        // 输入驱动上下文
        private struct LocomotionCtx
        {
            public bool WasAiming;
            public float SmoothSpeed;
            public float SpeedVelocity;
            public Vector3 LastAimMoveDir;

            public void ResetSpeed() { SmoothSpeed = 0f; LastAimMoveDir = Vector3.zero; }
        }

        // 曲线驱动上下文
        private struct CurveCtx
        {
            public float LastAngle;
            public bool IsInitialized;
            public MotionType? LastMotionType;
            public bool DidAlignOnMixed;

            public void Reset() { LastAngle = 0f; IsInitialized = false; }
        }

        // 运动扭曲上下文
        private struct WarpCtx
        {
            public WarpedMotionData Data;
            public Vector3[] Targets;
            public int CurrentIndex;
            public float SegmentStartTime;
            public Vector3 SegmentStartPosition;
            public Vector3 CompensationVel;

            public bool IsActive => Data != null;
            public void Clear() { Data = null; Targets = null; CompensationVel = Vector3.zero; }
        }

        private LocomotionCtx _loco;
        private CurveCtx _curve;
        private WarpCtx _warp;

        #endregion

        public MotionDriver(PlayerController player)
        {
            _player = player;
            _cc = player.CharController;
            _data = player.RuntimeData;
            _config = player.Config;
            _transform = player.transform;

            _loco.WasAiming = _data.IsAiming;
        }

        #region Public API: Core Motion Updates

        public void UpdateMotion(MotionClipData clipData, float stateTime)
        {
            HandleAimModeTransition();
            AutoHandleCurveDrivenEnter(clipData, stateTime);

            Vector3 velocity = clipData == null
                ? CalculateInputDrivenVelocity()
                : CalculateClipDrivenVelocity(clipData, stateTime);

            ExecuteMovement(velocity);
        }

        public void UpdateLocomotionFromInput(float speedMult = 1f)
        {
            HandleAimModeTransition();
            ExecuteMovement(CalculateInputDrivenVelocity(speedMult));
        }

        public void UpdateMotion() => ExecuteMovement(Vector3.zero);

        public void InterruptClipDrivenMotion()
        {
            _curve.LastMotionType = null;
            _curve.DidAlignOnMixed = false;
            _curve.Reset();
        }

        #endregion

        #region Public API: Motion Warping

        public void InitializeWarpData(WarpedMotionData data, Vector3[] targets)
        {
            if (data == null || data.WarpPoints.Count == 0 || targets == null || targets.Length != data.WarpPoints.Count)
            {
                Debug.LogError("运动扭曲数据初始化失败 参数不匹配");
                return;
            }

            _warp.Data = data;
            _warp.Targets = new Vector3[targets.Length];

            for (int i = 0; i < targets.Length; i++)
            {
                Vector3 worldOffset = _transform.TransformDirection(_warp.Data.WarpPoints[i].TargetPositionOffset);
                _warp.Targets[i] = targets[i] + worldOffset;
            }

            _warp.CurrentIndex = 0;
            _warp.SegmentStartTime = 0f;
            _warp.SegmentStartPosition = _transform.position;
            RecalculateWarpCompensation();
        }

        public void InitializeWarpData(WarpedMotionData data)
        {
            if (data?.WarpPoints == null || data.WarpPoints.Count == 0) return;

            Vector3[] targets = new Vector3[data.WarpPoints.Count];
            for (int i = 0; i < data.WarpPoints.Count; i++)
            {
                targets[i] = _transform.position + _transform.TransformVector(data.WarpPoints[i].BakedLocalOffset);
            }

            InitializeWarpData(data, targets);
        }

        public void UpdateWarpMotion(float normalizedTime)
        {
            if (!_warp.IsActive) return;

            _loco.SmoothSpeed = new Vector3(_cc.velocity.x, 0f, _cc.velocity.z).magnitude;
            _loco.SpeedVelocity = 0f;

            CheckAndAdvanceWarpSegment(normalizedTime);

            Vector3 localVel = new Vector3(
                _warp.Data.LocalVelocityX.Evaluate(normalizedTime),
                _warp.Data.LocalVelocityY.Evaluate(normalizedTime),
                _warp.Data.LocalVelocityZ.Evaluate(normalizedTime)
            );

            Vector3 finalVelocity = _transform.TransformDirection(localVel) + _warp.CompensationVel;

            if (_warp.Data.ApplyGravity)
            {
                finalVelocity += CalculateGravity();
            }
            else
            {
                _data.IsGrounded = _cc.isGrounded; // 仅同步数据
            }

            float rotVelY = _warp.Data.LocalRotationY.Evaluate(normalizedTime);

            _cc.Move(finalVelocity * Time.deltaTime);
            _transform.Rotate(0f, rotVelY * Time.deltaTime, 0f, Space.World);
            _data.CurrentSpeed = _cc.velocity.magnitude;
        }

        public void ClearWarpData() => _warp.Clear();

        #endregion

        #region Internal Logic: Velocity Calculation

        private void ExecuteMovement(Vector3 horizontalVelocity)
        {
            Vector3 verticalVelocity = CalculateGravity();
            _cc.Move((horizontalVelocity + verticalVelocity) * Time.deltaTime);
            _data.CurrentSpeed = _cc.velocity.magnitude;
        }

        private Vector3 CalculateClipDrivenVelocity(MotionClipData clipData, float stateTime)
        {
            bool isCurvePhase = clipData.Type == MotionType.CurveDriven ||
                               (clipData.Type == MotionType.Mixed && stateTime < clipData.RotationFinishedTime);

            if (isCurvePhase)
            {
                _curve.DidAlignOnMixed = false;
                return CalculateCurveVelocity(clipData, stateTime);
            }

            if (clipData.Type == MotionType.Mixed && !_curve.DidAlignOnMixed)
            {
                AlignAndResetForInputTransition();
                _curve.DidAlignOnMixed = true;
            }

            return CalculateInputDrivenVelocity();
        }

        private Vector3 CalculateInputDrivenVelocity(float speedMult = 1f)
        {
            return _data.IsAiming ? CalculateAimVelocity(speedMult) : CalculateFreeLookVelocity(speedMult);
        }

        #endregion

        #region Internal Logic: Specific Movement Modes

        private Vector3 CalculateFreeLookVelocity(float speedMult)
        {
            Vector3 moveDir = _data.DesiredWorldMoveDir;

            if (moveDir.sqrMagnitude < 0.0001f)
            {
                _data.CurrentYaw = _transform.eulerAngles.y;
                _loco.SmoothSpeed = 0f;
                return Vector3.zero;
            }

            float targetYaw = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
            ApplySmoothRotation(targetYaw, _config.Core.RotationSmoothTime);

            return CalculateSmoothedVelocity(moveDir, false, speedMult);
        }

        private Vector3 CalculateAimVelocity(float speedMult = 1f)
        {
            ApplySmoothRotation(_data.AuthorityYaw, _config.Aiming.AimRotationSmoothTime);

            Vector2 input = _data.MoveInput;
            if (input.sqrMagnitude < 0.001f)
            {
                _loco.ResetSpeed();
                return Vector3.zero;
            }

            // 利用四元数旋转代替繁琐的 Right/Forward 向量投射，数学等价且性能更高
            Vector3 moveDir = (Quaternion.Euler(0, _transform.eulerAngles.y, 0) * new Vector3(input.x, 0f, input.y)).normalized;

            if (_loco.LastAimMoveDir.sqrMagnitude > 0.1f && Vector3.Dot(moveDir, _loco.LastAimMoveDir) < 0f)
            {
                _loco.SmoothSpeed = 0f;
                _loco.SpeedVelocity = 0f;
            }

            _loco.LastAimMoveDir = moveDir;
            return CalculateSmoothedVelocity(moveDir, true, speedMult);
        }

        private Vector3 CalculateCurveVelocity(MotionClipData data, float time)
        {
            float curveAngle = data.RotationCurve.Evaluate(time * data.PlaybackSpeed);

            if (!_curve.IsInitialized)
            {
                _curve.LastAngle = curveAngle;
                _curve.IsInitialized = true;
            }

            float deltaAngle = curveAngle - _curve.LastAngle;
            _curve.LastAngle = curveAngle;

            if (Mathf.Abs(deltaAngle) > 0.0001f)
            {
                _transform.Rotate(0f, deltaAngle, 0f, Space.World);
            }

            _data.CurrentYaw = _transform.eulerAngles.y;

            float speed = data.SpeedCurve.Evaluate(time * data.PlaybackSpeed);
            Vector3 localDir = data.TargetLocalDirection;

            if (localDir.sqrMagnitude > 0.0001f)
            {
                // 使用基于平面的快速转换
                Vector3 worldDir = _transform.TransformDirection(localDir.SetY(0)).normalized;
                return worldDir * speed;
            }

            return _transform.forward * speed;
        }

        #endregion

        #region Internal Helpers

        private void ApplySmoothRotation(float targetYaw, float smoothTime)
        {
            float smoothedYaw = Mathf.SmoothDampAngle(_transform.eulerAngles.y, targetYaw, ref _data.RotationVelocity, smoothTime);
            _transform.rotation = Quaternion.Euler(0f, smoothedYaw, 0f);
            _data.CurrentYaw = smoothedYaw;
        }

        private Vector3 CalculateSmoothedVelocity(Vector3 moveDir, bool isAiming, float speedMult)
        {
            float baseSpeed = GetBaseSpeed(_data.CurrentLocomotionState, isAiming);
            if (!_data.IsGrounded) baseSpeed *= _config.Core.AirControl;

            _loco.SmoothSpeed = Mathf.SmoothDamp(_loco.SmoothSpeed, baseSpeed * speedMult, ref _loco.SpeedVelocity, _config.Core.MoveSpeedSmoothTime);
            return moveDir * _loco.SmoothSpeed;
        }

        private float GetBaseSpeed(LocomotionState state, bool isAiming) => state switch
        {
            LocomotionState.Walk => isAiming ? _config.Aiming.AimWalkSpeed : _config.Core.WalkSpeed,
            LocomotionState.Jog => isAiming ? _config.Aiming.AimJogSpeed : _config.Core.JogSpeed,
            LocomotionState.Sprint => isAiming ? _config.Aiming.AimSprintSpeed : _config.Core.SprintSpeed,
            _ => 0f
        };

        private Vector3 CalculateGravity()
        {
            _data.IsGrounded = _cc.isGrounded;

            _data.VerticalVelocity = (_data.IsGrounded && _data.VerticalVelocity < 0)
                ? _config.Core.ReboundForce
                : _data.VerticalVelocity + _config.Core.Gravity * Time.deltaTime;

            return new Vector3(0f, _data.VerticalVelocity, 0f);
        }

        private void HandleAimModeTransition()
        {
            if (_data.IsAiming == _loco.WasAiming) return;

            _data.RotationVelocity = 0f;
            _loco.LastAimMoveDir = Vector3.zero;
            _loco.WasAiming = _data.IsAiming;
        }

        private void AutoHandleCurveDrivenEnter(MotionClipData clipData, float stateTime)
        {
            MotionType? current = clipData?.Type;
            bool isCurvePhase = current == MotionType.CurveDriven || (current == MotionType.Mixed && stateTime < clipData?.RotationFinishedTime);
            bool wasCurveLogic = _curve.LastMotionType == MotionType.CurveDriven || _curve.LastMotionType == MotionType.Mixed;

            if (isCurvePhase && (!wasCurveLogic || !_curve.IsInitialized))
            {
                _curve.Reset();
                _data.RotationVelocity = 0f;
                _curve.DidAlignOnMixed = false;
            }

            _curve.LastMotionType = current;
        }

        private void AlignAndResetForInputTransition()
        {
            _data.RotationVelocity = 0f;
            _data.CurrentYaw = _transform.eulerAngles.y;
            _curve.IsInitialized = false;
        }

        #endregion

        #region Motion Warping Helpers

        private void CheckAndAdvanceWarpSegment(float normalizedTime)
        {
            if (_warp.CurrentIndex >= _warp.Data.WarpPoints.Count) return;

            float targetTime = _warp.Data.WarpPoints[_warp.CurrentIndex].NormalizedTime;
            if (normalizedTime >= targetTime)
            {
                _warp.CurrentIndex++;
                _warp.SegmentStartTime = targetTime;
                _warp.SegmentStartPosition = _transform.position;
                RecalculateWarpCompensation();
            }
        }

        private void RecalculateWarpCompensation()
        {
            if (_warp.CurrentIndex >= _warp.Data.WarpPoints.Count)
            {
                _warp.CompensationVel = Vector3.zero;
                return;
            }

            var warpPoint = _warp.Data.WarpPoints[_warp.CurrentIndex];
            float segmentSeconds = (warpPoint.NormalizedTime - _warp.SegmentStartTime) * _warp.Data.BakedDuration;

            if (segmentSeconds < 0.01f)
            {
                _warp.CompensationVel = Vector3.zero;
                return;
            }

            Vector3 realDelta = _warp.Targets[_warp.CurrentIndex] - _warp.SegmentStartPosition;
            Vector3 animDelta = _transform.TransformVector(warpPoint.BakedLocalOffset);

            _warp.CompensationVel = (realDelta - animDelta) / segmentSeconds;
        }

        #endregion
    }

    public static class Vector3Extensions
    {
        public static Vector3 SetY(this Vector3 vector, float y)
        {
            vector.y = y;
            return vector;
        }
    }
}