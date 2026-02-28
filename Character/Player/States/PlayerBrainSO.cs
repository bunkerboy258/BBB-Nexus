using System.Collections.Generic;
using UnityEngine;
using Characters.Player.Core;
using Characters.Player.States;

namespace Characters.Player.Data
{
    [CreateAssetMenu(fileName = "PlayerBrain_Default", menuName = "Player/Modules/Player Brain")]
    public class PlayerBrainSO : ScriptableObject
    {
        [Header("--- 1. 状态装载名单 (State Roster) ---")]
        [Tooltip("只需在下拉菜单中勾选玩家需要的状态。列表中排在【第0位】的将作为启动状态！")]
        public List<PlayerStateType> AvailableStates = new List<PlayerStateType>();

        [Header("--- 2. 全局打断管线 (Interceptors) ---")]
        [Tooltip("将打断器SO拖入此列表，从上到下决定绝对优先级。")]
        public List<StateInterceptorSO> GlobalInterceptors = new List<StateInterceptorSO>();

    }
    // 一些设计说明:......
    // 为什么打断器是面板拖拽使用而具体状态用了[Serialize]序列化 还特地搞一个枚举中介？
    // 因为打断器是“纯逻辑”组件，可以用引用序列化(继承ScriptableObject),像一个洗手池，如果被多个玩家共享，就只会处理玩家递来的手(data)，不会互相影响
    // 而状态是包含内部运行数据的，如果被共享就发生灾难，故必须使用值序列化，但是傻子unity的传统([Serializable])值序列化是不支持多态的
    // 就算使用[SerializeReference]这个支持多态的序列化特性 状态类又是依赖注入的 无法在面板上直接配置实例 所以只能用枚举来间接指定需要哪些状态，由代码来实例化并注入依赖
    // 其实也可以用工厂模式偷天换日(指向一个只会返回NewState的序列化资产)，但是这样要写一堆包装类
    // 反过来说，状态枚举的方式虽然麻烦了点，但它的好处是：1) 面板上清晰可见有哪些状态被启用；2) 代码里直接 switch 映射，清爽明了；3) 不需要写一大堆工厂包装类，反而更简洁。

}