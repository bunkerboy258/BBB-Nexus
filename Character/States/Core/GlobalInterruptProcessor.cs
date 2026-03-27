namespace BBBNexus
{
    // 全局拦截处理器 它负责在意图管线之后执行全局优先级的状态转移 
    // 职责 包装高优先级的拦截器集合 在状态逻辑之前检查是否需要强行切换状态
    public class GlobalInterruptProcessor
    {
        private readonly BBBCharacterController _player;

        public GlobalInterruptProcessor(BBBCharacterController player)
        {
            _player = player;
        }

        // 尝试处理全局拦截 
        // 依次遍历 PlayerBrainSO 中的拦截器集合 如果有拦截器返回 true 就切换状态并结束检测
        public bool TryProcessInterrupts(PlayerBaseState currentState)
        {
            // 如果没有配置全局拦截器 直接返回
            if (_player.Config == null || _player.Config.Brain == null || _player.Config.Brain.GlobalInterceptors == null)
                return false;

            // 遍历拦截器管道
            var pipeline = _player.Config.Brain.GlobalInterceptors;
            for (int i = 0; i < pipeline.Count; i++)
            {
                var interceptor = pipeline[i];
                if (interceptor != null && interceptor.TryIntercept(_player, currentState, out var nextState))
                {
                    _player.StateMachine.ChangeState(nextState);
                    return true;
                }
            }

            return false;
        }
    }
}