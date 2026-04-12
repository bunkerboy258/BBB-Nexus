using System;
using System.Collections.Generic;

namespace BBBNexus
{
    /// <summary>
    /// 装备服务接口 - 通用槽位管理器喵~
    /// 不负责实例化，只代理 IHub 数据库访问
    /// 不关心槽位是手、头、脚还是武器123
    /// </summary>
    public interface IEquipmentService
    {
        // ========== 核心操作 ==========

        /// <summary>
        /// 甲：获取【某栏目】的装备SO配置喵~
        /// 通常在准备实例化时调用
        /// </summary>
        /// <param name="slotKey">栏目key，如 "config:weapon1", "instance:mainhand"</param>
        /// <returns>EquippableItemSO ，null 表示空槽</returns>
        EquippableItemSO GetEquippedSO(string slotKey);

        /// <summary>
        /// 乙：销毁【某栏目】的装备SO喵~
        /// （清空槽位，不触发实例化销毁，只删数据库记录）
        /// </summary>
        bool TryRemoveEquipped(string slotKey);

        /// <summary>
        /// 丁：交换两个栏目的装备喵~
        /// </summary>
        bool TrySwapEquipSO(string slotKeyA, string slotKeyB);

        /// <summary>
        /// 丙：向【某栏目】设置一个装备SO喵~
        /// （数据库登记，不触发实例化）
        /// </summary>
        /// <param name="slotKey">栏目key</param>
        /// <param name="itemId">EquippableItemSO 的名字</param>
        bool TrySetEquipSO(string slotKey, EquippableItemSO equippableItemSO);

        // ========== 批量/查询 ==========

        /// <summary>
        /// 获取所有已装备的栏目喵~
        /// </summary>
        /// <returns>栏目key → EquippableItemSO 的映射</returns>
        Dictionary<string, EquippableItemSO> GetAllEquipped();

        /// <summary>
        /// 查询某栏目是否有装备喵~
        /// </summary>
        bool HasEquipped(string slotKey);

        /// <summary>
        /// 获取所有可用的栏目定义喵~
        /// （由胶水层反射注册表决定）
        /// </summary>
        IEnumerable<SlotDefinition> GetAllSlotDefinitions();

        // ========== 事件 ==========

        /// <summary>
        /// 装备数据库发生变化时触发喵~
        /// （参数是 slotKey）
        /// </summary>
        event Action<string> OnSlotChanged;

        // ========== 初始化 ==========

        /// <summary>
        /// 初始化装备服务，确保在任何访问前完成注册和准备喵~
        /// </summary>
        void Initialize();

        // ========== 配置槽位快捷操作（新增） ==========

        /// <summary>
        /// 将指定配置槽位的装备复制到实例槽位（主手）
        /// </summary>
        /// <param name="configSlotIndex">配置槽位索引 0-4 对应 weapon1-5</param>
        /// <returns>是否成功</returns>
        bool TryEquipFromConfig(int configSlotIndex);

        /// <summary>
        /// 获取配置槽位当前的装备ID
        /// </summary>
        /// <param name="configSlotIndex">配置槽位索引 0-4</param>
        /// <returns>ItemId 或 null</returns>
        string GetConfigSlotItemId(int configSlotIndex);

        /// <summary>
        /// 卸下当前主手装备（清空 instance:mainhand）
        /// </summary>
        /// <returns>是否成功</returns>
        bool TryUnequipMainHand();
    }

    /// <summary>
    /// 栏目定义喵~ 承载更多元数据
    /// </summary>
    public class SlotDefinition
    {
        /// <summary>栏目Key，如 "config:weapon1", "instance:mainhand"</summary>
        public string Key { get; set; }

        /// <summary>显示名称，如 "主手武器", "头部护甲"</summary>
        public string DisplayName { get; set; }

        /// <summary>栏目类别，如 "weapon", "armor", "accessory"</summary>
        public string Category { get; set; }

        /// <summary>可接受的装备类型</summary>
        public Type AcceptedType { get; set; }

        /// <summary>是否允许为空</summary>
        public bool AllowEmpty { get; set; } = true;

        /// <summary>排序优先级</summary>
        public int Priority { get; set; }

        /// <summary>图标路径或其他元数据</summary>
        public string IconPath { get; set; }
    }
}
