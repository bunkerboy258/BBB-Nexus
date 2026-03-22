using UnityEngine;
using System;

namespace BBBNexus
{
    [Serializable]
    public struct ActionRequest
    {
        public AnimationClip Clip;
        public float FadeDuration;
        public int Priority;
        public bool ApplyGravity;

        public ActionRequest(AnimationClip clip, int priority = 20, float fadeDuration = 0.2f, bool applyGravity = true)
        {
            Clip = clip;
            Priority = priority;
            FadeDuration = fadeDuration;
            ApplyGravity = applyGravity;
        }
    }
}