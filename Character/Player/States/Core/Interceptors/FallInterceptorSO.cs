using Characters.Player.Core;
using UnityEngine;

namespace Characters.Player.States
{
    // 下落全局拦截器 
    // 负责检测下落意图 当空中时间过长且向下速度大于配置值时自动触发下落动画 优先级较高
    [CreateAssetMenu(fileName = "FallInterceptor", menuName = "BBBNexus/Player/Interceptors/Fall")]
    public class FallInterceptorSO : StateInterceptorSO
    {
        public override bool TryIntercept(PlayerController player, PlayerBaseState currentState, out PlayerBaseState nextState)
        {
            nextState = null;
            var data = player.RuntimeData;
            var config = player.Config;

            // 检测下落意图和垂直速度
            if (
                data.WantsToFall &&
                data.VerticalVelocity < config.Core.FallVerticalVelocityThreshold &&
                currentState is not PlayerFallState &&
                currentState is not PlayerVaultState)
            {
                data.NextStatePlayOptions = config.LocomotionAnims.FadeInFallOptions;
                nextState = player.StateRegistry.GetState<PlayerFallState>();
                return true;
            }

            return false;
        }
    }
}