namespace BBBNexus
{
    /// <summary>
    /// 状态改写服务接口（壳子）。
    /// </summary>
    public interface IStateModifyService
    {
        /// <summary>
        /// 初始化服务（如需可在实现中建立后端连接或缓存）。
        /// </summary>
        void Initialize();
    }
}
