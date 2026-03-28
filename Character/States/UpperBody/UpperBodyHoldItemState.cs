using UnityEngine;

namespace BBBNexus
{
    // 上半身持有物品状态
    // 只要黑板数据变了 立刻呼叫 Driver 换装备
    // 装备换好后 无脑把 Update 权限下放给武器实体
    // 支持双持：主手和副手独立同步，每帧轮询所有装备的 OnUpdateLogic()
    public class UpperBodyHoldItemState : UpperBodyBaseState
    {
        private ItemInstance _cachedMainhandInstance;
        private ItemInstance _cachedOffhandInstance;

        public UpperBodyHoldItemState(BBBCharacterController p) : base(p) { }

        // 进入状态 强制上半身动画层权重为 1 执行一次强制同步
        public override void Enter()
        {
            SyncMainhandFromBlackboard();
            SyncOffhandFromBlackboard();
        }

        // 退出状态 让所有装备清理后事 停特效 解绑输入等
        public override void Exit()
        {
            player.EquipmentDriver.MainhandItemDirector?.OnForceUnequip();
            player.EquipmentDriver.OffhandItemDirector?.OnForceUnequip();
        }

        // 状态逻辑 检测黑板物品变化 否则交给武器自己更新
        protected override void UpdateStateLogic()
        {
            // 1. 检查主手装备变化
            if (_cachedMainhandInstance != player.RuntimeData.MainhandItem)
            {
                SyncMainhandFromBlackboard();
            }

            // 2. 检查副手装备变化
            if (_cachedOffhandInstance != player.RuntimeData.OffhandItem)
            {
                SyncOffhandFromBlackboard();
            }

            // 3. 退出条件：两个手都没东西
            if (player.RuntimeData.MainhandItem == null && player.RuntimeData.OffhandItem == null)
            {
                player.UpperBodyCtrl.StateMachine.ChangeState(
                    player.UpperBodyCtrl.StateRegistry.GetState<UpperBodyEmptyState>()
                );
                return;
            }

            // 4. 正常运行：轮询所有装备的 OnUpdateLogic()
            var allItems = player.EquipmentDriver.AllEquippedItems;
            for (int i = 0; i < allItems.Count; i++)
            {
                allItems[i]?.OnUpdateLogic();
            }
        }

        // 同步主手装备
        private void SyncMainhandFromBlackboard()
        {
            // 卸载旧主手武器
            var oldItem = player.EquipmentDriver.MainhandItemDirector;
            oldItem?.OnForceUnequip();

            // 更新缓存
            _cachedMainhandInstance = player.RuntimeData.MainhandItem;

            if (_cachedMainhandInstance != null)
            {
                // 装备新武器到主手
                player.EquipmentDriver.EquipItemToSlot(_cachedMainhandInstance, EquipmentSlot.MainHand);

                // 根据武器配置设置上半身层权重（优先使用主手武器配置）
                var itemData = player.EquipmentDriver.MainhandItemData;
                float targetWeight = itemData != null ? itemData.UpperBodyLayerWeight : 1f;
                player.AnimFacade.SetLayerWeight(1, targetWeight, 0.25f);
            }
            else
            {
                player.EquipmentDriver.UnequipMainhand();
            }
        }

        // 同步副手装备
        private void SyncOffhandFromBlackboard()
        {
            // 卸载旧副手武器
            var oldItem = player.EquipmentDriver.OffhandItemDirector;
            oldItem?.OnForceUnequip();

            // 更新缓存
            _cachedOffhandInstance = player.RuntimeData.OffhandItem;

            if (_cachedOffhandInstance != null)
            {
                // 装备新武器到副手
                player.EquipmentDriver.EquipItemToSlot(_cachedOffhandInstance, EquipmentSlot.OffHand);

                // 如果主手没有武器，使用副手武器配置设置上半身层权重
                if (player.RuntimeData.MainhandItem == null)
                {
                    var itemData = player.EquipmentDriver.OffhandItemData;
                    float targetWeight = itemData != null ? itemData.UpperBodyLayerWeight : 1f;
                    player.AnimFacade.SetLayerWeight(1, targetWeight, 0.25f);
                }
            }
            else
            {
                player.EquipmentDriver.UnequipOffhand();
            }
        }
    }
}
