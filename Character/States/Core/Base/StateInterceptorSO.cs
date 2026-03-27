using UnityEngine;

namespace BBBNexus
{
    public abstract class StateInterceptorSO : ScriptableObject
    {
        public abstract bool TryIntercept(BBBCharacterController player, PlayerBaseState currentState, out PlayerBaseState nextState);
    }
}