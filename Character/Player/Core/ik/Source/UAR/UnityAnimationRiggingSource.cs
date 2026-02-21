using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Characters.Player.Core
{
    public class UnityAnimationRiggingSource : PlayerIKSourceBase
    {
        [Header("Hand IK Components")]
        [SerializeField] private TwoBoneIKConstraint _leftHandIK;
        [SerializeField] private TwoBoneIKConstraint _rightHandIK;

        [Header("Hand IK Targets (Proxies)")]
        // 这是 Prefab 内部固定的空物体 (Target_Hand_L / R)
        // IK Constraint 的 data.target 永远指向它们，不要改引用！
        [SerializeField] private Transform _leftHandTarget;
        [SerializeField] private Transform _rightHandTarget;

        [Header("Head LookAt IK")]
        [SerializeField] private MultiAimConstraint _headLookAtIK;
        [SerializeField] private Transform _lookAtTarget; // Prefab 内部固定的 LookAt_Target

        // [可选] 用于将来控制 Layer 权重
        [SerializeField] private RigBuilder _rigBuilder;

        // --- 接口实现 ---

        public override void SetIKTarget(IKTarget target, Transform targetTransform, float weight)
        {
            switch (target)
            {
                case IKTarget.LeftHand:
                    if (_leftHandIK != null && _leftHandTarget != null)
                    {
                        // 1. 更新代理 Target 的位置 (核心逻辑)
                        if (targetTransform != null)
                        {
                            _leftHandTarget.position = targetTransform.position;
                            _leftHandTarget.rotation = targetTransform.rotation;
                        }

                        // 2. 更新权重
                        _leftHandIK.weight = weight;
                    }
                    break;

                case IKTarget.RightHand:
                    if (_rightHandIK != null && _rightHandTarget != null)
                    {
                        // 1. 更新代理 Target 的位置
                        if (targetTransform != null)
                        {
                            _rightHandTarget.position = targetTransform.position;
                            _rightHandTarget.rotation = targetTransform.rotation;
                        }

                        // 2. 更新权重
                        _rightHandIK.weight = weight;
                    }
                    break;
            }
        }

        public override void SetIKTarget(IKTarget target, Vector3 position, Quaternion rotation, float weight)
        {
            switch (target)
            {
                case IKTarget.LeftHand:
                    if (_leftHandIK != null && _leftHandTarget != null)
                    {
                        _leftHandTarget.position = position;
                        _leftHandTarget.rotation = rotation;
                        _leftHandIK.weight = weight;
                    }
                    break;

                case IKTarget.RightHand:
                    if (_rightHandIK != null && _rightHandTarget != null)
                    {
                        _rightHandTarget.position = position;
                        _rightHandTarget.rotation = rotation;
                        _rightHandIK.weight = weight;
                    }
                    break;

                case IKTarget.HeadLook:
                    if (_lookAtTarget != null)
                    {
                        _lookAtTarget.position = position;
                        if (_headLookAtIK != null)
                        {
                            _headLookAtIK.weight = weight;
                        }
                    }
                    break;
            }
        }

        public override void UpdateIKWeight(IKTarget target, float weight)
        {
            switch (target)
            {
                case IKTarget.LeftHand:
                    if (_leftHandIK != null) _leftHandIK.weight = weight;
                    break;

                case IKTarget.RightHand:
                    if (_rightHandIK != null) _rightHandIK.weight = weight;
                    break;

                case IKTarget.HeadLook:
                    if (_headLookAtIK != null) _headLookAtIK.weight = weight;
                    break;
            }
        }
    }
}