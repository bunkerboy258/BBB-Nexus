namespace Items.Data
{
    /// <summary>
    /// [核心分类] 定义了物品的主要功能类型。
    /// 这决定了物品在代码中的基础行为和数据结构。
    /// </summary>
    public enum ItemCategory
    {
        // --- 可装备 ---
        Weapon,     // 武器 (可造成伤害，有攻击属性)
        Tool,       // 工具 (手电筒、撬棍，有特定交互功能)
        Armor,      // 护甲 (提供防御属性)

        // --- 纯数据/消耗品 ---
        Consumable, // 消耗品 (药水、食物，使用后触发效果)
        Material,   // 材料 (用于合成、升级)
        KeyItem,    // 关键道具 (钥匙、任务物品，通常不可丢弃)
        Currency,   // 货币 (金币、钻石)

        // --- 其他 ---
        Junk,       // 杂物 (可出售的垃圾)
        Quest       // 任务触发器 (信件等)
    }

    /// <summary>
    /// [表现分类] 定义了可装备物品的握持方式。
    /// 这主要影响角色的动画姿态和 IK 逻辑。
    /// </summary>
    public enum ItemHoldType
    {
        None,       // 不持有 (空手)
        OneHanded,  // 单手 (手枪, 匕首)
        TwoHanded,  // 双手 (步枪, 大剑)
        Heavy,      // 重型 (加特林, RPG，可能有特殊移动限制)
        Shoulder,   // 肩扛 (火箭筒)
        Utility     // 功能性 (手电筒, 地图, 望远镜)
    }

    /// <summary>
    /// (可选) [属性分类] 定义了武器的攻击类型。
    /// 用于计算伤害、触发特效等。
    /// </summary>
    public enum DamageType
    {
        Kinetic,    // 动能 (子弹)
        Explosive,  // 爆炸
        Fire,       // 火焰
        Frost,      // 冰霜
        Electric    // 电击
    }
}
