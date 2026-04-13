namespace BBBNexus
{
    /// <summary>
    /// 需要 IHub 进行初始化的服务接口
    /// </summary>
    public interface IHubService
    {
        /// <summary>
        /// 使用指定的 IHub 初始化服务，创建必要的 Pack 和路径
        /// </summary>
        /// <param name="hub">已创建好的 IHub 实例</param>
        void Initialize(IHub hub);
    }
}
