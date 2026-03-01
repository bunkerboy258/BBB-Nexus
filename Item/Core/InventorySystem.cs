using System;
using System.Collections.Generic;
using System.Linq;
using Items.Data;
using UnityEngine;

namespace Items.Core
{
    /// <summary>
    /// 通用物品栏系统（新 ItemInstance 体系）。
    /// - 内部存储 ItemInstance（或其子类）。
    /// - 堆叠依据 ItemDefinitionSO.MaxStack 与 ItemInstance.CurrentAmount。
    /// </summary>
    public class InventorySystem
    {
        private readonly List<ItemInstance> _items;
        private readonly int _capacity;

        public event Action OnInventoryUpdated;

        public InventorySystem(int capacity)
        {
            _capacity = capacity;
            _items = new List<ItemInstance>(capacity);
        }

        public bool TryAdd(ItemDefinitionSO definition, int amount = 1)
        {
            if (definition == null || amount <= 0) return false;

            // 1) 先堆叠
            if (definition.MaxStack > 1)
            {
                for (int i = 0; i < _items.Count && amount > 0; i++)
                {
                    var inst = _items[i];
                    if (inst == null) continue;
                    if (inst.BaseData != definition) continue;

                    int space = Mathf.Max(0, definition.MaxStack - inst.CurrentAmount);
                    if (space <= 0) continue;

                    int add = Mathf.Min(space, amount);
                    inst.CurrentAmount += add;
                    amount -= add;
                }

                if (amount <= 0)
                {
                    NotifyUpdate();
                    return true;
                }
            }

            // 2) 再创建新实例塞入空位
            while (amount > 0)
            {
                if (_items.Count >= _capacity)
                {
                    Debug.LogWarning("[InventorySystem] 背包已满，部分物品未能添加！");
                    NotifyUpdate();
                    return false;
                }

                int stackAmount = definition.MaxStack > 1 ? Mathf.Min(amount, definition.MaxStack) : 1;
                _items.Add(new ItemInstance(definition, stackAmount));
                amount -= stackAmount;
            }

            NotifyUpdate();
            return true;
        }

        public void Remove(ItemDefinitionSO definition, int amount = 1)
        {
            if (definition == null || amount <= 0) return;

            for (int i = _items.Count - 1; i >= 0 && amount > 0; i--)
            {
                var inst = _items[i];
                if (inst == null || inst.BaseData != definition) continue;

                int toRemove = Mathf.Min(amount, inst.CurrentAmount);
                inst.CurrentAmount -= toRemove;
                amount -= toRemove;

                if (inst.CurrentAmount <= 0)
                {
                    _items.RemoveAt(i);
                }
            }

            NotifyUpdate();
        }

        public bool Has(ItemDefinitionSO definition, int amount = 1)
        {
            return GetCount(definition) >= amount;
        }

        public int GetCount(ItemDefinitionSO definition)
        {
            if (definition == null) return 0;
            int sum = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                var inst = _items[i];
                if (inst != null && inst.BaseData == definition)
                    sum += inst.CurrentAmount;
            }
            return sum;
        }

        public ItemInstance FindFirst(ItemDefinitionSO definition)
        {
            if (definition == null) return null;
            for (int i = 0; i < _items.Count; i++)
            {
                var inst = _items[i];
                if (inst != null && inst.BaseData == definition)
                    return inst;
            }
            return null;
        }

        public IReadOnlyList<ItemInstance> GetAllItems()
        {
            return _items;
        }

        private void NotifyUpdate()
        {
            OnInventoryUpdated?.Invoke();
        }
    }
}
