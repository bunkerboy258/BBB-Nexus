namespace BBBNexus
{
    // 可持有物品接口 定义所有装备物品必须实现的生命周期与逻辑接口 
    // 武器 道具 任何能拿在手上的东西都必须实现这个接口 
    public interface IHoldableItem
    {
        // 灵魂注入 当模型实例生成后 EquipmentDriver 立刻调用此方法注入实例数据 
        // 这一刻物品的逻辑系统获得了黑板中的真实数据 包括堆叠数量 属性修改等 
        void Initialize(ItemInstance instanceData);

        // 装备入场 状态机将权限转交给物品时被触发 
        // 这是拔枪 拿出等装备的启动时刻 物品应在此执行初始化表现与动画 
        void OnEquipEnter(PlayerController player);

        // 逻辑更新 每帧都被调用 物品应在此查询 InputReader 执行攻击 使用等逻辑 
        // 这是物品的核心行为驱动点 如果不实现此方法物品将无法响应输入 
        void OnUpdateLogic();

        // 强制卸载 状态机切换 角色死亡等事件时被强制调用 
        // 物品必须立即停止所有自身协程 清理 IK 调度 音效等 
        // 不能依赖 InputReader 的正常流程 务必完全清理以避免残留 Bug 
        void OnForceUnequip();
    }
}