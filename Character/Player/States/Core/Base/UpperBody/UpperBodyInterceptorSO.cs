using UnityEngine;

namespace BBBNexus
{
    // 上半身拦截器基类 
    // 用于管理上半身专属的状态拦截逻辑 独立于下半身的拦截器
    // 上半身和下半身各自有独立的拦截器管道 互不干扰
    public abstract class UpperBodyInterceptorSO : ScriptableObject
    {
        // 尝试拦截当前上半身状态 如果返回 true 则强制切换到 nextState
        // player 玩家控制器 currentState 当前上半身状态 nextState 输出 要切换的下一个上半身状态
        public abstract bool TryIntercept(PlayerController player, UpperBodyBaseState currentState, out UpperBodyBaseState nextState);
    }
}