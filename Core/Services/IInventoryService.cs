using System;
using System.Collections.Generic;

namespace BBBNexus
{
    /// <summary>
    /// 库存系统接口 - 无序物品管理器喵~
    /// 不负责物品实例化，只代理 IHub 数据库访问
    /// 对外暴露 SO 接口，内部存储请自己实现。
    /// </summary>
    public interface IInventoryService
    {
        // ========== 核心操作 ==========

        /// <summary>
        /// 存入指定数量的物品喵~
        /// （自动堆叠，返回实际存入数量）
        /// </summary>
        /// <param name="itemSO">物品 SO</param>
        /// <param name="count">数量</param>
        /// <returns>实际存入的数量（可能部分存入）</returns>
        int TryAdd(ItemDefinitionSO itemSO, int count);

        /// <summary>
        /// 移除指定数量的物品喵~
        /// （从后向前遍历，优先移除后面的）
        /// </summary>
        /// <param name="itemSO">物品 SO</param>
        /// <param name="count">数量</param>
        /// <returns>是否成功移除全部数量</returns>
        bool TryRemove(ItemDefinitionSO itemSO, int count);

        /// <summary>
        /// 强制设置某物品的总数量喵~
        /// （会调整槽位，删除或创建）
        /// </summary>
        /// <param name="itemSO">物品 SO</param>
        /// <param name="count">目标数量</param>
        void SetCount(ItemDefinitionSO itemSO, int count);

        // ========== 查询 ==========

        /// <summary>
        /// 获取某物品的总数量喵~
        /// </summary>
        int GetCount(ItemDefinitionSO itemSO);

        /// <summary>
        /// 检查是否有足够数量的物品喵~
        /// </summary>
        bool Has(ItemDefinitionSO itemSO, int count = 1);

        /// <summary>
        /// 获取所有非空物品及其数量喵~
        /// </summary>
        /// <returns>ItemId → Count 的映射</returns>
        Dictionary<string, int> GetAllItems();

        /// <summary>
        /// 获取所有非空物品及其 SO 和数量喵~
        /// 由实现层（胶水层）负责 ID → SO 的解析
        /// </summary>
        /// <returns>(ItemSO, Count) 的列表</returns>
        IEnumerable<(ItemDefinitionSO ItemSO, int Count)> GetAllItemsWithSO();

        /// <summary>
        /// 清空整个库存喵~
        /// </summary>
        void Clear();

        // ========== 容量限制 ==========

        /// <summary>
        /// 最大槽位数喵~
        /// </summary>
        int MaxSlots { get; }

        /// <summary>
        /// 已使用的槽位数喵~
        /// </summary>
        int UsedSlots { get; }

        /// <summary>
        /// 剩余可用槽位数喵~
        /// </summary>
        int AvailableSlots { get; }

        // ========== 事件 ==========

        /// <summary>
        /// 库存发生变化时触发喵~
        /// （参数是 ItemId）
        /// </summary>
        event Action<string> OnItemChanged;

        /// <summary>
        /// 库存整体刷新时触发喵~
        /// </summary>
        event Action OnInventoryUpdated;

        // ========== 初始化 ==========

        /// <summary>
        /// 初始化库存系统，确保后端结构存在喵~
        /// </summary>
        void Initialize();

        // ========== 批量/事务操作（新增） ==========

        /// <summary>
        /// 检查是否有足够空间添加指定物品喵~
        /// </summary>
        bool CanAdd(ItemDefinitionSO itemSO, int count);

        /// <summary>
        /// 批量添加物品（事务性：全部成功或全部失败）喵~
        /// </summary>
        bool TryAddBatch(IEnumerable<(ItemDefinitionSO Item, int Count)> entries, out int totalAdded);

        /// <summary>
        /// 批量移除物品（事务性：全部成功或全部失败）喵~
        /// </summary>
        bool TryRemoveBatch(IEnumerable<(ItemDefinitionSO Item, int Count)> entries);
    }
}
