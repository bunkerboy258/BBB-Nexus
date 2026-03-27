using UnityEngine;

namespace BBBNexus
{
    // 上半身退出不可用拦截器 
    // 当下半身脱离翻越 下落 翻滚等状态后 恢复上半身的控制权 回到正常状态
    [CreateAssetMenu(fileName = "ExitUnavailableInterceptor", menuName = "BBBNexus/Player/Interceptors/UpperBody/ExitUnavailable")]
    public class ExitUnavailableInterceptorSO : UpperBodyInterceptorSO
    {
        public override bool TryIntercept(BBBCharacterController player, UpperBodyBaseState currentState, out UpperBodyBaseState nextState)
        {
            nextState = null;

            // 1. 如果当前不是 Unavailable 状态 不需要处理
            if (currentState == null || currentState is not UpperBodyUnavailableState)
            {
                return false;
            }

            // 2. 获取下半身状态 如果还在 Unavailable 的触发条件中 继续保持不可用
            var playerBaseState = player.StateMachine.CurrentState;

            // 如果还在 Vault Fall Roll 中 不能退出 Unavailable
            if (playerBaseState is PlayerVaultState || playerBaseState is PlayerFallState || playerBaseState is PlayerRollState)
            {
                return false;
            }

            // 3. 根据当前是否装备物品 决定回到 HoldItem 还是 Empty
            if (player.RuntimeData != null && player.RuntimeData.CurrentItem != null)
            {
                nextState = player.UpperBodyCtrl.StateRegistry.GetState<UpperBodyHoldItemState>();
            }
            else
            {
                nextState = player.UpperBodyCtrl.StateRegistry.GetState<UpperBodyEmptyState>();
            }

            return true;
        }
    }
}