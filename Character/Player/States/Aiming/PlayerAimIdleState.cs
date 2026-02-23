using Animancer;
using Characters.Player.Data;
using UnityEngine;

namespace Characters.Player.States
{
    public class PlayerAimIdleState : PlayerBaseState
    {
        public PlayerAimIdleState(PlayerController player) : base(player) { }

        public override void Enter()
        {
            player.Animancer.Layers[0].Play(config.IdleAnim, 0.4f, FadeMode.FromStart);
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

            if (data.CurrentLocomotionState!=LocomotionState.Idle)
            {
                player.StateMachine.ChangeState(player.AimMoveState);
            }
        }

        public override void PhysicsUpdate()
        {
            player.MotionDriver.UpdateMotion(null, 0f, player.RuntimeData.ViewYaw);
        }

        public override void Exit()
        {

        }
    }
}
