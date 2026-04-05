using UnityEngine;

namespace BBBNexus
{
    [CreateAssetMenu(fileName = "GunnerTacticalConfig_", menuName = "BBBNexus/AI/Gunner Tactical Config")]
    public class GunnerTacticalConfigSO : AITacticalBrainConfigSO
    {
        [Header("--- 枪手瞄准 ---")]
        [Tooltip("举枪后到开火前的稳定等待时间（秒）。越长越有预判感。")]
        [SerializeField] public float AimSettleTime = 0.6f;

        [Header("--- 连射/点射 ---")]
        [Tooltip("每轮连射最少发数")]
        [SerializeField] public int BurstCountMin = 1;
        [Tooltip("每轮连射最多发数")]
        [SerializeField] public int BurstCountMax = 3;
        [Tooltip("连射时每发间隔（秒）")]
        [SerializeField] public float BurstShotInterval = 0.3f;

        [Header("--- 射击间歇 ---")]
        [Tooltip("一轮射击后的最短恢复时间（秒）")]
        [SerializeField] public float RecoveryDurationMin = 1.5f;
        [Tooltip("一轮射击后的最长恢复时间（秒）")]
        [SerializeField] public float RecoveryDurationMax = 3f;
    }
}
