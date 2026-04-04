using UnityEngine;

namespace BBBNexus
{
    public class SimpleSteeringSensor : NavigatorSensorBase
    {
        [Header("Sensor Config")]
        [Tooltip("可选的感知/绕障配置。赋值后会覆盖本组件上的数值字段，便于多个敌人共享同一套索敌参数。")]
        public SimpleSteeringSensorConfigSO Config;

        // ── 感知参数 ──────────────────────────────────────────────────────
        [Header("Detection — Vision Cone")]
        [Tooltip("视野检测距离（米）：玩家在此范围内且在视野角内才能被发现")]
        public float DetectionRange = 12f;
        [Tooltip("视野半角（度）。例如 60 = 前方 120° 扇形")]
        public float DetectionFOV = 60f;

        [Header("Detection — Alert Range")]
        [Tooltip("近距离无条件警觉范围（米）：玩家进入此距离无论朝向都会被发现（模拟听觉/感知）")]
        public float AlertRange = 3f;

        [Header("Detection — Lose Target")]
        [Tooltip("失去目标后多少秒重置为未警觉状态（秒）")]
        public float LostTargetCooldown = 4f;

        // ── 绕障参数 ──────────────────────────────────────────────────────
        [Header("Steering — Obstacle Avoidance")]
        [Tooltip("障碍物射线检测距离（米）")]
        public float ObstacleDetectRange = 1.5f;
        public LayerMask ObstacleMask;

        // ── 内部状态 ──────────────────────────────────────────────────────
        private bool  _isAlerted;
        private float _lostTargetTimer;

        // ── 调试 ──────────────────────────────────────────────────────────
        [Header("Debug")]
        public bool ShowGizmos = true;

        protected override void Awake()
        {
            base.Awake();
            ApplyConfigIfPresent();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ApplyConfigIfPresent();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ApplyConfigIfPresent();
        }
#endif

        protected override void ProcessSensorLogic()
        {
            Vector3 myPos     = transform.position;
            Vector3 targetPos = Target.position;

            Vector3 dirToTarget = targetPos - myPos;
            dirToTarget.y = 0f;
            float dist = dirToTarget.magnitude;

            if (dist < 0.05f)
            {
                // 完全重叠时保持当前警觉状态，输出零方向
                _currentContext = new NavigationContext(
                    Vector3.zero, Vector3.zero, dist, _isAlerted, false);
                return;
            }

            Vector3 flatDirNorm = dirToTarget.normalized;

            // ── 感知检测 ──────────────────────────────────────────────────
            bool canDetectNow = false;

            if (dist <= AlertRange)
            {
                // 近距离：无条件感知（听觉半径）
                canDetectNow = true;
            }
            else if (dist <= DetectionRange)
            {
                // 视野锥：检测朝向 + 遮挡
                Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                float angle = Vector3.Angle(flatForward, flatDirNorm);
                if (angle <= DetectionFOV)
                {
                    Vector3 eyePos = transform.position + Vector3.up * 1.3f;
                    Vector3 toTarget = Target.position + Vector3.up * 1.3f - eyePos;
                    canDetectNow = !Physics.Raycast(eyePos, toTarget.normalized, toTarget.magnitude, LayerMask.GetMask("Default"));
                }
            }

            if (canDetectNow)
            {
                _isAlerted         = true;
                _lostTargetTimer   = LostTargetCooldown;
            }
            else if (_isAlerted)
            {
                _lostTargetTimer -= Time.deltaTime;
                if (_lostTargetTimer <= 0f)
                    _isAlerted = false;
            }

            // 未警觉时：对 Brain 隐藏目标，AI 停止追击
            if (!_isAlerted)
            {
                _currentContext = new NavigationContext(
                    Vector3.zero, Vector3.zero, dist, false, false);
                return;
            }

            // ── 已警觉：计算导航方向（带障碍物绕行）────────────────────────
            Vector3 desiredDir = flatDirNorm;

            Vector3 rayStart = myPos + Vector3.up * 1.3f;
            if (Physics.Raycast(rayStart, desiredDir, out RaycastHit hit, ObstacleDetectRange, ObstacleMask))
            {
                // 沿障碍物法线投影，形成侧向绕行效果
                desiredDir = Vector3.ProjectOnPlane(desiredDir, hit.normal).normalized;
            }

            _currentContext = new NavigationContext(
                desiredDir,   // 导航方向（已绕障）
                flatDirNorm,  // 真实目标方向（用于朝向计算）
                dist,
                true,
                false);       // 僵尸不跳跃
        }

        protected override void OnDrawGizmos()
        {
            if (!ShowGizmos || Target == null) return;

            Vector3 pos = transform.position + Vector3.up * 1.3f;

            // 警觉距离（红圈）
            Gizmos.color = Color.red;
            DrawCircle(pos, AlertRange);

            // 视野距离（黄圈）
            Gizmos.color = Color.yellow;
            DrawCircle(pos, DetectionRange);

            // 视野锥（绿线）
            Gizmos.color = _isAlerted ? Color.green : new Color(0f, 1f, 0f, 0.4f);
            Vector3 flatFwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Quaternion leftRot  = Quaternion.Euler(0, -DetectionFOV, 0);
            Quaternion rightRot = Quaternion.Euler(0,  DetectionFOV, 0);
            Gizmos.DrawRay(pos, leftRot  * flatFwd * DetectionRange);
            Gizmos.DrawRay(pos, rightRot * flatFwd * DetectionRange);

            if (_isAlerted && _currentContext.HasValidTarget)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(pos, _currentContext.DesiredWorldDirection * 2f);
            }
        }

        private static void DrawCircle(Vector3 center, float radius)
        {
            const int segments = 32;
            float step = 360f / segments;
            Vector3 prev = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float rad = i * step * Mathf.Deg2Rad;
                Vector3 next = center + new Vector3(Mathf.Cos(rad) * radius, 0f, Mathf.Sin(rad) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        private void ApplyConfigIfPresent()
        {
            if (Config == null)
            {
                return;
            }

            DetectionRange = Config.DetectionRange;
            DetectionFOV = Config.DetectionFOV;
            AlertRange = Config.AlertRange;
            LostTargetCooldown = Config.LostTargetCooldown;
            ObstacleDetectRange = Config.ObstacleDetectRange;
        }
    }
}
