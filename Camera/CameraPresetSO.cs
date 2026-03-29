using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 相机表现力预设。
    /// 描述"拿着某种武器时摄像机应该是什么样子"，独立于武器本身的战斗配置。
    /// 武器 SO 通过 EquippableItemSO.CameraPreset 引用一个预设资产即可。
    ///
    /// 推荐预设资产：
    ///   CamPreset_Default     — 无武器 / 探索
    ///   CamPreset_PistolAim   — 手枪瞄准
    ///   CamPreset_RifleAim    — 步枪 / 冲锋枪瞄准
    ///   CamPreset_SniperAim   — 狙击（FOV 极小，慢推）
    ///   CamPreset_Fists       — 近战（略微压低视角，紧张感）
    /// </summary>
    [CreateAssetMenu(fileName = "CamPreset_New", menuName = "BBBNexus/Camera/CameraPreset")]
    public class CameraPresetSO : ScriptableObject
    {
        [Header("镜头参数")]
        [Tooltip("目标 FOV（度）。0 = 沿用 VirtualCamera Inspector 默认值。")]
        public float TargetFov = 0f;

        [Tooltip("相机跟随偏移（Transposer FollowOffset）。Zero = 不覆写。")]
        public Vector3 FollowOffset = Vector3.zero;

        [Tooltip("画面内目标偏移（Composer TrackedObjectOffset，可做肩膀视角）。Zero = 不覆写。")]
        public Vector3 TrackedObjectOffset = Vector3.zero;

        [Header("输入参数")]
        [Tooltip("视角灵敏度缩放系数（乘以 CoreSO.LookSensitivity）。0 = 不覆写。")]
        [Range(0f, 2f)]
        public float SensitivityScale = 0f;

        [Header("过渡手感")]
        [Tooltip("FOV 插值平滑时间（秒）。0 = 使用 CameraExpressionApplicator 默认值。")]
        public float FovSmoothTime = 0f;

        [Tooltip("偏移插值平滑时间（秒）。0 = 使用 CameraExpressionApplicator 默认值。")]
        public float OffsetSmoothTime = 0f;

        /// <summary>
        /// 将预设转换为帧级黑板数据，供武器 Behaviour 直接写入 RuntimeData.CameraExpression。
        /// </summary>
        public CameraExpression ToExpression() => new CameraExpression
        {
            HasRequest          = true,
            TargetFov           = TargetFov,
            FollowOffset        = FollowOffset,
            TrackedObjectOffset = TrackedObjectOffset,
            SensitivityScale    = SensitivityScale,
            FovSmoothTime       = FovSmoothTime,
            OffsetSmoothTime    = OffsetSmoothTime,
        };
    }
}
