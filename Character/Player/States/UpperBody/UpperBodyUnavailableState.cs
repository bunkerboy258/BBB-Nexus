using Core.StateMachine;

namespace Characters.Player.States
{
    public class UpperBodyUnavailableState : UpperBodyBaseState
    {
        public UpperBodyUnavailableState(PlayerController player) : base(player) { }

        public override void Enter()
        {
            player.AnimFacade.SetLayerWeight(1, 0f, 0.2f);
        }

        public override void Exit()
        {
            // 拉回上半身权重的逻辑在 HoldItem 里，不需要在这里写。
        }

        protected override void UpdateStateLogic()
        {
            // 为了安全 退出逻辑由唯一打断器接管
        }
    }
}