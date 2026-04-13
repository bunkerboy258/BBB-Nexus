using UnityEngine;

namespace BBBNexus
{
    // 上半身进入不可用拦截器 
    // 当下半身处于翻越 下落 翻滚等状态时 强制上半身进入不可用状态 禁用控制权
    [CreateAssetMenu(fileName = "EnterUnavailableInterceptor", menuName = "BBBNexus/Player/Interceptors/UpperBody/EnterUnavailable")]
    public class EnterUnavailableInterceptorSO : UpperBodyInterceptorSO
    {
        public override bool TryIntercept(BBBCharacterController player, UpperBodyBaseState currentState, out UpperBodyBaseState nextState)
        {
            nextState = null;

            // 1. 如果当前已经在 Unavailable 状态 不要重复进入
            if (currentState != null && currentState is UpperBodyUnavailableState)
            {
                return false;
            }

            // 2. 获取下半身的当前状态 判断是否需要禁用上半身
            var playerbasestate = player.StateMachine.CurrentState;

            // 3. 进行判断 如果是 Vault Roll 状态 禁用上半身（Fall 不锁，允许持枪下落）
            if (playerbasestate is PlayerVaultState || playerbasestate is PlayerRollState)
            {
                // 获取不可用 Unavailable 状态
                nextState = player.UpperBodyCtrl.StateRegistry.GetState<UpperBodyUnavailableState>();
                return true;
            }

            return false;
        }
    }
}