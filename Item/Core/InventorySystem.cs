using System.Collections.Generic;
using Items.Data;
using UnityEngine;
using System.Linq; // 引用 Linq

namespace Items.Core
{
    /// <summary>
    /// 通用物品栏系统。
    /// 职责：
    /// 1. 管理一个物品列表 (List<InventoryItem>)。
    /// 2. 处理物品的添加、移除、查找、堆叠逻辑。
    /// 3. UI 无关，仅负责数据操作。
    /// </summary>
    public class InventorySystem
    {
        // 存储所有物品实例
        private List<InventoryItem> _items;

        // 背包容量
        private readonly int _capacity;

        // --- 事件 ---
        // 当背包内容发生任何变化时触发，供 UI 或其他系统订阅
        public event System.Action OnInventoryUpdated;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="capacity">背包的总格子数</param>
        public InventorySystem(int capacity)
        {
            _capacity = capacity;
            _items = new List<InventoryItem>(capacity);
        }

        // --- 核心 API ---

        /// <summary>
        /// 尝试向背包中添加一个或多个物品。
        /// </summary>
        /// <param name="definition">要添加的物品定义</param>
        /// <param name="amount">数量</param>
        /// <returns>返回 true 如果所有物品都被成功添加</returns>
        public bool AddItem(ItemDefinitionSO definition, int amount = 1)
        {
            if (definition == null || amount <= 0) return false;

            // 1. 尝试堆叠 (仅对可堆叠物品)
            if (definition.IsStackable)
            {
                // 查找背包中已有的、且未满堆叠的同类物品
                var existingItems = _items.Where(i => i.Definition == definition && i.Quantity < definition.MaxStackSize).ToList();
                foreach (var item in existingItems)
                {
                    int space = definition.MaxStackSize - item.Quantity;
                    int add = Mathf.Min(space, amount);

                    item.AddQuantity(add);
                    amount -= add;

                    if (amount <= 0)
                    {
                        NotifyUpdate();
                        return true;
                    }
                }
            }

            // 2. 放入新槽位
            while (amount > 0)
            {
                if (_items.Count >= _capacity)
                {
                    Debug.LogWarning("背包已满，部分物品未能添加！");
                    NotifyUpdate();
                    return false; // 背包满了
                }

                int addAmount = definition.IsStackable ? Mathf.Min(amount, definition.MaxStackSize) : 1;

                var newItem = new InventoryItem(definition, addAmount);
                _items.Add(newItem);

                amount -= addAmount;
            }

            NotifyUpdate();
            return true;
        }

        /// <summary>
        /// 从背包中移除指定数量的物品。
        /// </summary>
        public void RemoveItem(ItemDefinitionSO definition, int amount = 1)
        {
            // 从后往前遍历，因为我们会修改列表
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                if (_items[i].Definition == definition)
                {
                    int toRemove = Mathf.Min(amount, _items[i].Quantity);
                    _items[i].RemoveQuantity(toRemove);
                    amount -= toRemove;

                    // 如果这组物品被移除完了，就从列表里删除
                    if (_items[i].Quantity <= 0)
                    {
                        _items.RemoveAt(i);
                    }

                    if (amount <= 0) break;
                }
            }
            NotifyUpdate();
        }

        /// <summary>
        /// 检查背包中是否有指定物品。
        /// </summary>
        public bool HasItem(ItemDefinitionSO definition, int amount = 1)
        {
            int count = GetItemCount(definition);
            return count >= amount;
        }

        /// <summary>
        /// 获取指定物品在背包中的总数量。
        /// </summary>
        public int GetItemCount(ItemDefinitionSO definition)
        {
            return _items.Where(i => i.Definition == definition).Sum(i => i.Quantity);
        }

        /// <summary>
        /// 获取背包中所有物品的只读列表 (供 UI 显示)。
        /// </summary>
        public IReadOnlyList<InventoryItem> GetAllItems()
        {
            return _items;
        }

        /// <summary>
        /// 触发更新事件。
        /// </summary>
        private void NotifyUpdate()
        {
            OnInventoryUpdated?.Invoke();
        }
    }
}
