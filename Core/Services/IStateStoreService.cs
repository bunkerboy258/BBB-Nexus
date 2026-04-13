using System;

namespace BBBNexus
{
    /// <summary>
    /// 角色状态存储服务接口（业务状态唯一数据源）。
    /// 设计约束：
    /// 0. BBB 永远不访问 Store 的数据。
    /// 1. BBB 不关心 Store 内部有哪些 key，只消费显式回调信号。
    /// 2. BBB 持有该服务的意义仅有两点：
    ///    - 获取角色死亡/复活/状态变化等表现触发时机；
    ///    - 标识该角色绑定的私有状态库实例。
    /// 职责：
    /// 1. 持有 StateProfileSO 并构建 StateRuntimeSet；
    /// 2. 完成后端初始化（Pack/目录/数据载入）；
    /// 3. 提供按 Key 的统一数值读写入口；
    /// 4. 提供 BBB 刚需的显式回调注入入口（Dead/Revive/StateChanged）。
    /// </summary>
    public interface IStateStoreService
    {
        StateProfileSO Profile { get; }

        StateRuntimeSet RuntimeSet { get; }

        void SetProfile(StateProfileSO profile);

        /// <summary>
        /// 初始化服务，确保后端结构存在
        /// </summary>
        void Initialize();

        bool TryGet(string key, out double value);
        bool TrySet(string key, double value);
        bool TryAdd(string key, double delta);
        bool TryGetBound(string key, out StateRuntimeBound bound);
        void ResetAllToDefault();

        // BBB 动画刚需回调注入（由上层在初始化时绑定）
        void BindOnDead(Action onDead);
        void BindOnRevive(Action onRevive);
        void BindOnStateChanged(Action<string, double> onStateChanged);
    }
}
