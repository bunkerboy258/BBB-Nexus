// 文件路径: Characters/Player/Animation/IAnimationFacade.cs
using UnityEngine;
using Animancer;

namespace Characters.Player.Animation
{
    public interface IAnimationFacade
    {
        // 初始化 AnimancerComponent 引用（可选，用于在外部显式初始化）
        void InitializeAnimancer(AnimancerComponent animancerComponent);

        // 播放单个普通 AnimationClip
        void PlayClip(AnimationClip clip, AnimPlayOptions options);

        // 播放高级配置树 (对应 Animancer 的 ITransition，如 ClipTransition, MixerTransition2D)
        void PlayTransition(object transitionObj, AnimPlayOptions options);

        // 运行时更新 1D/2D 混合树参数
        void SetMixerParameter(Vector2 parameter);

        // 安全的结束回调绑定
        void SetOnEndCallback(System.Action onEndAction);
        void ClearOnEndCallback();

        // 支持层权重控制（用于淡入/淡出分层动画）
        void SetLayerWeight(int layerIndex, float weight, float fadeDuration = 0f);

        // 设置特定层的遮罩
        void SetLayerMask(int layerIndex, AvatarMask mask);

        // 添加针对特定归一化时间的自定义回调支持
        void AddCallback(float normalizedTime, System.Action callback);

        // 状态读取
        float CurrentTime { get; }
        float CurrentNormalizedTime { get; }
    }
}
