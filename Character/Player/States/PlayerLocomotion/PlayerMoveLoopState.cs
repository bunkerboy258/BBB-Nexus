using UnityEngine;
using Animancer;
using Characters.Player.Data;
using Characters.Player.Animation;

namespace Characters.Player.States
{
    /// <summary>
    /// 玩家的"持续移动"循环状态。
    /// 
    /// 职责：
    /// 1. 根据运动状态（Walk/Jog/Sprint）和脚相位选择对应的离散循环动画。
    /// 2. 直接播放选中的动画（不使用 1D 混合器，避免浮点参数问题）。
    /// 3. 每帧计算脚步循环相位。
    /// 4. 检测运动状态变化，切换到对应动画。
    /// 5. 检测退出条件（无输入、瞄准、跳跃等）。
    /// 
    /// 关键点：
    /// - 使用离散动画代替 1D 混合器，避免浮点参数抖动
    /// - 当前仅实现前进方向（DesiredLocalMoveAngle ~-45 to 45）
    /// - 脚相位由 AnimancerState.NormalizedTime 计算
    /// - 未来可扩展到8个方向或更多
    /// </summary>
    public class PlayerMoveLoopState : PlayerBaseState
    {
        private LocomotionState _currentLocomotionState;

        private const float LocomotionChangeFadeTime = 0.3f;

        public PlayerMoveLoopState(PlayerController player) : base(player) { }

        #region State Lifecycle

        public override void Enter()
        {
            _currentLocomotionState = data.CurrentLocomotionState;

            // 1. 根据运动状态和脚相位选择动画
            var targetClip = SelectLoopAnimationForState(data.CurrentLocomotionState, data.ExpectedFootPhase);

            var options = AnimPlayOptions.Default;
            // 优先使用新的 PlayOptions 覆写
            if (data.NextStatePlayOptions.HasValue)
            {
                options = data.NextStatePlayOptions.Value;
                options.NormalizedTime = 0.14f;
                data.NextStatePlayOptions = null;
            }

            AnimFacade.PlayTransition(targetClip, options);
        }

        protected override void UpdateStateLogic()
        {
            // 使用权威的离散状态而非浮点检查
            if (data.CurrentLocomotionState == LocomotionState.Idle)
            {
                data.NextStatePlayOptions = config.LocomotionAnims.FadeInStopWalkOptions;
                switch (data.LastLocomotionState)
                {
                    case LocomotionState.Walk:
                        data.NextStatePlayOptions = config.LocomotionAnims.FadeInStopWalkOptions;
                        break;
                    case LocomotionState.Jog:
                        data.NextStatePlayOptions = config.LocomotionAnims.FadeInStopRunOptions;
                        break;
                    case LocomotionState.Sprint:
                        data.NextStatePlayOptions = config.LocomotionAnims.FadeInStopSprintOptions;
                        break;
                    default:
                        data.NextStatePlayOptions = AnimPlayOptions.Default;
                        break;
                }
                player.StateMachine.ChangeState(player.StopState);
                return;
            }

            if (data.WantsToVault)
            {
                data.NextStatePlayOptions = config.LocomotionAnims.FadeInVaultOptions;
                player.StateMachine.ChangeState(player.VaultState);
                return;
            }

            if (data.WantsToJump)
            {
                player.StateMachine.ChangeState(player.JumpState);
                return;
            }

            // 运动状态变化 → 切换动画
            if (data.CurrentLocomotionState != _currentLocomotionState)
            {
                SwitchLoopAnimation(data.CurrentLocomotionState);
            }

            // 每帧更新脚相位
            UpdateFootPhase();
        }

        public override void PhysicsUpdate()
        {
            player.MotionDriver.UpdateLocomotionFromInput();
        }

        public override void Exit()
        {
            // Loop 是持续状态，通常不绑 OnEnd。这里清一次避免残留。
            AnimFacade.ClearOnEndCallback();
        }

        #endregion

        #region Private Methods

        /// <summary>根据运动状态和脚相位，选择对应的循环动画。</summary>
        private ClipTransition SelectLoopAnimationForState(LocomotionState locomotionState, FootPhase footPhase)
        {
            bool isLeft = footPhase == FootPhase.LeftFootDown;

            return locomotionState switch
            {
                LocomotionState.Walk => isLeft ? config.LocomotionAnims. WalkLoopFwd_L : config.LocomotionAnims.WalkLoopFwd_R,
                LocomotionState.Jog => isLeft ? config.LocomotionAnims.JogLoopFwd_L : config.LocomotionAnims.JogLoopFwd_R,
                LocomotionState.Sprint => isLeft ? config.LocomotionAnims.SprintLoopFwd_L : config.LocomotionAnims.SprintLoopFwd_R,
                _ => isLeft ? config.LocomotionAnims.JogLoopFwd_L : config.LocomotionAnims.JogLoopFwd_R, // 默认 Jog
            };
        }

        /// <summary>切换到新的运动状态循环动画。</summary>
        private void SwitchLoopAnimation(LocomotionState newState)
        {
            float fromNormalizedTime = AnimFacade.CurrentNormalizedTime;

            _currentLocomotionState = newState;

            var targetClip = SelectLoopAnimationForState(newState, data.ExpectedFootPhase);
            if (targetClip == null)
            {
                Debug.LogWarning($"[MoveLoopState.SwitchLoopAnimation] 运动状态 {newState} 的循环动画未配置");
                return;
            }

            var options = AnimPlayOptions.Default;
            options.FadeDuration = LocomotionChangeFadeTime;
            options.NormalizedTime = fromNormalizedTime;

            AnimFacade.PlayTransition(targetClip, options);
        }

        /// <summary>根据当前播放动画的时间，计算脚步循环相位（0~1）。</summary>
        private void UpdateFootPhase()
        {
            float normalizedTime = AnimFacade.CurrentNormalizedTime;
            float cycleTime = normalizedTime - Mathf.Floor(normalizedTime);

            // 如果期望右脚着地，偏移 0.5
            if (data.ExpectedFootPhase == FootPhase.RightFootDown)
            {
                cycleTime = (cycleTime + 0.5f) % 1.0f;
            }

            data.CurrentRunCycleTime = cycleTime;
        }

        #endregion
    }
}
