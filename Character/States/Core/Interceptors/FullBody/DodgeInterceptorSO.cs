using UnityEngine;

namespace BBBNexus
{
    // 闪避全局拦截器 
    // 负责检测闪避输入 并根据前一个运动状态选择对应的闪避淡入参数
    [CreateAssetMenu(fileName = "DodgeInterceptor", menuName = "BBBNexus/Player/Interceptors/Dodge")]
    public class DodgeInterceptorSO : StateInterceptorSO
    {
        public override bool TryIntercept(BBBCharacterController player, PlayerBaseState currentState, out PlayerBaseState nextState)
        {
            nextState = null;
            var data = player.RuntimeData;

            // 检测闪避输入 根据前一个运动状态选择淡入参数
            if (data.WantsToDodge)
            {
                // 从冲刺状态闪避选择不同的参数 更加剧烈的动作
                data.NextStatePlayOptions = data.LastLocomotionState == LocomotionState.Sprint ?
                    player.Config.LocomotionAnims.FadeInMoveDodgeOptions :
                    player.Config.LocomotionAnims.FadeInQuickDodgeOptions;

                nextState = player.StateRegistry.GetState<PlayerDodgeState>();
                return true;
            }

            return false;
        }
    }
}