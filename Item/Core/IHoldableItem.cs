using Characters.Player;

namespace Items.Core
{
    /// <summary>
    /// 负责被ItemInstance注入，并响应状态机的调度。
    /// </summary>
    public interface IHoldableItem
    {
        /// <summary>
        /// 在模型刚被实例化出来时，由 EquipmentDriver 立即调用，把实例数据喂给它。
        /// </summary>
        void Initialize(ItemInstance instanceData);

        /// <summary>
        /// 状态机进入时把权限下放给物品时调用的物品初始化逻辑
        /// </summary>
        void OnEquipEnter(PlayerController player);

        /// <summary>
        /// 状态机每帧调用：物品在这里轮询 InputReader 并执行具体逻辑
        /// </summary>
        void OnUpdateLogic();

        /// <summary>
        /// 状态机决定销毁物品/切换物品时强制调用：
        /// 物品必须在这里切断特效、协程，并解除任何在 InputReader 上的事件注册
        /// </summary>
        void OnForceUnequip();
    }
}