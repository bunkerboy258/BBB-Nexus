using UnityEngine;
using Animancer;
using Characters.Player.Data;

namespace Characters.Player.Layers
{
    /// <summary>
    /// ���� Layer 2 (�沿����) �Ŀ�������
    /// �߼���ʼ��ά��һ���������� (��Ĭ��/ս��)����ʱ���� (������/գ��) ���������Զ��лػ������顣
    /// </summary>
    public class FacialController
    {
        private AnimancerLayer _layer;
        private PlayerSO _config;

        // ��ǰ�Ļ������� (Ĭ��Ϊ Neutral/Blink ѭ��)
        private ClipTransition _currentBaseExpression;

        // ��ǣ��Ƿ����ڲ�����ʱ���� (��ֹ�����������ʱ�����ʱ����)
        private bool _isPlayingTransient;

        public FacialController(AnimancerComponent animancer, PlayerSO config)
        {
            _config = config;

            // ��ȡ Layer 2
            _layer = animancer.Layers[2];

            // �������� (ֻӰ��ͷ������)
            _layer.Mask = _config.FacialMask;

            // ȷ�� Layer Ȩ��Ϊ 1
            _layer.Weight = 1f;

            // �����ı��鶯���� Additive �� (ֻ���ֵ)��ȡ��ע����������
            // _layer.IsAdditive = true;

            // ��ʼ��������Ĭ�ϱ��� (ͨ����գ��ѭ��)
            _currentBaseExpression = _config.BlinkAnim;
            PlayBaseExpression();
        }

        // ========================================================================
        // ���� API
        // ========================================================================

        /// <summary>
        /// ����һ����ʱ���� (�����ˡ�����)��������Ϻ��Զ��ָ��������顣
        /// </summary>
        /// <param name="expressionClip">���鶯��</param>
        /// <param name="fadeDuration">����ʱ��</param>
        public void PlayTransientExpression(ClipTransition expressionClip, float fadeDuration = 0.1f)
        {
            if (expressionClip == null || expressionClip.Clip == null) return;

            _isPlayingTransient = true;

            var state = _layer.Play(expressionClip, fadeDuration);

            // �ؼ�������������Զ��лػ�������
            state.Events(this).OnEnd = () =>
            {
                _isPlayingTransient = false;
                PlayBaseExpression(0.2f); // �����ػ�������
            };
        }

        /// <summary>
        /// �л��������� (�����ս��״̬�������Ϊ����)��
        /// </summary>
        /// <param name="newBaseExpression">�µĻ�������</param>
        public void SetBaseExpression(ClipTransition newBaseExpression)
        {
            if (newBaseExpression == null) return;

            _currentBaseExpression = newBaseExpression;

            // ֻ�е�û����ʱ�����ڲ���ʱ���������л�
            // �������ʱ������� (OnEnd) ʱ�����Զ��л�������µ� Base
            if (!_isPlayingTransient)
            {
                PlayBaseExpression(0.25f);
            }
        }

        // ========================================================================
        // ����ҵ���߼�
        // ========================================================================

        public void PlayHurtExpression()
        {
            PlayTransientExpression(_config.HurtFaceAnim, 0.1f);
        }

        // ========================================================================
        // �ڲ�����
        // ========================================================================

        private void PlayBaseExpression(float fadeDuration = 0.25f)
        {
            if (_currentBaseExpression != null && _currentBaseExpression.Clip != null)
            {
                _layer.Play(_currentBaseExpression, fadeDuration);
            }
        }
    }
}
