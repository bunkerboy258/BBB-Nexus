namespace BBBNexus
{
    // 战术姿态待机状态。
    // 当前为兼容既有资源，仍复用普通 idle 作为下半身基座待机片段。
    public class PlayerTacticalIdleState : PlayerBaseState
    {
        public PlayerTacticalIdleState(BBBCharacterController player) : base(player) { }

        // 进入状态 播放空闲动画 使用较长的淡入时间确保平滑过渡
        public override void Enter()
        {
            var options = AnimPlayOptions.Default;
            options.FadeDuration = 0.4f;
            options.NormalizedTime = 0f;
            AnimFacade.PlayTransition(config.LocomotionAnims.IdleAnim, options);
        }

        // 状态逻辑 检测松开瞄准 跳跃 或移动输入
        protected override void UpdateStateLogic()
        {
            if (!data.IsTacticalStance)
            {
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerIdleState>());
                return;
            }

            if (data.WantsDoubleJump)
            {
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerDoubleJumpState>());
                return;
            }

            if (data.WantsToJump)
            {
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerJumpState>());
                return;
            }

            if (data.CurrentLocomotionState != LocomotionState.Idle)
            {
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerTacticalMoveState>());
            }
        }

        // 物理更新 在瞄准时仍需处理重力等基础运动
        public override void PhysicsUpdate()
        {
            player.MotionDriver.UpdateMotion(null, 0f);
        }

        // 退出状态 无额外清理逻辑
        public override void Exit()
        {
        }
    }
}
