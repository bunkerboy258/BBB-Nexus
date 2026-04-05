using UnityEngine;
#if BBBNEXUS_HAS_CINEMACHINE
using Cinemachine;
#endif

namespace BBBNexus
{
    /// <summary>
    /// 相机表现力应用器。
    /// 持有场景中唯一的 CinemachineVirtualCamera，每帧从黑板读取
    /// PlayerRuntimeData.CameraExpression，用 SmoothDamp 将各参数平滑插值到目标值。
    /// HasRequest = false 时自动回归 Start 时缓存的 Inspector 默认值。
    /// Body 组件对应 Cinemachine3rdPersonFollow。
    /// </summary>
    public class CameraExpressionApplicator : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private BBBCharacterController _player;
#if BBBNEXUS_HAS_CINEMACHINE
        [SerializeField] private CinemachineVirtualCamera _vcam;
#endif

        [Header("默认平滑时间（秒）— 预设可覆写")]
        [SerializeField] private float _fovSmoothTime    = 0.12f;
        [SerializeField] private float _offsetSmoothTime = 0.15f;

        // ── 默认值（Start 时从 Inspector 快照）────────────────────────
        private float   _defFov;
        private Vector3 _defDamping;
        private Vector3 _defShoulderOffset;
        private float   _defVerticalArmLength;
        private float   _defCameraSide;
        private float   _defCameraDistance;
        private float   _defCameraRadius;
        private float   _defDampingIntoCollision;
        private float   _defDampingFromCollision;
        private bool    _defEnableAdaptiveShoulder;
        private float   _defShoulderClearanceMultiplier = 1.35f;
        private float   _defShoulderShrinkSmoothTime = 0.06f;
        private float   _defShoulderRecoverSmoothTime = 0.22f;
        private float   _defShoulderProbeRadius;
        private float   _defMinShoulderScale = 0.35f;
        private float   _defShoulderScaleDeadZone = 0.03f;

        // ── SmoothDamp 中间值 ─────────────────────────────────────────
        private float   _curFov;
        private Vector3 _curDamping;
        private Vector3 _curShoulderOffset;
        private float   _curVerticalArmLength;
        private float   _curCameraSide;
        private float   _curCameraDistance;
        private float   _curCameraRadius;
        private float   _curDampingIntoCollision;
        private float   _curDampingFromCollision;
        private float   _appliedShoulderX;
        private float   _curShoulderScale = 1f;

        // ── 镜头冲击 Impulse（优先级高于武器 CameraExpression）────────
        // 由 CameraImpulseService.RequestImpulse() 写入，自动超时失效。
        // 不经过 RuntimeData，不被 ResetIntent 清零。
        private CameraExpression _impulseExpr;
        private float            _impulseEndTime = -1f;

        /// <summary>
        /// 请求一次高优先级的镜头冲击，持续 <paramref name="duration"/> 秒后自动回退。
        /// 由 CameraImpulseService 调用；武器的每帧 CameraExpression 在冲击期间被覆盖。
        /// </summary>
        public void RequestImpulse(CameraExpression expr, float duration)
        {
            _impulseExpr    = expr;
            _impulseEndTime = Time.time + duration;
        }

        // ── SmoothDamp 速度 ───────────────────────────────────────────
        private float   _vFov;
        private Vector3 _vDamping;
        private Vector3 _vShoulder;
        private float   _vVAL;
        private float   _vCS;
        private float   _vCD;
        private float   _vCR;
        private float   _vDampingIntoCollision;
        private float   _vDampingFromCollision;
        private float   _vAppliedShoulderX;

#if BBBNEXUS_HAS_CINEMACHINE
        private Cinemachine3rdPersonFollow _thirdPersonFollow;
#endif

        private void Start()
        {
#if BBBNEXUS_HAS_CINEMACHINE
            if (_vcam == null) return;

            _thirdPersonFollow = _vcam.GetCinemachineComponent<Cinemachine3rdPersonFollow>();

            // 快照 Inspector 默认值
            _defFov = _vcam.m_Lens.FieldOfView;
            _defEnableAdaptiveShoulder = false;
            _defShoulderProbeRadius = 0f;

            if (_thirdPersonFollow != null)
            {
                _defDamping           = _thirdPersonFollow.Damping;
                _defShoulderOffset    = _thirdPersonFollow.ShoulderOffset;
                _defVerticalArmLength = _thirdPersonFollow.VerticalArmLength;
                _defCameraSide        = _thirdPersonFollow.CameraSide;
                _defCameraDistance    = _thirdPersonFollow.CameraDistance;
                _defCameraRadius      = _thirdPersonFollow.CameraRadius;
                _defDampingIntoCollision = _thirdPersonFollow.DampingIntoCollision;
                _defDampingFromCollision = _thirdPersonFollow.DampingFromCollision;
            }

            // 以默认值初始化插值中间值
            _curFov               = _defFov;
            _curDamping           = _defDamping;
            _curShoulderOffset    = _defShoulderOffset;
            _curVerticalArmLength = _defVerticalArmLength;
            _curCameraSide        = _defCameraSide;
            _curCameraDistance    = _defCameraDistance;
            _curCameraRadius      = _defCameraRadius;
            _curDampingIntoCollision = _defDampingIntoCollision;
            _curDampingFromCollision = _defDampingFromCollision;
            _appliedShoulderX     = _curShoulderOffset.x;
            _curShoulderScale     = 1f;
#endif
        }

        private void Update()
        {
            if (_player == null) return;
#if BBBNEXUS_HAS_CINEMACHINE
            if (_vcam == null) return;

            // 镜头冲击期间覆盖武器的 CameraExpression；超时后自动回退
            var weaponExpr = _player.RuntimeData.CameraExpression;
            CameraExpression e;
            if (Time.time < _impulseEndTime)
                e = _impulseExpr.IsRelative
                    ? ResolveRelativeImpulse(_impulseExpr, weaponExpr)
                    : _impulseExpr;
            else
                e = weaponExpr;
            float dt        = Time.deltaTime;
            float fovSmooth = (e.HasRequest && e.FovSmoothTime    > 0f) ? e.FovSmoothTime    : _fovSmoothTime;
            float offSmooth = (e.HasRequest && e.OffsetSmoothTime > 0f) ? e.OffsetSmoothTime : _offsetSmoothTime;

            // 目标值：有请求用预设，否则回归默认
            float   tFov  = e.HasRequest ? e.TargetFov         : _defFov;
            Vector3 tDamp = e.HasRequest ? e.Damping           : _defDamping;
            Vector3 tShldr = e.HasRequest ? e.ShoulderOffset   : _defShoulderOffset;
            float   tVAL  = e.HasRequest ? e.VerticalArmLength : _defVerticalArmLength;
            float   tCS   = e.HasRequest ? e.CameraSide        : _defCameraSide;
            float   tCD   = e.HasRequest ? e.CameraDistance    : _defCameraDistance;
            float   tCR   = e.HasRequest ? e.CameraRadius      : _defCameraRadius;
            float   tInto = e.HasRequest ? e.DampingIntoCollision : _defDampingIntoCollision;
            float   tFrom = e.HasRequest ? e.DampingFromCollision : _defDampingFromCollision;

            // SmoothDamp
            _curFov            = Mathf.SmoothDamp(_curFov,            tFov,  ref _vFov,     fovSmooth, float.MaxValue, dt);
            _curDamping        = Vector3.SmoothDamp(_curDamping,       tDamp, ref _vDamping, offSmooth, float.MaxValue, dt);
            _curShoulderOffset = Vector3.SmoothDamp(_curShoulderOffset, tShldr, ref _vShoulder, offSmooth, float.MaxValue, dt);
            _curVerticalArmLength = Mathf.SmoothDamp(_curVerticalArmLength, tVAL, ref _vVAL, offSmooth, float.MaxValue, dt);
            _curCameraSide     = Mathf.SmoothDamp(_curCameraSide,     tCS,   ref _vCS,     offSmooth, float.MaxValue, dt);
            _curCameraDistance = Mathf.SmoothDamp(_curCameraDistance, tCD,   ref _vCD,     offSmooth, float.MaxValue, dt);
            _curCameraRadius   = Mathf.SmoothDamp(_curCameraRadius,   tCR,   ref _vCR,     offSmooth, float.MaxValue, dt);
            _curDampingIntoCollision = Mathf.SmoothDamp(_curDampingIntoCollision, tInto, ref _vDampingIntoCollision, offSmooth, float.MaxValue, dt);
            _curDampingFromCollision = Mathf.SmoothDamp(_curDampingFromCollision, tFrom, ref _vDampingFromCollision, offSmooth, float.MaxValue, dt);

            // 写入 VirtualCamera
            _vcam.m_Lens.FieldOfView = _curFov;

            if (_thirdPersonFollow != null)
            {
                Vector3 appliedShoulder = ResolveAdaptiveShoulder(e, dt);

                _thirdPersonFollow.Damping           = _curDamping;
                _thirdPersonFollow.ShoulderOffset    = appliedShoulder;
                _thirdPersonFollow.VerticalArmLength = _curVerticalArmLength;
                _thirdPersonFollow.CameraSide        = _curCameraSide;
                _thirdPersonFollow.CameraDistance    = _curCameraDistance;
                _thirdPersonFollow.CameraRadius      = _curCameraRadius;
                _thirdPersonFollow.DampingIntoCollision = _curDampingIntoCollision;
                _thirdPersonFollow.DampingFromCollision = _curDampingFromCollision;
            }
#endif
        }

#if BBBNEXUS_HAS_CINEMACHINE
        /// <summary>
        /// 将相对 Δ 冲击叠加到武器基础状态，返回可直接用于 SmoothDamp 的绝对 CameraExpression。
        /// </summary>
        private CameraExpression ResolveRelativeImpulse(CameraExpression delta, CameraExpression weaponBase)
        {
            bool wb = weaponBase.HasRequest;
            return new CameraExpression
            {
                HasRequest            = true,
                IsRelative            = false,
                TargetFov             = (wb ? weaponBase.TargetFov         : _defFov)             + delta.TargetFov,
                CameraDistance        = (wb ? weaponBase.CameraDistance    : _defCameraDistance)  + delta.CameraDistance,
                ShoulderOffset        = (wb ? weaponBase.ShoulderOffset    : _defShoulderOffset)  + delta.ShoulderOffset,
                VerticalArmLength     = (wb ? weaponBase.VerticalArmLength : _defVerticalArmLength) + delta.VerticalArmLength,
                SensitivityScale      = (wb ? weaponBase.SensitivityScale  : 1f)                  + delta.SensitivityScale,
                // 以下字段 delta 无意义，直接继承武器基础值
                Damping               = wb ? weaponBase.Damping               : _defDamping,
                CameraSide            = wb ? weaponBase.CameraSide            : _defCameraSide,
                CameraRadius          = wb ? weaponBase.CameraRadius          : _defCameraRadius,
                DampingIntoCollision  = wb ? weaponBase.DampingIntoCollision  : _defDampingIntoCollision,
                DampingFromCollision  = wb ? weaponBase.DampingFromCollision  : _defDampingFromCollision,
                EnableAdaptiveShoulder         = wb && weaponBase.EnableAdaptiveShoulder,
                ShoulderClearanceMultiplier    = wb ? weaponBase.ShoulderClearanceMultiplier    : _defShoulderClearanceMultiplier,
                ShoulderShrinkSmoothTime       = wb ? weaponBase.ShoulderShrinkSmoothTime       : _defShoulderShrinkSmoothTime,
                ShoulderRecoverSmoothTime      = wb ? weaponBase.ShoulderRecoverSmoothTime      : _defShoulderRecoverSmoothTime,
                ShoulderProbeRadius            = wb ? weaponBase.ShoulderProbeRadius            : _defShoulderProbeRadius,
                MinShoulderScale               = wb ? weaponBase.MinShoulderScale               : _defMinShoulderScale,
                ShoulderScaleDeadZone          = wb ? weaponBase.ShoulderScaleDeadZone          : _defShoulderScaleDeadZone,
                // smooth times 由 delta 预设决定（0 = 沿用 Applicator 默认值）
                FovSmoothTime         = delta.FovSmoothTime,
                OffsetSmoothTime      = delta.OffsetSmoothTime,
            };
        }

        private Vector3 ResolveAdaptiveShoulder(CameraExpression expression, float dt)
        {
            Vector3 applied = _curShoulderOffset;

            bool enableAdaptive = expression.HasRequest
                ? expression.EnableAdaptiveShoulder
                : _defEnableAdaptiveShoulder;

            if (!enableAdaptive || _vcam.Follow == null)
            {
                _curShoulderScale = 1f;
                _appliedShoulderX = Mathf.SmoothDamp(
                    _appliedShoulderX,
                    applied.x,
                    ref _vAppliedShoulderX,
                    Mathf.Max(0.0001f, _defShoulderRecoverSmoothTime),
                    float.MaxValue,
                    dt);
                applied.x = _appliedShoulderX;
                return applied;
            }

            float clearanceMultiplier = expression.HasRequest && expression.ShoulderClearanceMultiplier > 0f
                ? expression.ShoulderClearanceMultiplier
                : _defShoulderClearanceMultiplier;
            float shrinkSmooth = expression.HasRequest && expression.ShoulderShrinkSmoothTime > 0f
                ? expression.ShoulderShrinkSmoothTime
                : _defShoulderShrinkSmoothTime;
            float recoverSmooth = expression.HasRequest && expression.ShoulderRecoverSmoothTime > 0f
                ? expression.ShoulderRecoverSmoothTime
                : _defShoulderRecoverSmoothTime;
            float probeRadius = expression.HasRequest && expression.ShoulderProbeRadius > 0f
                ? expression.ShoulderProbeRadius
                : (_defShoulderProbeRadius > 0f ? _defShoulderProbeRadius : _curCameraRadius);
            float minScale = expression.HasRequest
                ? expression.MinShoulderScale
                : _defMinShoulderScale;
            float scaleDeadZone = expression.HasRequest
                ? expression.ShoulderScaleDeadZone
                : _defShoulderScaleDeadZone;

            minScale = Mathf.Clamp01(minScale);
            scaleDeadZone = Mathf.Clamp01(scaleDeadZone);

            float targetX = applied.x;
            float absTargetX = Mathf.Abs(targetX);
            if (absTargetX <= 0.0001f)
            {
                _curShoulderScale = 1f;
                _appliedShoulderX = targetX;
                applied.x = targetX;
                return applied;
            }

            float sign = Mathf.Sign(targetX);
            Transform follow = _vcam.Follow;
            Vector3 origin = follow.position;
            Vector3 direction = follow.right * sign;
            float maxDistance = absTargetX * Mathf.Max(1f, clearanceMultiplier) + Mathf.Max(0.05f, probeRadius);
            int layerMask = _thirdPersonFollow.CameraCollisionFilter;

            float rawScale = 1f;
            if (Physics.SphereCast(origin, Mathf.Max(0.001f, probeRadius), direction, out RaycastHit hit, maxDistance, layerMask, QueryTriggerInteraction.Ignore))
            {
                if (string.IsNullOrEmpty(_thirdPersonFollow.IgnoreTag) || !hit.collider.CompareTag(_thirdPersonFollow.IgnoreTag))
                {
                    rawScale = hit.distance / (Mathf.Max(0.0001f, clearanceMultiplier) * absTargetX);
                }
            }

            float targetScale = Mathf.Clamp(rawScale, minScale, 1f);
            if (Mathf.Abs(targetScale - _curShoulderScale) <= scaleDeadZone)
                targetScale = _curShoulderScale;

            float targetAppliedX = sign * absTargetX * targetScale;
            float smoothTime = Mathf.Abs(targetAppliedX) < Mathf.Abs(_appliedShoulderX) ? shrinkSmooth : recoverSmooth;
            _appliedShoulderX = Mathf.SmoothDamp(
                _appliedShoulderX,
                targetAppliedX,
                ref _vAppliedShoulderX,
                Mathf.Max(0.0001f, smoothTime),
                float.MaxValue,
                dt);
            _curShoulderScale = absTargetX > 0.0001f
                ? Mathf.Clamp01(Mathf.Abs(_appliedShoulderX) / absTargetX)
                : 1f;
            applied.x = _appliedShoulderX;
            return applied;
        }
#endif
    }
}
