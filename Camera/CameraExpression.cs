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

        // ── Lens ──────────────────────────────────────────────────────
        public float TargetFov;

        // ── Cinemachine3rdPersonFollow ────────────────────────────────
        public UnityEngine.Vector3 Damping;
        public UnityEngine.Vector3 ShoulderOffset;
        public float VerticalArmLength;
        public float CameraSide;
        public float CameraDistance;
        public float CameraRadius;

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
            SensitivityScale  = 0f;
            FovSmoothTime     = 0f;
            OffsetSmoothTime  = 0f;
        }
    }
}
