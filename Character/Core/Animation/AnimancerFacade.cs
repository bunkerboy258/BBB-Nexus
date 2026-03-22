using System;
using System.Collections.Generic;
using UnityEngine;
using Animancer;

namespace BBBNexus
{
    // animancer动画转接器 
    [RequireComponent(typeof(AnimancerComponent))]
    public class AnimancerFacade : AnimationFacadeBase
    {
        private AnimancerComponent _animancer;

        // 多层回调字典 这是为了解决多层级动作串线
        // 每个动画层都有自己的独立回调槽位 互不干扰 
        // 别随便改这里的逻辑 不然换弹动作可能会卡在最后一帧 
        private Dictionary<int, Action> _layerOnEndActions = new Dictionary<int, Action>();

        // 回调包装类的对象池 避免闭包GC
        private Stack<CallbackWrapper> _wrapperPool = new Stack<CallbackWrapper>();

        // GC FIX: OnDisable 原实现 `new List<int>(_layerOnEndActions.Keys)` 会分配新 List 和内部数组。
        // 这里使用静态 scratch list 复用避免禁用/销毁时产生 GC 尖刺。
        private static readonly List<int> _layerKeyScratch = new List<int>(8);

        /// <summary>
        /// 专供 Override/代理模式的逻辑回调通道
        /// 注意：它不对应真实 AnimancerLayer 只是把回调绑定到 layer0 当前 state 上
        /// </summary>
        private CallbackWrapper _overrideCallbackWrapper;

        private class CallbackWrapper
        {
            public AnimancerState State;
            public int LayerIndex;
            public Action OnEndAction;
            public AnimancerFacade Facade;
            
            // 将实例方法缓存为委托，彻底消灭 new Action() 的隐藏GC
            public readonly Action DelegateInstance;

            public CallbackWrapper()
            {
                DelegateInstance = Execute;
            }

            private void Execute()
            {
                if (State != null)
                {
                    try { State.Events(Facade).OnEnd = null; State.Events(Facade).Clear(); } catch { }
                }

                Facade._layerOnEndActions.Remove(LayerIndex);
                try { OnEndAction?.Invoke(); } catch { }
                
                // 执行完毕后，清空引用防止内存泄漏，并把自己压回池中复用
                State = null;
                OnEndAction = null;
                Facade._wrapperPool.Push(this);
            }
        }
        //注：我们的回调注册(老版本) 实际上是声明了一个委托然后写入字典 
        //问题是 这个委托是使用了函数的内部值的 所以 为了避免让内部值脱离这个函数的生命周期
        //c#会偷偷打包出一个闭包 分配在堆内存上 会产生GC压力 如果只有一个角色 其实问题不大
        //(我有洁癖 遂构造了一个简单的委托对象池)

        private bool _fullBodyRootMotionEnabled;

        private void Awake()
        {
            _animancer = GetComponent<AnimancerComponent>();
        }

        private void EnsureAnimancer()
        {
            // 对象池预热/启用早期：可能在 Awake 之前就有逻辑调用到 Facade（例如状态机 Boot）。
            // 这里做懒初始化避免 NullReference。
            if (_animancer == null)
                _animancer = GetComponent<AnimancerComponent>();
        }

        // 脚本禁用时必须强制清空所有层级的回调 
        // 这是为了防止角色销毁后 内存里还挂着没跑完的动画逻辑 
        private void OnDisable()
        {
            if (_layerOnEndActions.Count > 0)
            {
#if UNITY_EDITOR
                // GC RISK NOTE: 这里不能 new List(keys)，否则会产生托管分配。
                // Debug.Log("[GC-RISK FIXED] AnimancerFacade.OnDisable: reused scratch list instead of allocating new List<int>.");
#endif
                _layerKeyScratch.Clear();
                foreach (var kv in _layerOnEndActions)
                    _layerKeyScratch.Add(kv.Key);

                for (int i = 0; i < _layerKeyScratch.Count; i++)
                {
                    ClearOnEndCallback(_layerKeyScratch[i]);
                }
            }
            _layerOnEndActions.Clear();
        }

        // 播放基础动画 先清理老回调再注入新动作 
        public override void PlayClip(AnimationClip clip, AnimPlayOptions options)
        {
            if (clip == null) return;
            EnsureAnimancer();
            if (_animancer == null) return;

            int layerIndex = options.Layer;

            ClearOnEndCallback(layerIndex);
            var layer = GetLayerOrFallback(layerIndex);
            if (layer == null) return;

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

            EnsureAnimancer();
            if (_animancer == null) return;

            int layerIndex = options.Layer;
            ClearOnEndCallback(layerIndex);

            var layer = GetLayerOrFallback(layerIndex);
            if (layer == null) return;

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
        public override void SetOnEndCallback(Action onEndAction, int layerIndex = 0)
        {
            var state = GetLayerOrFallback(layerIndex).CurrentState;

            if (onEndAction == null)
            {
                _layerOnEndActions.Remove(layerIndex);
                if (state != null) state.Events(this).OnEnd = null;
                return;
            }

            // 从对象池获取一个包装器，避免创建新的闭包对象
            CallbackWrapper wrapper = _wrapperPool.Count > 0 ? _wrapperPool.Pop() : new CallbackWrapper();
            wrapper.State = state;
            wrapper.LayerIndex = layerIndex;
            wrapper.OnEndAction = onEndAction;
            wrapper.Facade = this;

            _layerOnEndActions[layerIndex] = wrapper.DelegateInstance;
            if (state != null) state.Events(this).OnEnd = wrapper.DelegateInstance;
        }

        public override void SetOverrideOnEndCallback(Action onEndAction)
        {
            // 接管模式的回调通道绑定到 layer0 当前 state。
            var state = GetLayerOrFallback(0).CurrentState;

            if (onEndAction == null)
            {
                ClearOverrideOnEndCallback();
                return;
            }

            // 先清掉旧的绑定（仅影响 override 通道自身）。
            ClearOverrideOnEndCallback();

            CallbackWrapper wrapper = _wrapperPool.Count > 0 ? _wrapperPool.Pop() : new CallbackWrapper();
            wrapper.State = state;
            wrapper.LayerIndex = -1;
            wrapper.OnEndAction = onEndAction;
            wrapper.Facade = this;

            _overrideCallbackWrapper = wrapper;

            if (state != null)
            {
                state.Events(this).OnEnd = wrapper.DelegateInstance;
            }
        }

        public override void ClearOverrideOnEndCallback()
        {
            if (_overrideCallbackWrapper == null) return;

            var state = _overrideCallbackWrapper.State;
            if (state != null)
            {
                try
                {
                    state.Events(this).OnEnd = null;
                    state.Events(this).Clear();
                }
                catch { }
            }

            // 手动回收到池（不走 Execute，因为 Execute 会尝试操作 _layerOnEndActions[-1]）
            _overrideCallbackWrapper.State = null;
            _overrideCallbackWrapper.OnEndAction = null;
            _wrapperPool.Push(_overrideCallbackWrapper);
            _overrideCallbackWrapper = null;
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

        // 强行清理指定层的事件流 
        public override void ClearOnEndCallback(int layerIndex = 0)
        {
            EnsureAnimancer();
            if (_animancer == null)
            {
                _layerOnEndActions.Remove(layerIndex);
                return;
            }

            var layer = GetLayerOrFallback(layerIndex);
            var state = layer != null ? layer.CurrentState : null;
            if (state != null)
            {
                state.Events(this).OnEnd = null;
                state.Events(this).Clear();
            }
            _layerOnEndActions.Remove(layerIndex);
        }

        // 在指定时间点插入逻辑反馈 
        public override void AddCallback(float normalizedTime, Action callback, int layerIndex = 0)
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

        // 越界安全检查
        // 重要!!!!!!!!!!!!!!!!：Animancer 的 Layers 是惰性创建的 直接访问 layers[index] 会自动扩容创建该层
        // 之前的实现会在层不存在时回退到 layer0 导致对上半身/表情层的 Mask/Weight/Events误作用到 Base Layer
        // 从而出现“所有动画都不播放/权重为0/一直马步”等现象
        private AnimancerLayer GetLayerOrFallback(int layerIndex)
        {
            EnsureAnimancer();
            if (_animancer == null) return null;

            var layers = _animancer.Layers;

            if (layerIndex >= 0)
                return layers[layerIndex];

            return layers[0];
        }

        public override void PlayFullBodyAction(AnimationClip clip, float fadeDuration = 0.2f)
        {
            if (clip == null) return;

            _fullBodyRootMotionEnabled = true;
            _animancer.Animator.applyRootMotion = true;

            // 全身动作切换时 清理 layer0 的通用回调槽 
            ClearOnEndCallback(0);

            SetLayerWeight(1, 0f, fadeDuration);
            _animancer.Layers[0].Play(clip, fadeDuration);
        }

        public override void StopFullBodyAction()
        {
            if (_fullBodyRootMotionEnabled && _animancer != null && _animancer.Animator != null)
            {
                _animancer.Animator.applyRootMotion = false;
            }
            _fullBodyRootMotionEnabled = false;
        }

        // 基础层当前的播放进度
        public override float CurrentTime => GetLayerTime(0);
        public override float CurrentNormalizedTime => GetLayerNormalizedTime(0);

        public override float GetLayerTime(int layerIndex)
            => GetLayerOrFallback(layerIndex).CurrentState?.Time ?? 0f;

        public override float GetLayerNormalizedTime(int layerIndex)
            => GetLayerOrFallback(layerIndex).CurrentState?.NormalizedTime ?? 0f;
    }
}