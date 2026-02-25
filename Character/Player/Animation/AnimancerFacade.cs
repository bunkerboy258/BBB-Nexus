// 文件路径: Characters/Player/Animation/AnimancerFacade.cs
using UnityEngine;
using Animancer;

namespace Characters.Player.Animation
{
    //作为组件的原因：本质上是对 Unity/Animancer 运行时的直接桥接，而这两者都天然是组件化（MonoBehaviour / AnimancerComponent）的
    [RequireComponent(typeof(AnimancerComponent))]
    public class AnimancerFacade : MonoBehaviour, IAnimationFacade
    {
        private AnimancerComponent _animancer;
        private AnimancerState _currentState;
        
        // 缓存当前绑定的结束回调，用于防止状态机切换时回调串线
        private System.Action _currentOnEndAction;

        private void Awake()
        {
            _animancer = GetComponent<AnimancerComponent>();
        }

        private void OnDisable()
        {
            ClearOnEndCallback();
        }

        public void PlayClip(AnimationClip clip, AnimPlayOptions options)
        {
            if (clip == null) return;
            ClearOnEndCallback();

            var layer = GetLayerOrFallback(options.Layer);

            // 逻辑：如果 options 指定了 Fade，就用 options 的；否则用 Animancer 的默认渐变 (通常是 0.25)
            _currentState = options.FadeDuration.HasValue 
                ? layer.Play(clip, options.FadeDuration.Value) 
                : layer.Play(clip);

            // 低成本安全：帮助检测是否有其它系统在管理同一个 State（回调串线的根源）。
            _currentState?.AssertOwnership(this);

            ApplyOptions(_currentState, options);
            RebindOnEndIfNeeded();
        }

        public void PlayTransition(object transitionObj, AnimPlayOptions options)
        {
            var transition = transitionObj as ITransition;
            if (transition == null)
            {
                Debug.LogError("[AnimancerFacade] 传入的 transition 无法转换为 ITransition！请确保它是在 Inspector 中配置好的 Transition 对象。");
                return;
            }

            ClearOnEndCallback();
            var layer = GetLayerOrFallback(options.Layer);

            // Animancer 核心特性：ITransition 自身就包含了 FadeDuration。
            // 逻辑：优先使用 Options 里的 Fade。如果 Options 里没传 (null)，就直接 Play 从而触发 Transition 自带的默认 Fade。
            if (options.FadeDuration.HasValue)
            {
                _currentState = layer.Play(transition, options.FadeDuration.Value);
            }
            else
            {
                _currentState = layer.Play(transition);
            }

            _currentState?.AssertOwnership(this);

            ApplyOptions(_currentState, options);
            RebindOnEndIfNeeded();
        }

        public void SetMixerParameter(Vector2 parameter)
        {
            if (_currentState == null) return;

            // 智能识别并分发参数到 2D 或 1D Mixer (完全利用 Animancer 高级特性)
            if (_currentState is MixerState<Vector2> mixer2D)
            {
                mixer2D.Parameter = parameter;
            }
            else if (_currentState is MixerState<float> mixer1D)
            {
                mixer1D.Parameter = parameter.x;
            }
        }

        public void SetOnEndCallback(System.Action onEndAction)
        {
            _currentOnEndAction = onEndAction;
            RebindOnEndIfNeeded();
        }

        public void SetLayerWeight(int layerIndex, float weight, float fadeDuration = 0f)
        {
            var layer = GetLayerOrFallback(layerIndex);
            if (layer == null) return;

            if (fadeDuration > 0f)
            {
                layer.StartFade(weight, fadeDuration);
            }
            else
            {
                layer.Weight = weight;
            }
        }

        public void ClearOnEndCallback()
        {
            if (_currentState != null)
            {
                _currentState.Events(this).OnEnd = null;
                // 同时清理该 owner (this) 的所有自定义事件，防止内存泄漏或回调串线
                _currentState.Events(this).Clear();
            }
            _currentOnEndAction = null;
        }

        public void AddCallback(float normalizedTime, System.Action callback)
        {
            if (_currentState == null || callback == null) return;

            // 调用 Animancer 的事件系统，在指定归一化时间添加回调
            // 注意：使用 Events(this) 确保事件归属于 facade 管理，切换状态时可统一清理
            _currentState.Events(this).Add(normalizedTime, callback);
        }

        private void RebindOnEndIfNeeded()
        {
            if (_currentState == null) return;
            
            // 必须先清空当前调用者在当前 State 上的回调，防止重复触发
            _currentState.Events(this).OnEnd = null;
            if (_currentOnEndAction != null)
            {
                _currentState.Events(this).OnEnd = _currentOnEndAction;
            }
        }

        private static void ApplyOptions(AnimancerState state, AnimPlayOptions options)
        {
            if (state == null) return;
            
            state.Speed = options.Speed;
            
            // 只有当明确要求从特定时间开始时，才去覆盖
            if (options.NormalizedTime.HasValue)
            {
                state.NormalizedTime = options.NormalizedTime.Value;
            }

            // TODO: 未来如果要对接 FootPhase Sync，可在此处处理 options.ForcePhaseSync
        }

        private AnimancerLayer GetLayerOrFallback(int layerIndex)
        {
            var layers = _animancer.Layers;
            if ((uint)layerIndex < (uint)layers.Count)
                return layers[layerIndex];

            Debug.LogWarning($"[AnimancerFacade] Invalid layer index {layerIndex}, fallback to layer 0.");
            return layers[0];
        }

        public float CurrentTime => _currentState != null ? _currentState.Time : 0f;
        public float CurrentNormalizedTime => _currentState != null ? _currentState.NormalizedTime : 0f;
    }
}
