namespace BBBNexus
{
    /// <summary>
    /// Hub 抽象接口 = KV 数据库 + Ticker 工厂 + Tick 驱动
    /// BBBNexus 框架层定义，具体实现由外部提供
    /// </summary>
    public interface IHub
    {
        // ========== KV 操作（泛型版本） ==========

        /// <summary>
        /// 尝试获取值。泛型版本，类型安全。
        /// </summary>
        bool TryGet<T>(string key, out T value);

        /// <summary>
        /// 尝试设置值。泛型版本，无装箱开销。
        /// </summary>
        bool TrySet<T>(string key, T value);

        // ========== KV 操作（object 版本） ==========

        /// <summary>
        /// 尝试获取值。object 版本，动态场景用。
        /// </summary>
        bool TryGet(string key, out object value);

        /// <summary>
        /// 尝试设置值。object 版本，灵活但会装箱。
        /// </summary>
        bool TrySet(string key, object value);

        // ========== 删除 ==========

        /// <summary>
        /// 尝试删除值。无需泛型，只操作 key。
        /// </summary>
        bool TryRemove(string key);

        // ========== 原子交换（Swap） ==========

        /// <summary>
        /// 原子性交换两个 key 的值。
        /// 返回是否成功（任一 key 不存在也返回 false）。
        /// </summary>
        bool TrySwap(string keyA, string keyB);

        /// <summary>
        /// 带类型检查的 Swap，只有 T 类型一致时才交换。
        /// </summary>
        bool TrySwap<T>(string keyA, string keyB);

        // ========== 载荷操作（砖块方法） ==========

        /// <summary>
        /// 清空指定 key 的载荷（保留节点，清空内容）
        /// </summary>
        bool TryClearPayload(string key);

        /// <summary>
        /// 设置载荷（泛型版本，自动验证类型和序列化）
        /// 根据节点扩展名验证 payload 类型是否匹配
        /// </summary>
        bool TrySetPayload<T>(string key, T payload) where T : class;

        /// <summary>
        /// 交换两个 key 的载荷（保持节点路径不变，只交换内容）
        /// 要求：同 ContentSource，Reference 类型还需同 ContentKind
        /// </summary>
        bool TrySwapPayload(string keyA, string keyB);

        // ========== Ticker 工厂 ==========

        /// <summary>
        /// 让 IHub 创建一个 Ticker，返回唯一句柄（号码牌）。
        /// Ticker 是什么？IHub 内部决定！外部只拿到号码牌！
        /// </summary>
        /// <param name="type">Ticker 类型标识，由具体 IHub 实现定义</param>
        /// <returns>句柄（号码牌），用于 RemoveTicker</returns>
        object AddTicker(string type);

        /// <summary>
        /// 通过句柄移除 Ticker。
        /// </summary>
        bool RemoveTicker(object handle);

        // ========== Tick 驱动 ==========

        /// <summary>
        /// 每帧驱动入口。
        /// IHub 内部驱动所有它创建的 Ticker，怎么调？那是 IHub 的私事！
        /// </summary>
        void Tick();
    }
}
