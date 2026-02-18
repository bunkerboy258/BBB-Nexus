using Animancer;
using Characters.Player.Data;
using Core.StateMachine;
using UnityEngine;

namespace Characters.Player.States
{
    public class PlayerAimMoveState : PlayerBaseState
    {
        private MixerState<Vector2> _mixerState;

        public PlayerAimMoveState(PlayerController player) : base(player) { }

        public override void Enter()
        {
            var state = player.Animancer.Layers[0].Play(config.AimLocomotionMixer, 0.2f);
            _mixerState = state as MixerState<Vector2>;

            data.WantsLookAtIK = true;
        }

        protected override void UpdateStateLogic()
        {
            if (!data.IsAiming)
            {
                player.StateMachine.ChangeState(data.CurrentLocomotionState == LocomotionState.Idle ? (BaseState)player.MoveLoopState : player.IdleState);
                return;
            }

            if (data.WantsToJump)
            {
                player.StateMachine.ChangeState(player.JumpState);
                return;
            }

            if (data.CurrentLocomotionState==LocomotionState.Idle)
            {
                player.StateMachine.ChangeState(player.AimIdleState);
                return;
            }

            if (_mixerState != null)
            {
                _mixerState.Parameter = new Vector2(data.CurrentAnimBlendX, data.CurrentAnimBlendY);
            }
        }

        public override void PhysicsUpdate()
        {
            player.MotionDriver.UpdateMotion(null, 0f, player.RuntimeData.ViewYaw);
        }

        public override void Exit()
        {
            _mixerState = null;
        }
    }
}
