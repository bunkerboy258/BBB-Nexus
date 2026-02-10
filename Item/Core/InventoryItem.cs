using Items.Data;
using System;
using UnityEngine;

namespace Items.Core
{
    /// <summary>
    /// 运行时在物品栏中存储的物品实例。
    /// 包含对静态定义的引用，以及实例的动态数据（如数量、唯一ID）。
    /// </summary>
    [Serializable] // 标记为可序列化，方便保存游戏
    public class InventoryItem
    {
        // --- 核心数据 ---

        /// <summary>
        /// 指向物品的静态定义 (ScriptableObject)。
        /// </summary>
        public ItemDefinitionSO Definition { get; private set; }

        /// <summary>
        /// 当前的堆叠数量。
        /// </summary>
        public int Quantity { get; private set; }

        /// <summary>
        /// 实例的唯一ID，用于区分两把不同的“AK47”。
        /// </summary>
        public string InstanceID { get; private set; }

        // --- 构造函数 ---
        public InventoryItem(ItemDefinitionSO definition, int quantity = 1)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition), "物品定义不能为空！");
            }

            Definition = definition;
            Quantity = Mathf.Max(quantity, 1); // 数量至少为1

            // 只有非堆叠物品才需要唯一ID来区分
            if (!Definition.IsStackable)
            {
                InstanceID = Guid.NewGuid().ToString();
            }
            else
            {
                InstanceID = null; // 可堆叠物品共享ID
            }
        }

        // --- 属性与方法 ---

        /// <summary>
        /// 检查这个物品实例是否有效。
        /// </summary>
        public bool IsValid => Definition != null && Quantity > 0;

        /// <summary>
        /// 增加物品数量。
        /// </summary>
        public void AddQuantity(int amount)
        {
            Quantity += amount;
        }

        /// <summary>
        /// 移除物品数量。
        /// </summary>
        public void RemoveQuantity(int amount)
        {
            Quantity -= amount;
        }
    }
}

