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
            // 使用统一的新选项字段
            var fadeOpt = config.LocomotionAnims.FadeInJumpOptions;
            if (fadeOpt.FadeDuration.HasValue) options.FadeDuration = fadeOpt.FadeDuration;

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
                    _clipData = config.JumpAndLanding.JumpAirAnimWalk ?? config.JumpAndLanding.JumpAirAnim;
                    _jumpForce = config.JumpAndLanding.JumpForceWalk;
                    // 写入统一 NextStatePlayOptions
                    data.NextStatePlayOptions = config.JumpAndLanding.JumpToLandFadeInTime_WalkJogOptions;
                    break;

                case LocomotionState.Sprint:
                    if (isHandsEmpty)
                    {
                        _clipData = config.JumpAndLanding.JumpAirAnimSprintEmpty ?? config.JumpAndLanding.JumpAirAnim;
                        _jumpForce = config.JumpAndLanding.JumpForceSprintEmpty;
                        data.NextStatePlayOptions = config.JumpAndLanding.JumpToLandFadeInTime_SprintEmptyOptions;
                    }
                    else
                    {
                        _clipData = config.JumpAndLanding.JumpAirAnimSprint ?? config.JumpAndLanding.JumpAirAnim;
                        _jumpForce = config.JumpAndLanding.JumpForceSprint;
                        data.NextStatePlayOptions = config.JumpAndLanding.JumpToLandFadeInTime_SprintOptions;
                    }
                    break;

                default:
                    Debug.Log(" JumpAirAnim 配置缺失，使用默认跳跃动画");
                    _clipData = config.JumpAndLanding.JumpAirAnim;
                    _jumpForce = config.JumpAndLanding.JumpForce;
                    data.NextStatePlayOptions = AnimPlayOptions.Default;
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
