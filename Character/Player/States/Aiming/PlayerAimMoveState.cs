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

            data.WantsLookAtIK= true;
        }

        public override void LogicUpdate()
        {
            // 1. 退出检测
            if (!data.IsAiming)
            {
                player.StateMachine.ChangeState(HasMoveInput ? (BaseState)player.MoveLoopState : player.IdleState);
                return;
            }
            else if (data.WantsToJump)
            {
                player.StateMachine.ChangeState(player.JumpState);
            }
            else if (!HasMoveInput)
            {
                player.StateMachine.ChangeState(player.AimIdleState);
                return;
            }

            // 2. 更新参数
            if (_mixerState != null)
            {
                _mixerState.Parameter = new Vector2(data.CurrentAnimBlendX, data.CurrentAnimBlendY);
            }
        }

        public override void PhysicsUpdate()
        {
            // Use new MotionDriver API: call UpdateMotion with null clip to drive input/aim motion
            player.MotionDriver.UpdateMotion(null, 0f, player.RuntimeData.ViewYaw);
        }

        public override void Exit()
        {
            _mixerState = null;
        }
    }
}
