namespace BBBNexus
{
    // 上半身空闲状态 
    // 当玩家没有装备任何物品时进入此状态 将上身权重降至0 
    public class UpperBodyEmptyState : UpperBodyBaseState
    {
        public UpperBodyEmptyState(BBBCharacterController player) : base(player) { }

        // 进入状态 关闭上半身动画层 权重淡出到0
        public override void Enter()
        {
            // 关闭上身动画层权重 防止空闲姿态的影响 Layer 1 是上半身
            // 0.25f 是平滑淡出时间 防止生硬跳变
            player.AnimFacade.SetLayerWeight(1, 0f, 0.25f);
        }

        // 退出状态 权重由下一个状态的 HoldItem 去接管
        public override void Exit()
        {
        }

        // 状态逻辑 检测是否装备了新物品
        protected override void UpdateStateLogic()
        {
            // 检测到装备物品就切换到持握状态
            if (data.CurrentItem != null)
            {
                player.UpperBodyCtrl.StateMachine.ChangeState(
                    player.UpperBodyCtrl.StateRegistry.GetState<UpperBodyHoldItemState>()
                );
            }
        }
    }
}