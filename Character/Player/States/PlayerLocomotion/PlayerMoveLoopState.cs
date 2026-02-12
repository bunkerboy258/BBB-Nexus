using UnityEngine;
using Animancer;
using Characters.Player.Data;

namespace Characters.Player.States
{
    public class PlayerMoveLoopState : PlayerBaseState
    {
        // [修复] 类型必须是 MixerState<Vector2>，而不是 Transition
        private MixerState<Vector2> _currentMixerState;

        public PlayerMoveLoopState(PlayerController player) : base(player) { }

        #region State Lifecycle

        public override void Enter()
        {
            // 1. 获取配置 (Transition)
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

                // 4. 设置初始参数
                // [修复] 必须是 Vector2 (X:角度, Y:速度)
                // 注意：这里需要一个初始的 X 值，我们可以从 Data 里取，或者算一个
                float initialX = data.DesiredLocalMoveAngle; // 或者 data.CurrentAnimBlendX
                float initialY = data.IsRunning ? 1.0f : 0.7f;

                state.Parameter = new Vector2(initialX, initialY);

                // 更新缓存
                data.CurrentAnimBlendY = initialY;

                // 别忘了更新 X 轴的缓存
                data.CurrentAnimBlendX = initialX;

                // 5. 播放
                player.Animancer.Layers[0].Play(state, 0.25f);
            }
        }

        public override void LogicUpdate()
        {
            if(data.IsAiming)
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
            }

            // 2. 更新参数
            if (_currentMixerState != null)
            {
                // A. 设置混合参数 (Vector2)
                _currentMixerState.Parameter = new Vector2(data.CurrentAnimBlendX, data.CurrentAnimBlendY);

                // B. 更新相位
                UpdateFootPhase();
            }
        }

        public override void PhysicsUpdate()
        {
            // [修复] 传递速度时，加上空值检查
            player.MotionDriver.UpdateMotion(null, 0, 0f);
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

        #endregion
    }
}
