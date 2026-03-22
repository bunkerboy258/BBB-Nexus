using UnityEngine;

namespace BBBNexus
{
    // 玩家跳跃状态 
    // 负责播放跳跃动画 施加跳跃力量 检测落地时机切换到落地或下落状态 
    public class PlayerJumpState : PlayerBaseState
    {
        private MotionClipData _clipData;
        private bool _canCheckLand;
        private float _jumpForce;

        public PlayerJumpState(PlayerController player) : base(player) { }

        // 进入状态 选择跳跃动画 施加跳跃力量 注册结束回调
        public override void Enter()
        {
            _canCheckLand = false;

            // 写入音频意图（由 AudioController 统一消费）
            data.SfxQueue.Enqueue(PlayerSfxEvent.Jump);

            // 表情意图（仅状态机触发 / 最后一个覆盖）
            data.FacialEventRequest=PlayerFacialEvent.Jump;

            SelectJumpAnimation();

            ChooseOptionsAndPlay(_clipData.Clip);

            AnimFacade.SetOnEndCallback(() =>
            {
                // 回调触发时清理自己 防止动画没停止时反复触发
                AnimFacade.ClearOnEndCallback();

                if (player.CharController.isGrounded)
                {
                    player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerLandState>());
                }
                else player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerFallState>());
            });

            PerformJumpPhysics();

            // 消费跳跃输入 防止同帧重复触发
            player.InputPipeline.ConsumeJumpPressed();
        }

        // 根据当前运动状态和装备情况选择对应的跳跃动画和跳跃力量
        private void SelectJumpAnimation()
        {
            bool isHandsEmpty = data.CurrentItem == null;

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
                    Debug.Log(" JumpAirAnim 配置缺失 使用默认跳跃动画");
                    _clipData = config.JumpAndLanding.JumpAirAnim;
                    _jumpForce = config.JumpAndLanding.JumpForce;
                    break;
            }
        }

        // 施加跳跃力量 设置垂直速度和接地状态
        private void PerformJumpPhysics()
        {
            data.VerticalVelocity = _jumpForce;
            data.IsGrounded = false;
        }

        // 状态逻辑 检测二段跳和落地条件
        protected override void UpdateStateLogic()
        {
            if (data.WantsDoubleJump && !data.IsGrounded)
            {
                data.NextStatePlayOptions = data.CurrentLocomotionState == LocomotionState.Sprint
                    ? config.JumpAndLanding.DoubleJumpFadeInOptions
                    : config.JumpAndLanding.DoubleJumpSprintRollFadeInOptions;
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerDoubleJumpState>());
                return;
            }

            if (!_canCheckLand && AnimFacade.CurrentTime > 0.2f)
            {
                _canCheckLand = true;
            }

            if (_canCheckLand && data.VerticalVelocity <= 0 && player.CharController.isGrounded)
            {
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerLandState>());
            }
        }

        // 物理更新 委托 MotionDriver 处理重力等运动
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
