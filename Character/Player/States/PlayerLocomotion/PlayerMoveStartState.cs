using Animancer;
using Characters.Player.Data;
using UnityEngine;

namespace Characters.Player.States
{
    /// <summary>
    /// 玩家的“起步”状态。
    /// 职责:
    /// 1. 根据玩家输入，选择一个合适的起步动画 (如前、后、左、右)。
    /// 2. 播放该动画，并委托 MotionDriver 根据烘焙数据驱动角色移动。
    /// 3. 动画播放结束后，切换到持续移动状态 (PlayerMoveLoopState)。
    /// </summary>
    public class PlayerMoveStartState : PlayerBaseState
    {
        // --- 状态内成员变量 ---
        private AnimancerState _state;
        private float _stateDuration;
        private float _startYaw;
        private MotionClipData _currentClipData;
        private bool isrunstrat;


        // --- 常量 ---
        private const float SectorAngle = 45f;
        private const float HalfSectorAngle = SectorAngle / 2f;

        /// <summary>
        /// 状态构造函数。
        /// </summary>
        public PlayerMoveStartState(PlayerController player) : base(player) { }

        #region State Lifecycle

        public override void Enter()
        {
            Debug.Log("进入 MoveStart 状态");
            _stateDuration = 0f;
            _startYaw = player.transform.eulerAngles.y;

            // 选择要播放的动画数据
            _currentClipData = SelectClip(CalculateLocalAngle(), data.WantToRun);
            isrunstrat = data.WantToRun;

            // 播放动画并设置结束回调
            _state = player.Animancer.Layers[0].Play(_currentClipData.Clip);
            if (_currentClipData.AutoCalculateExitTime)
            {
                _state.Speed=_currentClipData.PlaybackSpeed;
            }
            data.ExpectedFootPhase = _currentClipData.EndPhase;
            _state.Events(this).OnEnd = () => player.StateMachine.ChangeState(player.MoveLoopState);
        }

        public override void LogicUpdate()
        {
            if (data.IsAiming)
            {
                player.StateMachine.ChangeState(player.AimMoveState);
            }
            else if (!HasMoveInput)
            {
                player.StateMachine.ChangeState(player.IdleState);
            }
            else if (data.WantsToJump)
            {
                player.StateMachine.ChangeState(player.JumpState);
            }
            else if (data.IsRunning && !isrunstrat)
            {
                player.StateMachine.ChangeState(player.MoveLoopState);
            }
        }

        public override void PhysicsUpdate()
        {
            if (_state == null) return;

            // 1. 更新内部计时器
            _stateDuration += Time.deltaTime * _state.Speed;

            // 2. 委托：将所有复杂的物理计算交给 MotionDriver
            player.MotionDriver.UpdateMotion(
                _currentClipData,
                _stateDuration,
                _startYaw
            );
        }

        public override void Exit()
        {
            // 清理引用，防止内存泄漏和逻辑错误
            _state = null;
            _currentClipData = null;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// 根据输入角度和是否想跑，从配置中选择最合适的动画数据。
        /// </summary>
        /// <param name="angle">本地角度 (-180 to 180)</param>
        /// <param name="isRunning">玩家是否意图跑步</param>
        /// <returns>选中的动画数据</returns>
        private MotionClipData SelectClip(float angle, bool isRunning)
        {
            // 8-Way Directional Selection Logic
            if (angle > -HalfSectorAngle && angle <= HalfSectorAngle) // Fwd
                return isRunning ? config.RunStartFwd : config.WalkStartFwd;

            if (angle > HalfSectorAngle && angle <= HalfSectorAngle + SectorAngle) // Fwd-Right
                return isRunning ? config.RunStartFwdRight : config.WalkStartFwdRight;

            if (angle > HalfSectorAngle + SectorAngle && angle <= HalfSectorAngle + SectorAngle * 2) // Right
                return isRunning ? config.RunStartRight : config.WalkStartRight;

            if (angle > HalfSectorAngle + SectorAngle * 2 && angle <= 180f - HalfSectorAngle) // Back-Right
                return isRunning ? config.RunStartBackRight : config.WalkStartBackRight;

            // Back (covers +157.5 to 180 and -180 to -157.5)
            if (angle > 180f - HalfSectorAngle || angle <= -180f + HalfSectorAngle)
                return isRunning ? config.RunStartBack : config.WalkStartBack;

            if (angle > -180f + HalfSectorAngle && angle <= -HalfSectorAngle - SectorAngle * 2) // Back-Left
                return isRunning ? config.RunStartBackLeft : config.WalkStartBackLeft;

            if (angle > -HalfSectorAngle - SectorAngle * 2 && angle <= -HalfSectorAngle - SectorAngle) // Left
                return isRunning ? config.RunStartLeft : config.WalkStartLeft;

            if (angle > -HalfSectorAngle - SectorAngle && angle <= -HalfSectorAngle) // Fwd-Left
                return isRunning ? config.RunStartFwdLeft : config.WalkStartFwdLeft;

            // 兜底：如果所有判断都失败，默认返回向前
            return isRunning ? config.RunStartFwd : config.WalkStartFwd;
        }

        #endregion
    }
}
