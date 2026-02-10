using UnityEngine;
using Animancer;
using Characters.Player.Data;

namespace Characters.Player.States
{
    /// <summary>
    /// 落地状态。
    /// 职责：根据落地瞬间的输入意图，决定是播放“原地缓冲”还是“跑动衔接缓冲”。
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

            // 决策是跑动落地还是原地落地

            bool wantToMove = HasMoveInput; // 或者 data.WantToRun

            if (data.IsAiming)
            {
                if(wantToMove) 
                { 
                    player.StateMachine.ChangeState(player.AimMoveState);
                    return;
                }
                else 
                { 
                    player.StateMachine.ChangeState(player.AimIdleState);
                    return;
                }
            }

            else if (wantToMove && config.LandToRunStart?.Clip?.Clip != null)
            {
                // 跑动落地 (Land -> Run)
                // 这个动画通常带有向前的 Root Motion，能让角色顺势冲出去
                _currentClip = config.LandToRunStart;
            }
            else
            {
                player.StateMachine.ChangeState(player.IdleState);
            }

            // 防空检查
            if (_currentClip == null || _currentClip.Clip.Clip == null)
            {
                // 如果没资源，根据意图切状态
                if (wantToMove) player.StateMachine.ChangeState(player.MoveLoopState);
                else player.StateMachine.ChangeState(player.IdleState);
                return;
            }

            // 2. 播放动画
            _state = player.Animancer.Layers[0].Play(_currentClip.Clip);

            // 3. 结束回调
            _state.Events(this).OnEnd = () =>
            {
                if (wantToMove)
                {
                    // 跑动落地播完了 -> 无缝衔接 Loop 状态
                    // (利用相位匹配和 Fade，过渡会很自然)
                    data.ExpectedFootPhase = _currentClip.EndPhase;
                    if(data.IsAiming)
                    {
                        player.StateMachine.ChangeState(player.AimMoveState);
                    }
                    else
                    {
                        player.StateMachine.ChangeState(player.MoveLoopState);
                    }
                }
                else
                {
                    // 原地落地播完了 -> 回 Idle
                    player.StateMachine.ChangeState(player.IdleState);
                }
            };
        }

        public override void LogicUpdate()
        {
            if (!HasMoveInput)
            {
                player.StateMachine.ChangeState(player.IdleState); 
            }
            else if (data.WantsToJump)
            {
                player.StateMachine.ChangeState(player.JumpState);
            }
        }

        public override void PhysicsUpdate()
        {
            if (_state == null) return;

            _stateDuration += Time.deltaTime * _state.Speed;

            // 使用 MotionDriver 驱动落地缓冲位移
            player.MotionDriver.UpdateMotion(
                _currentClip,
                _stateDuration,
                _startYaw
            );
        }

        public override void Exit()
        {
            _state = null;
            _currentClip = null;
        }
    }
}
