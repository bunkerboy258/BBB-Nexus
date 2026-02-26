using Characters.Player.Data;
using Characters.Player.Animation;
using Animancer;

namespace Characters.Player.States
{
    /// <summary>
    /// 玩家的"停止"状态。
    /// 职责：
    /// 1. 根据当前的运动状态（Walk/Jog/Sprint）和脚相位选择合适的急停动画。
    /// 2. 播放急停动画（包含减速/制动）。
    /// 3. 动画完成后切换到空闲状态（Idle）。
    /// 
    /// 映射关系：
    /// - Walk  → WalkStop (Left/Right)
    /// - Jog   → RunStop (Left/Right)   [RunStop 对应 Jog 的慢跑]
    /// - Sprint → SprintStop (Left/Right)
    /// </summary>
    public class PlayerStopState : PlayerBaseState
    {
        public PlayerStopState(PlayerController player) : base(player) { }

        public override void Enter()
        {
            // 根据运动状态和脚相位选择对应的急停动画
            ClipTransition stopClip = SelectStopClipForLocomotionState(data.LastLocomotionState, data.CurrentRunCycleTime);

            var options = AnimPlayOptions.Default;
            // 优先使用新的 PlayOptions 覆写
            if (data.NextStatePlayOptions.HasValue)
            {
                options = data.NextStatePlayOptions.Value;
                data.NextStatePlayOptions = null;
            }

            AnimFacade.PlayTransition(stopClip, options);
            //data.stopFadeInTime = 0f;

            // 动画完毕 -> 回到 Idle
            AnimFacade.SetOnEndCallback(() => player.StateMachine.ChangeState(player.IdleState));
        }

        protected override void UpdateStateLogic()
        {
            // 停止时检测输入 -> 重新开始移动
            if (data.CurrentLocomotionState != LocomotionState.Idle)
            {
                data.NextStatePlayOptions = new AnimPlayOptions { FadeDuration = 0.4f };
                player.StateMachine.ChangeState(player.MoveLoopState);
                return;
            }

            if (data.WantsToJump)
            {
                player.StateMachine.ChangeState(player.JumpState);
            }
        }

        public override void PhysicsUpdate()
        {
            // 停止状态下仍需更新重力和接地检测
            player.MotionDriver.UpdateMotion();
        }

        public override void Exit()
        {
            AnimFacade.ClearOnEndCallback();
        }

        #region Helper Methods

        /// <summary>
        /// 根据运动状态和脚相位选择对应的急停动画。
        /// </summary>
        /// <param name="locomotionState">当前运动状态（Walk/Jog/Sprint）</param>
        /// <param name="cycleTime">当前脚相位（0~1，0.5 为分界）</param>
        /// <returns>选中的急停动画</returns>
        private ClipTransition SelectStopClipForLocomotionState(LocomotionState locomotionState, float cycleTime)
        {
            // 判定脚相位：< 0.5 为左脚，>= 0.5 为右脚
            bool isLeftFoot = cycleTime < 0.5f;

            return locomotionState switch
            {
                // Walk：选择走路停止动画
                LocomotionState.Walk => isLeftFoot ? config.LocomotionAnims.WalkStopLeft : config.LocomotionAnims.WalkStopRight,

                // Jog：选择跑步停止动画（RunStop 对应 Jog 的慢跑）
                LocomotionState.Jog => isLeftFoot ? config.LocomotionAnims.RunStopLeft : config.LocomotionAnims.RunStopRight,

                // Sprint：选择冲刺停止动画
                LocomotionState.Sprint => isLeftFoot ? config.LocomotionAnims.SprintStopLeft : config.LocomotionAnims.SprintStopRight,

                // 默认：使用 RunStop（Jog）
                _ => isLeftFoot ? config.LocomotionAnims.RunStopLeft : config.LocomotionAnims.RunStopRight
            };
        }

        #endregion
    }
}

