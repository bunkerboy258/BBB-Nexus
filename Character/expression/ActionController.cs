using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// Action 控制器：响应黑板 WantsToInteract 意图，查找附近交互对象并执行。
    /// 不再把 E 键直接映射到 PlayerSO.Action 的占位动作序列。
    /// </summary>
    public sealed class ActionController
    {
        private readonly BBBCharacterController _player;
        private readonly PlayerRuntimeData _data;
        private readonly InputPipeline _input;
        private readonly PlayerInteractionSensor _sensor;

        public ActionController(BBBCharacterController player)
        {
            _player = player;
            _data = player.RuntimeData;
            _input = player.InputPipeline;
            _sensor = player.InteractionSensor;
        }

        public void Update()
        {
            if (_data == null || _input == null) return;
            if (_player.CharacterArbiter != null && _player.CharacterArbiter.IsActionBlocked()) return;
            if (!_data.WantsToInteract) return;

            _input.ConsumeInteractPressed();
            if (_sensor == null || !_sensor.HasInteractable)
                return;

            IInteractable interactable = _sensor.CurrentInteractable;
            if (interactable.TryGetInteractionRequest(_player, out ActionRequest request))
                _player.RequestOverride(in request, flushImmediately: true);

            interactable.Interact(_player);
        }
    }
}
