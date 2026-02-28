using Characters.Player.States;
using UnityEngine;

namespace Characters.Player.Core
{
    public abstract class StateInterceptorSO : ScriptableObject
    {
        public abstract bool TryIntercept(PlayerController player, PlayerBaseState currentState, out PlayerBaseState nextState);
    }
}