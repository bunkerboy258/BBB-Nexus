using System;
using Animancer;
using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 表情控制器 - 管理面部表情的播放、混合与生命周期
    /// 支持基础表情循环、事件驱动的瞬时表情、快捷键表情等
    /// </summary>
    public sealed class FacialController
    {
        private const int FacialLayer = 2;

        private readonly BBBCharacterController _player;
        private readonly PlayerSO _config;
        private readonly PlayerRuntimeData _data;

        private ClipTransition _baseExpression;

        private float _unlockTime;
        private PlayerFacialEvent _lockedEvent;
        private float _fallbackReturnTime;
        private bool _initialized;

        public FacialController(BBBCharacterController player)
        {
            _player = player;
            _config = player.Config;
            _data = player.RuntimeData;

            _baseExpression = _config != null && _config.Emj != null ? _config.Emj.BaseExpression : null;
        }

        /// <summary>
        /// 每帧更新 - 检查表情事件请求并播放对应动画
        /// </summary>
        public void Update()
        {
            if (_player == null || _player.AnimFacade == null) return;

            // 初始化
            if (!_initialized)
            {
                _initialized = true;

                if (_config != null && _config.Core != null)
                    _player.AnimFacade.SetLayerMask(FacialLayer, _config.Core.FacialMask);

                PlayBaseExpression(0f);
            }

            // 检查表情系统是否被仲裁阻断
            if (_config == null || _config.Emj == null) return;

            if (_data != null && _data.Arbitration.BlockFacial)
            {
                _player.AnimFacade.SetLayerWeight(FacialLayer, 0f);
                return;
            }

            _player.AnimFacade.SetLayerWeight(FacialLayer, 1f);

            // 检查表情锁定是否过期 如果是则回归基础表情
            if (_fallbackReturnTime > 0f && Time.time >= _fallbackReturnTime)
            {
                ClearLock(clearCallback: true);
                PlayBaseExpression(0.2f);
            }

            // 如果仍在锁定期间 不处理新请求
            if (Time.time < _unlockTime)
                return;

            // 获取本帧表情事件请求
            var evt = _data != null ? _data.FacialEventRequest : PlayerFacialEvent.None;
            if (evt == PlayerFacialEvent.None)
                return;

            // 避免同一表情重复播放
            if (_lockedEvent == evt && Time.time < _unlockTime + 0.0001f)
                return;

            // 尝试从配置获取对应的动画并播放
            if (_config.Emj.TryGet(evt, out var transition))
                PlayTransientExpression(evt, transition, 0.1f);
        }

        /// <summary>
        /// 播放瞬时表情动画 - 设置锁定时间并注册自动回归基础表情的回调
        /// </summary>
        private void PlayTransientExpression(PlayerFacialEvent evt, ClipTransition transition, float fade)
        {
            if (transition == null || transition.Clip == null) return;

            _lockedEvent = evt;

            // 计算动画时长 用作锁定时长基准
            var len = transition.Clip.length;
            if (len <= 0f) len = 0.25f;

            // 锁定时间 = 当前时间 + 动画时长（减去淡出裕度）
            _unlockTime = Time.time + Mathf.Max(0.05f, len - 0.02f);

            // 回归基础表情的时间 = 锁定时间 + 淡出延迟
            _fallbackReturnTime = Time.time + len + 0.1f;

            // 构造动画播放选项
            var options = new AnimPlayOptions
            {
                Layer = FacialLayer,
                FadeDuration = fade,
                Speed = -1f,
                NormalizedTime = -1f,
            };

            // 播放表情动画
            _player.AnimFacade.PlayTransition(transition, options);

            // 注册动画结束回调 用于过期时自动回归
            _player.AnimFacade.SetOnEndCallback(() =>
            {
                if (Time.time < _unlockTime - 0.01f) return;

                ClearLock(clearCallback: false);
                PlayBaseExpression(0.2f);
            }, FacialLayer);
        }

        /// <summary>
        /// 清理表情锁定状态
        /// </summary>
        private void ClearLock(bool clearCallback)
        {
            _lockedEvent = PlayerFacialEvent.None;
            _unlockTime = 0f;
            _fallbackReturnTime = 0f;

            if (clearCallback)
            {
                _player.AnimFacade.ClearOnEndCallback(FacialLayer);
            }
        }

        /// <summary>
        /// 播放基础表情 - 作为常态循环表情
        /// </summary>
        private void PlayBaseExpression(float fade = 0.25f)
        {
            if (_player == null || _player.AnimFacade == null) return;
            if (_baseExpression == null || _baseExpression.Clip == null) return;

            var options = new AnimPlayOptions
            {
                Layer = FacialLayer,
                FadeDuration = fade,
                Speed = -1f,
                NormalizedTime = -1f,
            };

            _player.AnimFacade.PlayTransition(_baseExpression, options);
            _player.AnimFacade.ClearOnEndCallback(FacialLayer);
        }
    }
}
