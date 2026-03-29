using UnityEngine;

namespace BBBNexus
{
    // 玩家起步状态 
    // 负责根据运动状态和移动方向选择8方向起步动画 并驱动角色初始移动 
    // 动画结束后切换到循环移动状态 
    public class PlayerMoveStartState : PlayerBaseState
    {
        private MotionClipData _currentClipData;
        private LocomotionState _startLocomotionState;

        private const float SectorAngle = 45f;
        private const float HalfSectorAngle = SectorAngle / 2f;

        public PlayerMoveStartState(BBBCharacterController player) : base(player) { }

        // 进入状态 选择对应方向的起步动画并注册结束回调
        public override void Enter()
        {
            _startLocomotionState = data.CurrentLocomotionState;

            // 根据当前运动状态和移动方向选择起步动画
            _currentClipData = SelectClipForLocomotionState(data.DesiredLocalMoveAngle, data.CurrentLocomotionState);

            ChooseOptionsAndPlay(_currentClipData.Clip);

            // 末相位用于 Loop Stop 的左右脚选择
            data.ExpectedFootPhase = _currentClipData.EndPhase;

            // End 回调 切换到 MoveLoop
            AnimFacade.SetOnEndCallback(() =>
            {
                // 应用自定义淡入时间
                var nextOptions = data.CurrentLocomotionState switch
                {
                    LocomotionState.Walk => config.LocomotionAnims.FadeInWalkLoopOptions,
                    LocomotionState.Jog => config.LocomotionAnims.FadeInRunLoopOptions,
                    LocomotionState.Sprint => config.LocomotionAnims.FadeInSprintLoopOptions,
                    _ => AnimPlayOptions.Default
                };
                data.NextStatePlayOptions = nextOptions;

                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerMoveLoopState>());
            });
        }

        // 状态逻辑 检测瞄准 空闲等打断条件
        // 跳跃由全局拦截器统一处理，避免状态内重复判断
        protected override void UpdateStateLogic()
        {
            if (data.IsAiming)
            {
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerAimMoveState>());
            }
            else if (data.CurrentLocomotionState == LocomotionState.Idle)
            {
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerIdleState>());
            }
            // 如果运动状态在起步中途改变 切到循环状态让其处理状态转换
            else if (data.CurrentLocomotionState != _startLocomotionState)
            {
                data.NextStatePlayOptions = config.LocomotionAnims.FadeInLoopBreakInOptions;
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerMoveLoopState>());
            }
        }

        // 物理更新 委托 MotionDriver 根据烘焙曲线驱动角色移动
        public override void PhysicsUpdate()
        {
            if (_currentClipData == null) return;

            float stateTime = AnimFacade.CurrentTime;

            // 委托 将所有复杂的物理计算交给 MotionDriver
            player.MotionDriver.UpdateMotion(_currentClipData, stateTime);
        }

        // 退出状态 清理回调 中断曲线驱动 防止下一个起步瞬移
        public override void Exit()
        {
            AnimFacade.ClearOnEndCallback();
            _currentClipData = null;

            // 清掉上一次的曲线增量旋转缓存 避免下次进入继承旧角度导致瞬回
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

        // 根据运动状态和本地移动角度 选择对应的起步动画
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
                _ => jogClip
            };
        }

        // 根据输入角度选择8个方向中的一个动画
        private MotionClipData SelectDirectionClip(float angle, bool isWalk = false, bool isSprint = false)
        {
            // 8方向量化选择 根据角度落在哪个45度扇区来决定方向
            if (angle > -HalfSectorAngle && angle <= HalfSectorAngle)
            {
                if (isWalk) return config.LocomotionAnims.WalkStartFwd;
                if (isSprint) return config.LocomotionAnims.SprintStartFwd;
                return config.LocomotionAnims.RunStartFwd;
            }

            if (angle > HalfSectorAngle && angle <= HalfSectorAngle + SectorAngle)
            {
                if (isWalk) return config.LocomotionAnims.WalkStartFwdRight;
                if (isSprint) return config.LocomotionAnims.SprintStartFwdRight;
                return config.LocomotionAnims.RunStartFwdRight;
            }

            if (angle > HalfSectorAngle + SectorAngle && angle <= HalfSectorAngle + SectorAngle * 2)
            {
                if (isWalk) return config.LocomotionAnims.WalkStartRight;
                if (isSprint) return config.LocomotionAnims.SprintStartRight;
                return config.LocomotionAnims.RunStartRight;
            }

            if (angle > HalfSectorAngle + SectorAngle * 2 && angle <= 180f - HalfSectorAngle)
            {
                if (isWalk) return config.LocomotionAnims.WalkStartBackRight;
                if (isSprint) return config.LocomotionAnims.SprintStartBackRight;
                return config.LocomotionAnims.RunStartBackRight;
            }

            // Back 覆盖 157.5 到 180 和 -180 到 -157.5
            if (angle > 180f - HalfSectorAngle || angle <= -180f + HalfSectorAngle)
            {
                if (isWalk) return config.LocomotionAnims.WalkStartBack;
                if (isSprint) return config.LocomotionAnims.SprintStartBack;
                return config.LocomotionAnims.RunStartBack;
            }

            if (angle > -180f + HalfSectorAngle && angle <= -HalfSectorAngle - SectorAngle * 2)
            {
                if (isWalk) return config.LocomotionAnims.WalkStartBackLeft;
                if (isSprint) return config.LocomotionAnims.SprintStartBackLeft;
                return config.LocomotionAnims.RunStartBackLeft;
            }

            if (angle > -HalfSectorAngle - SectorAngle * 2 && angle <= -HalfSectorAngle - SectorAngle)
            {
                if (isWalk) return config.LocomotionAnims.WalkStartLeft;
                if (isSprint) return config.LocomotionAnims.SprintStartLeft;
                return config.LocomotionAnims.RunStartLeft;
            }

            if (angle > -HalfSectorAngle - SectorAngle && angle <= -HalfSectorAngle)
            {
                if (isWalk) return config.LocomotionAnims.WalkStartFwdLeft;
                if (isSprint) return config.LocomotionAnims.SprintStartFwdLeft;
                return config.LocomotionAnims.RunStartFwdLeft;
            }

            // 兜底 默认向前起步
            if (isWalk) return config.LocomotionAnims.WalkStartFwd;
            if (isSprint) return config.LocomotionAnims.SprintStartFwd;
            return config.LocomotionAnims.RunStartFwd;
        }
    }
}
