using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BBBNexus
{
    // 通用背包系统 管理物品的存储 堆叠 查询 
    // 支持自动堆叠 自动分割 单独操作等特性 
    public class InventorySystem
    {
        // 背包槽位数组
        private readonly ItemInstance[] _items;
        // 背包容量
        private readonly int _capacity;

        // 背包发生变化时的事件 UI 或其他系统可订阅以刷新显示 
        public event Action OnInventoryUpdated;

        // 注：GetAllItems 如果每次都 new List/用 LINQ ToList 会产生托管分配与迭代器分配
        // 这里提供一个复用缓冲区用于无分配查询
        private readonly List<ItemInstance> _allItemsCache = new List<ItemInstance>(32);

        // 初始化背包
        public InventorySystem(int capacity)
        {
            _capacity = capacity;
            // 创建指定大小的槽位数组
            _items = new ItemInstance[capacity];
        }

        // 尝试添加物品到背包 支持自动堆叠与分割 
        public bool TryAdd(ItemInstance instance)
        {
            // 验证输入
            if (instance == null || instance.CurrentAmount <= 0) return false;

            var definition = instance.BaseData;
            int remaining = instance.CurrentAmount;

            // 模拟堆叠 用于判断是否需要额外的空槽位 
            if (definition.MaxStack > 1)
            {
                // 遍历所有槽位 寻找相同定义的物品进行堆叠 
                for (int i = 0; i < _capacity && remaining > 0; i++)
                {
                    var existing = _items[i];
                    // 跳过空槽位或不同定义的物品 
                    if (existing == null || existing.BaseData != definition) continue;

                    // 计算该槽位还有多少空间 
                    int space = Mathf.Max(0, existing.BaseData.MaxStack - existing.CurrentAmount);
                    if (space <= 0) continue;

                    // 计算本次可堆叠的数量 
                    int add = Mathf.Min(space, remaining);
                    remaining -= add;
                }
            }

            // 如果全部可以通过堆叠解决 则直接提交 
            if (remaining <= 0)
            {
                // 提交 真正把数量堆入已有堆栈 
                int toStack = instance.CurrentAmount;
                for (int i = 0; i < _capacity && toStack > 0; i++)
                {
                    var existing = _items[i];
                    if (existing == null || existing.BaseData != definition) continue;

                    int space = Mathf.Max(0, existing.BaseData.MaxStack - existing.CurrentAmount);
                    if (space <= 0) continue;

                    int add = Mathf.Min(space, toStack);
                    existing.CurrentAmount += add;
                    toStack -= add;
                }

                // 原实例已全部消耗 
                instance.CurrentAmount = 0;
                NotifyUpdate();
                return true;
            }

            // 计算空槽数量 判断是否有足够槽位来放下剩余数量 
            int emptyCount = 0;
            for (int i = 0; i < _capacity; i++)
            {
                if (_items[i] == null) emptyCount++;
            }

            // 计算需要的槽位数 
            int requiredSlots;
            if (definition.MaxStack > 1)
            {
                // 向上取整 计算需要几个槽位 
                requiredSlots = (remaining + definition.MaxStack - 1) / definition.MaxStack;
            }
            else
            {
                // 无堆叠物品 每个占一个槽 
                requiredSlots = remaining;
            }

            // 检查是否有足够空位 
            if (emptyCount < requiredSlots)
            {
                Debug.LogWarning("[InventorySystem] 背包空位不足，无法添加整个物品实例。");
                // 不做任何修改就返回失败 
                return false;
            }

            // 提交 先堆叠到已有槽 再把剩余拆分到空槽 
            remaining = instance.CurrentAmount;

            // 堆叠提交 
            if (definition.MaxStack > 1)
            {
                for (int i = 0; i < _capacity && remaining > 0; i++)
                {
                    var existing = _items[i];
                    if (existing == null || existing.BaseData != definition) continue;

                    int space = Mathf.Max(0, existing.BaseData.MaxStack - existing.CurrentAmount);
                    if (space <= 0) continue;

                    int add = Mathf.Min(space, remaining);
                    existing.CurrentAmount += add;
                    remaining -= add;
                }
            }

            // 拆分到空槽 
            bool originalPlaced = false;
            for (int i = 0; i < _capacity && remaining > 0; i++)
            {
                // 跳过非空槽位 
                if (_items[i] != null) continue;

                // 计算本次放入多少 
                int put = definition.MaxStack > 1 ? Mathf.Min(remaining, definition.MaxStack) : 1;

                if (!originalPlaced)
                {
                    // 第一个空槽放入原始实例 保留其 InstanceID 
                    instance.CurrentAmount = put;
                    _items[i] = instance;
                    originalPlaced = true;
                }
                else
                {
                    // 后续空槽创建新实例 
                    _items[i] = new ItemInstance(definition, put);
                }

                remaining -= put;
            }

            // 验证 剩余应该为零 
            if (remaining != 0)
            {
                Debug.LogWarning("[InventorySystem] 意外：拆分后仍有剩余，背包状态可能不一致。");
            }

            NotifyUpdate();
            return true;
        }

        // 完全移除指定槽位的物品 
        public ItemInstance RemoveAt(int slotIndex)
        {
            // 范围检查 
            if (slotIndex < 0 || slotIndex >= _capacity) return null;
            var item = _items[slotIndex];
            _items[slotIndex] = null;
            if (item != null) NotifyUpdate();
            return item;
        }

        // 将物品实例放置到指定槽位 
        // 如果槽位已有物品 则替换并返回原物品 
        public ItemInstance SetAt(int slotIndex, ItemInstance instance)
        {
            if (slotIndex < 0 || slotIndex >= _capacity) return instance;
            var oldItem = _items[slotIndex];
            _items[slotIndex] = instance;
            NotifyUpdate();
            return oldItem;
        }

        // 获取指定槽位的物品 
        public ItemInstance GetAt(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _capacity) return null;
            return _items[slotIndex];
        }

        // 按定义移除指定数量的物品 从后向前遍历 
        public void Remove(ItemDefinitionSO definition, int amount = 1)
        {
            if (definition == null || amount <= 0) return;

            // 从后向前遍历 确保优先清空最后的槽位 
            for (int i = _capacity - 1; i >= 0 && amount > 0; i--)
            {
                var inst = _items[i];
                if (inst == null || inst.BaseData != definition) continue;

                // 计算本次应该移除多少 
                int toRemove = Mathf.Min(amount, inst.CurrentAmount);
                inst.CurrentAmount -= toRemove;
                amount -= toRemove;

                // 数量为零则清空槽位 
                if (inst.CurrentAmount <= 0)
                {
                    _items[i] = null;
                }
            }

            NotifyUpdate();
        }

        // 检查是否有足够的指定物品 
        public bool Has(ItemDefinitionSO definition, int amount = 1)
        {
            return GetCount(definition) >= amount;
        }

        // 获取指定定义的物品总数 跨所有槽位汇总 
        public int GetCount(ItemDefinitionSO definition)
        {
            if (definition == null) return 0;
            int sum = 0;
            for (int i = 0; i < _capacity; i++)
            {
                var inst = _items[i];
                if (inst != null && inst.BaseData == definition)
                    sum += inst.CurrentAmount;
            }
            return sum;
        }

        // 查找指定定义的第一个物品实例 
        public ItemInstance FindFirst(ItemDefinitionSO definition)
        {
            if (definition == null) return null;
            for (int i = 0; i < _capacity; i++)
            {
                var inst = _items[i];
                if (inst != null && inst.BaseData == definition)
                    return inst;
            }
            return null;
        }

        /// <summary>
        /// 获取所有非空槽位的物品列表
        /// 调用方传入一个 List 作为缓冲区
        /// </summary>
        public void GetAllItemsNonAlloc(List<ItemInstance> results)
        {
            if (results == null) return;
            results.Clear();
            for (int i = 0; i < _capacity; i++)
            {
                var inst = _items[i];
                if (inst != null) results.Add(inst);
            }
        }

        /// <summary>
        /// 获取所有非空槽位的物品列表
        /// 注：返回的是内部缓存 外部请只读使用 不要保存引用用于长期持有
        /// </summary>
        public IReadOnlyList<ItemInstance> GetAllItems()
        {
            // 注：旧实现_items.Where(...).ToList()会
            // 分配 LINQ 迭代器对象
            // predicate 如果捕获外部变量会分配闭包
            // ToList() 分配新 List 以及可能的内部数组扩容
            // 所以这里改为复用 _allItemsCache
            _allItemsCache.Clear();
            for (int i = 0; i < _capacity; i++)
            {
                var inst = _items[i];
                if (inst != null) _allItemsCache.Add(inst);
            }
            return _allItemsCache;
        }

        // 通知监听者背包已更新 触发 UI 刷新等后续动作 
        private void NotifyUpdate()
        {
            OnInventoryUpdated?.Invoke();
        }
    }
}
