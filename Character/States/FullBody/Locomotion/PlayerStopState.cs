using Animancer;

namespace BBBNexus
{
    // 玩家停止状态 
    // 负责根据当前运动状态和脚相位选择急停动画 播放减速制动动画 最后回到空闲状态 
    public class PlayerStopState : PlayerBaseState
    {
        public PlayerStopState(BBBCharacterController player) : base(player) { }

        // 进入状态 选择对应的急停动画并注册结束回调
        public override void Enter()
        {
            // 根据运动状态和脚相位选择对应的急停动画
            ClipTransition stopClip = SelectStopClipForLocomotionState(data.LastLocomotionState, data.ExpectedFootPhase);

            ChooseOptionsAndPlay(stopClip);

            // 动画完毕 回到 Idle
            AnimFacade.SetOnEndCallback(() =>
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerIdleState>()));
        }

        // 状态逻辑 检测再次输入移动或跳跃
        protected override void UpdateStateLogic()
        {
            // 停止时检测输入 重新开始移动
            if (data.CurrentLocomotionState != LocomotionState.Idle)
            {
                data.NextStatePlayOptions = new AnimPlayOptions { FadeDuration = 0.4f };
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerMoveLoopState>());
                return;
            }

        }

        // 物理更新 停止状态下仍需更新重力和接地检测
        public override void PhysicsUpdate()
        {
            player.MotionDriver.UpdateMotion();
        }

        // 退出状态 清理回调
        public override void Exit()
        {
            AnimFacade.ClearOnEndCallback();
        }

        // 根据运动状态和脚相位选择对应的急停动画
        private ClipTransition SelectStopClipForLocomotionState(LocomotionState locomotionState, FootPhase footPhase)
        {
            // 判定脚相位 小于 0.5 为左脚 大于等于 0.5 为右脚
            bool isLeftFoot = footPhase == FootPhase.LeftFootDown;

            return locomotionState switch
            {
                // Walk 选择走路停止动画
                LocomotionState.Walk => isLeftFoot ? config.LocomotionAnims.WalkStopLeft : config.LocomotionAnims.WalkStopRight,

                // Jog 选择跑步停止动画 RunStop 对应 Jog 的慢跑
                LocomotionState.Jog => isLeftFoot ? config.LocomotionAnims.RunStopLeft : config.LocomotionAnims.RunStopRight,

                // Sprint 选择冲刺停止动画
                LocomotionState.Sprint => isLeftFoot ? config.LocomotionAnims.SprintStopLeft : config.LocomotionAnims.SprintStopRight,

                // 默认 使用 RunStop Jog
                _ => isLeftFoot ? config.LocomotionAnims.RunStopLeft : config.LocomotionAnims.RunStopRight
            };
        }
    }
}

