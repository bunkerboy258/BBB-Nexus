using Animancer;
using Characters.Player.Layers;
using UnityEngine;

namespace Characters.Player.States.UpperBody
{
    /// <summary>
    /// 上半身攻击状态：播放攻击动画，动画结束后自动切回上半身空闲状态
    /// 核心逻辑：播放攻击动画并绑定结束事件，事件触发后清理引用防止重复回调
    /// </summary>
    public class UpperBodyAttackState : UpperBodyBaseState
    {
        /// <summary>
        /// 构造函数：传递玩家控制器和上半身控制器引用到基类
        /// </summary>
        /// <param name="player">玩家核心控制器</param>
        /// <param name="controller">上半身分层控制器</param>
        public UpperBodyAttackState(PlayerController p) : base(p) { }

        /// <summary>
        /// 进入状态：播放攻击动画，绑定动画结束事件（自动切回空闲状态）
        /// </summary>
        public override void Enter()
        {

        }
        protected override void UpdateStateLogic()
        {
            // 该状态逻辑完全由动画事件驱动，无需在每帧更新中处理逻辑
        }

        public override void PhysicsUpdate() { }
        public override void Exit()
        {
            // 离开状态时无需额外处理
        }
    }
}