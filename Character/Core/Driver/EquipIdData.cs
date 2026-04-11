using System;
using NekoGraph;

namespace BBBNexus
{
    /// <summary>
    /// .equipid VFS 文件节点的数据结构。
    /// 只保存一个可在 MetaLib 中解析到真实装备 SO 的全局 ID。
    /// 使用纯文本格式存储（单字符串，无需 CSV 解析）。
    /// </summary>
    [Serializable]
    [VFSContentKind(VFSContentKind.Csv)]
    public class EquipIdData
    {
        public string Id;
    }
}
