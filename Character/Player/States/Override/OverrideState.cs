using System;
using UnityEngine;

namespace BBBNexus
{
    [Serializable]
    public sealed class OverrideState : PlayerBaseState
    {
        private bool _applied;

        public int CurrentPriority => data.Override.IsActive ? data.Override.Request.Priority : 0;

        public OverrideState(PlayerController player) : base(player) { }

        public override void Enter()
        {
            _applied = false;
            Apply();
        }

        public override void Exit()
        {
            AnimFacade.ClearOnEndCallback(0);
            AnimFacade.StopFullBodyAction();
            data.Override.Clear();
        }

        protected override bool CheckInterrupts() => false;

        protected override void UpdateStateLogic()
        {
            if (!_applied) Apply();
        }

        public override void PhysicsUpdate()
        {
            if (!data.Override.IsActive) return;

            if (data.Override.Request.ApplyGravity)
                player.MotionDriver.UpdateGravityOnly();
        }

        private void Apply()
        {
            if (!data.Override.IsActive) return;

            _applied = true;

            var req = data.Override.Request;

            AnimFacade.PlayFullBodyAction(req.Clip, req.FadeDuration);
            AnimFacade.SetOnEndCallback(OnClipEnd, 0);
        }

        private void OnClipEnd()
        {
            AnimFacade.ClearOnEndCallback(0);

            if (!data.Override.IsActive) return;

            if (data.Override.Request.ExitMode == OverrideExitMode.Keep)
                return;

            if (data.Override.ReturnState != null)
            {
                player.StateMachine.ChangeState(data.Override.ReturnState);
                return;
            }

            if (data.CurrentLocomotionState != LocomotionState.Idle)
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerMoveLoopState>());
            else
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerIdleState>());
        }
    }
}