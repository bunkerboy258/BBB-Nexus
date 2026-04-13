using UnityEngine;

namespace BBBNexus
{
    // 每帧将世界空间"理想命中点"写入黑板。
    // 由 MainProcessorPipeline.UpdateParameterProcessors() 驱动。
    // 玩家模式：从相机中心向前 Raycast。
    // AI 模式（无相机）：从角色眼部朝 AuthorityYaw/Pitch 方向 Raycast。
    // 下游消费者（如 PistolBehaviour）从 RuntimeData.TargetAimPoint 读取后
    // 再做枪口→目标点的二次射线，得到真实命中结果。
    public class AimPointParameterProcessor
    {
        private readonly PlayerRuntimeData _data;
        private readonly Transform _selfTransform;
        private readonly float _maxRange;
        private readonly LayerMask _layerMask;

        public AimPointParameterProcessor(PlayerRuntimeData data, float maxRange = 150f, LayerMask layerMask = default, Transform selfTransform = null)
        {
            _data = data;
            _selfTransform = selfTransform;
            _maxRange = maxRange;
            // default(LayerMask) 的 value 为 0，代表"没有层"——改为全层
            _layerMask = (layerMask.value == 0) ? ~0 : layerMask;
        }

        public void Update()
        {
            var cam = _data.CameraTransform;

            Vector3 origin;
            Vector3 direction;

            if (cam != null)
            {
                // 玩家模式：以相机为准
                origin = cam.position;
                direction = cam.forward;
                _data.CameraLookDirection = direction;
            }
            else if (_selfTransform != null)
            {
                origin = _selfTransform.position + Vector3.up * 1.3f;

                // AI 没有玩家相机。若能拿到导航目标，则直接把准星意图写到目标身体上；
                // 真实遮挡与命中仍由武器从枪口再做一次射线决定。
                if (TryGetAiTargetPoint(out var aiTargetPoint))
                {
                    direction = (aiTargetPoint - origin).normalized;
                    _data.CameraLookDirection = direction;
                    _data.TargetAimPoint = aiTargetPoint;
                    return;
                }

                // 回退：沿当前权威朝向生成远点，至少保证无目标时协议完整。
                direction = Quaternion.Euler(_data.AuthorityPitch, _data.AuthorityYaw, 0f) * Vector3.forward;
                _data.CameraLookDirection = direction;
            }
            else
            {
                return;
            }

            if (Physics.Raycast(origin, direction, out RaycastHit hit, _maxRange, _layerMask, QueryTriggerInteraction.Ignore))
                _data.TargetAimPoint = hit.point;
            else
                _data.TargetAimPoint = origin + direction * _maxRange;
        }

        private bool TryGetAiTargetPoint(out Vector3 point)
        {
            point = default;

            var sensor = _selfTransform.GetComponentInChildren<NavigatorSensorBase>();
            var target = sensor != null ? sensor.Target : null;
            if (target == null)
                return false;

            if (target.TryGetComponent<BBBCharacterController>(out var controller))
            {
                if (controller.HeadBone != null)
                {
                    point = controller.HeadBone.position;
                    return true;
                }

                point = controller.transform.position + Vector3.up * 1.3f;
                return true;
            }

            if (target.TryGetComponent<Collider>(out var collider))
            {
                point = collider.bounds.center;
                return true;
            }

            point = target.position + Vector3.up * 1.3f;
            return true;
        }
    }
}
