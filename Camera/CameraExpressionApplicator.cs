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
    /// 无武器写入时自动回归 Inspector 默认值。
    ///
    /// 职责边界：
    ///   - 只管"摄像机长什么样"（FOV / 跟随偏移 / 目标偏移）
    ///   - 不管"镜头指向哪里"（那是 CameraLookAtDriver 的工作）
    /// </summary>
    public class CameraExpressionApplicator : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private BBBCharacterController _player;

#if BBBNEXUS_HAS_CINEMACHINE
        [SerializeField] private CinemachineVirtualCamera _vcam;
#endif

        [Header("平滑时间（秒）")]
        [SerializeField] private float _fovSmoothTime          = 0.12f;
        [SerializeField] private float _followSmoothTime       = 0.15f;
        [SerializeField] private float _trackedOffsetSmoothTime = 0.15f;

        // ── Inspector 默认值（Start 时缓存）────────────────────────────
        private float   _defaultFov;
        private Vector3 _defaultFollowOffset;
        private Vector3 _defaultTrackedObjectOffset;

        // ── SmoothDamp 速度 ────────────────────────────────────────────
        private float   _fovVelocity;
        private Vector3 _followVelocity;
        private Vector3 _trackedOffsetVelocity;

        // ── 当前插值中间值（实际写入 vcam）────────────────────────────
        private float   _currentFov;
        private Vector3 _currentFollowOffset;
        private Vector3 _currentTrackedObjectOffset;

#if BBBNEXUS_HAS_CINEMACHINE
        private CinemachineTransposer _transposer;
        private CinemachineComposer   _composer;
#endif

        // ── 生命周期 ───────────────────────────────────────────────────

        private void Start()
        {
#if BBBNEXUS_HAS_CINEMACHINE
            if (_vcam == null) return;

            _transposer = _vcam.GetCinemachineComponent<CinemachineTransposer>();
            _composer   = _vcam.GetCinemachineComponent<CinemachineComposer>();

            // 缓存 Inspector 配置好的基准值
            _defaultFov                 = _vcam.m_Lens.FieldOfView;
            _defaultFollowOffset        = _transposer != null ? _transposer.m_FollowOffset        : Vector3.zero;
            _defaultTrackedObjectOffset = _composer   != null ? _composer.m_TrackedObjectOffset   : Vector3.zero;

            // 以基准值初始化当前插值状态，避免第一帧从 zero 飞过来
            _currentFov                 = _defaultFov;
            _currentFollowOffset        = _defaultFollowOffset;
            _currentTrackedObjectOffset = _defaultTrackedObjectOffset;
#endif
        }

        private void Update()
        {
            if (_player == null) return;

#if BBBNEXUS_HAS_CINEMACHINE
            if (_vcam == null) return;

            var expr = _player.RuntimeData.CameraExpression;

            // 没有武器请求 → 回归默认；有请求 → 使用请求值（≤0 / Zero 视为"不覆写此项"）
            float   targetFov           = (expr.HasRequest && expr.TargetFov > 0f)           ? expr.TargetFov           : _defaultFov;
            Vector3 targetFollowOffset  = (expr.HasRequest && expr.FollowOffset != Vector3.zero) ? expr.FollowOffset  : _defaultFollowOffset;
            Vector3 targetTrackedOffset = (expr.HasRequest && expr.TrackedObjectOffset != Vector3.zero) ? expr.TrackedObjectOffset : _defaultTrackedObjectOffset;

            // 预设可以覆写平滑时间；≤ 0 则回退到 Inspector 默认值
            float fovSmooth    = (expr.HasRequest && expr.FovSmoothTime    > 0f) ? expr.FovSmoothTime    : _fovSmoothTime;
            float offsetSmooth = (expr.HasRequest && expr.OffsetSmoothTime > 0f) ? expr.OffsetSmoothTime : _followSmoothTime;

            float dt = Time.deltaTime;

            _currentFov = Mathf.SmoothDamp(
                _currentFov, targetFov, ref _fovVelocity, fovSmooth, float.MaxValue, dt);

            _currentFollowOffset = Vector3.SmoothDamp(
                _currentFollowOffset, targetFollowOffset, ref _followVelocity, offsetSmooth, float.MaxValue, dt);

            _currentTrackedObjectOffset = Vector3.SmoothDamp(
                _currentTrackedObjectOffset, targetTrackedOffset, ref _trackedOffsetVelocity, offsetSmooth, float.MaxValue, dt);

            // 写入 VirtualCamera
            _vcam.m_Lens.FieldOfView = _currentFov;
            if (_transposer != null) _transposer.m_FollowOffset        = _currentFollowOffset;
            if (_composer   != null) _composer.m_TrackedObjectOffset   = _currentTrackedObjectOffset;
#endif
        }
    }
}
