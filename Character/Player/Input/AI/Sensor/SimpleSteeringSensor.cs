using UnityEngine;

namespace BBBNexus
{
    public class SimpleSteeringSensor : NavigatorSensorBase
    {
        [Header("Steering Settings")]
        public float ObstacleDetectRange = 1.5f;
        public LayerMask ObstacleMask;

        [Header("Jump Detection Settings")]
        [Tooltip("如果 AI 在此时间内移动距离不足阈值，触发跳跃尝试（秒）")]
        public float StuckCheckInterval = 0.3f;
        [Tooltip("在 StuckCheckInterval 时间内，移动距离不应低于此值，否则视为卡住（米）")]
        public float StuckDistanceThreshold = 0.2f;

        private Vector3 _lastCheckPos;
        private float _stuckCheckTimer;
        private bool _wasStuck;

        private void Start()
        {
            _lastCheckPos = transform.position;
            _stuckCheckTimer = StuckCheckInterval;
        }

        protected override void ProcessSensorLogic()
        {
            Vector3 myPos = transform.position;
            Vector3 targetPos = Target.position;

            Vector3 dirToTarget = targetPos - myPos;
            dirToTarget.y = 0;

            float dist = dirToTarget.magnitude;
            if (dist < 0.1f) return;

            Vector3 desiredDir = dirToTarget.normalized;
            Vector3 rayStart = myPos + Vector3.up * 1f;
            Vector3 trueTargetDir = dirToTarget.normalized;

            bool needsJump = false;

            // 【超低开销探测 1】：探测前方是否有坑/悬崖（往前预测 1.5 米，向下打射线）
            Vector3 gapCheckStart = myPos + desiredDir * 1.5f + Vector3.up * 0.5f;
            if (!Physics.Raycast(gapCheckStart, Vector3.down, 2f, ObstacleMask))
            {
                needsJump = true; // 前方悬空，必须跳！
            }

            // 【超低开销探测 2】：探测墙壁/障碍（复用你原本的避障逻辑）
            if (Physics.Raycast(rayStart, desiredDir, out RaycastHit hit, ObstacleDetectRange, ObstacleMask))
            {
                desiredDir = Vector3.ProjectOnPlane(desiredDir, hit.normal).normalized;

                // 如果撞墙距离太近且不是坑，死马当活马医，尝试起跳越过矮墙
                if (!needsJump && hit.distance < 0.8f)
                {
                    needsJump = true;
                }
            }

            // 【超低开销探测 3】：长时间卡住检测（每 0.3 秒检测一次）
            _stuckCheckTimer -= Time.deltaTime;
            if (_stuckCheckTimer <= 0)
            {
                float movedDistance = Vector3.Distance(myPos, _lastCheckPos);
                
                // 如果在 StuckCheckInterval 时间内移动距离不足，判定为卡住
                if (movedDistance < StuckDistanceThreshold && !_wasStuck)
                {
                    needsJump = true;  // 尝试跳跃脱困
                    _wasStuck = true;
                }
                else if (movedDistance >= StuckDistanceThreshold * 1.5f)
                {
                    // 移动充分，重置卡住状态
                    _wasStuck = false;
                }

                _lastCheckPos = myPos;
                _stuckCheckTimer = StuckCheckInterval;
            }

            _currentContext = new NavigationContext(desiredDir, trueTargetDir, dist, true, needsJump);
        }
    }
}