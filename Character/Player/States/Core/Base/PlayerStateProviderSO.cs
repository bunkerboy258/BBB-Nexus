using UnityEngine;

namespace Characters.Player.States
{
    /// <summary>
    /// 状态提供者基类 (ScriptableObject)
    /// 用于在 Inspector 面板中配置并向状态机注入具体的 C# 状态类。
    /// </summary>
    public abstract class PlayerStateProviderSO : ScriptableObject
    {
        /// <summary>
        /// 实例化并返回具体的玩家状态
        /// </summary>
        public abstract PlayerBaseState CreateState(PlayerController player);
    }
}