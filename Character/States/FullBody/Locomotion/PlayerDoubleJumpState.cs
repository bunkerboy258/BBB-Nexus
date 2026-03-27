using Animancer;
using UnityEngine;

namespace BBBNexus
{
    // 玩家二段跳状态 
    // 负责在空中执行二段跳 选择对应的二段跳动画 标记已使用二段跳 最后落地
    public class PlayerDoubleJumpState : PlayerBaseState
    {
        private MotionClipData _clipData;
        private bool _canCheckLand;
        private float _jumpForce;

        public PlayerDoubleJumpState(BBBCharacterController player) : base(player) { }

        // 进入状态 选择二段跳动画 施加跳跃力量 标记已使用二段跳
        public override void Enter()
        {
            _canCheckLand = false;
            data.HasPerformedDoubleJumpInAir = true;

            SelectDoubleJumpAnimation();
            ChooseOptionsAndPlay(_clipData.Clip);
            PerformJumpPhysics();

            // 消费跳跃输入 防止同帧重复触发
            player.InputPipeline.ConsumeJumpPressed();

            AnimFacade.SetOnEndCallback(() =>
            {
                if (player.CharController.isGrounded)
                {
                    player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerLandState>());
                }
            });
        }

        // 根据运动状态和装备选择对应的二段跳动画和力量
        private void SelectDoubleJumpAnimation()
        {
            bool isHandsEmpty = data.CurrentItem == null;

            // 根据当前运动状态和装备选择 
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
                    Debug.Log(" DoubleJumpUp 配置缺失 使用 JumpAirAnim 作为后备");
                    baseClip = config.JumpAndLanding.JumpAirAnim;
                    baseForce = config.JumpAndLanding.DoubleJumpForceUp;
                    break;
            }
            _clipData = baseClip;
            _jumpForce = baseForce;
        }

        // 施加二段跳力量 设置垂直速度和接地状态
        private void PerformJumpPhysics()
        {
            data.VerticalVelocity = _jumpForce;
            data.IsGrounded = false;
        }

        // 状态逻辑 检测落地时机
        protected override void UpdateStateLogic()
        {
            // 给动画启动后 0.2s 才开始检测落地
            if (!_canCheckLand && AnimFacade.CurrentTime > 0.2f)
            {
                _canCheckLand = true;
            }

            // 检测落地
            if (_canCheckLand && data.VerticalVelocity <= 0 && player.CharController.isGrounded)
            {
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerLandState>());
            }
        }

        // 物理更新 委托 MotionDriver 处理重力
        public override void PhysicsUpdate()
        {
            player.MotionDriver.UpdateMotion(null, 0f);
        }

        // 退出状态 清理回调和动画数据
        public override void Exit()
        {
            AnimFacade.ClearOnEndCallback();
            _clipData = null;
        }
    }
}
