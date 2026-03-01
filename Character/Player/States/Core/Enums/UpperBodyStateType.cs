namespace Characters.Player.Data
{
    /// <summary>
    /// 玩家上半身可用状态枚举
    /// </summary>
    public enum UpperBodyStateType
    {
        EmptyHands,  // 对应 UpperBodyEmptyState (空手/待机，层权重为 0)
        HoldItem,    // 对应 UpperBodyHoldItemState (持有物品，自动代理状态)
        Unavailable  // 对应 UpperBodyUnavailableState (不可用/被强制打断)
    }
}