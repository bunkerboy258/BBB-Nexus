using Characters.Player.Core;
using Characters.Player.Data;
using UnityEngine;

namespace Characters.Player.States
{
    [CreateAssetMenu(fileName = "AimInterceptor", menuName = "Player/Interceptors/Aim")]
    public class AimInterceptorSO : StateInterceptorSO
    {
        public override bool TryIntercept(PlayerController player, PlayerBaseState currentState, out PlayerBaseState nextState)
        {
            nextState = null;
            var data = player.RuntimeData;

            // 原逻辑：全局瞄准切换保护机制
            if (data.IsAiming)
            {
                // 如果当前已经在瞄准状态（AimIdle/AimMove），让状态正常运行
                if (currentState is PlayerAimIdleState || currentState is PlayerAimMoveState)
                    return false;

                // 保护动作完整性：如果处于跳跃、二段跳、落地、翻越等状态，不在此处强行拦截
                if (currentState is PlayerJumpState ||
                    currentState is PlayerDoubleJumpState ||
                    currentState is PlayerLandState ||
                    currentState is PlayerVaultState)
                    return false;

                // 根据当前运动状态决定是原地瞄准还是移动瞄准
                nextState = data.CurrentLocomotionState == LocomotionState.Idle ?
                    player.AimIdleState : player.AimMoveState;

                return true;
            }

            return false;
        }
    }
}