using UnityEngine;

namespace Characters.Player.Core.IK
{
    public class NativeIKSource : IPlayerIKSource
    {  
        private Animator _animator;

        // --- 内部状态 ---
        private Transform _leftHandTarget;
        private float _leftHandWeight;

        private Transform _rightHandTarget;
        private float _rightHandWeight;

        private Vector3 _lookAtPos;
        private float _lookAtWeight;

        public NativeIKSource(Animator animator)
        {
            _animator = animator;
        }

        // 接口方法：设置数据
        public void SetLeftHandIK(Transform target, float weight)
        {
            _leftHandTarget = target;
            _leftHandWeight = weight;
        }

        public void SetRightHandIK(Transform target, float weight)
        {
            _rightHandTarget = target;
            _rightHandWeight = weight;
        }

        public void SetLookAtTarget(Vector3 targetPosition, float weight)
        {
            _lookAtPos = targetPosition;
            _lookAtWeight = weight;
        }

        // 接口方法：执行逻辑
        // 这个方法必须在 PlayerController 的 OnAnimatorIK 中被调用
        public void OnUpdateIK(int layerIndex)
        {
            if (_animator == null) return;
            // --- 1. 左手 IK ---
            if (_leftHandTarget != null && _leftHandWeight > 0)
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, _leftHandWeight);
                _animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, _leftHandWeight);
                _animator.SetIKPosition(AvatarIKGoal.LeftHand, _leftHandTarget.position);
                _animator.SetIKRotation(AvatarIKGoal.LeftHand, _leftHandTarget.rotation);
            }
            else
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);  
                _animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
            }

            // --- 2. 右手 IK ---
            if (_rightHandTarget != null && _rightHandWeight > 0)
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, _rightHandWeight);
                _animator.SetIKRotationWeight(AvatarIKGoal.RightHand, _rightHandWeight);
                _animator.SetIKPosition(AvatarIKGoal.RightHand, _rightHandTarget.position);
                _animator.SetIKRotation(AvatarIKGoal.RightHand, _rightHandTarget.rotation);
            }
            else
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
                _animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
            }

            // --- 3. Look At (身体瞄准) ---
            if (_lookAtWeight > 0)
            {
                _animator.SetLookAtWeight(_lookAtWeight, 0.3f, 1f, 0.5f, 0.7f); // 参数可调
                _animator.SetLookAtPosition(_lookAtPos);
            }
            else
            {
                _animator.SetLookAtWeight(0f);
            }
        }
    }
}
