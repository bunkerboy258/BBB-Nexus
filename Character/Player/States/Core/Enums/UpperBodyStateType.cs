namespace BBBNexus
{
    // 上半身状态类型枚举 
    // 定义了所有可用的上半身状态 独立于下半身的状态系统
    public enum UpperBodyStateType
    {
        EmptyHands,  // 空手状态 对应 UpperBodyEmptyState 层权重为0
        HoldItem,    // 持握物品状态 对应 UpperBodyHoldItemState 自动代理控制权
        Unavailable  // 不可用状态 对应 UpperBodyUnavailableState 被强制打断
    }
}