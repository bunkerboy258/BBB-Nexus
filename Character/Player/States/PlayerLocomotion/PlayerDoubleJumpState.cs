using Animancer;
using Characters.Player.Animation;
using Characters.Player.Data;
using Core.StateMachine;
using UnityEngine;

namespace Characters.Player.States
{
    /// <summary>
    /// 玩家二段跳状态。
    /// 
    /// 职责：
    /// 1. 在空中执行二段跳，复用 PlayerJumpState 的动画选择和物理逻辑。
    /// 2. 在进入时标记 HasPerformedDoubleJumpInAir = true，防止重复二段跳。
    /// 3. 等待落地后转移到 LandState。
    /// 
    /// 与 PlayerJumpState 的区别：
    /// - JumpState：地面起跳，初速度由 CurrentLocomotionState 决定
    /// - DoubleJumpState：空中跳跃，需要在进入时标记"本次空中已执行二段跳"
    /// </summary>
    public class PlayerDoubleJumpState : PlayerBaseState
    {
        private MotionClipData _clipData;
        private bool _canCheckLand;
        private float _jumpForce;

        public PlayerDoubleJumpState(PlayerController player) : base(player) { }

        public override void Enter()
        {
            _canCheckLand = false;
            data.HasPerformedDoubleJumpInAir = true;

            SelectDoubleJumpAnimation();
            ChooseOptionsAndPlay(_clipData.Clip);
            PerformJumpPhysics();

            AnimFacade.SetOnEndCallback(() =>
            {
                if (player.CharController.isGrounded)
                {
                    player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerLandState>());
                }
            });
        }

        /// <summary>
        /// 根据运动状态和装备情况选择合适的二段跳动画和力度。
        /// 
        /// 逻辑：
        /// 1. 二段跳仅向上（Up 方向）
        /// 2. 按运动状态和装备情况选择 DoubleJumpUp 或基础跳跃配置
        /// 3. 未找到则 fallback 到标准跳跃动画
        /// </summary>
        private void SelectDoubleJumpAnimation()
        {
            bool isHandsEmpty = data.CurrentEquipment.Definition == null;
            //Debug.Log(data.CurrentEquipment.Definition);

            // 根据运动状态和装备获取基础配置
            MotionClipData baseClip = null;
            float baseForce = config.JumpAndLanding.JumpForce;

            switch (data.CurrentLocomotionState)
            {
                case LocomotionState.Idle:
                case LocomotionState.Walk:
                case LocomotionState.Jog:
                    baseClip = config.JumpAndLanding.DoubleJumpUp;
                    baseForce = config.JumpAndLanding.DoubleJumpForceUp;
                    break;

                case LocomotionState.Sprint:
                    if (isHandsEmpty)
                    {
                        baseClip = config.JumpAndLanding.DoubleJumpSprintRoll;
                        baseForce = config.JumpAndLanding.DoubleJumpEmptyHandSprintForceUp;
                    }
                    else
                    {
                        baseClip = config.JumpAndLanding.DoubleJumpUp;
                        baseForce = config.JumpAndLanding.DoubleJumpForceUp;
                    }
                    break;

                default:
                    Debug.Log(" DoubleJumpUp 配置缺失，使用 JumpAirAnim 作为二段跳动画");
                    baseClip = config.JumpAndLanding.JumpAirAnim;
                    baseForce = config.JumpAndLanding.DoubleJumpForceUp;
                    break;
            }
            _clipData = baseClip;
            _jumpForce = baseForce;
        }

        private void PerformJumpPhysics()
        {
            data.VerticalVelocity = _jumpForce;
            data.IsGrounded = false;
        }

        protected override void UpdateStateLogic()
        {
            // 防抖：起跳后至少过 0.2s 才开始检测落地
            if (!_canCheckLand && AnimFacade.CurrentTime > 0.2f)
            {
                _canCheckLand = true;
            }

            // 落地检测
            if (_canCheckLand && data.VerticalVelocity <= 0 && player.CharController.isGrounded)
            {
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerLandState>());
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
