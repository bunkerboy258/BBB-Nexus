using UnityEngine;

namespace BBBNexus
{
    [CreateAssetMenu(fileName = "SimpleSteeringSensorConfig_", menuName = "BBBNexus/AI/Simple Steering Sensor Config")]
    public class SimpleSteeringSensorConfigSO : ScriptableObject
    {
        [Header("Detection — Vision Cone")]
        [Tooltip("视野检测距离（米）：目标在此范围内且在视野角内才能被发现。")]
        public float DetectionRange = 12f;

        [Tooltip("视野半角（度）。例如 60 表示前方 120° 扇形。")]
        public float DetectionFOV = 60f;

        [Header("Detection — Alert Range")]
        [Tooltip("近距离无条件警觉范围（米）：目标进入此距离无论朝向都会被发现。")]
        public float AlertRange = 3f;

        [Header("Detection — Lose Target")]
        [Tooltip("失去目标后多少秒重置为未警觉状态（秒）。")]
        public float LostTargetCooldown = 4f;

        [Header("Steering — Obstacle Avoidance")]
        [Tooltip("障碍物射线检测距离（米）。")]
        public float ObstacleDetectRange = 1.5f;

        [Header("Detection — Occlusion")]
        [Tooltip("索敌遮挡检测层。玩家被这些层阻挡时不可被感知。0 = 不检测遮挡（可以隔墙发现）。")]
        public LayerMask DetectionBlockMask;
        [Tooltip("绕障检测层。用于 AI 导航时避开障碍物。")]
        public LayerMask ObstacleMask;
    }
}
