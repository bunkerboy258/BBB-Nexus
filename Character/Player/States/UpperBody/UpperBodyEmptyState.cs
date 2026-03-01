using Core.StateMachine;

namespace Characters.Player.States
{
    public class UpperBodyEmptyState : UpperBodyBaseState
    {
        public UpperBodyEmptyState(PlayerController player) : base(player) { }

        public override void Enter()
        {
            // 玩家手里没东西，淡出上半身动画层 (Layer 1)
            // 0.25f 是平滑过渡时间，防止瞬间抽搐
            player.AnimFacade.SetLayerWeight(1, 0f, 0.25f);
        }

        public override void Exit()
        {
            // 退出时不做任何事，权重的恢复交给 HoldItem 去做
        }

        protected override void UpdateStateLogic()
        {
            // 如果检测到手里突然有了“导演（武器）”，立刻切入持有状态
            if (player.EquipmentDriver.CurrentItemDirector != null)
            {
                player.UpperBodyCtrl.StateMachine.ChangeState(
                    player.UpperBodyCtrl.StateRegistry.GetState<UpperBodyHoldItemState>()
                );
            }
        }
    }
}