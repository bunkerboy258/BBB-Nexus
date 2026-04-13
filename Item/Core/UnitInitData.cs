using System;

namespace BBBNexus
{
    /// <summary>
    /// .unitinit VFS 文件节点的数据结构。
    /// 存储单位的初始化属性。
    /// </summary>
    [Serializable]
    public class UnitInitData
    {
        /// <summary>
        /// 最大生命值
        /// </summary>
        public float MaxHealth = 100f;

        /// <summary>
        /// 最大理智值
        /// </summary>
        public float MaxSanity = 100f;

        /// <summary>
        /// 最大体力值
        /// </summary>
        public float MaxStamina = 1000f;

        public UnitInitData()
        {
            MaxHealth = 100f;
            MaxSanity = 100f;
            MaxStamina = 1000f;
        }
    }
}
