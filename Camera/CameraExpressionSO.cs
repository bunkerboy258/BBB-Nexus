using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 相机表现力预设资产（持久化版 CameraExpression）。
    /// 在 Inspector 里配好参数，或用编辑器工具从场景 VirtualCamera 抓取，
    /// 武器 Behaviour 调用 ToExpression() 写入帧级黑板即可。
    /// Body 组件对应 Cinemachine3rdPersonFollow。
    /// </summary>
    [CreateAssetMenu(fileName = "CamExpr_New", menuName = "BBBNexus/Camera/CameraExpressionSO")]
    public class CameraExpressionSO : ScriptableObject
    {
        [Header("Lens")]
        [Tooltip("目标 FOV（度）")]
        public float TargetFov = 60f;

        [Header("Body — Cinemachine3rdPersonFollow")]
        [Tooltip("位置跟随阻尼（XYZ）")]
        public Vector3 Damping = new Vector3(0.1f, 0.25f, 0.3f);

        [Tooltip("肩膀偏移（XYZ）。X 正值 = 右肩，负值 = 左肩")]
        public Vector3 ShoulderOffset = new Vector3(1f, 0f, 0f);

        [Tooltip("垂直手臂长度（从肩膀往下偏移相机挂点的距离）")]
        public float VerticalArmLength = 0f;

        [Tooltip("相机靠哪侧肩膀（0 = 左肩, 1 = 右肩）")]
        [Range(0f, 1f)]
        public float CameraSide = 0.6f;

        [Tooltip("相机距目标的距离")]
        public float CameraDistance = 4f;

        [Tooltip("碰撞检测用的相机球半径")]
        public float CameraRadius = 0.15f;

        [Tooltip("进入碰撞状态时的阻尼。越小越快缩进，越大越柔和。")]
        public float DampingIntoCollision = 0f;

        [Tooltip("脱离碰撞状态时的阻尼。越小越快恢复，越大越柔和。")]
        public float DampingFromCollision = 0f;

        [Header("Adaptive Shoulder")]
        [Tooltip("是否根据角色到越肩方向上的遮挡净空，自动缩小越肩度。")]
        public bool EnableAdaptiveShoulder = false;

        [Tooltip("遮挡净空必须始终大于 当前越肩距离 * 此系数，否则开始缩肩。")]
        public float ShoulderClearanceMultiplier = 1.35f;

        [Tooltip("越肩缩小时的平滑时间（秒）。越小越快收肩。")]
        public float ShoulderShrinkSmoothTime = 0.06f;

        [Tooltip("越肩恢复时的平滑时间（秒）。越大越不容易弹回。")]
        public float ShoulderRecoverSmoothTime = 0.22f;

        [Tooltip("侧向探测半径。<= 0 时自动沿用 CameraRadius。")]
        public float ShoulderProbeRadius = 0f;

        [Tooltip("环境拥挤时允许保留的最小越肩比例。0 = 可完全收中。")]
        [Range(0f, 1f)]
        public float MinShoulderScale = 0.35f;

        [Tooltip("越肩比例变化低于此阈值时忽略，避免边缘抖动。")]
        [Range(0f, 1f)]
        public float ShoulderScaleDeadZone = 0.03f;

        [Header("输入")]
        [Tooltip("视角灵敏度缩放（乘以 CoreSO.LookSensitivity）。1 = 不变")]
        [Range(0.1f, 2f)]
        public float SensitivityScale = 1f;

        [Header("Applicator 过渡手感")]
        [Tooltip("FOV 插值平滑时间（秒）。0 = 使用 Applicator 组件默认值")]
        public float FovSmoothTime = 0f;
        [Tooltip("位移 / 偏移插值平滑时间（秒）。0 = 使用 Applicator 组件默认值")]
        public float OffsetSmoothTime = 0f;

        /// <summary>转换为帧级黑板数据，直接写入 RuntimeData.CameraExpression。</summary>
        public CameraExpression ToExpression() => new CameraExpression
        {
            HasRequest        = true,
            TargetFov         = TargetFov,
            Damping           = Damping,
            ShoulderOffset    = ShoulderOffset,
            VerticalArmLength = VerticalArmLength,
            CameraSide        = CameraSide,
            CameraDistance    = CameraDistance,
            CameraRadius      = CameraRadius,
            DampingIntoCollision = DampingIntoCollision,
            DampingFromCollision = DampingFromCollision,
            EnableAdaptiveShoulder = EnableAdaptiveShoulder,
            ShoulderClearanceMultiplier = ShoulderClearanceMultiplier,
            ShoulderShrinkSmoothTime = ShoulderShrinkSmoothTime,
            ShoulderRecoverSmoothTime = ShoulderRecoverSmoothTime,
            ShoulderProbeRadius = ShoulderProbeRadius,
            MinShoulderScale = MinShoulderScale,
            ShoulderScaleDeadZone = ShoulderScaleDeadZone,
            SensitivityScale  = SensitivityScale,
            FovSmoothTime     = FovSmoothTime,
            OffsetSmoothTime  = OffsetSmoothTime,
        };
    }
}
