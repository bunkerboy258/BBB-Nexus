using UnityEngine;

namespace BBBNexus
{
    public abstract class UpperBodyInterceptorSO : ScriptableObject
    {
        public abstract bool TryIntercept(PlayerController player, UpperBodyBaseState currentState, out UpperBodyBaseState nextState);
    }
}