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

            // 原逻辑：处理 WantsToRoll，并根据上一个运动状态选择淡入参数
            if (data.WantsToRoll)
            {
                data.NextStatePlayOptions = data.LastLocomotionState == LocomotionState.Sprint ?
                    player.Config.LocomotionAnims.FadeInMoveDodgeOptions :
                    player.Config.LocomotionAnims.FadeInQuickDodgeOptions;

                nextState = player.RollState;
                return true;
            }

            return false;
        }
    }
}