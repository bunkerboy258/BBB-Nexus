using UnityEngine;
using Characters.Player.Data;
using Characters.Player.Animation;

namespace Characters.Player.States
{
    public class PlayerJumpState : PlayerBaseState
    {
        private MotionClipData _clipData;
        private bool _canCheckLand;
        private float _jumpForce;

        public PlayerJumpState(PlayerController player) : base(player) { }

        public override void Enter()
        {
            _canCheckLand = false;

            SelectJumpAnimation();

            ChooseOptionsAndPlay(_clipData.Clip);

            AnimFacade.SetOnEndCallback(() =>
            {
                // Ensure the end callback is cleared when invoked so it doesn't keep firing
                // every frame if the animation hasn't been stopped by playing another one.
                AnimFacade.ClearOnEndCallback();

                if (player.CharController.isGrounded)
                {
                    player.StateMachine.ChangeState(player.LandState);
                }
            });

            PerformJumpPhysics();
        }

        private void SelectJumpAnimation()
        {
            bool isHandsEmpty = data.CurrentEquipment.Definition == null;

            switch (data.CurrentLocomotionState)
            {
                case LocomotionState.Idle:
                case LocomotionState.Walk:
                case LocomotionState.Jog:
                    _clipData = config.JumpAndLanding.JumpAirAnimWalk ?? config.JumpAndLanding.JumpAirAnim;
                    _jumpForce = config.JumpAndLanding.JumpForceWalk;
                    break;

                case LocomotionState.Sprint:
                    if (isHandsEmpty)
                    {
                        _clipData = config.JumpAndLanding.JumpAirAnimSprintEmpty ?? config.JumpAndLanding.JumpAirAnim;
                        _jumpForce = config.JumpAndLanding.JumpForceSprintEmpty;
                    }
                    else
                    {
                        _clipData = config.JumpAndLanding.JumpAirAnimSprint ?? config.JumpAndLanding.JumpAirAnim;
                        _jumpForce = config.JumpAndLanding.JumpForceSprint;
                    }
                    break;

                default:
                    Debug.Log(" JumpAirAnim 配置缺失，使用默认跳跃动画");
                    _clipData = config.JumpAndLanding.JumpAirAnim;
                    _jumpForce = config.JumpAndLanding.JumpForce;
                    break;
            }
        }

        private void PerformJumpPhysics()
        {
            data.VerticalVelocity = _jumpForce;
            data.IsGrounded = false;
        }

        protected override void UpdateStateLogic()
        {
            if (data.WantsDoubleJump && !data.IsGrounded)
            {
                data.NextStatePlayOptions = data.CurrentLocomotionState == LocomotionState.Sprint
                    ? config.JumpAndLanding.DoubleJumpFadeInOptions
                    : config.JumpAndLanding.DoubleJumpSprintRollFadeInOptions;
                player.StateMachine.ChangeState(player.DoubleJumpState);
                return;
            }

            if (!_canCheckLand && AnimFacade.CurrentTime > 0.2f)
            {
                _canCheckLand = true;
            }

            if (_canCheckLand && data.VerticalVelocity <= 0 && player.CharController.isGrounded)
            {
                player.StateMachine.ChangeState(player.LandState);
            }
        }

        public override void PhysicsUpdate()
        {
            player.MotionDriver.UpdateMotion(null, 0f);
        }

        public override void Exit()
        {
            AnimFacade.ClearOnEndCallback();
            _clipData = null;
        }
    }
}
