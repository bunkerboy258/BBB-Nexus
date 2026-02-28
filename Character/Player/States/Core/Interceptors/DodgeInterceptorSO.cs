using Characters.Player.Core;
using Characters.Player.Data;
using Characters.Player.States;
using UnityEngine;

namespace Characters.Player.Core.Interceptors
{
    [CreateAssetMenu(fileName = "DodgeInterceptor", menuName = "Player/Interceptors/Dodge")]
    public class DodgeInterceptorSO : StateInterceptorSO
    {
        public override bool TryIntercept(PlayerController player, PlayerBaseState currentState, out PlayerBaseState nextState)
        {
            nextState = null;
            var data = player.RuntimeData;

            // 原逻辑：处理 WantsToDodge，并根据上一个运动状态选择淡入参数
            if (data.WantsToDodge)
            {
                data.NextStatePlayOptions = data.LastLocomotionState == LocomotionState.Sprint ?
                    player.Config.LocomotionAnims.FadeInMoveDodgeOptions :
                    player.Config.LocomotionAnims.FadeInQuickDodgeOptions;

                nextState = player.DodgeState;
                return true;
            }

            return false;
        }
    }
}