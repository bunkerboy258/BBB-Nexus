using System;

namespace BBBNexus
{
    [Serializable]
    public sealed class StatusEffectState : PlayerBaseState
    {
        private bool _applied;

        public StatusEffectState(BBBCharacterController player) : base(player) { }

        public override void Enter()
        {
            _applied = false;
            var effect = data.StatusEffect.Effect;
            bool isHardInterrupt = effect != null && effect.InterruptMode == StatusInterruptMode.Hard;
            data.StatusControl.IsActive = data.StatusEffect.IsActive;
            data.StatusControl.Priority = effect != null ? effect.Priority : 0;
            data.StatusControl.InterruptMode = effect != null ? effect.InterruptMode : StatusInterruptMode.None;
            data.StatusControl.BlocksAction = isHardInterrupt && effect.BlockAction;
            data.StatusControl.BlocksLocomotion = isHardInterrupt;
            data.StatusControl.BlocksInput = isHardInterrupt && effect.BlockInput;
            data.StatusControl.UsesLegacyStatusState = true;
            Apply();
        }

        public override void Exit()
        {
            AnimFacade.ClearOverrideOnEndCallback();
            AnimFacade.StopFullBodyAction();
            data.StatusEffect.Clear();
            data.StatusControl.Clear();
        }

        protected override bool CheckInterrupts() => false;

        protected override void UpdateStateLogic()
        {
            if (!_applied)
                Apply();
        }

        public override void PhysicsUpdate()
        {
            player.MotionDriver.UpdateGravityOnly();
        }

        public void ForceReapply()
        {
            _applied = false;
            Apply();
        }

        private void Apply()
        {
            if (!data.StatusEffect.IsActive || data.StatusEffect.Effect == null)
                return;

            _applied = true;

            var transition = data.StatusEffect.Effect.SelectHitClip(data.StatusEffect.HitAngle);
            if (transition?.Clip == null)
                return;

            AnimFacade.PlayFullBodyActionTransition(transition);
            AnimFacade.SetOverrideOnEndCallback(OnClipEnd);
        }

        private void OnClipEnd()
        {
            AnimFacade.ClearOverrideOnEndCallback();

            if (!data.StatusEffect.IsActive)
                return;

            ReturnToPreviousState();
        }

        public void ReturnToPreviousState()
        {
            var returnState = data.StatusEffect.ReturnState;
            player.StateMachine.ChangeState(ResolveReturnState(returnState));
        }

        private PlayerBaseState ResolveReturnState(BaseState returnState)
        {
            if (returnState is OverrideState || returnState is StatusEffectState || returnState == null)
            {
                return data.CurrentLocomotionState != LocomotionState.Idle
                    ? player.StateRegistry.GetState<PlayerMoveLoopState>()
                    : player.StateRegistry.GetState<PlayerIdleState>();
            }

            if (returnState is PlayerMoveLoopState || returnState is PlayerIdleState)
            {
                return data.CurrentLocomotionState != LocomotionState.Idle
                    ? player.StateRegistry.GetState<PlayerMoveLoopState>()
                    : player.StateRegistry.GetState<PlayerIdleState>();
            }

            return returnState as PlayerBaseState ??
                   (data.CurrentLocomotionState != LocomotionState.Idle
                       ? player.StateRegistry.GetState<PlayerMoveLoopState>()
                       : player.StateRegistry.GetState<PlayerIdleState>());
        }
    }
}
