using Characters.Player.States;
using UnityEngine;

namespace Characters.Player.Core
{
    /// <summary>
    /// 上半身专属全局打断器基类
    /// </summary>
    public abstract class UpperBodyInterceptorSO : ScriptableObject
    {
        public abstract bool TryIntercept(PlayerController player, UpperBodyBaseState currentState, out UpperBodyBaseState nextState);
    }
}