using UnityEngine;

namespace BBBNexus
{
    // 上半身持有物品状态 
    // 只要黑板数据变了 立刻呼叫 Driver 换枪
    // 枪换好后 无脑把 Update 权限下放给武器实体
    public class UpperBodyHoldItemState : UpperBodyBaseState
    {
        private IHoldableItem _currentItem;
        private ItemInstance _cachedInstance;

        public UpperBodyHoldItemState(BBBCharacterController p) : base(p) { }

        // 进入状态 强制上半身动画层权重为1 执行一次强制同步
        public override void Enter()
        {
            // 持有武器时 强制上半身动画层权重为 1
            player.AnimFacade.SetLayerWeight(1, 1f, 0.25f);

            // 刚进入状态 执行一次强制同步
            SyncEquipmentFromBlackboard();
        }

        // 退出状态 让当前武器清理后事 停特效 解绑输入等
        public override void Exit()
        {
            _currentItem?.OnForceUnequip();
        }

        // 状态逻辑 检测黑板物品变化 否则交给武器自己更新
        protected override void UpdateStateLogic()
        {
            // 1. 检查黑板上的物品有没有被换
            if (_cachedInstance != player.RuntimeData.CurrentItem)
            {
                SyncEquipmentFromBlackboard();
                return;
            }

            // 2. 退出条件 如果发现黑板里没东西了 玩家收枪了 切回空手状态
            if (player.RuntimeData.CurrentItem == null)
            {
                player.UpperBodyCtrl.StateMachine.ChangeState(
                    player.UpperBodyCtrl.StateRegistry.GetState<UpperBodyEmptyState>()
                );
                return;
            }

            // 3. 正常运行 让物品自己干活
            _currentItem?.OnUpdateLogic();
        }

        // 核心调度方法 处理物品的装载与控制权移交
        private void SyncEquipmentFromBlackboard()
        {
            // 剥夺旧武器的控制权
            _currentItem?.OnForceUnequip();

            // 更新缓存
            _cachedInstance = player.RuntimeData.CurrentItem;

            if (_cachedInstance != null)
            {
                // 打印模型 注入实例数据
                player.EquipmentDriver.EquipItem(_cachedInstance);

                // 拿到刚造出来的物品的最高权限
                _currentItem = player.EquipmentDriver.CurrentItemDirector;

                // 正式激活物品控制 初始化物品逻辑
                _currentItem?.OnEquipEnter(player);
            }
            else
            {
                // 销毁模型
                player.EquipmentDriver.UnequipCurrentItem();
                _currentItem = null;
            }
        }
    }
}