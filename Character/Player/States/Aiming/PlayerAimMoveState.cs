using Characters.Player.Data;
using Characters.Player.Animation;
using Core.StateMachine;
using UnityEngine;

namespace Characters.Player.States
{
    public class PlayerAimMoveState : PlayerBaseState
    {
        public PlayerAimMoveState(PlayerController player) : base(player) { }

        public override void Enter()
        {
            var options = AnimPlayOptions.Default;
            options.FadeDuration = 0.2f;
            AnimFacade.PlayTransition(config.Aiming. AimLocomotionMixer, options);

            data.WantsLookAtIK = true;
        }

        protected override void UpdateStateLogic()
        {
            if (!data.IsAiming)
            {
                player.StateMachine.ChangeState(
                    data.CurrentLocomotionState == LocomotionState.Idle
                        ? (BaseState)player.StateRegistry.GetState<PlayerIdleState>()
                        : player.StateRegistry.GetState<PlayerMoveLoopState>());
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

            if (data.CurrentLocomotionState == LocomotionState.Idle)
            {
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerAimIdleState>());
                return;
            }

            AnimFacade.SetMixerParameter(new Vector2(data.CurrentAnimBlendX, data.CurrentAnimBlendY));
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
