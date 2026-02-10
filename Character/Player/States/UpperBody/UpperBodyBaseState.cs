using Animancer;
using Characters.Player.Data;
using Characters.Player.Layers;
using Core.StateMachine;
using UnityEngine;

namespace Characters.Player.States.UpperBody
{
    public abstract class UpperBodyBaseState : BaseState
    {
        protected PlayerController player;
        protected UpperBodyController controller;
        protected PlayerRuntimeData data;
        protected AnimancerLayer layer;

        protected UpperBodyBaseState(PlayerController player, UpperBodyController controller)
        {
            this.player = player;
            this.controller = controller;
            this.data = player.RuntimeData;
            this.layer = player.Animancer.Layers[1];
        }

        // 🔥 [核心] 封闭 LogicUpdate，强制子类实现两个分步逻辑 🔥
        public sealed override void LogicUpdate()
        {
            // 1. 优先检查强制打断 (Interruption)
            if (CheckInterrupts()) return;

            // 2. 如果没被打断，执行正常状态逻辑 (Transition)
            UpdateStateLogic();
        }

        /// <summary>
        /// 检查是否有高优先级的强制打断条件 (如翻越、装备变更)。
        /// </summary>
        /// <returns>如果切换了状态，返回 true</returns>
        protected virtual bool CheckInterrupts()
        {
            // --- 全局通用打断逻辑 ---

            // 1. 翻越 (Vault) -> Unavailable
            if (data.IsVaulting)
            {
                // 如果已经在 Unavailable 状态就不用切了 (由子类重写避免重复切)
                // 这里我们假设 BaseState 的默认行为是切过去
                controller.ChangeState(controller.UnavailableState);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 状态自身的正常逻辑 (如 Idle 检测 Aim)。
        /// 子类必须实现这个，而不是 LogicUpdate。
        /// </summary>
        protected abstract void UpdateStateLogic();

        public override void PhysicsUpdate() { }
    }
}
