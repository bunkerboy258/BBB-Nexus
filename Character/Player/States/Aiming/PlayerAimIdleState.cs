using Characters.Player.Data;
using Characters.Player.Animation;

namespace Characters.Player.States
{
    public class PlayerAimIdleState : PlayerBaseState
    {
        public PlayerAimIdleState(PlayerController player) : base(player) { }

        public override void Enter()
        {
            var options = AnimPlayOptions.Default;
            options.FadeDuration = 0.4f;
            options.NormalizedTime = 0f;
            AnimFacade.PlayTransition(config.LocomotionAnims.IdleAnim, options);
        }

        protected override void UpdateStateLogic()
        {
            if (!data.IsAiming)
            {
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerIdleState>());
                return;
            }

            if (data.WantsDoubleJump)
            {
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerDoubleJumpState>());
                return;
            }

            if (data.WantsToJump)
            {
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerJumpState>());
                return;
            }

            if (data.CurrentLocomotionState != LocomotionState.Idle)
            {
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerAimMoveState>());
            }
        }

        public override void PhysicsUpdate()
        {
            player.MotionDriver.UpdateMotion(null, 0f);
        }

        public override void Exit()
        {
        }
    }
}
