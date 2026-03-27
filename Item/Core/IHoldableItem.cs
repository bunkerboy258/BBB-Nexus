namespace BBBNexus
{
    /// <summary>
    /// 定义所有装备物品必须实现的生命周期与逻辑接口
    /// 武器、道具、任何能拿在手上的东西都必须实现这个接口
    /// </summary>
    public interface IHoldableItem
    {
        /// <summary>
        /// 当模型实例生成后 EquipmentDriver 立刻调用此方法注入实例数据
        /// </summary>
        void Initialize(ItemInstance instanceData);

        /// <summary>
        /// 状态机将权限转交给物品时被触发
        /// </summary>
        void OnEquipEnter(BBBCharacterController player);

        /// <summary>
        /// 物品的核心行为驱动
        /// </summary>
        void OnUpdateLogic();

        /// <summary>
        /// 状态机切换、角色死亡等事件时被强制调用
        /// </summary>
        void OnForceUnequip();
    }
}