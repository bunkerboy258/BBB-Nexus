using UnityEngine;

namespace BBBNexus
{
    // 状态拦截器基类 
    // 所有拦截器都继承自这个类 通过重写 TryIntercept 方法来实现自己的拦截逻辑
    // 拦截器是状态转移的守门人 在状态逻辑执行之前有权力强制切换状态
    public abstract class StateInterceptorSO : ScriptableObject
    {
        // 尝试拦截当前状态 如果返回 true 则强制切换到 nextState
        // player 玩家控制器 currentState 当前状态 nextState 输出 要切换的下一个状态
        public abstract bool TryIntercept(PlayerController player, PlayerBaseState currentState, out PlayerBaseState nextState);
    }
}