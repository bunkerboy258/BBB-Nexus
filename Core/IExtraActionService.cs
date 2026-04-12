namespace BBBNexus
{
    /// <summary>
    /// 每帧由 ExtraActionController 推送给外部服务的通用意图快照喵~
    /// BBBNexus 不知道 ExtraAction1~N 是什么，语义由外部实现决定。
    /// </summary>
    public readonly struct ExtraActionIntents
    {
        public readonly bool WantsExtraAction1;
        public readonly bool WantsExtraAction2;
        public readonly bool WantsExtraAction3;
        public readonly bool WantsExtraAction4;

        public ExtraActionIntents(bool a1, bool a2, bool a3, bool a4)
        {
            WantsExtraAction1 = a1;
            WantsExtraAction2 = a2;
            WantsExtraAction3 = a3;
            WantsExtraAction4 = a4;
        }
    }

    /// <summary>
    /// 极简契约：一个黑板副本属性 + 一个推送方法，无游戏语义。
    /// 外部实现自行决定 ExtraAction1~N 的含义、边沿检测、业务分发。
    /// </summary>
    public interface IExtraActionService
    {
        ExtraActionIntents CurrentIntents { get; }
        void PushIntents(in ExtraActionIntents intents);
    }

    /// <summary>全局服务定位器，由外部实现在 Awake 时注册喵~</summary>
    public static class ExtraActionServiceRegistry
    {
        public static IExtraActionService Current { get; set; }
    }
}
