using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// Action 控制器：响应黑板 WantsToAction 意图 循环提交“接管动作请求”
    /// 目前仅实现了基础功能：每次触发都会把索引推进 0....7 并提交一个 ActionRequest 允许互相打断
    /// </summary>
    public sealed class ActionController
    {
        private readonly BBBCharacterController _player;
        private readonly PlayerRuntimeData _data;
        private readonly PlayerSO _config;
        private readonly InputPipeline _input;

        private int _index;

        // 默认优先级：保证能打断普通移动 但低于翻滚/闪避等
        private const int DefaultPriority = 25;

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

            if (_data.Arbitration.BlockAction) return;

            if (!_data.WantsToAction) return;

            // 消费输入缓存
            _input.ConsumeActionPressed();

            var clip = _config.Action.GetClip(_index);
            _index = (_index + 1) % ActionSO.ActionCount;

            if (clip == null) return;

            // 发送接管请求：flushImmediately = true 确保本帧进入 OverrideState
            var req = new ActionRequest(clip, DefaultPriority, 0.15f, true);
            _player.RequestOverride(in req, flushImmediately: true);
        }
    }
}
