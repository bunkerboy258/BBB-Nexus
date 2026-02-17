using UnityEngine;
using Animancer;
using Characters.Player.Data;
using Core.StateMachine;

namespace Characters.Player.States
{
    public class PlayerJumpState : PlayerBaseState
    {
        private AnimancerState _state;
        private float _stateDuration;
        private float _startYaw;
        private MotionClipData _clipData;
        private bool _canCheckLand;
        private float _jumpForce;

        public PlayerJumpState(PlayerController player) : base(player) { }

        public override void Enter()
        {
            _stateDuration = 0f;
            _startYaw = player.transform.eulerAngles.y;
            _canCheckLand = false;

            // 1. 根据运动状态和装备情况选择跳跃动画和力度
            SelectJumpAnimation();

            // Log selected jump clip and force for debugging
            //Debug.Log($"[PlayerJumpState.Enter] Selected jump clip: clip={_clipData?.Clip}, JumpForce={_jumpForce}, NextLoopFadeInTime={_clipData?.NextLoopFadeInTime}");

            // 确保从头播放
            _state = player.Animancer.Layers[0].Play(_clipData.Clip);
            _state.Time = 0f;

            // [新增] 动画完成末回调：如果还未落地，进入 FallState
            _state.Events(this).OnEnd = () =>
            {
                // 动画播完但仍未落地：保持当前状态，等待落地检测切换到 LandState
                // 若已落地则立即进入 LandState
                if (player.CharController.isGrounded)
                {
                    player.StateMachine.ChangeState(player.LandState);
                }
            };

            // 2. 施加物理力
            PerformJumpPhysics();
        }

        /// <summary>
        /// 根据当前运动状态和装备情况选择合适的跳跃动画和力度。
        /// 
        /// 逻辑：
        /// - Walk/Jog/Idle：使用 JumpAirAnimWalk 和 JumpForceWalk
        /// - Sprint + 有装备：使用 JumpAirAnimSprint 和 JumpForceSprint
        /// - Sprint + 空手：使用 JumpAirAnimSprintEmpty 和 JumpForceSprintEmpty
        /// </summary>
        private void SelectJumpAnimation()
        {
            bool isHandsEmpty = data.CurrentEquipment.Definition == null;

            switch (data.CurrentLocomotionState)
            {
                // Treat Idle the same as Walk/Jog for jump configuration so stationary jumps use Walk settings
                case LocomotionState.Idle:
                case LocomotionState.Walk:
                case LocomotionState.Jog:
                    // Walk/Jog/Idle 状态：使用默认的 Walk 配置
                    _clipData = config.JumpAirAnimWalk ?? config.JumpAirAnim; // fallback to default if not set
                    _jumpForce = config.JumpForceWalk;
                    data.LandFadeInTime = config.JumpToLandFadeInTime_WalkJog;
                    break;

                case LocomotionState.Sprint:
                    if (isHandsEmpty)
                    {
                        // Sprint + 空手：使用特殊的空手配置
                        _clipData = config.JumpAirAnimSprintEmpty ?? config.JumpAirAnim; // fallback to default if not set
                        _jumpForce = config.JumpForceSprintEmpty;
                        data.LandFadeInTime = config.JumpToLandFadeInTime_SprintEmpty;
                    }
                    else
                    {
                        // Sprint + 有装备：使用 Sprint 配置
                        _clipData = config.JumpAirAnimSprint ?? config.JumpAirAnim; // fallback to default if not set
                        _jumpForce = config.JumpForceSprint;
                        data.LandFadeInTime = config.JumpToLandFadeInTime_Sprint;
                    }
                    break;

                default:
                    // Fallback: use default jump settings
                    _clipData = config.JumpAirAnim;
                    _jumpForce = config.JumpForce;
                    data.LandFadeInTime = config.JumpToLandFadeInTime;
                    break;
            }
        }

        private void PerformJumpPhysics()
        {
            data.VerticalVelocity = _jumpForce;
            data.IsGrounded = false;
        }

        protected override void UpdateStateLogic()
        {
            if (data.WantsDoubleJump)
            {
                player.StateMachine.ChangeState(player.DoubleJumpState);
                return; 
            }
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

            // [注意] 下落高度计算已由 MovementParameterProcessor 接管
            // MovementParameterProcessor 通过位置变化持续计算下落高度，
            // 并在落地时生成 FallHeightLevel（消费型状态）
            player.MotionDriver.UpdateMotion(null, 0, _startYaw);
        }

        public override void Exit()
        {
            _state = null;
        }
    }
}
