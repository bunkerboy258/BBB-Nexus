using UnityEngine;
using Animancer;
using Characters.Player.Data;

namespace Characters.Player.Expression
{
    /// <summary>
    /// Facial layer controller
    /// </summary>
    public class FacialController
    {
        private AnimancerLayer _layer;
        private PlayerSO _config;

        // current base expression (default Blink loop)
        private ClipTransition _currentBaseExpression;

        // whether a transient expression is playing
        private bool _isPlayingTransient;

        public FacialController(AnimancerComponent animancer, PlayerSO config)
        {
            _config = config;

            // get Layer 2
            _layer = animancer.Layers[2];

            // set mask from Core module
            if (_config.Core != null)
                _layer.Mask = _config.Core.FacialMask;

            // ensure layer weight
            _layer.Weight = 1f;

            // default base expression from Core module
            if (_config.Core != null)
                _currentBaseExpression = _config.Core.BlinkAnim;

            PlayBaseExpression();
        }

        public void PlayTransientExpression(ClipTransition expressionClip, float fadeDuration = 0.1f)
        {
            if (expressionClip == null || expressionClip.Clip == null) return;

            _isPlayingTransient = true;

            var state = _layer.Play(expressionClip, fadeDuration);

            state.Events(this).OnEnd = () =>
            {
                _isPlayingTransient = false;
                PlayBaseExpression(0.2f);
            };
        }

        public void SetBaseExpression(ClipTransition newBaseExpression)
        {
            if (newBaseExpression == null) return;

            _currentBaseExpression = newBaseExpression;

            if (!_isPlayingTransient)
            {
                PlayBaseExpression(0.25f);
            }
        }

        public void PlayHurtExpression()
        {
            if (_config.Core != null)
                PlayTransientExpression(_config.Core.HurtFaceAnim, 0.1f);
        }

        private void PlayBaseExpression(float fadeDuration = 0.25f)
        {
            if (_currentBaseExpression != null && _currentBaseExpression.Clip != null)
            {
                _layer.Play(_currentBaseExpression, fadeDuration);
            }
        }
    }
}
