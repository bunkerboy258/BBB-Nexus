namespace BBBNexus
{
    /// <summary>
    /// 帧级相机表现力请求（黑板字段）。
    /// 武器 Behaviour 在 OnUpdateLogic() 中写入，CameraExpressionApplicator 消费，ResetIntent 清零。
    /// HasRequest = false 时 Applicator 回归所有 Inspector 默认值。
    /// Body 组件对应 Cinemachine3rdPersonFollow。
    /// </summary>
    public struct CameraExpression
    {
        public bool HasRequest;

        /// <summary>
        /// 为 true 时各字段表示相对于武器基础状态的 Δ 值，由 CameraExpressionApplicator 叠加解算。
        /// 武器 Behaviour 写入的帧级表达式始终为 false（绝对值）。
        /// </summary>
        public bool IsRelative;

        // ── Lens ──────────────────────────────────────────────────────
        public float TargetFov;

        // ── Cinemachine3rdPersonFollow ────────────────────────────────
        public UnityEngine.Vector3 Damping;
        public UnityEngine.Vector3 ShoulderOffset;
        public float VerticalArmLength;
        public float CameraSide;
        public float CameraDistance;
        public float CameraRadius;
        public float DampingIntoCollision;
        public float DampingFromCollision;
        public bool EnableAdaptiveShoulder;
        public float ShoulderClearanceMultiplier;
        public float ShoulderShrinkSmoothTime;
        public float ShoulderRecoverSmoothTime;
        public float ShoulderProbeRadius;
        public float MinShoulderScale;
        public float ShoulderScaleDeadZone;

        // ── 输入 ──────────────────────────────────────────────────────
        public float SensitivityScale;

        // ── Applicator 过渡手感 ───────────────────────────────────────
        public float FovSmoothTime;
        public float OffsetSmoothTime;

        public void Clear()
        {
            HasRequest        = false;
            TargetFov         = 0f;
            Damping           = UnityEngine.Vector3.zero;
            ShoulderOffset    = UnityEngine.Vector3.zero;
            VerticalArmLength = 0f;
            CameraSide        = 0f;
            CameraDistance    = 0f;
            CameraRadius      = 0f;
            DampingIntoCollision = 0f;
            DampingFromCollision = 0f;
            EnableAdaptiveShoulder = false;
            ShoulderClearanceMultiplier = 0f;
            ShoulderShrinkSmoothTime = 0f;
            ShoulderRecoverSmoothTime = 0f;
            ShoulderProbeRadius = 0f;
            MinShoulderScale = 0f;
            ShoulderScaleDeadZone = 0f;
            SensitivityScale  = 0f;
            FovSmoothTime     = 0f;
            OffsetSmoothTime  = 0f;
        }
    }
}
