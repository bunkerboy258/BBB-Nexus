namespace BBBNexus
{
    /// <summary>
    /// 弹药服务 - 替代 AmmoPackVfs
    /// 备用弹药（背包子弹）通过 IInventoryService 管理
    /// 弹夹状态纯内存管理，不持久化
    /// </summary>
    public static class AmmoService
    {
        /// <summary>
        /// 查询备用弹药数量
        /// </summary>
        public static int GetReserveAmmo(IInventoryService inventoryService, AmmoItemSO ammoItem)
        {
            if (inventoryService == null || ammoItem == null) return 0;
            return inventoryService.GetCount(ammoItem);
        }

        /// <summary>
        /// 消耗备用弹药
        /// </summary>
        public static bool TryConsumeReserveAmmo(IInventoryService inventoryService, AmmoItemSO ammoItem, int amount)
        {
            if (amount <= 0) return true;
            if (inventoryService == null || ammoItem == null) return false;
            return inventoryService.TryRemove(ammoItem, amount);
        }
    }
}
