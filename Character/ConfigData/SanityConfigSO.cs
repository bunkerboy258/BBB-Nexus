using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 理智系统数值配置 SO
    /// 统一管理所有与理智消耗、恢复、格挡相关的数值，避免分散在各 MonoBehaviour 中。
    ///
    /// 创建方式：Project 窗口右键 → Create → BBBNexus → Sanity Config
    /// </summary>
    [CreateAssetMenu(fileName = "SanityConfig", menuName = "BBBNexus/Sanity Config")]
    public class SanityConfigSO : ScriptableObject
    {
        [Header("理智上限")]
        [Tooltip("玩家理智初始最大值")]
        public float MaxSanity = 100f;

        [Header("扣除速率（单位：点/秒）")]
        [Tooltip("闭眼时每秒扣除的理智量")]
        public float EyesClosedDrainRate = 4f;

        [Tooltip("睁眼且暴露在阳光下时每秒扣除的理智量")]
        public float SunDrainRate = 12f;

        [Header("恢复速率（三阶段，单位：点/秒）")]
        [Tooltip("睁眼非暴晒后，第一阶段的恢复速率")]
        public float RecoverRateStage1 = 3f;

        [Tooltip("睁眼非暴晒后，第二阶段的恢复速率")]
        public float RecoverRateStage2 = 7f;

        [Tooltip("睁眼非暴晒后，第三阶段的恢复速率")]
        public float RecoverRateStage3 = 12f;

        [Tooltip("第一阶段持续时长（秒），之后切换到第二阶段")]
        public float RecoverStage1Duration = 0.8f;

        [Tooltip("第二阶段持续时长（秒），之后切换到第三阶段")]
        public float RecoverStage2Duration = 2.0f;

        [Header("格挡与弹反")]
        [Tooltip("触发闭眼格挡时立刻消耗的理智值")]
        public float ParrySanityCost = 12f;

        [Tooltip("触发完美睁眼弹反时立刻恢复的理智值")]
        public float PerfectParrySanityRestore = 18f;

        [Tooltip("睁眼边沿后的完美弹反判定窗口（秒）")]
        public float PerfectParryWindowSeconds = 0.2f;
    }
}
