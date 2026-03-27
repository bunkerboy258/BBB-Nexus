using UnityEngine;

namespace BBBNexus
{
    // 落地全局拦截器 
    // 负责检测刚刚落地事件 当下落高度等级大于0时触发落地缓冲动画
    [CreateAssetMenu(fileName = "LandInterceptor", menuName = "BBBNexus/Player/Interceptors/Land")]
    public class LandInterceptorSO : StateInterceptorSO
    {
        public override bool TryIntercept(BBBCharacterController player, PlayerBaseState currentState, out PlayerBaseState nextState)
        {
            nextState = null;
            var data = player.RuntimeData;

            // 检测刚刚落地 如果下落高度等级大于0 则切换到落地状态播放缓冲动画
            if (data.JustLanded && data.FallHeightLevel > 0 && currentState is not PlayerLandState)
            {
                nextState = player.StateRegistry.GetState<PlayerLandState>();
                return true;
            }

            return false;
        }
    }
}