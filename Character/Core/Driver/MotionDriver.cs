using UnityEngine;

namespace BBBNexus
{

    /*CalculateInputDrivenVelocity()  （主分支）
    ├── CalculateFreeLookVelocity()    [自由视角模式]
    │   ├── 1. 读取 DesiredWorldMoveDir（输入方向）
    │   ├── 2. 通过 SmoothDampAngle 平滑角色朝向（CurrentYaw）
    │   └── 3. 计算平滑速度（SmoothSpeed）
    │
    └── CalculateAimVelocity()         [瞄准模式]
        ├── 1. 角色朝向 AuthorityYaw（权威朝向）
        ├── 2. 将摇杆输入投影到 forward/right
        ├── 3. 检测反向输入（直接清零速度平滑状态）
        └── 4. 计算平滑速度

    CalculateClipDrivenVelocity()   （动画驱动分支）
    ├── 曲线段阶段 → CalculateCurveVelocity()
    │   ├── 1. 读取旋转曲线计算转向角度
    │   ├── 2. 读取速度曲线
    │   └── 3. 使用动画目标方向生成世界速度
    │
    └── 混合段阶段（Mixed） → 切回 CalculateInputDrivenVelocity()
        ├── 1. 对齐速度/旋转状态
        └── 2. 恢复输入驱动*/

    /// <summary>
    /// 角色运动的核心驱动器 负责将输入、动画曲线、物理参数
    /// 转换为实际的 CharacterController.Move()调用 驱动角色在场景中的实际位移
    /// </summary>
    public class MotionDriver
    {
        private const int CharacterBlockMaxOverlapCount = 16;
        private static readonly Collider[] CharacterBlockOverlaps = new Collider[CharacterBlockMaxOverlapCount];

        // 注：在最新版本 主要优化了Unity的底层开销(eulerAngles/velocity/materialized quaternion) 并保证每帧重力只积分一次
        #region Dependencies
        private readonly BBBCharacterController _player;
        private readonly CharacterController _cc;
        private readonly PlayerRuntimeData _data;
        private readonly PlayerSO _config;
        private readonly Transform _transform;
        #endregion

        #region Contexts

        private struct LocomotionCtx
        {
            public bool WasAiming;

            // 平滑速度(标量)
            public float SmoothSpeed;
            public float SpeedVelocity;

            // 用于检测瞄准形态下反向切输入，避免 SmoothDamp 造成“拖拽”
            public Vector3 LastAimMoveDir;

            public void ResetSpeed()
            {
                SmoothSpeed = 0f;
                SpeedVelocity = 0f;
                LastAimMoveDir = Vector3.zero;
            }
        }

        private struct CurveCtx
        {
            public float LastAngle;
            public bool IsInitialized;
            public MotionType? LastMotionType;
            public bool DidAlignOnMixed;

            public void Reset()
            {
                LastAngle = 0f;
                IsInitialized = false;
            }
        }

        private struct WarpCtx
        {
            public WarpedMotionData Data;
            public Vector3[] Targets;
            public int CurrentIndex;
            public float SegmentStartTime;
            public Vector3 SegmentStartPosition;
            public Vector3 CompensationVel;

            public bool IsActive => Data != null;

            public void Clear()
            {
                Data = null;
                Targets = null;
                CompensationVel = Vector3.zero;
                CurrentIndex = 0;
                SegmentStartTime = 0f;
                SegmentStartPosition = Vector3.zero;
            }
        }

        private LocomotionCtx _loco;
        private CurveCtx _curve;
        private WarpCtx _warp;

        // 单帧重力缓存：避免同帧多处调用重复积分 VerticalVelocity
        private int _gravityFrame = -1;
        private Vector3 _cachedGravity;

        #endregion

        public MotionDriver(BBBCharacterController player)
        {
            _player = player;
            _cc = player.CharController;
            _data = player.RuntimeData;
            _config = player.Config;
            _transform = player.transform;

            _loco.WasAiming = _data.IsTacticalStance;
        }

        #region Public API

        /// <summary>
        /// 请求将角色旋转到指定 Yaw。MotionDriver 是唯一的 transform.rotation 写入者。
        /// smoothTime = 0：本帧同步立即到位（适合需要当帧生效的场景，如 Dodge 开始前对齐朝向）。
        /// smoothTime > 0：写入黑板，由后续 MotionDriver 更新平滑驱动（适合锁定跟随等持续旋转）。
        /// </summary>
        public void RequestYaw(float targetYaw, float smoothTime = 0f)
        {
            if (smoothTime <= 0f)
            {
                _transform.rotation = Quaternion.Euler(0f, targetYaw, 0f);
                _data.CurrentYaw = targetYaw;
                _data.RotationVelocity = 0f;
            }
            else
            {
                _data.RequestedTargetYaw = targetYaw;
                _data.RequestedYawSmoothTime = smoothTime;
            }
        }

        public void RequestHorizontalDisplacement(Vector3 displacement)
        {
            displacement.y = 0f;
            _data.RequestedHorizontalDisplacement += displacement;
        }

        public void RequestVerticalDisplacement(float displacement)
        {
            _data.RequestedVerticalDisplacement += displacement;
        }

        public void UpdateGravityOnly(bool hardStop = false, bool skipGravity = false)
        {
            ConsumeYawRequest();
            // 即使只做重力，也要消费并阻挡水平位移请求（攻击对齐/根运动等），防止撞入其他角色
            Vector3 hv = Vector3.zero;
            Vector3 requestedDisplacement = ConsumeRequestedHorizontalDisplacement();
            if (requestedDisplacement.sqrMagnitude > 0.0001f && Time.deltaTime > 0.000001f)
            {
                hv = requestedDisplacement / Time.deltaTime;
            }
            hv = ResolveCharacterBlocking(hv, hardStop);
            float requestedVerticalDisplacement = ConsumeRequestedVerticalDisplacement();
            Vector3 vv = BuildVerticalVelocity(skipGravity, requestedVerticalDisplacement);
            _cc.Move((hv + vv) * Time.deltaTime);
            _data.CurrentSpeed = _cc.velocity.magnitude;
        }

        /// <summary>
        /// 将动画根运动的 XZ 位移过角色间阻挡过滤后返回。
        /// </summary>
        public Vector3 ApplyRootMotionHorizontal(Vector3 horizontalDisplacement, bool hardStop)
        {
            horizontalDisplacement.y = 0f;
            if (horizontalDisplacement.sqrMagnitude <= 0.0001f || Time.deltaTime <= 0.000001f)
            {
                return Vector3.zero;
            }

            Vector3 filteredVelocity = ResolveCharacterBlocking(horizontalDisplacement / Time.deltaTime, hardStop);
            return filteredVelocity * Time.deltaTime;
        }

        public void UpdateMotion(MotionClipData clipData, float stateTime)
        {
            HandleAimModeTransitionIfNeeded();
            AutoHandleCurveDrivenEnter(clipData, stateTime);

            Vector3 hv = clipData == null
                ? CalculateInputDrivenVelocity(1f)
                : CalculateClipDrivenVelocity(clipData, stateTime);

            ExecuteMovement(hv);
        }

        public void UpdateLocomotionFromInput(float speedMult = 1f)
        {
            HandleAimModeTransitionIfNeeded();
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

        #region Warp API

        public void InitializeWarpData(WarpedMotionData data, Vector3[] targets)
        {
            if (data == null || data.WarpPoints == null || data.WarpPoints.Count == 0 ||
                targets == null || targets.Length != data.WarpPoints.Count)
            {
                Debug.LogError("运动扭曲数据初始化失败 参数不匹配");
                return;
            }

            _warp.Data = data;
            _warp.Targets = new Vector3[targets.Length];

            // 目标偏移：按角色根空间转换到世界。
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

            // warp 期间不使用普通平滑(直接读当前水平速度只是为了保持数据一致)
            Vector3 v = _cc.velocity;
            _loco.SmoothSpeed = new Vector3(v.x, 0f, v.z).magnitude;
            _loco.SpeedVelocity = 0f;

            CheckAndAdvanceWarpSegment(normalizedTime);

            // 本地速度曲线 -> 世界
            Vector3 localVel = new Vector3(
                _warp.Data.LocalVelocityX.Evaluate(normalizedTime),
                _warp.Data.LocalVelocityY.Evaluate(normalizedTime),
                _warp.Data.LocalVelocityZ.Evaluate(normalizedTime)
            );

            Vector3 finalVelocity = _transform.TransformDirection(localVel) + _warp.CompensationVel;

            if (_warp.Data.ApplyGravity)
            {
                finalVelocity += GetGravityThisFrame();
            }
            else
            {
                _data.IsGrounded = _cc.isGrounded;
            }

            float rotVelY = _warp.Data.LocalRotationY.Evaluate(normalizedTime);

            // Warp 也需要防撞：只阻挡水平分量，保留垂直
            Vector3 warpHorizontal = new Vector3(finalVelocity.x, 0f, finalVelocity.z);
            warpHorizontal = ResolveCharacterBlocking(warpHorizontal);
            finalVelocity = new Vector3(warpHorizontal.x, finalVelocity.y, warpHorizontal.z);
            _cc.Move(finalVelocity * Time.deltaTime);
            if (Mathf.Abs(rotVelY) > 0.0001f)
                _transform.Rotate(0f, rotVelY * Time.deltaTime, 0f, Space.World);

            _data.CurrentSpeed = _cc.velocity.magnitude;
        }

        public void ClearWarpData() => _warp.Clear();

        #endregion

        #region Core Movement

        private void ExecuteMovement(Vector3 horizontalVelocity)
        {
            Vector3 requestedDisplacement = ConsumeRequestedHorizontalDisplacement();
            if (requestedDisplacement.sqrMagnitude > 0.0001f && Time.deltaTime > 0.000001f)
            {
                horizontalVelocity += requestedDisplacement / Time.deltaTime;
            }

            // 统一出口：所有水平速度在 Move 前都要做角色间防碰撞，
            // 墙壁与地形仍由 CharacterController 原生碰撞负责。
            horizontalVelocity = ResolveCharacterBlocking(horizontalVelocity);

            float requestedVerticalDisplacement = ConsumeRequestedVerticalDisplacement();
            Vector3 vv = BuildVerticalVelocity(skipGravity: false, requestedVerticalDisplacement);
            _cc.Move((horizontalVelocity + vv) * Time.deltaTime);
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

            // Mixed 从曲线段切到输入段：只对齐一次
            if (clipData.Type == MotionType.Mixed && !_curve.DidAlignOnMixed)
            {
                AlignAndResetForInputTransition();
                _curve.DidAlignOnMixed = true;
            }

            return CalculateInputDrivenVelocity(1f);
        }

        private Vector3 CalculateInputDrivenVelocity(float speedMult)
        {
            return _data.IsTacticalStance
                ? CalculateAimVelocity(speedMult)
                : CalculateFreeLookVelocity(speedMult);
        }

        #endregion

        #region Movement Modes

        private Vector3 CalculateFreeLookVelocity(float speedMult)
        {
            Vector3 moveDir = _data.DesiredWorldMoveDir;

            if (moveDir.sqrMagnitude < 0.0001f)
            {
                ConsumeYawRequest();
                _loco.SmoothSpeed = 0f;
                return Vector3.zero;
            }

            if (!ConsumeYawRequest())
            {
                float targetYaw = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
                ApplySmoothYaw(targetYaw, _config.Core.RotationSmoothTime);
            }

            return CalculateSmoothedVelocity(moveDir, isAiming: false, speedMult);
        }

        private Vector3 CalculateAimVelocity(float speedMult)
        {
            if (!ConsumeYawRequest())
            {
                float smoothTime = _config.TacticalMotionBase != null
                    ? _config.TacticalMotionBase.AimRotationSmoothTime
                    : _config.Core.RotationSmoothTime;
                ApplySmoothYaw(_data.AuthorityYaw, smoothTime);
            }

            Vector2 input = _data.MoveInput;
            if (input.sqrMagnitude < 0.001f)
            {
                _loco.ResetSpeed();
                return Vector3.zero;
            }

            // 平面 forward/right 投影
            Vector3 f = _transform.forward;
            f.y = 0f;
            float fMag = f.magnitude;
            if (fMag > 0.0001f) f /= fMag;

            Vector3 r = _transform.right;
            r.y = 0f;
            float rMag = r.magnitude;
            if (rMag > 0.0001f) r /= rMag;

            Vector3 move = (r * input.x + f * input.y);
            if (move.sqrMagnitude > 0.0001f) move.Normalize();

            // 反向切输入，直接清零 SmoothDamp 状态
            if (_loco.LastAimMoveDir.sqrMagnitude > 0.1f && Vector3.Dot(move, _loco.LastAimMoveDir) < 0f)
            {
                _loco.SmoothSpeed = 0f;
                _loco.SpeedVelocity = 0f;
            }

            _loco.LastAimMoveDir = move;
            return CalculateSmoothedVelocity(move, isAiming: true, speedMult);
        }

        private Vector3 CalculateCurveVelocity(MotionClipData data, float time)
        {
            float t = time * data.PlaybackSpeed;

            // 旋转曲线：用 deltaAngle 推进
            float curveAngle = data.RotationCurve.Evaluate(t);
            if (!_curve.IsInitialized)
            {
                _curve.LastAngle = curveAngle;
                _curve.IsInitialized = true;
            }

            float deltaAngle = curveAngle - _curve.LastAngle;
            _curve.LastAngle = curveAngle;

            if (Mathf.Abs(deltaAngle) > 0.0001f)
                _transform.Rotate(0f, deltaAngle, 0f, Space.World);

            // 动画驱动阶段：仍同步 CurrentYaw，供其他系统读取
            _data.CurrentYaw = _transform.eulerAngles.y;

            float speed = data.SpeedCurve.Evaluate(t);
            Vector3 localDir = data.TargetLocalDirection;

            if (localDir.sqrMagnitude > 0.0001f)
            {
                // 仅平面转换
                Vector3 worldDir = _transform.TransformDirection(localDir.SetY(0f));
                worldDir.y = 0f;
                if (worldDir.sqrMagnitude > 0.0001f) worldDir.Normalize();
                return worldDir * speed;
            }

            return _transform.forward * speed;
        }

        #endregion

        #region Helpers

        // 消费黑板上的 RequestedTargetYaw，立即或平滑应用后清除
        // 返回 true 表示有 override 且已处理（调用方可跳过自己的目标 yaw 计算）
        private bool ConsumeYawRequest()
        {
            if (float.IsNaN(_data.RequestedTargetYaw)) return false;

            float target = _data.RequestedTargetYaw;
            float smooth = _data.RequestedYawSmoothTime;
            _data.RequestedTargetYaw = float.NaN;

            if (smooth <= 0f)
            {
                _transform.rotation = Quaternion.Euler(0f, target, 0f);
                _data.CurrentYaw = target;
                _data.RotationVelocity = 0f;
            }
            else
            {
                ApplySmoothYaw(target, smooth);
            }

            return true;
        }

        private Vector3 ConsumeRequestedHorizontalDisplacement()
        {
            Vector3 displacement = _data.RequestedHorizontalDisplacement;
            _data.RequestedHorizontalDisplacement = Vector3.zero;
            return displacement;
        }

        private float ConsumeRequestedVerticalDisplacement()
        {
            float displacement = _data.RequestedVerticalDisplacement;
            _data.RequestedVerticalDisplacement = 0f;
            return displacement;
        }

        private void ApplySmoothYaw(float targetYaw, float smoothTime)
        {
            // 用 CurrentYaw 做权威 yaw
            float currentYaw = _data.CurrentYaw;
            if (currentYaw == 0f)
            {
                // 首帧或外部未初始化时，兜底读一次
                currentYaw = _transform.eulerAngles.y;
            }

            float smoothed = Mathf.SmoothDampAngle(currentYaw, targetYaw, ref _data.RotationVelocity, smoothTime);
            _transform.rotation = Quaternion.Euler(0f, smoothed, 0f);
            _data.CurrentYaw = smoothed;
        }

        private Vector3 CalculateSmoothedVelocity(Vector3 moveDir, bool isAiming, float speedMult)
        {
            float baseSpeed = GetBaseSpeed(_data.CurrentLocomotionState, isAiming);
            if (!_data.IsGrounded) baseSpeed *= _config.Core.AirControl;

            float targetSpeed = baseSpeed * speedMult;
            _loco.SmoothSpeed = Mathf.SmoothDamp(_loco.SmoothSpeed, targetSpeed, ref _loco.SpeedVelocity, _config.Core.MoveSpeedSmoothTime);
            return moveDir * _loco.SmoothSpeed;
        }

        private float GetBaseSpeed(LocomotionState state, bool isAiming) => state switch
        {
            LocomotionState.Walk => isAiming ? (_config.TacticalMotionBase != null ? _config.TacticalMotionBase.AimWalkSpeed : _config.Core.WalkSpeed) : _config.Core.WalkSpeed,
            LocomotionState.Jog => isAiming ? (_config.TacticalMotionBase != null ? _config.TacticalMotionBase.AimJogSpeed : _config.Core.JogSpeed) : _config.Core.JogSpeed,
            LocomotionState.Sprint => isAiming ? (_config.TacticalMotionBase != null ? _config.TacticalMotionBase.AimSprintSpeed : _config.Core.SprintSpeed) : _config.Core.SprintSpeed,
            _ => 0f
        };

        /// <summary>
        /// 获取本帧重力
        /// </summary>
        private Vector3 GetGravityThisFrame()
        {
            int frame = Time.frameCount;
            if (_gravityFrame == frame) return _cachedGravity;
            _gravityFrame = frame;

            _data.IsGrounded = _cc.isGrounded;

            // grounded 且向下速度为负：回弹到小负值/贴地力
            if (_data.IsGrounded && _data.VerticalVelocity < 0f)
                _data.VerticalVelocity = _config.Core.ReboundForce;
            else
                _data.VerticalVelocity += _config.Core.Gravity * Time.deltaTime;

            _cachedGravity = new Vector3(0f, _data.VerticalVelocity, 0f);
            return _cachedGravity;
        }

        private Vector3 BuildVerticalVelocity(bool skipGravity, float requestedVerticalDisplacement)
        {
            Vector3 gravityVelocity = skipGravity ? Vector3.zero : GetGravityThisFrame();
            if (Mathf.Abs(requestedVerticalDisplacement) <= 0.0001f || Time.deltaTime <= 0.000001f)
            {
                return gravityVelocity;
            }

            float requestedVerticalVelocity = requestedVerticalDisplacement / Time.deltaTime;
            _data.VerticalVelocity = requestedVerticalVelocity;
            _cachedGravity = new Vector3(0f, requestedVerticalVelocity, 0f);
            _gravityFrame = Time.frameCount;

            return gravityVelocity + new Vector3(0f, requestedVerticalVelocity, 0f);
        }

        private void HandleAimModeTransitionIfNeeded()
        {
            if (_data.IsTacticalStance == _loco.WasAiming) return;

            // 形态切换：清理旋转与速度平滑状态
            _data.RotationVelocity = 0f;
            _loco.LastAimMoveDir = Vector3.zero;
            _loco.SpeedVelocity = 0f;
            _loco.WasAiming = _data.IsTacticalStance;
        }

        private void AutoHandleCurveDrivenEnter(MotionClipData clipData, float stateTime)
        {
            MotionType? current = clipData?.Type;
            bool isCurvePhase = current == MotionType.CurveDriven ||
                                (current == MotionType.Mixed && stateTime < clipData?.RotationFinishedTime);

            bool wasCurveLogic = _curve.LastMotionType == MotionType.CurveDriven ||
                                 _curve.LastMotionType == MotionType.Mixed;

            // 进入曲线段：重置曲线内部状态
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
            // Mixed 切换输入段：清理旋转速度 避免 SmoothDampAngle 残留
            _data.RotationVelocity = 0f;
            _data.CurrentYaw = _transform.eulerAngles.y;
            _curve.IsInitialized = false;
        }

        /// <param name="hardStop">
        /// true = 攻击模式：检测到阻挡时整体归零，不产生切线滑移。
        /// false = 行走模式：剥掉朝向分量，保留切线分量（原有行为）。
        /// </param>
        private Vector3 ResolveCharacterBlocking(Vector3 horizontalVelocity, bool hardStop = false)
        {
            if (_cc == null || horizontalVelocity.sqrMagnitude <= 0.0001f)
            {
                return horizontalVelocity;
            }

            float moveDistance = horizontalVelocity.magnitude * Time.deltaTime;
            if (moveDistance <= 0.0001f)
            {
                return horizontalVelocity;
            }

            Vector3 center = _transform.TransformPoint(_cc.center);
            float queryRadius = Mathf.Max(_cc.radius + moveDistance + 0.08f, 0.2f);
            int count = Physics.OverlapSphereNonAlloc(center, queryRadius, CharacterBlockOverlaps, ~0, QueryTriggerInteraction.Ignore);
            if (count <= 0)
            {
                return horizontalVelocity;
            }

            Vector3 adjustedVelocity = horizontalVelocity;
            for (int i = 0; i < count; i++)
            {
                Collider other = CharacterBlockOverlaps[i];
                CharacterBlockOverlaps[i] = null;
                if (other == null || other.transform.IsChildOf(_transform))
                {
                    continue;
                }

                var otherCharacter = other.GetComponentInParent<BBBCharacterController>();
                if (otherCharacter == null || otherCharacter == _player || otherCharacter.CharController == null)
                {
                    continue;
                }

                Vector3 toOther = otherCharacter.transform.position - _transform.position;
                toOther.y = 0f;
                float sqrDistance = toOther.sqrMagnitude;
                if (sqrDistance <= 0.0001f)
                {
                    continue;
                }

                Vector3 toOtherDir = toOther.normalized;
                float inwardSpeed = Vector3.Dot(adjustedVelocity, toOtherDir);
                if (inwardSpeed <= 0f)
                {
                    continue;
                }

                float blockDistance = Mathf.Max(0.1f, _cc.radius) + Mathf.Max(0.1f, otherCharacter.CharController.radius) + 0.05f;
                if (Mathf.Sqrt(sqrDistance) > blockDistance)
                {
                    continue;
                }

                if (hardStop)
                    return Vector3.zero;

                adjustedVelocity -= toOtherDir * inwardSpeed;
            }

            return adjustedVelocity;
        }

        #endregion

        #region Warp helpers

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
