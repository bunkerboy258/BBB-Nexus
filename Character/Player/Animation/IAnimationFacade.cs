using UnityEngine;

namespace Characters.Player.Animation
{
    public interface IAnimationFacade
    {
        void PlayClip(AnimationClip clip, AnimPlayOptions options);
        void PlayTransition(object transitionObj, AnimPlayOptions options);
        void SetMixerParameter(Vector2 parameter, int layerIndex = 0);
        void SetOnEndCallback(System.Action onEndAction, int layerIndex = 0);
        void ClearOnEndCallback(int layerIndex = 0);
        void SetLayerWeight(int layerIndex, float weight, float fadeDuration = 0f);
        void SetLayerMask(int layerIndex, AvatarMask mask);
        void AddCallback(float normalizedTime, System.Action callback, int layerIndex = 0);
        float CurrentTime { get; }
        float CurrentNormalizedTime { get; }
        float GetLayerTime(int layerIndex);
        float GetLayerNormalizedTime(int layerIndex);
    }
}