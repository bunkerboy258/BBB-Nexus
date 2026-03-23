using UnityEngine;

namespace BBBNexus
{
    public abstract class StateInterceptorSO : ScriptableObject
    {
        public abstract bool TryIntercept(PlayerController player, PlayerBaseState currentState, out PlayerBaseState nextState);
    }
}