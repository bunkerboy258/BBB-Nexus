namespace BBBNexus
{
    /// <summary>
    /// 角色自身 SFX 事件表（硬编码稳定符号）。
    /// 
    /// 原则：
    /// - 只放“角色自身”事件（脚步/跳跃/落地/翻滚/受击等）
    /// - 不放具体武器事件（武器系统可自有音频链路）
    /// - 内容映射由角色的 AudioSO 决定
    /// </summary>
    public enum PlayerSfxEvent
    {
        None = 0,

        Footstep,
        Jump,
        Land,

        Roll,
        Dodge,

        Hurt,
        Death,

        // 可按需扩展：喘气/落水/攀爬等
        Breath,
    }
}
