using Animancer;
using Characters.Player.Animation;
using Characters.Player.Data;
using Core.StateMachine;
using UnityEngine;

namespace Characters.Player.States
{
    /// <summary>
    /// 玩家二段跳状态。
    /// 
    /// 职责：
    /// 1. 在空中执行二段跳，复用 PlayerJumpState 的动画选择和物理逻辑。
    /// 2. 在进入时标记 HasPerformedDoubleJumpInAir = true，防止重复二段跳。
    /// 3. 等待落地后转移到 LandState。
    /// 
    /// 与 PlayerJumpState 的区别：
    /// - JumpState：地面起跳，初速度由 CurrentLocomotionState 决定
    /// - DoubleJumpState：空中跳跃，需要在进入时标记"本次空中已执行二段跳"
    /// </summary>
    public class PlayerDoubleJumpState : PlayerBaseState
    {
        private MotionClipData _clipData;
        private bool _canCheckLand;
        private float _jumpForce;

        public PlayerDoubleJumpState(PlayerController player) : base(player) { }

        public override void Enter()
        {
            _canCheckLand = false;

            // 1. 标记本次空中已执行二段跳（防止重复）
            data.HasPerformedDoubleJumpInAir = true;

            bool isHandsEmpty = data.CurrentEquipment.Definition == null;
            bool isSprint = data.CurrentLocomotionState == LocomotionState.Sprint;

            // 新增：Sprint+空手时播放翻滚动画，使用独立淡入配置
            if (isSprint && isHandsEmpty && config.JumpAndLanding. DoubleJumpSprintRoll != null && config.JumpAndLanding.DoubleJumpSprintRoll.Clip != null)
            {
                _clipData = config.JumpAndLanding.DoubleJumpSprintRoll;
                _jumpForce = config.JumpAndLanding.DoubleJumpForceUp > 0.01f ? config.JumpAndLanding.DoubleJumpForceUp : config.JumpAndLanding.JumpForceSprintEmpty;

                var option = AnimPlayOptions.Default;
                if (data.NextStateFadeOverride.HasValue)
                {
                    option.FadeDuration = data.NextStateFadeOverride.Value;
                    data.NextStateFadeOverride = null;
                }

                option.NormalizedTime = 0f;
                AnimFacade.PlayTransition(_clipData.Clip, option);
            }
            else
            {
                // 2. 根据二段跳方向和装备情况选择动画（原逻辑）
                SelectDoubleJumpAnimation();
                data.LandFadeInTime = config.JumpAndLanding.DoubleJumpToLandFadeInTime;

                var options = AnimPlayOptions.Default;
                options.FadeDuration = config.JumpAndLanding.DoubleJumpFadeInTime;
                options.NormalizedTime = 0f;
                AnimFacade.PlayTransition(_clipData.Clip, options);
            }

            // 4. 动画完成末回调
            AnimFacade.SetOnEndCallback(() =>
            {
                if (player.CharController.isGrounded)
                {
                    player.StateMachine.ChangeState(player.LandState);
                }
            });

            // 5. 施加物理力
            PerformJumpPhysics();
        }

        /// <summary>
        /// 根据运动状态和装备情况选择合适的二段跳动画和力度。
        /// 
        /// 逻辑：
        /// 1. 二段跳仅向上（Up 方向）
        /// 2. 按运动状态和装备情况选择 DoubleJumpUp 或基础跳跃配置
        /// 3. 未找到则 fallback 到标准跳跃动画
        /// </summary>
        private void SelectDoubleJumpAnimation()
        {
            bool isHandsEmpty = data.CurrentEquipment.Definition == null;

            // 根据运动状态和装备获取基础配置
            MotionClipData baseClip = null;
            float baseForce = config.JumpAndLanding.JumpForce;

            switch (data.CurrentLocomotionState)
            {
                case LocomotionState.Idle:
                case LocomotionState.Walk:
                case LocomotionState.Jog:
                    baseClip = config.JumpAndLanding.JumpAirAnimWalk;
                    baseForce = config.JumpAndLanding.JumpForceWalk;
                    break;

                case LocomotionState.Sprint:
                    if (isHandsEmpty)
                    {
                        baseClip = config.JumpAndLanding.JumpAirAnimSprintEmpty;
                        baseForce = config.JumpAndLanding.JumpForceSprintEmpty;
                    }
                    else
                    {
                        baseClip = config.JumpAndLanding.JumpAirAnimSprint;
                        baseForce = config.JumpAndLanding.JumpForceSprint;
                    }
                    break;

                default:
                    baseClip = config.JumpAndLanding.JumpAirAnim;
                    baseForce = config.JumpAndLanding.JumpForce;
                    break;
            }

            // 二段跳力度：优先使用独立配置（默认值由 SO 给出）
            float doubleJumpForce = config.JumpAndLanding.DoubleJumpForceUp > 0.01f ? config.JumpAndLanding.DoubleJumpForceUp : baseForce;

            // 向上二段跳：优先选择 DoubleJumpUp，否则回退到基础配置
            _clipData = config.JumpAndLanding.DoubleJumpUp ?? baseClip ?? config.JumpAndLanding.JumpAirAnim;
            _jumpForce = doubleJumpForce;

            // Debug 输出：记录选择结果，便于在控制台追踪问题
            var clipName = _clipData?.Clip != null ? _clipData.Clip.Name : "none";
            /*Debug.Log($"[PlayerDoubleJumpState.SelectDoubleJumpAnimation] " +
                      $"LocomotionState={data.CurrentLocomotionState}, IsHandsEmpty={isHandsEmpty}, " +
                      $"SelectedClip={clipName}, JumpForce={_jumpForce}");*/
        }

        private void PerformJumpPhysics()
        {
            data.VerticalVelocity = _jumpForce;
            data.IsGrounded = false;
        }

        protected override void UpdateStateLogic()
        {
            // 防抖：起跳后至少过 0.2s 才开始检测落地
            if (!_canCheckLand && AnimFacade.CurrentTime > 0.2f)
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
            player.MotionDriver.UpdateMotion(null, 0f);
        }

        public override void Exit()
        {
            AnimFacade.ClearOnEndCallback();
            _clipData = null;
        }
    }
}
