using UnityEngine;

namespace BBBNexus
{
    // TacticalMotionBase 全局拦截器。
    // 负责把 locomotion 主状态机切到战术持枪下半身基座。
    [CreateAssetMenu(fileName = "TacticalMotionInterceptor", menuName = "BBBNexus/Player/Interceptors/TacticalMotion")]
    public class TacticalMotionInterceptorSO : StateInterceptorSO
    {
        public override bool TryIntercept(BBBCharacterController player, PlayerBaseState currentState, out PlayerBaseState nextState)
        {
            nextState = null;
            var data = player.RuntimeData;

            if (data.IsTacticalStance)
            {
                if (currentState is PlayerTacticalIdleState || currentState is PlayerTacticalMoveState)
                    return false;

                // 保护动作完整性 如果处于空中/翻越状态 不在此处强行拦截
                if (currentState is PlayerJumpState ||
                    currentState is PlayerDoubleJumpState ||
                    currentState is PlayerFallState ||
                    currentState is PlayerLandState ||
                    currentState is PlayerVaultState)
                    return false;

                nextState = data.CurrentLocomotionState == LocomotionState.Idle
                    ? player.StateRegistry.GetState<PlayerTacticalIdleState>()
                    : player.StateRegistry.GetState<PlayerTacticalMoveState>();

                return true;
            }

            return false;
        }
    }
}
