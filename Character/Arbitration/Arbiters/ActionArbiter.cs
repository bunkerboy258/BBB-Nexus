using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 动作仲裁器
    /// 只读取黑板上（帧级）的最高优先级动作请求 并决定是否应用 
    /// </summary>
    public class ActionArbiter
    {
        private readonly BBBCharacterController _player;
        private readonly PlayerRuntimeData _data;

        public ActionArbiter(BBBCharacterController player)
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

            if (_data.StatusControl.IsActive && _data.StatusControl.BlocksAction)
                return;

            var request = _data.ActionArbitration.HighestPriorityRequest;
            int currentResistance = GetCurrentOverrideResistance();

            if (!_data.ActionControl.IsActive)
            {
                if (request.Priority <= currentResistance) return;

                _data.Override.IsActive = true;
                _data.Override.Request = request;
                _data.Override.ReturnState = _player.StateMachine.CurrentState;
                SyncActionControl(request, usesLegacyOverrideState: false);
                ApplyRequest(request);
                return;
            }

            if (request.Priority < currentResistance) return;

            if (_data.Override.IsActive &&
                _data.Override.Request.Transition == null &&
                request.Transition == null &&
                _data.Override.Request.Clip == request.Clip)
            {
                return;
            }

            _data.Override.IsActive = true;
            _data.Override.Request = request;
            SyncActionControl(request, usesLegacyOverrideState: false);
            ApplyRequest(request);
        }

        public void CancelActiveAction(bool stopAnimation = true)
        {
            if (!_data.ActionControl.IsActive && !_data.Override.IsActive)
                return;

            _player.AnimFacade?.ClearOverrideOnEndCallback();
            if (stopAnimation)
                _player.AnimFacade?.StopFullBodyAction();

            _data.Override.Clear();
            _data.ActionControl.Clear();
        }

        /// <summary>
        /// 评估当前代理状态的抗打断级别
        /// </summary>
        private int GetCurrentOverrideResistance()
        {
            var current = _player.StateMachine.CurrentState;

            if (_data.ActionControl.IsActive)
                return _data.ActionControl.Priority;

            if (current is PlayerRollState) return 100;
            if (current is PlayerDodgeState) return 80;

            return 0;
        }

        private void ApplyRequest(in ActionRequest request)
        {
            _player.AnimFacade?.ClearOverrideOnEndCallback();

            if (request.Transition != null)
                _player.AnimFacade?.PlayFullBodyActionTransition(request.Transition);
            else
                _player.AnimFacade?.PlayFullBodyAction(request.Clip, request.FadeDuration, request.Speed);

            _player.AnimFacade?.SetOverrideOnEndCallback(OnActionClipEnd);
        }

        private void OnActionClipEnd()
        {
            CancelActiveAction(stopAnimation: true);
        }

        private void SyncActionControl(in ActionRequest request, bool usesLegacyOverrideState)
        {
            _data.ActionControl.IsActive = true;
            _data.ActionControl.Priority = request.Priority;
            _data.ActionControl.BlocksLocomotion = true;
            _data.ActionControl.BlocksUpperBody = true;
            _data.ActionControl.UsesLegacyOverrideState = usesLegacyOverrideState;
        }
    }
}
