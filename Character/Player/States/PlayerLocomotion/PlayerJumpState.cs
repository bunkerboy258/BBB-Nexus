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

            // 播放跳跃（从头）。AnimPlayOptions 暂不支持 FadeMode.FromStart，
            // 这里通过 NormalizedTime=0 强制从头开始。
            var options = AnimPlayOptions.Default;
            options.NormalizedTime = 0f;
            AnimFacade.PlayTransition(_clipData.Clip, options);

            AnimFacade.SetOnEndCallback(() =>
            {
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
                    _clipData = config.JumpAirAnimWalk ?? config.JumpAirAnim;
                    _jumpForce = config.JumpForceWalk;
                    data.LandFadeInTime = config.JumpToLandFadeInTime_WalkJog;
                    break;

                case LocomotionState.Sprint:
                    if (isHandsEmpty)
                    {
                        _clipData = config.JumpAirAnimSprintEmpty ?? config.JumpAirAnim;
                        _jumpForce = config.JumpForceSprintEmpty;
                        data.LandFadeInTime = config.JumpToLandFadeInTime_SprintEmpty;
                    }
                    else
                    {
                        _clipData = config.JumpAirAnimSprint ?? config.JumpAirAnim;
                        _jumpForce = config.JumpForceSprint;
                        data.LandFadeInTime = config.JumpToLandFadeInTime_Sprint;
                    }
                    break;

                default:
                    Debug.Log(" JumpAirAnim 配置缺失，使用默认跳跃动画");
                    _clipData = config.JumpAirAnim;
                    _jumpForce = config.JumpForce;
                    data.LandFadeInTime = 0.2f;
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
