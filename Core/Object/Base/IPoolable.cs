namespace BBBNexus
{
    /// <summary>
    /// 对象池支持接口：被池化对象用此接口复位内部状态
    /// 注意：对象可能包含多个组件实现该接口（含子物体） 池会全部调用
    /// </summary>
    public interface IPoolable
    {
        void OnSpawned();
        void OnDespawned();
    }
}
