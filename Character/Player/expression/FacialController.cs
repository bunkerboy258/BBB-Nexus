using System;
using Animancer;
using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// Facial expression controller.
    /// 
    /// Notes:
    /// - Uses <see cref="AnimationFacadeBase"/> (AnimancerFacade) to play on a dedicated layer.
    /// - Consumes <see cref="PlayerRuntimeData.FacialEventRequest"/> (cleared in PlayerController.LateUpdate).
    /// - Uses an unlock timeout fallback so it can always return to the base expression even if
    ///   the clip doesn't actually animate bindings (e.g. rig mismatch warnings).
    /// </summary>
    public sealed class FacialController
    {
        private const int FacialLayer = 2;

        private readonly PlayerController _player;
        private readonly PlayerSO _config;
        private readonly PlayerRuntimeData _data;

        private ClipTransition _baseExpression;

        // Lock to avoid replaying transient expressions every frame.
        private float _unlockTime;
        private PlayerFacialEvent _lockedEvent;

        private float _fallbackReturnTime;

        private bool _initialized;

        public FacialController(PlayerController player)
        {
            _player = player;
            _config = player.Config;
            _data = player.RuntimeData;

            _baseExpression = _config != null && _config.Emj != null ? _config.Emj.BaseExpression : null;
        }

        public void Update()
        {
            if (_player == null || _player.AnimFacade == null) return;

            // Lazy init to avoid touching Animancer before everything is ready.
            if (!_initialized)
            {
                _initialized = true;

                if (_config != null && _config.Core != null)
                    _player.AnimFacade.SetLayerMask(FacialLayer, _config.Core.FacialMask);

                PlayBaseExpression(0f);
            }

            if (_config == null || _config.Emj == null) return;

            if (_data != null && _data.Arbitration.BlockFacial)
            {
                _player.AnimFacade.SetLayerWeight(FacialLayer, 0f);
                return;
            }

            _player.AnimFacade.SetLayerWeight(FacialLayer, 1f);

            // Fallback: if something went wrong and we never unlocked, force base expression.
            if (_fallbackReturnTime > 0f && Time.time >= _fallbackReturnTime)
            {
                ClearLock(clearCallback: true);
                PlayBaseExpression(0.2f);
            }

            // Still locked.
            if (Time.time < _unlockTime)
                return;

            var evt = _data != null ? _data.FacialEventRequest : PlayerFacialEvent.None;
            if (evt == PlayerFacialEvent.None)
                return;

            // Prevent spamming same event within the same lock window.
            if (_lockedEvent == evt && Time.time < _unlockTime + 0.0001f)
                return;

            if (_config.Emj.TryGet(evt, out var transition))
                PlayTransientExpression(evt, transition, 0.1f);
        }

        private void PlayTransientExpression(PlayerFacialEvent evt, ClipTransition transition, float fade)
        {
            if (transition == null || transition.Clip == null) return;

            _lockedEvent = evt;

            // Estimate length for timeout. If clip can't play, Length may be 0 -> use small fallback.
            var len = transition.Clip.length;
            if (len <= 0f) len = 0.25f;

            // Lock long enough for the transient to be noticeable.
            _unlockTime = Time.time + Mathf.Max(0.05f, len - 0.02f);

            // Absolute fallback to ensure base expression resumes.
            _fallbackReturnTime = Time.time + len + 0.1f;

            var options = new AnimPlayOptions
            {
                Layer = FacialLayer,
                FadeDuration = fade,
                Speed = -1f,
                NormalizedTime = -1f,
            };

            _player.AnimFacade.PlayTransition(transition, options);

            // Prefer real end callback when possible.
            _player.AnimFacade.SetOnEndCallback(() =>
            {
                // If another transient started, ignore.
                if (Time.time < _unlockTime - 0.01f) return;

                ClearLock(clearCallback: false);
                PlayBaseExpression(0.2f);
            }, FacialLayer);
        }

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
