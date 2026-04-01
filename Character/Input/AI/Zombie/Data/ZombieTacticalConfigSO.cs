using UnityEngine;

namespace BBBNexus
{
    [CreateAssetMenu(fileName = "ZombieTacticalConfig_", menuName = "BBBNexus/AI/Zombie Tactical Config")]
    public class ZombieTacticalConfigSO : AITacticalBrainConfigSO
    {
        [Header("--- 连段配置（Zombie 专属）---")]
        [Tooltip("每轮攻击的最少连段数")]
        [SerializeField] public int ComboHitsMin = 1;
        [Tooltip("每轮攻击的最多连段数")]
        [SerializeField] public int ComboHitsMax = 3;
        [Tooltip("每段攻击预留的意图持续时间（秒）。需大于单段动画时长以确保 combo window 能接住。")]
        [SerializeField] public float PerHitCommitTime = 0.7f;

        [Header("--- 攻击冷却（Zombie 专属）---")]
        [Tooltip("一轮连段结束后的最短等待时间（秒）")]
        [SerializeField] public float AttackCooldownMin = 1.0f;
        [Tooltip("一轮连段结束后的最长等待时间（秒）")]
        [SerializeField] public float AttackCooldownMax = 2.5f;
    }
}
