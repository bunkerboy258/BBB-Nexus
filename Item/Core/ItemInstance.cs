using System;

namespace BBBNexus
{
    // 物品运行时逻辑实例 存储在背包和手上 记录物品逻辑状态 
    public class ItemInstance
    {
        // 运行时的唯一实例ID
        public string InstanceID { get; private set; }

        // 绑定的离线配置 
        // 一旦赋值就不会改变 所有实例共享同一份配置 
        public ItemDefinitionSO BaseData { get; private set; }

        // 当前的堆叠数量 对于无堆叠物品永远是 1 对于消耗品会逐次递减 
        // 当数量归零时 背包系统会将该实例从槽位移除 
        public int CurrentAmount { get; set; }

        // 构造函数 接收静态配置与初始数量 生成一个内存中的独立实例 
        // 每次构造都会分配新的 InstanceID 即使配置相同 
        public ItemInstance(ItemDefinitionSO baseData, int amount = 1)
        {
            // 使用 GUID 保证全局唯一性 即使在不同场景或会话中也不会重复 
            InstanceID = Guid.NewGuid().ToString();
            // 保存配置引用 后续所有查询都通过这个引用获取数据 
            BaseData = baseData;
            // 初始化堆叠数量 
            CurrentAmount = amount;
        }

        // 类型转换接口 安全地将配置强转为特定子类 
        public T GetSODataAs<T>() where T : ItemDefinitionSO
        {
            // 强转失败会返回 null 上游需自行处理 
            return BaseData as T;
        }
    }
}