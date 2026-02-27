using UnityEngine;
using Characters.Player.Data;
using Characters.Player.Animation;

namespace Characters.Player.States
{
    /// <summary>
    /// 落地状态。
    /// 职责：
    /// A：按 FallHeightLevel 选落地缓冲动画（Walk/Jog/Sprint 四档 + 超限）并播放完再回 MoveLoop/Idle
    /// B：落地时如果 IsAiming 直接进 AimIdle/AimMove（不播落地缓冲）
    /// C：落地动画末相位写入 ExpectedFootPhase
    /// </summary>
    public class PlayerLandState : PlayerBaseState
    {
        private int level;
        private MotionClipData _currentClip;
        private bool _endTimeTriggered;

        public PlayerLandState(PlayerController player) : base(player) { }

        // 落地缓冲中一般不希望被通用强制打断（避免反复进入/退出）。
        protected override bool CheckInterrupts() => false;

        public override void Enter()
        {
            level = 0;
            _endTimeTriggered = false;

            // 重置本次空中的二段跳标记（为下次空中做准备）
            data.HasPerformedDoubleJumpInAir = false;

            bool wantToMove = data.CurrentLocomotionState != LocomotionState.Idle;

            // B) 落地瞬间如果在瞄准：直接切瞄准状态，不播放落地缓冲
            if (data.IsAiming)
            {
                // 消费 FallHeightLevel（一次性消费数据），然后清零
                data.FallHeightLevel = 0;
                player.StateMachine.ChangeState(wantToMove ? player.AimMoveState : player.AimIdleState);
                return;
            }

            // A) 根据 FallHeightLevel + LocomotionState 选择落地缓冲动画
            _currentClip = SelectLandingBufferClip(data.CurrentLocomotionState, data.FallHeightLevel);

            // 消费 FallHeightLevel（一次性消费数据），然后清零
            data.FallHeightLevel = 0;

            ChooseOptionsAndPlay(_currentClip.Clip);

            // 3) 结束回调：写相位 + 切换状态
            AnimFacade.SetOnEndCallback(() =>
            {
                // C) 末相位写入（用于 MoveLoop 选左右脚相位）
                data.ExpectedFootPhase = _currentClip.EndPhase;

                player.StateMachine.ChangeState(wantToMove ? player.MoveLoopState : player.IdleState);
            });

            data.ExpectedFootPhase = _currentClip.EndPhase;
        }

        protected override void UpdateStateLogic()
        {
            // LandState 一般不响应切换（避免打断缓冲），只允许高优先级：跳跃
            if (data.WantsToJump)
            {
                player.StateMachine.ChangeState(player.JumpState);
                return;
            }

            // 如果权威运动状态不为 Idle：允许按 EndTime 提前切回 MoveLoop。
            // 说明：wantToMove 取 Enter 时的快照，这里按“权威状态”判断。
            if (!_endTimeTriggered && data.CurrentLocomotionState != LocomotionState.Idle && 
                _currentClip != null && _currentClip.EndTime > 0f && AnimFacade.CurrentTime >= _currentClip.EndTime)
            {
                _endTimeTriggered = true;
                data.ExpectedFootPhase = _currentClip.EndPhase;
                data.NextStatePlayOptions = config.JumpAndLanding.LandToIdleOptions;

                player.StateMachine.ChangeState(player.MoveLoopState);
            }
        }

        public override void PhysicsUpdate()
        {
            if (_currentClip == null) return;

            float stateTime = AnimFacade.CurrentTime;
            player.MotionDriver.UpdateMotion(_currentClip, stateTime);
        }

        public override void Exit()
        {
            AnimFacade.ClearOnEndCallback();
            _currentClip = null;
            SetupMoveLoopByLevel();
        }

        private MotionClipData SelectLandingBufferClip(LocomotionState locomotionState, int fallHeightLevel)
        {
            // fallHeightLevel: 0-3 => L1-L4, 4 => ExceedLimit
            if (fallHeightLevel >= 4)
            {
                data.NextStatePlayOptions = config.JumpAndLanding.LandToLoopFadeInTime_ExceedLimitOptions;
                return config.JumpAndLanding.LandBuffer_ExceedLimit;
            }

            bool isSprinting = locomotionState == LocomotionState.Sprint;

            if (!isSprinting)
            {
                switch (fallHeightLevel)
                {
                    case 0:
                        data.NextStatePlayOptions = config.JumpAndLanding.LandToLoopFadeInTime_WalkJog_L1Options;
                        level = 0;
                        return config.JumpAndLanding.LandBuffer_WalkJog_L1;
                    case 1:
                        data.NextStatePlayOptions = config.JumpAndLanding.LandToLoopFadeInTime_WalkJog_L2Options;
                        level = 1;
                        return config.JumpAndLanding.LandBuffer_WalkJog_L2;
                    case 2:
                        data.NextStatePlayOptions = config.JumpAndLanding.LandToLoopFadeInTime_WalkJog_L3Options;
                        level = 2;
                        return config.JumpAndLanding.LandBuffer_WalkJog_L3;
                    case 3:
                        data.NextStatePlayOptions = config.JumpAndLanding.LandToLoopFadeInTime_WalkJog_L4Options;
                        level = 3;
                        return config.JumpAndLanding.LandBuffer_WalkJog_L4;
                    default:
                        data.NextStatePlayOptions = config.JumpAndLanding.LandToLoopFadeInTime_WalkJog_L1Options;
                        level = 0;
                        return config.JumpAndLanding.LandBuffer_WalkJog_L1;
                }
            }

            switch (fallHeightLevel)
            {
                case 0:
                    data.NextStatePlayOptions = config.JumpAndLanding.LandToLoopFadeInTime_Sprint_L1Options;
                    level = 10;
                    return config.JumpAndLanding.LandBuffer_Sprint_L1;
                case 1:
                    data.NextStatePlayOptions = config.JumpAndLanding.LandToLoopFadeInTime_Sprint_L2Options;
                    level = 11;
                    return config.JumpAndLanding.LandBuffer_Sprint_L2;
                case 2:
                    data.NextStatePlayOptions = config.JumpAndLanding.LandToLoopFadeInTime_Sprint_L3Options;
                    level = 12;
                    return config.JumpAndLanding.LandBuffer_Sprint_L3;
                case 3:
                    data.NextStatePlayOptions = config.JumpAndLanding.LandToLoopFadeInTime_Sprint_L4Options;
                    level = 13;
                    return config.JumpAndLanding.LandBuffer_Sprint_L4;
                default:
                    data.NextStatePlayOptions = config.JumpAndLanding.LandToLoopFadeInTime_Sprint_L1Options;
                    level = 10;
                    return config.JumpAndLanding.LandBuffer_Sprint_L1;
            }
        }

        private void SetupMoveLoopByLevel()
        {
            // level 映射关系：
            // 0-3: Walk/Jog L1-L4
            // 10-13: Sprint L1-L4
            // 其他: 默认走路 L1

            switch (level)
            {
                // Walk/Jog 档位
                case 0:
                    data.NextStatePlayOptions = config.JumpAndLanding.LandToLoopFadeInTime_WalkJog_L1Options;
                    break;
                case 1:
                    data.NextStatePlayOptions = config.JumpAndLanding.LandToLoopFadeInTime_WalkJog_L2Options;
                    break;
                case 2:
                    data.NextStatePlayOptions = config.JumpAndLanding.LandToLoopFadeInTime_WalkJog_L3Options;
                    break;
                case 3:
                    data.NextStatePlayOptions = config.JumpAndLanding.LandToLoopFadeInTime_WalkJog_L4Options;
                    break;

                // Sprint 档位
                case 10:
                    data.NextStatePlayOptions = config.JumpAndLanding.LandToLoopFadeInTime_Sprint_L1Options;
                    break;
                case 11:
                    data.NextStatePlayOptions = config.JumpAndLanding.LandToLoopFadeInTime_Sprint_L2Options;
                    break;
                case 12:
                    data.NextStatePlayOptions = config.JumpAndLanding.LandToLoopFadeInTime_Sprint_L3Options;
                    break;
                case 13:
                    data.NextStatePlayOptions = config.JumpAndLanding.LandToLoopFadeInTime_Sprint_L4Options;
                    break;

                // 默认降级到走路
                default:
                    data.NextStatePlayOptions = config.JumpAndLanding.LandToLoopFadeInTime_WalkJog_L1Options;
                    break;
            }
        }
    }
}
