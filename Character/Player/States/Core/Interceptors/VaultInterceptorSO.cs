using Characters.Player.Core;
using UnityEngine;

namespace Characters.Player.States
{
    [CreateAssetMenu(fileName = "VaultInterceptor", menuName = "Player/Interceptors/Vault")]
    public class VaultInterceptorSO : StateInterceptorSO
    {
        public override bool TryIntercept(PlayerController player, PlayerBaseState currentState, out PlayerBaseState nextState)
        {
            nextState = null;
            var data = player.RuntimeData;

            // 原逻辑：处理 WantsToVault，且不在 VaultState
            if (data.WantsToVault && currentState is not PlayerVaultState)
            {
                nextState = player.VaultState;
                return true;
            }

            return false;
        }
    }
}