using UnityEngine;

namespace BBBNexus
{
    [CreateAssetMenu(fileName = "AITacticalConfig_", menuName = "BBBNexus/AI/Tactical Brain Config")]
    public class AITacticalBrainConfigSO : ScriptableObject
    {
        [Header("--- 距离与目标 ---")]
        [SerializeField] public float EngagementRange = 15f;
        [SerializeField] public float AttackRange = 2f;          

        [Header("--- 跳跃配置 ---")]
        [SerializeField] public float JumpCooldown = 1.5f;
        [SerializeField] public float DoubleJumpDelay = 0.35f;

        [Header("--- 闪避配置 (Dodge) ---")]
        [Tooltip("触发闪避的最近距离（米）")]
        [SerializeField] public float DodgeTriggerRange = 8f;
        [Tooltip("闪避冷却时间（秒）- 越大越谨慎")]
        [SerializeField] public float DodgeCooldown = 2.5f;
        [Tooltip("闪避触发概率 (0-1) - 0.4 = 40%")]
        [Range(0f, 1f)]
        [SerializeField] public float DodgeChance = 0.4f;
        [Tooltip("一个靠近周期内最多闪避次数")]
        [SerializeField] public int DodgeMaxAttempts = 3;

        [Header("--- 翻滚配置 (Roll) ---")]
        [Tooltip("触发翻滚的最近距离（米）")]
        [SerializeField] public float RollTriggerRange = 6f;
        [Tooltip("翻滚冷却时间（秒）- 越大越谨慎")]
        [SerializeField] public float RollCooldown = 3f;
        [Tooltip("翻滚触发概率 (0-1) - 0.25 = 25%")]
        [Range(0f, 1f)]
        [SerializeField] public float RollChance = 0.25f;
        [Tooltip("一个靠近周期内最多翻滚次数")]
        [SerializeField] public int RollMaxAttempts = 2;

        [Header("--- 迂回战术 ---")]
        [Tooltip("迂回的最小间隔（秒）")]
        [SerializeField] public float StrafeCooldownMin = 1.5f;
        [Tooltip("迂回的最大间隔（秒）")]
        [SerializeField] public float StrafeCooldownMax = 3.5f;
    }
}
