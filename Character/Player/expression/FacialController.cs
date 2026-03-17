using Animancer;
using UnityEngine;

namespace BBBNexus
{
    public class FacialController
    {
        private readonly AnimancerLayer _layer;
        private readonly PlayerSO _config;
        private readonly PlayerRuntimeData _data;
        private readonly InputPipeline _inputPipeline;

        private ClipTransition _currentBaseExpression;
        private bool _isPlayingTransient;
        private int _transientPlayToken;

        public FacialController(PlayerController player)
        {
            _config = player.Config;
            _data = player.RuntimeData;
            _inputPipeline = player.InputPipeline;

            _layer = player.Animancer.Layers[2];

            if (_config != null && _config.Core != null)
                _layer.Mask = _config.Core.FacialMask;

            _layer.Weight = 1f;

            if (_config != null && _config.Emj != null && _config.Emj.BaseExpression != null && _config.Emj.BaseExpression.Clip != null)
                _currentBaseExpression = _config.Emj.BaseExpression;
            else if (_config != null && _config.Core != null)
                _currentBaseExpression = _config.Core.BlinkAnim;

            PlayBaseExpression();
        }

        public void Update()
        {
            if (_data == null || _config == null || _config.Emj == null) return;

            if (_data.Arbitration.BlockFacial)
            {
                if (_layer.Weight > 0f) _layer.Weight = 0f;
                return;
            }

            if (_data.CurrentLOD > CharacterLOD.High)
            {
                if (_layer.Weight > 0f) _layer.Weight = 0f;
                return;
            }
            else
            {
                if (_layer.Weight < 1f) _layer.Weight = 1f;
            }

            if (_data.WantsExpression1)
            {
                _inputPipeline.ConsumeExpression1Pressed();
                PlayTransientExpression(_config.Emj.SpecialExpression1, 0.1f);
            }
            else if (_data.WantsExpression2)
            {
                _inputPipeline.ConsumeExpression2Pressed();
                PlayTransientExpression(_config.Emj.SpecialExpression2, 0.1f);
            }
            else if (_data.WantsExpression3)
            {
                _inputPipeline.ConsumeExpression3Pressed();
                PlayTransientExpression(_config.Emj.SpecialExpression3, 0.1f);
            }
            else if (_data.WantsExpression4)
            {
                _inputPipeline.ConsumeExpression4Pressed();
                PlayTransientExpression(_config.Emj.SpecialExpression4, 0.1f);
            }
        }

        public void PlayTransientExpression(ClipTransition expressionClip, float fadeDuration = 0.1f)
        {
            if (expressionClip == null || expressionClip.Clip == null) return;

            _transientPlayToken++;
            var token = _transientPlayToken;
            _isPlayingTransient = true;

            var state = _layer.Play(expressionClip, fadeDuration);
            state.Events(this).OnEnd = () =>
            {
                if (token != _transientPlayToken) return;
                _isPlayingTransient = false;
                PlayBaseExpression(0.2f);
            };
        }

        private void PlayBaseExpression(float fadeDuration = 0.25f)
        {
            if (_currentBaseExpression != null && _currentBaseExpression.Clip != null)
                _layer.Play(_currentBaseExpression, fadeDuration);
        }
    }
}