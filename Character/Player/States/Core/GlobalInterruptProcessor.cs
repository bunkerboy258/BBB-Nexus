using Characters.Player.Core;
using Characters.Player.States;

namespace Characters.Player.States
{
    /// <summary>
    /// 全局打断处理器
    /// 职责：封装打断管线的遍历与触发逻辑，保持状态机和控制器的轻量化。
    /// </summary>
    public class GlobalInterruptProcessor
    {
        private readonly PlayerController _player;

        public GlobalInterruptProcessor(PlayerController player)
        {
            _player = player;
        }

        /// <summary>
        /// 尝试处理全局打断。
        /// </summary>
        /// <param name="currentState">当前正在运行的状态</param>
        /// <returns>如果发生了状态打断/切换，返回 true</returns>
        public bool TryProcessInterrupts(PlayerBaseState currentState)
        {
            // 如果没有配置管线 跳过
            if (_player.Config == null || _player.Config.Brain == null || _player.Config.Brain.GlobalInterceptors == null)
                return false;

            // 遍历配置在 PlayerBrainSO 中的打断器列表
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