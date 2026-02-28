using Characters.Player.Core;
using UnityEngine;

namespace Characters.Player.States
{
    [CreateAssetMenu(fileName = "LandInterceptor", menuName = "Player/Interceptors/Land")]
    public class LandInterceptorSO : StateInterceptorSO
    {
        public override bool TryIntercept(PlayerController player, PlayerBaseState currentState, out PlayerBaseState nextState)
        {
            nextState = null;
            var data = player.RuntimeData;

            // 原逻辑：刚落地且 FallHeightLevel>0，且不在 LandState
            if (data.JustLanded && data.FallHeightLevel > 0 && currentState is not PlayerLandState)
            {
                nextState = player.LandState;
                return true;
            }

            return false;
        }
    }
}