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

        protected override void UpdateStateLogic()
        {
            // 1. 退出瞄准检测
            if (!data.IsAiming)
            {
                player.StateMachine.ChangeState(player.IdleState);
                return;
            }

            // 新增：在瞄准空闲状态也响应空中二段跳意图
            if (data.WantsDoubleJump)
            {
                player.StateMachine.ChangeState(player.DoubleJumpState);
                return;
            }

            // 2. 进入移动检测
            if (data.WantsToJump)
            {
                player.StateMachine.ChangeState(player.JumpState);
                return;
            }

            if (data.CurrentLocomotionState!=LocomotionState.Idle)
            {
                player.StateMachine.ChangeState(player.AimMoveState);
            }
        }

        public override void PhysicsUpdate()
        {
            // Use new MotionDriver API: call UpdateMotion with null clip to drive input/aim motion
            player.MotionDriver.UpdateMotion(null, 0f, player.RuntimeData.ViewYaw);
        }

        public override void Exit()
        {
            // 无需特殊清理
        }
    }
}
