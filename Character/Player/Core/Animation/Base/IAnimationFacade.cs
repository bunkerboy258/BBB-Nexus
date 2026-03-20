using System;
using UnityEngine;

namespace BBBNexus
{
    public interface IAnimationFacade
    {
        float CurrentTime { get; }
        float CurrentNormalizedTime { get; }
        void PlayClip(AnimationClip clip, AnimPlayOptions options);
        void PlayTransition(object transitionObj, AnimPlayOptions options);
        void SetMixerParameter(Vector2 parameter, int layerIndex = 0);
        void SetOnEndCallback(Action onEndAction, int layerIndex = 0);
        void ClearOnEndCallback(int layerIndex = 0);
        void SetOverrideOnEndCallback(Action onEndAction);
        void ClearOverrideOnEndCallback();
        void SetLayerWeight(int layerIndex, float weight, float fadeDuration = 0f);
        void SetLayerMask(int layerIndex, AvatarMask mask);
        void AddCallback(float normalizedTime, Action callback, int layerIndex = 0);
        float GetLayerTime(int layerIndex);
        float GetLayerNormalizedTime(int layerIndex);
        void PlayFullBodyAction(AnimationClip clip, float fadeDuration = 0.2f);
        void StopFullBodyAction();
    }
}