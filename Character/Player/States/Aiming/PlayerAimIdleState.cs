using UnityEngine;
using Characters.Player.Data;

namespace Characters.Player.States
{
    public class PlayerAimIdleState : PlayerBaseState
    {
        public PlayerAimIdleState(PlayerController player) : base(player) { }

        public override void Enter()
        {
            // 1. 播放瞄准站立动画 (通常是一个循环的 Pose)
            // 假设 PlayerSO 中有 AimIdleAnim
            if (config.AimIdleAnim != null)
            {
                player.Animancer.Layers[0].Play(config.AimIdleAnim, 0.2f);
            }

            // 2. 确保 MotionDriver 不进行水平移动
            // 我们可以在 PhysicsUpdate 里调用无参版本，或者在这里什么都不做
        }

        public override void LogicUpdate()
        {
            // 1. 退出瞄准检测
            if (!data.IsAiming)
            {
                player.StateMachine.ChangeState(player.IdleState);
                return;
            }

            // 2. 进入移动检测
            else if (data.WantsToJump)
            {
                player.StateMachine.ChangeState(player.JumpState);
            }

            else if (HasMoveInput)
            {
                player.StateMachine.ChangeState(player.AimMoveState);
            }
        }

        public override void PhysicsUpdate()
        {
            // 3. 驱动转身
            // 即使站着不动，也应该能转动身体跟随相机
            // 我们可以复用 MotionDriver 的 Aim 模式，它会自动处理旋转
            player.MotionDriver.UpdateAimMotion(1f);
        }

        public override void Exit()
        {
            // 无需特殊清理
        }
    }
}
