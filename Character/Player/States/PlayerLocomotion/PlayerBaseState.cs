using Core.StateMachine;
using Characters.Player.Data;
using UnityEngine;

namespace Characters.Player.States
{
    /// <summary>
    /// 玩家基础状态抽象类
    /// - 封装通用引用与工具方法
    /// - 统一 LogicUpdate 执行顺序：先强制转移，再状态自身逻辑
    /// </summary>
    public abstract class PlayerBaseState : BaseState
    {
        protected PlayerController player;
        protected PlayerRuntimeData data;
        protected PlayerSO config;

        protected PlayerBaseState(PlayerController player)
        {
            this.player = player;
            this.data = player.RuntimeData;
            this.config = player.Config;
        }


        /// <summary>
        /// 统一封闭 LogicUpdate：
        /// 1) CheckInterrupts：全局强制转移（高优先级）
        /// 2) UpdateStateLogic：状态自身逻辑
        /// </summary>
        public sealed override void LogicUpdate()
        {
            if (CheckInterrupts()) return;
            UpdateStateLogic();
        }

        /// <summary>
        /// 全局强制转移检测。默认实现处理：
        /// - 刚落地（JustLanded）且 FallHeightLevel>0 -> LandState
        /// - IsAiming 全局优先转到 AimMove/AimIdle（仅当不是正在 Vault/Land）
        /// 子类可 override 返回 true 来阻止默认行为（例如 Vault/Land 状态）。
        /// </summary>
        protected virtual bool CheckInterrupts()
        {
            // 1) 刚落地事件 -> 进入 LandState（由 LandState 决定后续切换）
            if (data.JustLanded && data.FallHeightLevel > 0&&this is not PlayerLandState)
            {
                data.LandFadeInTime = 0.3f;
                player.StateMachine.ChangeState(player.LandState);
                return true;
            }

            if(data.WantsToVault&&this is not PlayerVaultState)
            {
                player.StateMachine.ChangeState(player.VaultState);
                return true;
            }
            // 2) 全局瞄准切换：只在“非瞄准状态”时做一次性切换，避免每帧打断 Aim 状态自身逻辑。
            if (data.IsAiming)
            {
                // 如果当前已经在瞄准状态（AimIdle/AimMove），让状态正常运行。
                if (this is PlayerAimIdleState || this is PlayerAimMoveState)
                    return false;

                // 新增：如果处于跳跃、二段跳、落地、翻越等状态，不在此处强行拦截，保证动作表现完整。
                if (this is PlayerJumpState || this is PlayerDoubleJumpState || this is PlayerLandState || this is PlayerVaultState)
                    return false;

                player.StateMachine.ChangeState(data.CurrentLocomotionState==LocomotionState.Idle ? player.AimIdleState : player.AimMoveState);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 状态自身的正常逻辑。
        /// 之前写在 LogicUpdate 里的内容应迁移到这里。
        /// </summary>
        protected abstract void UpdateStateLogic();
    }
}