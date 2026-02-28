using UnityEngine;
using Characters.Player.Data;
using Characters.Player.Animation;

namespace Characters.Player.States
{
    /// <summary>
    /// 玩家下落状态。
    /// 职责：
    /// - 进入时播放 LocomotionAnimSetSO 中的 FallAnim
    /// - 持续检测落地事件，当检测到落地时立即转移到 LandState
    /// - 应用物理驱动（重力等）
    /// </summary>
    public class PlayerFallState : PlayerBaseState
    {
        public PlayerFallState(PlayerController player) : base(player) { }

        #region State Lifecycle

        /// <summary>
        /// 进入状态：播放下落动画
        /// </summary>
        public override void Enter()
        {
            ChooseOptionsAndPlay(config.LocomotionAnims.FallAnim);
        }

        /// <summary>
        /// 更新状态逻辑：持续检测落地事件，防止卡死在下落状态
        /// </summary>
        protected override void UpdateStateLogic()
        {
            // 检测是否已经落地：刚落地且有下落高度
            if (data.IsGrounded)
            {
                player.StateMachine.ChangeState(player.LandState);
                return;
            }
        }

        /// <summary>
        /// 物理更新：继续应用物理驱动（重力等）
        /// </summary>
        public override void PhysicsUpdate()
        {
            player.MotionDriver.UpdateMotion();
        }

        /// <summary>
        /// 退出状态：清理资源
        /// </summary>
        public override void Exit()
        {
            // 无需额外清理逻辑
        }

        #endregion
    }
}
