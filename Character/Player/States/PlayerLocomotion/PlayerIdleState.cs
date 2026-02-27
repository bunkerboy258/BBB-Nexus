using UnityEngine;
using Characters.Player.Data;
using Characters.Player.Animation;

namespace Characters.Player.States
{
    /// <summary>
    /// 玩家基础空闲状态类
    /// 职责：
    /// 1. 播放角色的空闲动画，保证动画过渡平滑；
    /// 2. 检测玩家的输入意图（长按移动/短按转身），并触发对应状态切换。
    /// </summary>
    public class PlayerIdleState : PlayerBaseState
    {
        /// <summary>
        /// 状态构造函数：将玩家核心控制器传递到基类
        /// </summary>
        public PlayerIdleState(PlayerController player) : base(player) { }

        #region State Lifecycle（状态生命周期）

        /// <summary>
        /// 进入状态：播放空闲动画，设置平滑淡入时长避免动画跳变
        /// </summary>
        public override void Enter()
        {
            ChooseOptionsAndPlay(config.LocomotionAnims.IdleAnim);
        }

        /// <summary>
        /// 更新状态逻辑：检测移动/转身意图，触发状态切换
        /// （检测到意图后return，避免重复判断）
        /// </summary>
        protected override void UpdateStateLogic()
        {
            if (data.CurrentLocomotionState != LocomotionState.Idle)
            {
                switch (data.CurrentLocomotionState)
                {
                    case LocomotionState.Walk:
                        data.NextStatePlayOptions = config.LocomotionAnims.FadeInWalkStartOptions;
                        break;
                    case LocomotionState.Jog:
                        data.NextStatePlayOptions = config.LocomotionAnims.FadeInRunStartOptions;
                        break;
                    case LocomotionState.Sprint:
                        data.NextStatePlayOptions = config.LocomotionAnims.FadeInSprintStartOptions;
                        break;
                    default:
                        data.NextStatePlayOptions = AnimPlayOptions.Default;
                        break;
                }

                player.StateMachine.ChangeState(player.MoveStartState);
                return;
            }

            if (data.WantsToJump)
            {
                //Debug.Log("IdleState detected jump input, transitioning to JumpState");
                data.NextStatePlayOptions = config.LocomotionAnims.FadeInJumpOptions;
                player.StateMachine.ChangeState(player.JumpState);
            }
        }

        /// <summary>
        /// 物理更新：空闲状态仍需驱动运动逻辑（防止角色浮空/接地状态异常）
        /// </summary>
        public override void PhysicsUpdate()
        {
            // 即使在空闲状态，也需要调用MotionDriver更新运动（重力、接地检测等）
            // 避免角色因未更新物理导致浮空、碰撞检测失效
            player.MotionDriver.UpdateMotion();
        }

        /// <summary>
        /// 退出状态：空闲状态退出时无额外清理逻辑
        /// </summary>
        public override void Exit()
        {
            // 退出空闲状态时无需清理资源，仅触发状态切换即可
        }

        #endregion

    }
}
