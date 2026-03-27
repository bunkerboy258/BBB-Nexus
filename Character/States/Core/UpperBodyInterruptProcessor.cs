namespace BBBNexus
{
    // 上半身拦截处理器 它负责管理上半身子状态机的优先级切换 
    // 职责 遍历上半身专属的拦截器 根据优先级决定是否切换上半身状态
    public class UpperBodyInterruptProcessor
    {
        private readonly BBBCharacterController _player;
        private readonly UpperBodyController _upperBody;

        public UpperBodyInterruptProcessor(BBBCharacterController player, UpperBodyController upperBody)
        {
            _player = player;
            _upperBody = upperBody;
        }

        // 尝试处理上半身拦截 
        // 依次遍历 PlayerBrainSO 中的上半身拦截器集合 如果有拦截器返回 true 就切换状态
        public bool TryProcessInterrupts(UpperBodyBaseState currentState)
        {
            if (_player.Config == null || _player.Config.Brain == null || _player.Config.Brain.UpperBodyInterceptors == null)
                return false;

            var pipeline = _player.Config.Brain.UpperBodyInterceptors;
            for (int i = 0; i < pipeline.Count; i++)
            {
                var interceptor = pipeline[i];
                if (interceptor != null && interceptor.TryIntercept(_player, currentState, out var nextState))
                {
                    _upperBody.StateMachine.ChangeState(nextState);
                    return true;
                }
            }
            return false;
        }
    }
}