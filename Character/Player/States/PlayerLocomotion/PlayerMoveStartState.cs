using Animancer;
using Characters.Player.Data;
using UnityEngine;

namespace Characters.Player.States
{
    /// <summary>
    /// 玩家的"起步"状态。
    /// 职责:
    /// 1. 根据玩家的运动状态（Walk/Jog/Sprint），选择一个合适的起步动画（8个方向）。
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
        private LocomotionState _startLocomotionState; // 记录进入时的运动状态

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
            //Debug.Log($"进入 MoveStart 状态 (LocomotionState: {data.CurrentLocomotionState})");
            _stateDuration = 0f;
            _startYaw = player.transform.eulerAngles.y;
            _startLocomotionState = data.CurrentLocomotionState;

            // 根据当前运动状态和移动方向选择起步动画
            _currentClipData = SelectClipForLocomotionState(data.DesiredLocalMoveAngle, data.CurrentLocomotionState);

            // 播放动画并设置结束回调
            _state = player.Animancer.Layers[0].Play(_currentClipData.Clip,data.moveStartFadeInTime);
            data.moveStartFadeInTime = 0f;

            // 精简：不再有 ExitTime/截断点逻辑，直接使用烘焙倍速。
            _state.Speed = _currentClipData.PlaybackSpeed;

            // 末相位仍然用于 Loop/Stop 的左右脚选择
            data.ExpectedFootPhase = _currentClipData.EndPhase;

            _state.Events(this).OnEnd = () => player.StateMachine.ChangeState(player.MoveLoopState);

        }

        protected override void UpdateStateLogic()
        {
            if (data.IsAiming)
            {
                player.StateMachine.ChangeState(player.AimMoveState);
            }
            else if (data.CurrentLocomotionState == LocomotionState.Idle)
            {
                player.StateMachine.ChangeState(player.IdleState);
            }
            else if (data.WantsToJump)
            {
                player.StateMachine.ChangeState(player.JumpState);
            }
            // 如果运动状态在起步中途改变，切到循环状态让其处理状态转换
            else if (data.CurrentLocomotionState != _startLocomotionState)
            {
                data.loopFadeInTime = 0.4f;
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

            float targetY = data.CurrentLocomotionState switch
            {
                LocomotionState.Walk => 0.35f,
                LocomotionState.Jog => 0.7f,
                LocomotionState.Sprint => 0.98f,
                _ => 0.7f
            };
            data.CurrentAnimBlendY = targetY;
            //Debug.Log($"[MoveStartState.Exit] ExpectedFootPhase={data.ExpectedFootPhase}, " + $"LocomotionState={data.CurrentLocomotionState}");
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// 根据运动状态和本地移动角度，选择对应的起步动画。
        /// </summary>
        /// <param name="angle">本地角度 (-180 to 180)</param>
        /// <param name="locomotionState">当前运动状态（Walk/Jog/Sprint）</param>
        /// <returns>选中的动画数据</returns>
        private MotionClipData SelectClipForLocomotionState(float angle, LocomotionState locomotionState)
        {
            // 首先根据方向选择基础方向的动画
            MotionClipData walkClip = SelectDirectionClip(angle, isWalk: true);
            MotionClipData jogClip = SelectDirectionClip(angle, isWalk: false);
            MotionClipData sprintClip = SelectDirectionClip(angle, isSprint: true);

            // 然后根据运动状态返回对应的动画
            return locomotionState switch
            {
                LocomotionState.Walk => walkClip,
                LocomotionState.Jog => jogClip,
                LocomotionState.Sprint => sprintClip,
                _ => jogClip // 默认为 Jog
            };
        }

        /// <summary>
        /// 根据输入角度选择8个方向中的一个动画。
        /// </summary>
        private MotionClipData SelectDirectionClip(float angle, bool isWalk = false, bool isSprint = false)
        {
            // 8-Way Directional Selection Logic
            if (angle > -HalfSectorAngle && angle <= HalfSectorAngle) // Fwd
            {
                if (isWalk) return config.WalkStartFwd;
                if (isSprint) return config.SprintStartFwd;
                return config.RunStartFwd; // Jog (RunStart 对应 Jog)
            }

            if (angle > HalfSectorAngle && angle <= HalfSectorAngle + SectorAngle) // Fwd-Right
            {
                if (isWalk) return config.WalkStartFwdRight;
                if (isSprint) return config.SprintStartFwdRight;
                return config.RunStartFwdRight;
            }

            if (angle > HalfSectorAngle + SectorAngle && angle <= HalfSectorAngle + SectorAngle * 2) // Right
            {
                if (isWalk) return config.WalkStartRight;
                if (isSprint) return config.SprintStartRight;
                return config.RunStartRight;
            }

            if (angle > HalfSectorAngle + SectorAngle * 2 && angle <= 180f - HalfSectorAngle) // Back-Right
            {
                if (isWalk) return config.WalkStartBackRight;
                if (isSprint) return config.SprintStartBackRight;
                return config.RunStartBackRight;
            }

            // Back (covers +157.5 to 180 and -180 to -157.5)
            if (angle > 180f - HalfSectorAngle || angle <= -180f + HalfSectorAngle)
            {
                if (isWalk) return config.WalkStartBack;
                if (isSprint) return config.SprintStartBack;
                return config.RunStartBack;
            }

            if (angle > -180f + HalfSectorAngle && angle <= -HalfSectorAngle - SectorAngle * 2) // Back-Left
            {
                if (isWalk) return config.WalkStartBackLeft;
                if (isSprint) return config.SprintStartBackLeft;
                return config.RunStartBackLeft;
            }

            if (angle > -HalfSectorAngle - SectorAngle * 2 && angle <= -HalfSectorAngle - SectorAngle) // Left
            {
                if (isWalk) return config.WalkStartLeft;
                if (isSprint) return config.SprintStartLeft;
                return config.RunStartLeft;
            }

            if (angle > -HalfSectorAngle - SectorAngle && angle <= -HalfSectorAngle) // Fwd-Left
            {
                if (isWalk) return config.WalkStartFwdLeft;
                if (isSprint) return config.SprintStartFwdLeft;
                return config.RunStartFwdLeft;
            }

            // 兜底：默认向前起步
            if (isWalk) return config.WalkStartFwd;
            if (isSprint) return config.SprintStartFwd;
            return config.RunStartFwd;
        }

        #endregion
    }
}
