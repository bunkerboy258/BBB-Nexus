// 文件路径: Characters/Player/Animation/AnimPlayOptions.cs
namespace Characters.Player.Animation
{
    /// <summary>
    /// 动画播放代理选项。
    /// 集中管理所有播放层的额外配置，保持业务数据的纯净。
    /// </summary>
    public struct AnimPlayOptions
    {
        /// <summary>
        /// 过渡时间。如果为 null，则使用动画/Transition自身在 Inspector 中配置的默认 Fade。
        /// </summary>
        public float? FadeDuration;

        /// <summary>
        /// 目标播放层级，默认为 0 (基础层)。
        /// </summary>
        public int Layer;

        /// <summary>
        /// 播放速度，默认为 1。
        /// </summary>
        public float Speed;

        /// <summary>
        /// 指定起始的归一化时间 (0~1)。如果为 null，则由系统自行决定。
        /// </summary>
        public float? NormalizedTime;

        /// <summary>
        /// 是否强制执行相位同步 (Foot Phase Sync) - 留作未来高级同步接口。
        /// </summary>
        public bool ForcePhaseSync;

        // --- 快捷默认值 ---
        public static AnimPlayOptions Default => new AnimPlayOptions
        {
            FadeDuration = null,
            Layer = 0,
            Speed = 1f,
            NormalizedTime = null,
            ForcePhaseSync = false
        };

    }
}
