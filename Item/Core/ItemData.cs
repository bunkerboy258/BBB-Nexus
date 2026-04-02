using System;

namespace BBBNexus
{
    /// <summary>
    /// .item VFS 文件节点的数据结构。
    /// 只保存一个背包条目的最小引用，不直接内嵌大型运行时状态。
    /// </summary>
    [Serializable]
    public class ItemData
    {
        /// <summary>
        /// 指向 MetaLib / ItemDefinitionSO 的全局物品 ID。
        /// 抽象物体（钥匙、通行证）与实体物体统一走这一路径。
        /// </summary>
        public string Id;

        /// <summary>
        /// 当前堆叠数量。无堆叠物通常为 1。
        /// </summary>
        public int Count = 1;

        /// <summary>
        /// 可选的运行时实例 ID。
        /// 当该物品需要和外部状态文件（如 .ammo）或独立实例语义关联时使用。
        /// 纯抽象堆叠物可以留空。
        /// </summary>
        public string InstanceId;
    }
}
