using UnityEngine;

namespace BBBNexus
{
    [CreateAssetMenu(fileName = "ShielderTacticalConfig_", menuName = "BBBNexus/AI/Shielder Tactical Config")]
    public class ShielderTacticalConfigSO : AITacticalBrainConfigSO
    {
        [Header("--- 攻击出手（Shielder 专属）---")]
        [Tooltip("在保压区等待出手的最短时间（秒）")]
        public float AttackWindupMin = 1.0f;
        [Tooltip("在保压区等待出手的最长时间（秒）")]
        public float AttackWindupMax = 2.5f;
        [Tooltip("攻击意图持续时间（秒）：WantsToAttack 保持为 true 的时长，需覆盖完整出击动画")]
        public float AttackCommitTime = 0.6f;

        [Header("--- 收招冷却（Shielder 专属）---")]
        [Tooltip("出击后收盾冷却的最短时间（秒）")]
        public float RecoveryDurationMin = 1.2f;
        [Tooltip("出击后收盾冷却的最长时间（秒）")]
        public float RecoveryDurationMax = 2.0f;

        [Header("--- 对齐约束（Shielder 专属）---")]
        [Tooltip("允许出击的最大朝向偏差（度）。超过此角度时等待转向到位再出击。")]
        public float AttackFacingAngle = 25f;
    }
}
