using System.Collections.Generic;
using UnityEngine;
using Animancer;

namespace BBBNexus
{
    // 表现层的核心 负责转接动画意图 
    [RequireComponent(typeof(AnimancerComponent))]
    public class AnimancerFacade : AnimationFacadeBase
    {
        // 插件核心组件引用 它是实际干活的底层驱动 
        private AnimancerComponent _animancer;

        // 多层回调字典 这是为了解决多层级动作串线
        // 每个动画层都有自己的独立回调槽位 互不干扰 
        // 别随便改这里的逻辑 不然换弹动作可能会卡在最后一帧 
        private Dictionary<int, System.Action> _layerOnEndActions = new Dictionary<int, System.Action>();

        private bool _fullBodyRootMotionEnabled;

        private void Awake()
        {
            _animancer = GetComponent<AnimancerComponent>();
        }

        // 脚本禁用时必须强制清空所有层级的回调 
        // 这是为了防止角色销毁后 内存里还挂着没跑完的动画逻辑 
        private void OnDisable()
        {
            if (_layerOnEndActions.Count > 0)
            {
                var keys = new List<int>(_layerOnEndActions.Keys);
                for (int i = 0; i < keys.Count; i++)
                {
                    ClearOnEndCallback(keys[i]);
                }
            }
            _layerOnEndActions.Clear();
        }

        // 播放基础动画 先清理老回调再注入新动作 
        public override void PlayClip(AnimationClip clip, AnimPlayOptions options)
        {
            if (clip == null) return;
            int layerIndex = options.Layer;

            ClearOnEndCallback(layerIndex);
            var layer = GetLayerOrFallback(layerIndex);

            // 根据配置决定是瞬间切换还是淡入 
            var state = options.FadeDuration >= 0
                ? layer.Play(clip, options.FadeDuration)
                : layer.Play(clip);

            state.AssertOwnership(this);
            // 注入播放速率 确保动画跟得上意图管线的计算速度 
            ApplyOptions(state, options);
            // 重新绑定黑板注入的结束指令 
            RebindOnEndIfNeeded(layerIndex, state);
        }

        // 播放混合树或序列动画 
        // 核心流程跟播放基础动画一样 确保逻辑闭环 
        public override void PlayTransition(object transitionObj, AnimPlayOptions options)
        {
            var transition = transitionObj as ITransition;
            if (transition == null) return;

            int layerIndex = options.Layer;
            ClearOnEndCallback(layerIndex);

            var layer = GetLayerOrFallback(layerIndex);
            var state = options.FadeDuration >= 0
                ? layer.Play(transition, options.FadeDuration)
                : layer.Play(transition);

            state.AssertOwnership(this);
            ApplyOptions(state, options);
            RebindOnEndIfNeeded(layerIndex, state);
        }
        // 注 这个方法乍一看是有装拆箱风险的 但是实际并不会
        // 在Animancer的源码中 所有的Transition都是clas，它们天生就是分配在堆上的引用类型

        // 核心逻辑 它是让角色跑动起来不再滑步的关键 
        // 负责把意图管线算出来的摇杆矢量 喂给动画混合树 
        // 这里必须精准获取当前激活的层级状态 拿错了角色就会动作漂移 
        public override void SetMixerParameter(Vector2 parameter, int layerIndex = 0)
        {
            var state = GetLayerOrFallback(layerIndex).CurrentState;
            if (state == null) return;

            // 自动匹配混合空间维度 注入黑板里的运动参数 
            if (state is MixerState<Vector2> mixer2D)
            {
                mixer2D.Parameter = parameter;
            }
            else if (state is MixerState<float> mixer1D)
            {
                mixer1D.Parameter = parameter.x;
            }
        }

        // 注册状态机跳转的结束指令 
        public override void SetOnEndCallback(System.Action onEndAction, int layerIndex = 0)
        {
            var state = GetLayerOrFallback(layerIndex).CurrentState;

            if (onEndAction == null)
            {
                _layerOnEndActions.Remove(layerIndex);
                if (state != null) state.Events(this).OnEnd = null;
                return;
            }

            // 包装一层保护逻辑 确保回调跑完就自动爆炸
            // 绝对不能让过期的回调留在下一帧 否则逻辑会彻底乱套 
            System.Action wrapper = null;
            wrapper = () =>
            {
                if (state != null)
                {
                    try { state.Events(this).OnEnd = null; state.Events(this).Clear(); } catch { }
                }

                _layerOnEndActions.Remove(layerIndex);
                try { onEndAction.Invoke(); } catch { }
            };

            _layerOnEndActions[layerIndex] = wrapper;
            if (state != null) state.Events(this).OnEnd = wrapper;
        }

        // 动态调权重 实现上半身动作和面部表情叠加
        public override void SetLayerWeight(int layerIndex, float weight, float fadeDuration = 0f)
        {
            var layer = GetLayerOrFallback(layerIndex);
            if (layer == null) return;

            if (fadeDuration > 0f) layer.StartFade(weight, fadeDuration);
            else layer.Weight = weight;
        }

        // 注入动画遮罩 决定当前层级能控制哪些骨头 
        public override void SetLayerMask(int layerIndex, AvatarMask mask)
        {
            var layer = GetLayerOrFallback(layerIndex);
            if (layer != null) layer.Mask = mask;
        }

        // 强行清理指定层的事件流 这是一个极其重要的防御手段 
        public override void ClearOnEndCallback(int layerIndex = 0)
        {
            var state = GetLayerOrFallback(layerIndex).CurrentState;
            if (state != null)
            {
                state.Events(this).OnEnd = null;
                state.Events(this).Clear();
            }
            _layerOnEndActions.Remove(layerIndex);
        }

        // 在指定时间点插入逻辑反馈 
        public override void AddCallback(float normalizedTime, System.Action callback, int layerIndex = 0)
        {
            var state = GetLayerOrFallback(layerIndex).CurrentState;
            if (state == null || callback == null) return;
            state.Events(this).Add(normalizedTime, callback);
        }

        // 如果状态刷新了 就得重新检查有没有遗留的回调需要链接上去 
        private void RebindOnEndIfNeeded(int layerIndex, AnimancerState state)
        {
            if (state == null) return;

            try
            {
                state.Events(this).OnEnd = null;
                if (_layerOnEndActions.TryGetValue(layerIndex, out var action))
                {
                    state.Events(this).OnEnd = action;
                }
            }
            catch { }
        }

        // 把烘焙器离线算好的物理参数 注入到当前动画状态里 
        private static void ApplyOptions(AnimancerState state, AnimPlayOptions options)
        {
            if (state == null) return;
            if (options.Speed > 0f) state.Speed = options.Speed;
            if (options.NormalizedTime >= 0) state.NormalizedTime = options.NormalizedTime;
        }

        // 越界安全检查 如果层级索引乱填 就强制退回到基础层 
        private AnimancerLayer GetLayerOrFallback(int layerIndex)
        {
            var layers = _animancer.Layers;
            if ((uint)layerIndex < (uint)layers.Count) return layers[layerIndex];

            return layers[0];
        }

        public override void PlayFullBodyAction(AnimationClip clip, float fadeDuration = 0.2f)
        {
            if (clip == null) return;

            if (_animancer.Animator != null)
            {
                _fullBodyRootMotionEnabled = true;
                _animancer.Animator.applyRootMotion = true;
            }

            const int layerIndex = 0;
            ClearOnEndCallback(layerIndex);

            SetLayerWeight(1, 0f, fadeDuration);
            _animancer.Layers[layerIndex].Play(clip, fadeDuration);
        }

        public override void StopFullBodyAction()
        {
            if (_fullBodyRootMotionEnabled && _animancer != null && _animancer.Animator != null)
            {
                _animancer.Animator.applyRootMotion = false;
            }
            _fullBodyRootMotionEnabled = false;
        }

        // 基础层当前的播放进度 它是意图管线判断动作是否播完的重要依据 
        public override float CurrentTime => GetLayerTime(0);
        public override float CurrentNormalizedTime => GetLayerNormalizedTime(0);

        public override float GetLayerTime(int layerIndex)
            => GetLayerOrFallback(layerIndex).CurrentState?.Time ?? 0f;

        public override float GetLayerNormalizedTime(int layerIndex)
            => GetLayerOrFallback(layerIndex).CurrentState?.NormalizedTime ?? 0f;
    }
}