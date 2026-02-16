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
    /// C：落地动画末相位写入 ExpectedFootPhase，并通过 MotionClipData.NextLoopFadeInTime 写入 LoopFadeInTime（自动衔接）
    /// </summary>
    public class PlayerLandState : PlayerBaseState
    {
        private AnimancerState _state;
        private float _stateDuration;
        private float _startYaw;
        private MotionClipData _currentClip;

        public PlayerLandState(PlayerController player) : base(player) { }

        public override void Enter()
        {
            _stateDuration = 0f;
            _startYaw = player.transform.eulerAngles.y;

            bool wantToMove = HasMoveInput;

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
            Debug.Log(data.FallHeightLevel);
            // 输出选择的动画名称（ClipTransition.Clip 的名字）
            Debug.Log(_currentClip?.Clip?.Clip != null ? _currentClip.Clip.Clip.name : "<null landing clip>");
            
            // 消费 FallHeightLevel（一次性消费数据），然后清零
            data.FallHeightLevel = 0;
            
            // 防空检查：没资源则直接根据输入回到 MoveLoop/Idle
            if (_currentClip == null || _currentClip.Clip == null || _currentClip.Clip.Clip == null)
            {
                player.StateMachine.ChangeState(wantToMove ? player.MoveLoopState : player.IdleState);
                return;
            }

            // 2) 播放动画
            _state = player.Animancer.Layers[0].Play(_currentClip.Clip);

            // 3) 结束回调：写相位 + 写淡入时间 + 切换状态
            _state.Events(this).OnEnd = () =>
            {
                // C) 末相位写入（用于 MoveLoop 选左右脚相位）
                data.ExpectedFootPhase = _currentClip.EndPhase;

                // C) 将落地动画建议的下一段淡入写给 MoveLoop.Enter 消费
                if (_currentClip.NextLoopFadeInTime > 0f)
                {
                    data.LoopFadeInTime = _currentClip.NextLoopFadeInTime;
                }

                player.StateMachine.ChangeState(wantToMove ? player.MoveLoopState : player.IdleState);
            };

            data.ExpectedFootPhase= _currentClip.EndPhase;
        }

        public override void LogicUpdate()
        {
            // LandState 一般不响应切换（避免打断缓冲），只允许高优先级：跳跃
            if (data.WantsToJump)
            {
                player.StateMachine.ChangeState(player.JumpState);
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
                return config.LandBuffer_ExceedLimit;
            }

            bool isSprinting = locomotionState == LocomotionState.Sprint;

            // Walk/Jog 共用一组缓冲动画
            if (!isSprinting)
            {
                return fallHeightLevel switch
                {
                    0 => config.LandBuffer_WalkJog_L1,
                    1 => config.LandBuffer_WalkJog_L2,
                    2 => config.LandBuffer_WalkJog_L3,
                    3 => config.LandBuffer_WalkJog_L4,
                    _ => config.LandBuffer_WalkJog_L1,
                };
            }

            // Sprint 单独一组缓冲动画
            return fallHeightLevel switch
            {
                0 => config.LandBuffer_Sprint_L1,
                1 => config.LandBuffer_Sprint_L2,
                2 => config.LandBuffer_Sprint_L3,
                3 => config.LandBuffer_Sprint_L4,
                _ => config.LandBuffer_Sprint_L1,
            };
        }
    }
}
