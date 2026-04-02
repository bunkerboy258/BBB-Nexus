using System;

namespace BBBNexus
{
    /// <summary>
    /// .equipid VFS 文件节点的数据结构。
    /// 只保存一个可在 MetaLib 中解析到真实装备 SO 的全局 ID。
    /// </summary>
    [Serializable]
    public class EquipIdData
    {
        public string Id;
        public string InstanceId;
    }
}
