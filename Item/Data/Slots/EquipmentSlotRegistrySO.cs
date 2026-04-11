using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 配置槽位启用标志
    /// </summary>
    [System.Flags]
    public enum ConfigSlotFlags
    {
        None = 0,
        Weapon1 = 1 << 0,   // weapon:1
        Weapon2 = 1 << 1,   // weapon:2
        Weapon3 = 1 << 2,   // weapon:3
        Weapon4 = 1 << 3,   // weapon:4
        Weapon5 = 1 << 4,   // weapon:5
        All = Weapon1 | Weapon2 | Weapon3 | Weapon4 | Weapon5
    }

    /// <summary>
    /// 实例槽位启用标志
    /// </summary>
    [System.Flags]
    public enum InstanceSlotFlags
    {
        None = 0,
        MainHand = 1 << 0,   // instance:mainhand
        OffHand = 1 << 1,    // instance:offhand
        Head = 1 << 2,       // instance:head
        Body = 1 << 3,       // instance:body
        All = MainHand | OffHand | Head | Body
    }

    /// <summary>
    /// 装备槽位注册表 SO
    /// 所有槽位 key 硬编码，SO 只控制启用哪些
    /// </summary>
    [CreateAssetMenu(fileName = "SlotRegistry_Player", menuName = "BBBNexus/Equipment/Slot Registry")]
    public class EquipmentSlotRegistrySO : ScriptableObject
    {
        [Header("配置槽位 - 快捷栏")]
        [Tooltip("启用的快捷栏槽位（数字键1-5）。最多5个，按顺序对应。")]
        public ConfigSlotFlags EnabledConfigSlots = ConfigSlotFlags.All;

        [Header("实例槽位 - 身体部位")]
        [Tooltip("启用的实例槽位。未启用的槽位无法装备物品。")]
        public InstanceSlotFlags EnabledInstanceSlots = InstanceSlotFlags.MainHand | InstanceSlotFlags.OffHand;

        [Header("可选配置")]
        [Tooltip("默认空槽位时装备的 SO（如空手）")]
        public EquippableItemSO DefaultEmptyItem;

        /// <summary>
        /// 获取启用的配置槽位 key 列表（按顺序）
        /// </summary>
        public string[] GetEnabledConfigSlotKeys()
        {
            var slots = new System.Collections.Generic.List<string>();
            if ((EnabledConfigSlots & ConfigSlotFlags.Weapon1) != 0) slots.Add("weapon:1");
            if ((EnabledConfigSlots & ConfigSlotFlags.Weapon2) != 0) slots.Add("weapon:2");
            if ((EnabledConfigSlots & ConfigSlotFlags.Weapon3) != 0) slots.Add("weapon:3");
            if ((EnabledConfigSlots & ConfigSlotFlags.Weapon4) != 0) slots.Add("weapon:4");
            if ((EnabledConfigSlots & ConfigSlotFlags.Weapon5) != 0) slots.Add("weapon:5");
            return slots.ToArray();
        }

        /// <summary>
        /// 获取启用的实例槽位 key 列表
        /// </summary>
        public string[] GetEnabledInstanceSlotKeys()
        {
            var slots = new System.Collections.Generic.List<string>();
            if ((EnabledInstanceSlots & InstanceSlotFlags.MainHand) != 0) slots.Add("instance:mainhand");
            if ((EnabledInstanceSlots & InstanceSlotFlags.OffHand) != 0) slots.Add("instance:offhand");
            if ((EnabledInstanceSlots & InstanceSlotFlags.Head) != 0) slots.Add("instance:head");
            if ((EnabledInstanceSlots & InstanceSlotFlags.Body) != 0) slots.Add("instance:body");
            return slots.ToArray();
        }

        /// <summary>
        /// 检查配置槽位是否启用
        /// </summary>
        public bool IsConfigSlotEnabled(string key)
        {
            return key switch
            {
                "weapon:1" => (EnabledConfigSlots & ConfigSlotFlags.Weapon1) != 0,
                "weapon:2" => (EnabledConfigSlots & ConfigSlotFlags.Weapon2) != 0,
                "weapon:3" => (EnabledConfigSlots & ConfigSlotFlags.Weapon3) != 0,
                "weapon:4" => (EnabledConfigSlots & ConfigSlotFlags.Weapon4) != 0,
                "weapon:5" => (EnabledConfigSlots & ConfigSlotFlags.Weapon5) != 0,
                _ => false
            };
        }

        /// <summary>
        /// 检查实例槽位是否启用
        /// </summary>
        public bool IsInstanceSlotEnabled(string key)
        {
            return key switch
            {
                "instance:mainhand" => (EnabledInstanceSlots & InstanceSlotFlags.MainHand) != 0,
                "instance:offhand" => (EnabledInstanceSlots & InstanceSlotFlags.OffHand) != 0,
                "instance:head" => (EnabledInstanceSlots & InstanceSlotFlags.Head) != 0,
                "instance:body" => (EnabledInstanceSlots & InstanceSlotFlags.Body) != 0,
                _ => false
            };
        }

        /// <summary>
        /// 获取槽位的显示名称（硬编码）
        /// </summary>
        public static string GetSlotDisplayName(string key)
        {
            return key switch
            {
                "weapon:1" => "快捷栏1",
                "weapon:2" => "快捷栏2",
                "weapon:3" => "快捷栏3",
                "weapon:4" => "快捷栏4",
                "weapon:5" => "快捷栏5",
                "instance:mainhand" => "主手",
                "instance:offhand" => "副手",
                "instance:head" => "头部",
                "instance:body" => "躯干",
                _ => key
            };
        }
    }
}
