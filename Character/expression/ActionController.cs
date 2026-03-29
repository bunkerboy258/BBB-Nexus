using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// Action 控制器：响应黑板 WantsToInteract 意图，按顺序提交情境动作请求。
    /// 用于开门/开箱/爬梯子等交互动作；连招窗口内连按可推进段数。
    /// </summary>
    public sealed class ActionController
    {
        private readonly BBBCharacterController _player;
        private readonly PlayerRuntimeData _data;
        private readonly PlayerSO _config;
        private readonly InputPipeline _input;

        private int _index;
        private float _comboWindowTimer;

        // 默认优先级：保证能打断普通移动 但低于翻滚/闪避等
        private const int DefaultPriority = 25;
        // 连招窗口时长：上一击结束后 N 秒内未续招则重置段数
        private const float ComboWindowDuration = 1.5f;

        public ActionController(BBBCharacterController player)
        {
            _player = player;
            _data = player.RuntimeData;
            _config = player.Config;
            _input = player.InputPipeline;
            _index = 0;
        }

        public void Update()
        {
            if (_data == null || _config == null || _input == null) return;
            if (_config.Action == null) return;

            // 连招窗口倒计时，超时重置段数
            if (_comboWindowTimer > 0f)
            {
                _comboWindowTimer -= Time.deltaTime;
                if (_comboWindowTimer <= 0f)
                    _index = 0;
            }

            if (_data.Arbitration.BlockAction) return;
            if (!_data.WantsToInteract) return;

            _input.ConsumeInteractPressed();

            var clip = _config.Action.GetClip(_index);

            // 遇到空槽（连招末尾）：重置段数，本次不出招
            if (clip == null)
            {
                _index = 0;
                _comboWindowTimer = 0f;
                return;
            }

            _index = (_index + 1) % ActionSO.ActionCount;
            _comboWindowTimer = ComboWindowDuration;

            var req = new ActionRequest(clip, DefaultPriority, 0.15f, true);
            _player.RequestOverride(in req, flushImmediately: true);
        }
    }
}
