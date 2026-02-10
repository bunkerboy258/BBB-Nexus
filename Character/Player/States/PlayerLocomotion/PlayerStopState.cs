using Animancer;

namespace Characters.Player.States
{
    // 急停状态
    public class PlayerStopState : PlayerBaseState
    {
        public PlayerStopState(PlayerController player) : base(player) { }

        public override void Enter()
        {
            ClipTransition stopClip;

            // 根据 [是否跑步] 和 [左/右脚周期] 选择动画
            if (data.IsRunning)
                stopClip = (data.CurrentRunCycleTime < 0.5f) ? config.RunStopLeft : config.RunStopRight;
            else
                stopClip = (data.CurrentRunCycleTime < 0.5f) ? config.WalkStopLeft : config.WalkStopRight;

            var state = player.Animancer.Layers[0].Play(stopClip);

            // 播完 -> 回 Idle
            state.Events(this).OnEnd = () => player.StateMachine.ChangeState(player.IdleState);
        }

        public override void LogicUpdate()
        {
            // 急停时若有输入 -> 重新起步
            if (HasMoveInput) player.StateMachine.ChangeState(player.MoveStartState);
        }
        public override void PhysicsUpdate() { }
        public override void Exit() { }
    }
}

