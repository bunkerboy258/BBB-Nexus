using UnityEngine;
using Animancer;
using Characters.Player.Data;

namespace Characters.Player.States
{
    public class PlayerJumpState : PlayerBaseState
    {
        private AnimancerState _state;
        private float _stateDuration;
        private float _startYaw;
        private MotionClipData _clipData;
        private bool _canCheckLand;

        public PlayerJumpState(PlayerController player) : base(player) { }

        public override void Enter()
        {
            _stateDuration = 0f;
            _startYaw = player.transform.eulerAngles.y;
            _canCheckLand = false;

            // 1. 播放跳跃动画 (含滞空)
            _clipData = config.JumpAirAnim;

            // 确保从头播放
            _state = player.Animancer.Layers[0].Play(_clipData.Clip);
            _state.Time = 0f;

            // 2. 施加物理力
            PerformJumpPhysics();
        }

        private void PerformJumpPhysics()
        {
            data.VerticalVelocity = config.JumpForce;
            data.IsGrounded = false;
        }

        public override void LogicUpdate()
        {
            // 防抖：起跳后至少过 0.2s 才开始检测落地
            if (!_canCheckLand && _state.Time > 0.2f)
            {
                _canCheckLand = true;
            }

            // 落地检测
            if (_canCheckLand && data.VerticalVelocity <= 0 && player.CharController.isGrounded)
            {
                player.StateMachine.ChangeState(player.LandState);
            }
        }

        public override void PhysicsUpdate()
        {
            if (_state == null) return;

            player.MotionDriver.UpdateMotion(null, 0, _startYaw);
        }

        public override void Exit()
        {
            _state = null;
        }
    }
}
