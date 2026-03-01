using Characters.Player.Layers;

namespace Characters.Player.States.UpperBody
{
    public class UpperBodyUnavailableState : UpperBodyBaseState
    {
        public UpperBodyUnavailableState(PlayerController p) : base(p) { }

        public override void Enter()
        {
            // 通过适配器淡出 Layer 1 (上半身层)
            player.AnimFacade.SetLayerWeight(1, 0f, 0.2f);
        }

        // 重写打断检查：因为我们已经是打断后的结果了
        protected override bool CheckInterrupts()
        {
            // 唯一退出条件：所有打断因素都消失了
            // 比如翻越结束了 (IsVaulting = false)
            if (!data.IsVaulting)
            {
                controller.StateMachine.ChangeState(controller.StateRegistry.GetState<UpperBodyIdleState>());
                return true;
            }
            return false;
        }

        protected override void UpdateStateLogic() { } // 啥也不干

        public override void Exit()
        {
            // 通过适配器恢复 Layer 1 权重
            player.AnimFacade.SetLayerWeight(1, 1f, 0.2f);
        }
    }
}
