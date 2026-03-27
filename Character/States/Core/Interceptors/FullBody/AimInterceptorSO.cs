using UnityEngine;

namespace BBBNexus
{
    // 瞄准全局拦截器 
    // 负责瞄准模式的全局启用与禁用 优先级在运动状态之上 
    // 按住右键时强制切到瞄准状态 松开时回到普通移动
    [CreateAssetMenu(fileName = "AimInterceptor", menuName = "BBBNexus/Player/Interceptors/Aim")]
    public class AimInterceptorSO : StateInterceptorSO
    {
        public override bool TryIntercept(BBBCharacterController player, PlayerBaseState currentState, out PlayerBaseState nextState)
        {
            nextState = null;
            var data = player.RuntimeData;

            // 瞄准模式的全局切换保护机制
            if (data.IsAiming)
            {
                // 如果当前已经在瞄准状态 AimIdle AimMove 让状态正常运行 不拦截
                if (currentState is PlayerAimIdleState || currentState is PlayerAimMoveState)
                    return false;

                // 保护动作完整性 如果处于跳跃 二段跳 落地 翻越等状态 不在此处强行拦截
                if (currentState is PlayerJumpState ||
                    currentState is PlayerDoubleJumpState ||
                    currentState is PlayerLandState ||
                    currentState is PlayerVaultState)
                    return false;

                // 根据当前运动状态决定是原地瞄准还是移动瞄准
                nextState = data.CurrentLocomotionState == LocomotionState.Idle
                    ? player.StateRegistry.GetState<PlayerAimIdleState>()
                    : player.StateRegistry.GetState<PlayerAimMoveState>();

                return true;
            }

            return false;
        }
    }
}