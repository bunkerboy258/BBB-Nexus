namespace BBBNexus
{
    // 玩家瞄准空闲状态 
    // 在瞄准模式下保持站立 对应FreeLook的IdleState 
    public class PlayerAimIdleState : PlayerBaseState
    {
        public PlayerAimIdleState(BBBCharacterController player) : base(player) { }

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
            if (!data.IsAiming)
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
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerAimMoveState>());
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
