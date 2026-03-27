using UnityEngine;

namespace BBBNexus
{
    // 玩家下落状态 
    // 负责播放下落动画 持续应用重力 检测落地时机切换到LandState 
    public class PlayerFallState : PlayerBaseState
    {
        public PlayerFallState(BBBCharacterController player) : base(player) { }

        // 进入状态 播放下落动画
        public override void Enter()
        {
            ChooseOptionsAndPlay(config.LocomotionAnims.FallAnim);
        }

        // 状态逻辑 检测落地事件并切换到LandState
        protected override void UpdateStateLogic()
        {
            // 检查是否已经接地 接地就立即切到落地状态
            if (data.IsGrounded)
            {
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerLandState>());
                return;
            }
        }

        // 物理更新 持续应用重力与运动逻辑
        public override void PhysicsUpdate()
        {
            player.MotionDriver.UpdateMotion();
        }

        // 退出状态 无额外清理逻辑
        public override void Exit()
        {
        }
    }
}
