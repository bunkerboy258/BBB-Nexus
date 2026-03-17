using UnityEngine;
using Characters.Player.Arbitration;
using Characters.Player.Data;

namespace Characters.Player.Arbitration.Arbiters
{
    /// <summary>
    /// 动作仲裁器
    /// 负责接收所有外部/内部的高权限动作请求，仲裁优先级，并强制驱动状态机
    /// </summary>
    public class ActionArbiter
    {
        private readonly PlayerController _player;

        // 缓存本帧最高优先级的请求
        private bool _hasPendingRequest;
        private ActionRequest _highestPriorityRequest;

        public ActionArbiter(PlayerController player)
        {
            _player = player;
        }

        /// <summary>
        /// 提交动作请求
        /// </summary>
        public void SubmitRequest(in ActionRequest request, bool flushImmediately = false)
        {
            // 挑出本帧优先级最高的请求
            if (!_hasPendingRequest || request.Priority > _highestPriorityRequest.Priority)
            {
                _highestPriorityRequest = request;
                _hasPendingRequest = true;
            }

            if (flushImmediately)
            {
                Arbitrate();
            }
        }

        /// <summary>
        /// 核心仲裁管线 (在 Update 最早期执行)
        /// </summary>
        public void Arbitrate()
        {
            if (!_hasPendingRequest) return;

            int currentResistance = GetCurrentOverrideResistance();

            // 仲裁：如果请求的优先级大于当前状态的霸体/抗性，则强切！
            if (_highestPriorityRequest.Priority > currentResistance)
            {
                _player.RuntimeData.Override.IsActive = true;
                _player.RuntimeData.Override.Request = _highestPriorityRequest;
                _player.RuntimeData.Override.ReturnState = _player.StateMachine.CurrentState;

                var state = _player.StateRegistry.GetState<Characters.Player.States.Override.OverrideState>();
                _player.StateMachine.ChangeState(state);
            }

            // 仲裁结束，清空本帧缓存
            _hasPendingRequest = false;
        }

        /// <summary>
        /// 评估当前状态的“霸体抗打断”级别
        /// </summary>
        private int GetCurrentOverrideResistance()
        {
            var current = _player.StateMachine.CurrentState;

            if (current is Characters.Player.States.Override.OverrideState s)
                return s.CurrentPriority;

            // 翻滚时的无敌帧，极难被打断
            if (current is Characters.Player.States.PlayerRollState) return 100;
            if (current is Characters.Player.States.PlayerDodgeState) return 80;

            // 普通跑跳状态毫无抗性
            return 0;
        }
    }
}