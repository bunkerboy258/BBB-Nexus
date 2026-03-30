using System.Collections.Generic;
using UnityEngine;

namespace BBBNexus
{
    [CreateAssetMenu(fileName = "PlayerBrain_Default", menuName = "BBBNexus/Player/Modules/Player Brain")]
    public class PlayerBrainSO : ScriptableObject
    {
        [Header("--- 1. Locomotion 状态装载名单 (State Roster) ---")]
        [Tooltip("这里承载的是 Locomotion 域状态名单。列表中排在第 0 位的将作为启动状态。")]
        public List<PlayerStateType> AvailableStates = new List<PlayerStateType>();

        [Header("--- 2. Locomotion 全局打断管线 (Interceptors) ---")]
        [Tooltip("这里承载的是 Locomotion 域拦截器，从上到下决定优先级。")]
        public List<StateInterceptorSO> GlobalInterceptors = new List<StateInterceptorSO>();

        [Header("--- 上半身状态 (Upper Body & Combat) ---")]
        [Tooltip("列表中排在首位的将作为上半身启动状态 ")]
        public List<UpperBodyStateType> UpperBodyStates = new List<UpperBodyStateType>();

        [Tooltip("上半身专属打断管线")]
        public List<UpperBodyInterceptorSO> UpperBodyInterceptors = new List<UpperBodyInterceptorSO>();

        public IReadOnlyList<PlayerStateType> LocomotionStates => AvailableStates;
        public IReadOnlyList<StateInterceptorSO> LocomotionInterceptors => GlobalInterceptors;

    }
    // 一些设计说明:......
    // 为什么打断器是面板拖拽使用而具体状态用了[Serialize]序列化 还特地搞一个枚举中介？
    // 因为打断器是“纯逻辑”组件，可以用引用序列化(继承ScriptableObject),像一个洗手池，如果被多个玩家共享，就只会处理玩家递来的手(data)，不会互相影响
    // 而状态是包含内部运行数据的，如果被共享就发生灾难，故必须使用值序列化，但是傻子unity的传统([Serializable])值序列化是不支持多态的
    // 就算使用[SerializeReference]这个支持多态的序列化特性 状态类又是依赖注入的 无法在面板上直接配置实例 所以只能用枚举来间接指定需要哪些状态，由代码来实例化并注入依赖
    // 其实也可以用工厂模式偷天换日(指向一个只会返回NewState的序列化资产)，但是这样要写一堆包装类
    // 反过来说，状态枚举的方式虽然麻烦了点，但它的好处是：1) 面板上清晰可见有哪些状态被启用；2) 代码里直接 switch 映射，清爽明了；3) 不需要写一大堆工厂包装类，反而更简洁。

}
