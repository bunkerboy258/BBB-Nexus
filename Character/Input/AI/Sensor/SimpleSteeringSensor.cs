using UnityEngine;

namespace BBBNexus
{
    public class SimpleSteeringSensor : NavigatorSensorBase
    {
        [Header("Steering Settings")]
        public float ObstacleDetectRange = 1.5f;
        public LayerMask ObstacleMask;

        [Header("Jump Detection Settings")]
        [Tooltip("如果 AI 在此时间内移动距离不足阈值，触发跳跃尝试")]
        public float StuckCheckInterval = 0.3f;
        [Tooltip("在 StuckCheckInterval 时间内，移动距离不应低于此值，否则视为卡住")]
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

            Vector3 gapCheckStart = myPos + desiredDir * 1.5f + Vector3.up * 0.5f;
            if (!Physics.Raycast(gapCheckStart, Vector3.down, 2f, ObstacleMask))
            {
                needsJump = true; 
            }

            if (Physics.Raycast(rayStart, desiredDir, out RaycastHit hit, ObstacleDetectRange, ObstacleMask))
            {
                desiredDir = Vector3.ProjectOnPlane(desiredDir, hit.normal).normalized;

                if (!needsJump && hit.distance < 0.8f)
                {
                    needsJump = true;
                }
            }

            _stuckCheckTimer -= Time.deltaTime;
            if (_stuckCheckTimer <= 0)
            {
                float movedDistance = Vector3.Distance(myPos, _lastCheckPos);
                
                if (movedDistance < StuckDistanceThreshold && !_wasStuck)
                {
                    needsJump = true; 
                    _wasStuck = true;
                }
                else if (movedDistance >= StuckDistanceThreshold * 1.5f)
                {
                    _wasStuck = false;
                }

                _lastCheckPos = myPos;
                _stuckCheckTimer = StuckCheckInterval;
            }

            _currentContext = new NavigationContext(desiredDir, trueTargetDir, dist, true, needsJump);
        }
    }
}