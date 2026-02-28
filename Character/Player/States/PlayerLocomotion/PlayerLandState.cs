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
                player.StateMachine.ChangeState(wantToMove
                    ? player.StateRegistry.GetState<PlayerAimMoveState>()
                    : player.StateRegistry.GetState<PlayerAimIdleState>());
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

                player.StateMachine.ChangeState(wantToMove
                    ? player.StateRegistry.GetState<PlayerMoveLoopState>()
                    : player.StateRegistry.GetState<PlayerIdleState>());
            });

            data.ExpectedFootPhase = _currentClip.EndPhase;
        }

        protected override void UpdateStateLogic()
        {
            // LandState 一般不响应切换（避免打断缓冲），只允许高优先级：跳跃
            if (data.WantsToJump)
            {
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerJumpState>());
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

                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerMoveLoopState>());
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
                        data.NextStatePlayOptions = config.JumpAndLanding.LandHeight_Level1_options;
                        level = 0;
                        return config.JumpAndLanding.LandBuffer_WalkJog_L0;
                    case 1:
                        data.NextStatePlayOptions = config.JumpAndLanding.LandHeight_Level1_options;
                        level = 1;
                        return config.JumpAndLanding.LandBuffer_WalkJog_L1;
                    case 2:
                        data.NextStatePlayOptions = config.JumpAndLanding.LandHeight_Level1_options;
                        level = 2;
                        return config.JumpAndLanding.LandBuffer_WalkJog_L2;
                    case 3:
                        data.NextStatePlayOptions = config.JumpAndLanding.LandHeight_Level1_options;
                        level = 3;
                        return config.JumpAndLanding.LandBuffer_WalkJog_L3;
                    case 4:
                        data.NextStatePlayOptions = config.JumpAndLanding.LandHeight_Level1_options;
                        level = 99;
                        return config.JumpAndLanding.LandBuffer_ExceedLimit;
                    default:
                        Debug.Log("下落高度等级计算出现未知错误");
                        data.NextStatePlayOptions = config.JumpAndLanding.LandHeight_Level1_options;
                        level = 0;
                        return config.JumpAndLanding.LandBuffer_WalkJog_L1;
                }
            }

            switch (fallHeightLevel)
            {
                case 0:
                    data.NextStatePlayOptions = config.JumpAndLanding.LandHeight_Level1_options;
                    level = 10;
                    return config.JumpAndLanding.LandBuffer_Sprint_L0;
                case 1:
                    data.NextStatePlayOptions = config.JumpAndLanding.LandHeight_Level1_options;
                    level = 11;
                    return config.JumpAndLanding.LandBuffer_Sprint_L1;
                case 2:
                    data.NextStatePlayOptions = config.JumpAndLanding.LandHeight_Level1_options;
                    level = 12;
                    return config.JumpAndLanding.LandBuffer_Sprint_L2;
                case 3:
                    data.NextStatePlayOptions = config.JumpAndLanding.LandHeight_Level1_options;
                    level = 13;
                    return config.JumpAndLanding.LandBuffer_Sprint_L3;
                case 4:
                    data.NextStatePlayOptions = config.JumpAndLanding.LandHeight_Level1_options;
                    level = 99;
                    return config.JumpAndLanding.LandBuffer_ExceedLimit;
                default:
                    data.NextStatePlayOptions = config.JumpAndLanding.LandHeight_Level1_options;
                    level = 10;
                    return config.JumpAndLanding.LandBuffer_Sprint_L1;
            }
        }

        private void SetupMoveLoopByLevel()
        {
            switch (level)
            {
                // Walk/Jog 档位
                case 0:
                    data.NextStatePlayOptions = config.JumpAndLanding.LandToLoopFadeInTime_WalkJog_L0ptions;
                    break;
                case 1:
                    data.NextStatePlayOptions = config.JumpAndLanding.LandToLoopFadeInTime_WalkJog_L1ptions;
                    break;
                case 2:
                    data.NextStatePlayOptions = config.JumpAndLanding.LandToLoopFadeInTime_WalkJog_L2ptions;
                    break;
                case 3:
                    data.NextStatePlayOptions = config.JumpAndLanding.LandToLoopFadeInTime_WalkJog_L3ptions;
                    break;

                // Sprint 档位
                case 10:
                    data.NextStatePlayOptions = config.JumpAndLanding.LandToLoopFadeInTime_Sprint_L0ptions;
                    break;
                case 11:
                    data.NextStatePlayOptions = config.JumpAndLanding.LandToLoopFadeInTime_Sprint_L1ptions;
                    break;
                case 12:
                    data.NextStatePlayOptions = config.JumpAndLanding.LandToLoopFadeInTime_Sprint_L2ptions;
                    break;
                case 13:
                    data.NextStatePlayOptions = config.JumpAndLanding.LandToLoopFadeInTime_Sprint_L3ptions;
                    break;

                case 99:
                    data.NextStatePlayOptions = config.JumpAndLanding.LandToLoopFadeInTime_ExceedLimitOptions;
                    break;

                default:
                    data.NextStatePlayOptions = config.JumpAndLanding.LandToLoopFadeInTime_WalkJog_L0ptions;
                    break;
            }
        }
    }
}
