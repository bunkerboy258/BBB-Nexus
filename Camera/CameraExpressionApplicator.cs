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

        // ── SmoothDamp 中间值 ─────────────────────────────────────────
        private float   _curFov;
        private Vector3 _curDamping;
        private Vector3 _curShoulderOffset;
        private float   _curVerticalArmLength;
        private float   _curCameraSide;
        private float   _curCameraDistance;
        private float   _curCameraRadius;

        // ── SmoothDamp 速度 ───────────────────────────────────────────
        private float   _vFov;
        private Vector3 _vDamping;
        private Vector3 _vShoulder;
        private float   _vVAL;
        private float   _vCS;
        private float   _vCD;
        private float   _vCR;

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

            if (_thirdPersonFollow != null)
            {
                _defDamping           = _thirdPersonFollow.Damping;
                _defShoulderOffset    = _thirdPersonFollow.ShoulderOffset;
                _defVerticalArmLength = _thirdPersonFollow.VerticalArmLength;
                _defCameraSide        = _thirdPersonFollow.CameraSide;
                _defCameraDistance    = _thirdPersonFollow.CameraDistance;
                _defCameraRadius      = _thirdPersonFollow.CameraRadius;
            }

            // 以默认值初始化插值中间值
            _curFov               = _defFov;
            _curDamping           = _defDamping;
            _curShoulderOffset    = _defShoulderOffset;
            _curVerticalArmLength = _defVerticalArmLength;
            _curCameraSide        = _defCameraSide;
            _curCameraDistance    = _defCameraDistance;
            _curCameraRadius      = _defCameraRadius;
#endif
        }

        private void Update()
        {
            if (_player == null) return;
#if BBBNEXUS_HAS_CINEMACHINE
            if (_vcam == null) return;

            var   e         = _player.RuntimeData.CameraExpression;
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

            // SmoothDamp
            _curFov            = Mathf.SmoothDamp(_curFov,            tFov,  ref _vFov,     fovSmooth, float.MaxValue, dt);
            _curDamping        = Vector3.SmoothDamp(_curDamping,       tDamp, ref _vDamping, offSmooth, float.MaxValue, dt);
            _curShoulderOffset = Vector3.SmoothDamp(_curShoulderOffset, tShldr, ref _vShoulder, offSmooth, float.MaxValue, dt);
            _curVerticalArmLength = Mathf.SmoothDamp(_curVerticalArmLength, tVAL, ref _vVAL, offSmooth, float.MaxValue, dt);
            _curCameraSide     = Mathf.SmoothDamp(_curCameraSide,     tCS,   ref _vCS,     offSmooth, float.MaxValue, dt);
            _curCameraDistance = Mathf.SmoothDamp(_curCameraDistance, tCD,   ref _vCD,     offSmooth, float.MaxValue, dt);
            _curCameraRadius   = Mathf.SmoothDamp(_curCameraRadius,   tCR,   ref _vCR,     offSmooth, float.MaxValue, dt);

            // 写入 VirtualCamera
            _vcam.m_Lens.FieldOfView = _curFov;

            if (_thirdPersonFollow != null)
            {
                _thirdPersonFollow.Damping           = _curDamping;
                _thirdPersonFollow.ShoulderOffset    = _curShoulderOffset;
                _thirdPersonFollow.VerticalArmLength = _curVerticalArmLength;
                _thirdPersonFollow.CameraSide        = _curCameraSide;
                _thirdPersonFollow.CameraDistance    = _curCameraDistance;
                _thirdPersonFollow.CameraRadius      = _curCameraRadius;
            }
#endif
        }
    }
}
