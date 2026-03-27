namespace BBBNexus
{
    // 上半身不可用状态 
    // 当玩家无法操纵上半身时进入 如掉落 受击等情况 强制解除当前装备
    public class UpperBodyUnavailableState : UpperBodyBaseState
    {
        public UpperBodyUnavailableState(BBBCharacterController player) : base(player) { }

        // 进入状态 关闭上半身层权重 强制解除装备
        public override void Enter()
        {
            player.AnimFacade.SetLayerWeight(1, 0f, 0.2f);

            // 强制卸下当前装备 确保玩家回复时能正确保存装备选择
            // 注意 不要重置 RuntimeData.CurrentItem
            // RuntimeData.CurrentItem 才是真正的装备装备图 装备选择
            // Fall Unavailable 只应该在上半身暂时无法控制时清掉 不影响装备槽位的选择
            if (player != null && player.EquipmentDriver != null)
            {
                player.EquipmentDriver.UnequipCurrentItem();
            }
        }

        // 退出状态 上半身权限由下一个状态 HoldItem 重新获得
        public override void Exit()
        {
        }

        // 状态逻辑 为了安全 此退出逻辑是唯一不能中断的条件
        protected override void UpdateStateLogic()
        {
        }
    }
}