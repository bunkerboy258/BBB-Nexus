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
            AnimFacade.PlayTransition(config.IdleAnim, options);
        }

        protected override void UpdateStateLogic()
        {
            if (!data.IsAiming)
            {
                player.StateMachine.ChangeState(player.IdleState);
                return;
            }

            if (data.WantsDoubleJump)
            {
                player.StateMachine.ChangeState(player.DoubleJumpState);
                return;
            }

            if (data.WantsToJump)
            {
                player.StateMachine.ChangeState(player.JumpState);
                return;
            }

            if (data.CurrentLocomotionState != LocomotionState.Idle)
            {
                player.StateMachine.ChangeState(player.AimMoveState);
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
