using Characters.Player.Core;
using UnityEngine;

namespace Characters.Player.States
{
    [CreateAssetMenu(fileName = "FallInterceptor", menuName = "Player/Interceptors/Fall")]
    public class FallInterceptorSO : StateInterceptorSO
    {
        public override bool TryIntercept(PlayerController player, PlayerBaseState currentState, out PlayerBaseState nextState)
        {
            nextState = null;
            var data = player.RuntimeData;

            // 原逻辑：WantsToFall 为真 且当前不在 FallState 和 VaultState
            if (data.WantsToFall && currentState is not PlayerFallState && currentState is not PlayerVaultState)
            {
                data.NextStatePlayOptions = player.Config.LocomotionAnims.FadeInFallOptions;
                nextState = player.StateRegistry.GetState<PlayerFallState>();
                return true;
            }

            return false;
        }
    }
}