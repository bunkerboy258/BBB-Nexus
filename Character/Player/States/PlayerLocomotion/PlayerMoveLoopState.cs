using UnityEngine;
using Animancer;
using Characters.Player.Data;

namespace Characters.Player.States
{
    /// <summary>
    /// 玩家的"持续移动"循环状态。
    /// 职责：
    /// 1. 使用单一的循环混合器（MoveLoopMixer），供所有运动状态共用。
    /// 2. 根据脚相位选择左脚/右脚的混合器版本。
    /// 3. 实时更新动画混合参数（方向X和速度Y）。
    /// 4. 动画内部根据 Y 值（0.35=Walk / 0.7=Jog / 0.98=Sprint）自动混合对应速度的动画。
    /// 5. 检测脚相位并维护循环相位。
    /// 
    /// 参数 Y 值映射（由 MovementParameterProcessor 提供）：
    /// - 0.35 → Walk（行走）
    /// - 0.7  → Jog（慢跑）
    /// - 0.98 → Sprint（冲刺）
    /// </summary>
    public class PlayerMoveLoopState : PlayerBaseState
    {
        // [修复] 类型必须是 MixerState<Vector2>，而不是 Transition
        private MixerState<Vector2> _currentMixerState;

        // --- 淡入支持 ---
        /// <summary>
        /// 是否从 MoveStartState 中途直接进入（运动状态改变导致）。
        /// 如果为 true，本次进入会使用淡入时间，之后重置为 false。
        /// </summary>
        private bool _shouldFadeInFromStart = false;

        /// <summary>
        /// 淡入时间（秒）。用于从 MoveStartState 直接切入时的平滑过渡。
        /// </summary>
        private const float FadeInTimeFromStart = 0.4f;

        public PlayerMoveLoopState(PlayerController player) : base(player) { }

        #region State Lifecycle

        public override void Enter()
        {
            Debug.Log($"进入 MoveLoop 状态");
            
            Debug.Log($"当前运动状态: {data.CurrentLocomotionState}, 预期脚相位: {data.ExpectedFootPhase}, 当前动画混合参数: ({data.CurrentAnimBlendX}, {data.CurrentAnimBlendY})");
            // 1. 根据脚相位选择混合器（左/右脚）
            var targetMixer = (data.ExpectedFootPhase == FootPhase.LeftFootDown)
                ? config.MoveLoopMixer_L
                : config.MoveLoopMixer_R;

            if (targetMixer == null)
            {
                Debug.LogWarning("移动循环状态的混合器资源未配置！");
                player.StateMachine.ChangeState(player.IdleState);
                return;
            }

            // 2. 获取或创建状态 (State)
            // [修复] 强转为 MixerState<Vector2>
            var state = player.Animancer.Layers[0].GetOrCreateState(targetMixer) as MixerState<Vector2>;
            _currentMixerState = state;

            if (state != null)
            {
                // 3. 重置时间
                state.Time = 0f;

                // 4. 设置初始参数（使用当前的动画混合参数）
                state.Parameter = new Vector2(data.CurrentAnimBlendX, data.CurrentAnimBlendY);

                // 5. 播放（根据是否从MoveStart中途进入来决定淡入时间）
                float fadeTime = _shouldFadeInFromStart ? FadeInTimeFromStart : 0f;
                player.Animancer.Layers[0].Play(state, fadeTime);
                
                // 重置标志位
                _shouldFadeInFromStart = false;
                
                if (fadeTime > 0f)
                {
                    Debug.Log($"从 MoveStart 中途进入，使用淡入时间 {fadeTime}s");
                }
            }
        }

        public override void LogicUpdate()
        {
            if (data.IsAiming)
            {
                player.StateMachine.ChangeState(player.AimMoveState);
                return;
            }
            else if (!HasMoveInput)
            {
                player.StateMachine.ChangeState(player.StopState);
                return;
            }
            else if (data.WantsToVault)
            {
                player.StateMachine.ChangeState(player.VaultState);
                return;
            }
            else if (data.WantsToJump)
            {
                player.StateMachine.ChangeState(player.JumpState);
                return;
            }

            // 2. 更新参数
            if (_currentMixerState != null)
            {
                // A. 设置混合参数 (Vector2: X=方向, Y=速度强度)
                // Y 值由 MovementParameterProcessor 根据 CurrentLocomotionState 提供
                // (0.35=Walk, 0.7=Jog, 0.98=Sprint)
                _currentMixerState.Parameter = new Vector2(data.CurrentAnimBlendX, data.CurrentAnimBlendY);

                // B. 更新脚相位
                UpdateFootPhase();
            }
        }

        public override void PhysicsUpdate()
        {
            // 根据当前运动状态计算速度，委托给 MotionDriver 处理移动
            player.MotionDriver.UpdateLocomotionFromInput();
        }

        public override void Exit()
        {
            _currentMixerState = null;
        }

        #endregion

        #region Helper Methods

        private void UpdateFootPhase()
        {
            if (_currentMixerState == null) return;

            float rawTime = _currentMixerState.NormalizedTime;
            float cycleProgress = rawTime - Mathf.Floor(rawTime);

            if (data.ExpectedFootPhase == FootPhase.RightFootDown)
            {
                cycleProgress = (cycleProgress + 0.5f) % 1.0f;
            }

            data.CurrentRunCycleTime = cycleProgress;
        }

        /// <summary>
        /// 标记下一次进入时应该使用淡入。
        /// 由 MoveStartState 在检测到运动状态变化时调用。
        /// </summary>
        public void MarkForFadeInTransition()
        {
            _shouldFadeInFromStart = true;
        }

        #endregion
    }
}
