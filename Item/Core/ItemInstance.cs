using Items.Data;
using System;

namespace Items.Core
{
    /// <summary>
    /// 物品运行时逻辑实例
    /// 存在于背包或玩家黑板中，是物品逻辑的中心。
    /// </summary>
    public class ItemInstance
    {
        /// <summary>
        /// 运行时的唯一实例 ID（用于区分两把相同的 AK47）
        /// </summary>
        public string InstanceID { get; private set; }

        /// <summary>
        /// 绑定的静态图纸 (只读)
        /// </summary>
        public ItemDefinitionSO BaseData { get; private set; }

        /// <summary>
        /// 当前的堆叠数量
        /// </summary>
        public int CurrentAmount { get; set; }

        /// <summary>
        /// 构造函数：基于静态图纸生成一个内存中的活体实例
        /// </summary>
        public ItemInstance(ItemDefinitionSO baseData, int amount = 1)
        {
            InstanceID = Guid.NewGuid().ToString(); // 赋予唯一的灵魂代码
            BaseData = baseData;
            CurrentAmount = amount;
        }

        // ==========================================
        // 核心：类型安全的强转快捷方法（供表现层提取特定数据用）
        // ==========================================
        public T GetSODataAs<T>() where T : ItemDefinitionSO
        {
            return BaseData as T;
        }
    }
}