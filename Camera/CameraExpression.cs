namespace BBBNexus
{
    /// <summary>
    /// 帧级相机表现力请求。
    /// 由武器 Behaviour 在 OnUpdateLogic() 中写入 PlayerRuntimeData.CameraExpression，
    /// CameraExpressionApplicator 每帧读取并 SmoothDamp 到唯一的 VirtualCamera 上，
    /// ResetIntent() 末尾自动清零。
    ///
    /// 武器侧写法示例：
    ///   _player.RuntimeData.CameraExpression = new CameraExpression
    ///   {
    ///       HasRequest          = true,
    ///       TargetFov           = 40f,
    ///       FollowOffset        = new Vector3(0f, 1.6f, -4f),
    ///       TrackedObjectOffset = new Vector3(0.8f, 0f, 0f),  // 肩膀偏移
    ///       SensitivityScale    = 0.5f,
    ///   };
    /// </summary>
    public struct CameraExpression
    {
        /// <summary>本帧是否有武器写入请求；false 时其余字段忽略，Applicator 回归默认值。</summary>
        public bool HasRequest;

        /// <summary>目标 FOV（度）。≤ 0 表示不覆写，沿用默认值。</summary>
        public float TargetFov;

        /// <summary>相机跟随偏移（Transposer FollowOffset）。Zero 表示不覆写。</summary>
        public UnityEngine.Vector3 FollowOffset;

        /// <summary>画面中目标的偏移（Composer TrackedObjectOffset，可做肩膀视角）。Zero 表示不覆写。</summary>
        public UnityEngine.Vector3 TrackedObjectOffset;

        /// <summary>视角灵敏度缩放系数。≤ 0 表示不覆写，使用角色配置默认值。</summary>
        public float SensitivityScale;

        /// <summary>FOV 插值平滑时间（秒）。≤ 0 = 使用 CameraExpressionApplicator 组件默认值。</summary>
        public float FovSmoothTime;

        /// <summary>位移偏移插值平滑时间（秒）。≤ 0 = 使用 CameraExpressionApplicator 组件默认值。</summary>
        public float OffsetSmoothTime;

        public void Clear()
        {
            HasRequest          = false;
            TargetFov           = 0f;
            FollowOffset        = UnityEngine.Vector3.zero;
            TrackedObjectOffset = UnityEngine.Vector3.zero;
            SensitivityScale    = 0f;
            FovSmoothTime       = 0f;
            OffsetSmoothTime    = 0f;
        }
    }
}
