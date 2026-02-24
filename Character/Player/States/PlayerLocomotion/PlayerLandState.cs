using UnityEngine;
using Animancer;
using Characters.Player.Data;

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
        private AnimancerState _state;
        private float _stateDuration;
        private float _startYaw;
        private MotionClipData _currentClip;
        private bool _endTimeTriggered;

        public PlayerLandState(PlayerController player) : base(player) { }

        // 落地缓冲中一般不希望被通用强制打断（避免反复进入/退出）。
        protected override bool CheckInterrupts() => false;

        public override void Enter()
        {
            _stateDuration = 0f;
            _startYaw = player.transform.eulerAngles.y;
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
            
            // 防空检查：没资源则直接根据输入回到 MoveLoop/Idle
            if (_currentClip == null || _currentClip.Clip == null || _currentClip.Clip.Clip == null)
            {
                player.StateMachine.ChangeState(wantToMove ? player.MoveLoopState : player.IdleState);
                return;
            }

            //Debug.Log(data.LandFadeInTime);
            _state = player.Animancer.Layers[0].Play(_currentClip.Clip, data.LandFadeInTime);
            data.LandFadeInTime = 0f; // 播放后重置，避免下次误用

            // 3) 结束回调：写相位 + 切换状态
            _state.Events(this).OnEnd = () =>
            {
                // C) 末相位写入（用于 MoveLoop 选左右脚相位）
                data.ExpectedFootPhase = _currentClip.EndPhase;

                player.StateMachine.ChangeState(wantToMove ? player.MoveLoopState : player.IdleState);
            };

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
                _currentClip.EndTime > 0f && _stateDuration >= _currentClip.EndTime)
            {
                _endTimeTriggered = true;
                data.ExpectedFootPhase = _currentClip.EndPhase;
                data.moveStartFadeInTime = 0.4f;

                player.StateMachine.ChangeState(player.MoveLoopState);
            }
        }

        public override void PhysicsUpdate()
        {
            if (_state == null) return;

            _stateDuration += Time.deltaTime * _state.Speed;

            // 使用 MotionDriver 驱动落地缓冲位移
            player.MotionDriver.UpdateMotion(_currentClip, _stateDuration, _startYaw);
        }

        public override void Exit()
        {
            _state = null;
            _currentClip = null;
        }

        private MotionClipData SelectLandingBufferClip(LocomotionState locomotionState, int fallHeightLevel)
        {
            // fallHeightLevel: 0-3 => L1-L4, 4 => ExceedLimit
            if (fallHeightLevel >= 4)
            {
                data.loopFadeInTime = config.LandToLoopFadeInTime_ExceedLimit;
                return config.LandBuffer_ExceedLimit;
            }

            bool isSprinting = locomotionState == LocomotionState.Sprint;

            if (!isSprinting)
            {
                switch (fallHeightLevel)
                {
                    case 0:
                        data.loopFadeInTime = config.LandToLoopFadeInTime_WalkJog_L1;
                        return config.LandBuffer_WalkJog_L1;
                    case 1:
                        data.loopFadeInTime = config.LandToLoopFadeInTime_WalkJog_L2;
                        return config.LandBuffer_WalkJog_L2;
                    case 2:
                        data.loopFadeInTime = config.LandToLoopFadeInTime_WalkJog_L3;
                        return config.LandBuffer_WalkJog_L3;
                    case 3:
                        data.loopFadeInTime = config.LandToLoopFadeInTime_WalkJog_L4;
                        return config.LandBuffer_WalkJog_L4;
                    default:
                        data.loopFadeInTime = config.LandToLoopFadeInTime_WalkJog_L1;
                        return config.LandBuffer_WalkJog_L1;
                }
            }

            switch (fallHeightLevel)
            {
                case 0:
                    data.loopFadeInTime = config.LandToLoopFadeInTime_Sprint_L1;
                    return config.LandBuffer_Sprint_L1;
                case 1:
                    data.loopFadeInTime = config.LandToLoopFadeInTime_Sprint_L2;
                    return config.LandBuffer_Sprint_L2;
                case 2:
                    data.loopFadeInTime = config.LandToLoopFadeInTime_Sprint_L3;
                    return config.LandBuffer_Sprint_L3;
                case 3:
                    data.loopFadeInTime = config.LandToLoopFadeInTime_Sprint_L4;
                    return config.LandBuffer_Sprint_L4;
                default:
                    data.loopFadeInTime = config.LandToLoopFadeInTime_Sprint_L1;
                    return config.LandBuffer_Sprint_L1;
            }
        }
    }
}
