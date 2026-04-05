using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 镜头冲击服务（场景单例，仿 HitStopService 模式）。
    ///
    /// 向 CameraExpressionApplicator 发送一次性的高优先级镜头冲击：
    ///   - 冲击持续期间覆盖武器的每帧 CameraExpression
    ///   - 超时后 Applicator 自动回退到武器 CameraExpression，SmoothDamp 负责平滑过渡
    ///   - 不修改 RuntimeData，不被 ResetIntent 清零
    ///
    /// 使用方式：
    ///   CameraImpulseService.Instance?.Request(presetSO, duration);
    /// </summary>
    public sealed class CameraImpulseService : SingletonMono<CameraImpulseService>
    {
        [Tooltip("场景中的 CameraExpressionApplicator（在 Inspector 中手动赋值）")]
        [SerializeField] private CameraExpressionApplicator _applicator;

        [SerializeField] private bool _debugLog = false;

        /// <summary>
        /// 发起一次相对 Δ 镜头冲击请求，持续时间内置于 <paramref name="preset"/>。
        /// </summary>
        public void Request(CameraImpulseDeltaSO preset)
        {
            if (preset == null)
            {
                if (_debugLog) Debug.LogWarning("[CameraImpulse] preset 为空，跳过。", this);
                return;
            }

            if (_applicator == null)
            {
                if (_debugLog) Debug.LogWarning("[CameraImpulse] Applicator 未赋值，跳过。", this);
                return;
            }

            _applicator.RequestImpulse(preset.ToExpression(), preset.Duration);

            if (_debugLog)
                Debug.Log($"[CameraImpulse] 发起 Δ 冲击 preset='{preset.name}' duration={preset.Duration:F3}s", this);
        }

        /// <summary>
        /// 发起一次镜头冲击请求。
        /// </summary>
        /// <param name="preset">冲击期间要应用的相机表现力预设。</param>
        /// <param name="duration">冲击持续时间（秒）。到时后 Applicator 自动回退。</param>
        public void Request(CameraExpressionSO preset, float duration)
        {
            if (preset == null)
            {
                if (_debugLog) Debug.LogWarning("[CameraImpulse] preset 为空，跳过。", this);
                return;
            }

            if (_applicator == null)
            {
                if (_debugLog) Debug.LogWarning("[CameraImpulse] Applicator 未赋值，跳过。", this);
                return;
            }

            _applicator.RequestImpulse(preset.ToExpression(), duration);

            if (_debugLog)
                Debug.Log($"[CameraImpulse] 发起冲击 preset='{preset.name}' duration={duration:F3}s", this);
        }
    }
}
