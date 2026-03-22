using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 动作仲裁器
    /// 只读取黑板上（帧级）的最高优先级动作请求 并决定是否应用 
    /// </summary>
    public class ActionArbiter
    {
        private readonly PlayerController _player;
        private readonly PlayerRuntimeData _data;

        public ActionArbiter(PlayerController player)
        {
            _player = player;
            _data = player.RuntimeData;
        }

        /// <summary>
        /// 核心仲裁管线  
        /// </summary>
        public void Arbitrate()
        {
            if (!_data.ActionArbitration.HasRequest) return;

            var request = _data.ActionArbitration.HighestPriorityRequest;
            int currentResistance = GetCurrentOverrideResistance();

            bool isInOverride = _player.StateMachine.CurrentState is OverrideState;

            // 不在Override时只允许更高优先级进入
            if (!isInOverride)
            {
                if (request.Priority <= currentResistance) return;

                _data.Override.IsActive = true;
                _data.Override.Request = request;
                _data.Override.ReturnState = _player.StateMachine.CurrentState;

                var state = _player.StateRegistry.GetState<OverrideState>();
                _player.StateMachine.ChangeState(state);
                return;
            }

            // 在Override时 允许同优先级或更高优先级刷新
            if (request.Priority < currentResistance) return;

            // 若clip相同则忽略避免重复Apply
            if (_data.Override.IsActive && _data.Override.Request.Clip == request.Clip) return;

            _data.Override.IsActive = true;
            _data.Override.Request = request;

            // 关键 重新触发Apply让新动画立刻播放
            var overrideState = (OverrideState)_player.StateMachine.CurrentState;
            overrideState.ForceReapply();
        }

        /// <summary>
        /// 评估当前代理状态的抗打断级别
        /// </summary>
        private int GetCurrentOverrideResistance()
        {
            var current = _player.StateMachine.CurrentState;

            if (current is OverrideState s)
                return s.CurrentPriority;

            if (current is PlayerRollState) return 100;
            if (current is PlayerDodgeState) return 80;

            return 0;
        }
    }
}