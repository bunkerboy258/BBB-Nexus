using System;

namespace BBBNexus
{
    /// <summary>
    /// 角色状态服务接口 - 最大属性的持久化读写
    /// 不与任何后端直接耦合，只代理 IHub 数据库访问
    /// 日后存档只需访问 IHub 即可
    /// </summary>
    public interface ICharStateService
    {
        /// <summary>
        /// 读取角色最大属性数据
        /// </summary>
        bool TryGetMaxCoreState(out MaxCoreStateData data);

        /// <summary>
        /// 写入角色最大属性数据
        /// </summary>
        void SetMaxCoreState(MaxCoreStateData data);

        /// <summary>
        /// 初始化服务，确保后端结构存在
        /// </summary>
        void Initialize();

        /// <summary>
        /// 状态数据变化时触发
        /// </summary>
        event Action OnStateChanged;
    }
}
