using Characters.Player.Core;
using Characters.Player.Data;
using UnityEngine;

namespace Characters.Player.States
{
    [CreateAssetMenu(fileName = "RollInterceptor", menuName = "Player/Interceptors/Roll")]
    public class RollInterceptorSO : StateInterceptorSO
    {
        public override bool TryIntercept(PlayerController player, PlayerBaseState currentState, out PlayerBaseState nextState)
        {
            nextState = null;
            var data = player.RuntimeData;

            if (data.WantsToRoll)
            {
                data.NextStatePlayOptions = data.LastLocomotionState == LocomotionState.Sprint ?
                    player.Config.LocomotionAnims.FadeInMoveDodgeOptions :
                    player.Config.LocomotionAnims.FadeInQuickDodgeOptions;

                nextState = player.StateRegistry.GetState<PlayerRollState>();
                return true;
            }

            return false;
        }
    }
}