using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 相机冲击 Δ 预设（相对于当前武器基础摄像机状态的叠加量）。
    /// 适用于格挡、受击等需要短暂强调打击感的场景。
    /// 持续时间内置于 SO，调用方只需 CameraImpulseService.Instance?.Request(so)。
    /// </summary>
    [CreateAssetMenu(fileName = "CamImpulse_New", menuName = "BBBNexus/Camera/CameraImpulseDeltaSO")]
    public class CameraImpulseDeltaSO : ScriptableObject
    {
        [Header("持续时间")]
        [Tooltip("冲击持续时间（秒）。到时后 Applicator 自动通过 SmoothDamp 回退。")]
        [Min(0f)]
        public float Duration = 0.15f;

        [Header("FOV Δ（度）")]
        [Tooltip("在当前武器 FOV 基础上叠加的变化量。正值广角，负值收紧。")]
        public float FovDelta = 0f;

        [Header("距离 / 偏移 Δ")]
        [Tooltip("在当前 CameraDistance 上叠加的变化量。负值拉近。")]
        public float CameraDistanceDelta = 0f;

        [Tooltip("在当前 ShoulderOffset 上叠加的变化量。")]
        public Vector3 ShoulderOffsetDelta = Vector3.zero;

        [Tooltip("在当前 VerticalArmLength 上叠加的变化量。")]
        public float VerticalArmLengthDelta = 0f;

        [Header("灵敏度 Δ（乘法）")]
        [Tooltip("在当前 SensitivityScale 上叠加的变化量。0 = 不变。")]
        public float SensitivityScaleDelta = 0f;

        [Header("Applicator 过渡手感")]
        [Tooltip("FOV 插值平滑时间（秒）。0 = 使用 Applicator 默认值。")]
        public float FovSmoothTime = 0f;

        [Tooltip("位移 / 偏移插值平滑时间（秒）。0 = 使用 Applicator 默认值。")]
        public float OffsetSmoothTime = 0f;

        /// <summary>转换为帧级相对 CameraExpression，由 CameraImpulseService 送入 Applicator。</summary>
        public CameraExpression ToExpression() => new CameraExpression
        {
            HasRequest            = true,
            IsRelative            = true,
            TargetFov             = FovDelta,
            CameraDistance        = CameraDistanceDelta,
            ShoulderOffset        = ShoulderOffsetDelta,
            VerticalArmLength     = VerticalArmLengthDelta,
            SensitivityScale      = SensitivityScaleDelta,
            FovSmoothTime         = FovSmoothTime,
            OffsetSmoothTime      = OffsetSmoothTime,
        };
    }
}
