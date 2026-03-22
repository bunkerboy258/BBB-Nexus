using System;
using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 动画外观基类 - 所有动画外观类的抽象基类
    /// 所有具体动画外观(如AnimancerFacade等)都应继承此类
    /// </summary>
    public abstract class AnimationFacadeBase : MonoBehaviour, IAnimationFacade
    {
        /// <summary>
        /// 播放基础动画片段
        /// </summary>
        public abstract void PlayClip(AnimationClip clip, AnimPlayOptions options);

        /// <summary>
        /// 播放混合树或序列动画
        /// </summary>
        public abstract void PlayTransition(object transitionObj, AnimPlayOptions options);

        /// <summary>
        /// 设置混合树参数
        /// </summary>
        public abstract void SetMixerParameter(Vector2 parameter, int layerIndex = 0);

        /// <summary>
        /// 设置动画结束回调
        /// </summary>
        public abstract void SetOnEndCallback(Action onEndAction, int layerIndex = 0);

        /// <summary>
        /// 清除动画结束回调
        /// </summary>
        public abstract void ClearOnEndCallback(int layerIndex = 0);

        /// <summary>
        /// 设置覆盖动画结束回调
        /// </summary>
        public abstract void SetOverrideOnEndCallback(Action onEndAction);

        /// <summary>
        /// 清除覆盖动画结束回调
        /// </summary>
        public abstract void ClearOverrideOnEndCallback();

        /// <summary>
        /// 设置动画层权重
        /// </summary>
        public abstract void SetLayerWeight(int layerIndex, float weight, float fadeDuration = 0f);

        /// <summary>
        /// 设置动画层遮罩
        /// </summary>
        public abstract void SetLayerMask(int layerIndex, AvatarMask mask);

        /// <summary>
        /// 添加指定时间的回调
        /// </summary>
        public abstract void AddCallback(float normalizedTime, Action callback, int layerIndex = 0);

        /// <summary>
        /// 获取当前播放时间(秒)
        /// </summary>
        public abstract float CurrentTime { get; }

        /// <summary>
        /// 获取当前归一化播放时间 0-1
        /// </summary>
        public abstract float CurrentNormalizedTime { get; }

        /// <summary>
        /// 获取指定层的播放时间
        /// </summary>
        public abstract float GetLayerTime(int layerIndex);

        /// <summary>
        /// 获取指定层的归一化播放时间
        /// </summary>
        public abstract float GetLayerNormalizedTime(int layerIndex);

        /// <summary>
        /// 强行播放全身动画
        /// </summary>
        public abstract void PlayFullBodyAction(AnimationClip clip, float fadeDuration = 0.2f);

        /// <summary>
        /// 停止全身动画
        /// </summary>
        public abstract void StopFullBodyAction();
    }
}
