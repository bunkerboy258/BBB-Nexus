using Characters.Player.Layers;

namespace Characters.Player.States.UpperBody
{
    public class UpperBodyUnavailableState : UpperBodyBaseState
    {
        public UpperBodyUnavailableState(PlayerController p, UpperBodyController c) : base(p, c) { }

        public override void Enter()
        {
            // 不播动画，或者淡出 Layer 1
            layer.StartFade(0f, 0.2f);
        }

        // 重写打断检查：因为我们已经是打断后的结果了
        protected override bool CheckInterrupts()
        {
            // 唯一退出条件：所有打断因素都消失了
            // 比如翻越结束了 (IsVaulting = false)
            if (!data.IsVaulting)
            {
                controller.ChangeState(controller.IdleState);
                return true;
            }
            return false;
        }

        protected override void UpdateStateLogic() { } // 啥也不干

        public override void Exit()
        {
            // 恢复权重
            layer.StartFade(1f, 0.2f);
        }
    }
}
