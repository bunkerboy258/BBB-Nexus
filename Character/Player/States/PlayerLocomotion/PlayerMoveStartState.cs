using UnityEngine;
using Characters.Player.Data;
using Characters.Player.Animation;

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
        private float _stateDuration;
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
            _stateDuration = 0f;
            _startLocomotionState = data.CurrentLocomotionState;

            // 根据当前运动状态和移动方向选择起步动画
            _currentClipData = SelectClipForLocomotionState(data.DesiredLocalMoveAngle, data.CurrentLocomotionState);

            var options = AnimPlayOptions.Default;

            // 优先使用新的 PlayOptions 覆写
            if (data.NextStatePlayOptions.HasValue)
            {
                options = data.NextStatePlayOptions.Value;
                data.NextStatePlayOptions = null;
            }

            // 播放起步动画（使用 Transition，本项目配置的是 ClipTransition）
            AnimFacade.PlayTransition(_currentClipData.Clip, options);

            // 末相位仍然用于 Loop/Stop 的左右脚选择
            data.ExpectedFootPhase = _currentClipData.EndPhase;

            // End 回调：切换到 MoveLoop
            AnimFacade.SetOnEndCallback(() =>
            {
                // 应用自定义淡入时间（迁移为 PlayOptions）
                var nextOptions = new AnimPlayOptions();
                nextOptions.FadeDuration = data.CurrentLocomotionState switch
                {
                    LocomotionState.Walk => config.LocomotionAnims.FadeInWalkLoopOptions.FadeDuration ?? 0f,
                    LocomotionState.Jog => config.LocomotionAnims.FadeInRunLoopOptions.FadeDuration ?? 0f,
                    LocomotionState.Sprint => config.LocomotionAnims.FadeInSprintLoopOptions.FadeDuration ?? 0f,
                    _ => 0f
                };
                data.NextStatePlayOptions = nextOptions;

                player.StateMachine.ChangeState(player.MoveLoopState);
            });
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
                // 兼容旧字段设置 -> 现在统一写入 NextStatePlayOptions
                data.NextStatePlayOptions = config.LocomotionAnims.FadeInLoopBreakInOptions;
                player.StateMachine.ChangeState(player.MoveLoopState);
            }
        }

        public override void PhysicsUpdate()
        {
            if (_currentClipData == null) return;

            // 注意：stateTime 需要和烘焙曲线的时间轴一致。
            // 这里使用 facade 的 CurrentTime（它已经包含 transition 的 speed 影响）。
            float stateTime = AnimFacade.CurrentTime;

            // 委托：将所有复杂的物理计算交给 MotionDriver
            player.MotionDriver.UpdateMotion(_currentClipData, stateTime);
        }

        public override void Exit()
        {
            AnimFacade.ClearOnEndCallback();
            _currentClipData = null;

            //这里是为了保证 MoveStart 无论正常结束还是被打断退出，
            //都会把上一次的曲线增量旋转缓存清掉，避免下次进入继承旧角度导致“瞬回”
            //如果不清掉 玩家在movestart中途进入idle 下一次movestart会瞬移
            player.MotionDriver.InterruptClipDrivenMotion();

            float targetY = data.CurrentLocomotionState switch
            {
                LocomotionState.Walk => 0.35f,
                LocomotionState.Jog => 0.7f,
                LocomotionState.Sprint => 0.98f,
                _ => 0.7f
            };
            data.CurrentAnimBlendY = targetY;
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
                if (isWalk) return config.LocomotionAnims.WalkStartFwd;
                if (isSprint) return config.LocomotionAnims.SprintStartFwd;
                return config.LocomotionAnims.RunStartFwd; // Jog (RunStart 对应 Jog)
            }

            if (angle > HalfSectorAngle && angle <= HalfSectorAngle + SectorAngle) // Fwd-Right
            {
                if (isWalk) return config.LocomotionAnims.WalkStartFwdRight;
                if (isSprint) return config.LocomotionAnims.SprintStartFwdRight;
                return config.LocomotionAnims.RunStartFwdRight;
            }

            if (angle > HalfSectorAngle + SectorAngle && angle <= HalfSectorAngle + SectorAngle * 2) // Right
            {
                if (isWalk) return config.LocomotionAnims.WalkStartRight;
                if (isSprint) return config.LocomotionAnims.SprintStartRight;
                return config.LocomotionAnims.RunStartRight;
            }

            if (angle > HalfSectorAngle + SectorAngle * 2 && angle <= 180f - HalfSectorAngle) // Back-Right
            {
                if (isWalk) return config.LocomotionAnims.WalkStartBackRight;
                if (isSprint) return config.LocomotionAnims.SprintStartBackRight;
                return config.LocomotionAnims.RunStartBackRight;
            }

            // Back (covers +157.5 to 180 and -180 to -157.5)
            if (angle > 180f - HalfSectorAngle || angle <= -180f + HalfSectorAngle)
            {
                if (isWalk) return config.LocomotionAnims.WalkStartBack;
                if (isSprint) return config.LocomotionAnims.SprintStartBack;
                return config.LocomotionAnims.RunStartBack;
            }

            if (angle > -180f + HalfSectorAngle && angle <= -HalfSectorAngle - SectorAngle * 2) // Back-Left
            {
                if (isWalk) return config.LocomotionAnims.WalkStartBackLeft;
                if (isSprint) return config.LocomotionAnims.SprintStartBackLeft;
                return config.LocomotionAnims.RunStartBackLeft;
            }

            if (angle > -HalfSectorAngle - SectorAngle * 2 && angle <= -HalfSectorAngle - SectorAngle) // Left
            {
                if (isWalk) return config.LocomotionAnims.WalkStartLeft;
                if (isSprint) return config.LocomotionAnims.SprintStartLeft;
                return config.LocomotionAnims.RunStartLeft;
            }

            if (angle > -HalfSectorAngle - SectorAngle && angle <= -HalfSectorAngle) // Fwd-Left
            {
                if (isWalk) return config.LocomotionAnims.WalkStartFwdLeft;
                if (isSprint) return config.LocomotionAnims.SprintStartFwdLeft;
                return config.LocomotionAnims.RunStartFwdLeft;
            }

            // 兜底：默认向前起步
            if (isWalk) return config.LocomotionAnims.WalkStartFwd;
            if (isSprint) return config.LocomotionAnims.SprintStartFwd;
            return config.LocomotionAnims.RunStartFwd;
        }

        #endregion
    }
}
