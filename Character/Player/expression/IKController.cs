using UnityEngine;
using Characters.Player.Core;      
using Characters.Player.Data;

namespace Characters.Player.Layers
{
    /// <summary>
    /// IK 系统管理器 (Data-Driven)。
    /// 职责：
    /// 1. 每帧读取 RuntimeData 中的 IK 意图 (WantsHandIK, Targets)。
    /// 2. 计算权重的平滑过渡 (SmoothDamp)。
    /// 3. 将最终数据传递给底层的 IK 策略 (UAR/FinalIK)。
    /// </summary>
    public class IKController
    {
        private PlayerController _player;
        private PlayerRuntimeData _data;
        private PlayerSO _config;

        // 引用 PlayerController 中的 IK 源 (MonoBehaviour)
        private IPlayerIKSource _ikSource => _player.IKSource;

        // --- 运行时平滑状态 ---
        private float _leftHandWeight;
        private float _leftHandVelocity;

        private float _rightHandWeight;
        private float _rightHandVelocity;

        private float _lookAtWeight;
        private float _lookAtVelocity;

        public IKController(PlayerController player)
        {
            _player = player;
            _data = player.RuntimeData;
            _config = player.Config;

            // _ikSource 不需要初始化，它是通过属性直接访问 PlayerController 上的组件
        }

        /// <summary>
        /// 在 PlayerController.Update (或 LateUpdate) 中调用。
        /// 负责计算权重的平滑过渡，并将数据同步到底层 IK 源。
        /// </summary>
        public void Update()
        {
            if (_ikSource == null)
            {
                Debug.LogError("[IKController] IK Source is NULL! Please ensure PlayerController has a valid IK Source component.");
                return;
            }

            // =================================================================================
            // 1. 左手 IK 处理
            // =================================================================================
            float targetLeftW = _data.WantsLeftHandIK ? 1f : 0f;
            _leftHandWeight = Mathf.SmoothDamp(_leftHandWeight, targetLeftW, ref _leftHandVelocity, 0.15f); // 0.15f 可提取配置

            if (_leftHandWeight > 0.01f && _data.LeftHandGoal != null)
            {
                // 有目标且权重 > 0：设置 Target 并应用权重
                _ikSource.SetIKTarget(IKTarget.LeftHand, _data.LeftHandGoal, _leftHandWeight);
            }
            else
            {
                // 无目标或权重归零：仅淡出权重 (目标位置保持不变或设为null，取决于实现)
                _ikSource.UpdateIKWeight(IKTarget.LeftHand, 0f);

                // 重置平滑速度，防止下次激活时突变
                if (_leftHandWeight < 0.01f) _leftHandVelocity = 0f;
            }

            // =================================================================================
            // 2. 右手 IK 处理
            // =================================================================================
            float targetRightW = _data.WantsRightHandIK ? 1f : 0f;
            _rightHandWeight = Mathf.SmoothDamp(_rightHandWeight, targetRightW, ref _rightHandVelocity, 0.15f);

            if (_rightHandWeight > 0.01f && _data.RightHandGoal != null)
            {
                _ikSource.SetIKTarget(IKTarget.RightHand, _data.RightHandGoal, _rightHandWeight);
            }
            else
            {
                _ikSource.UpdateIKWeight(IKTarget.RightHand, 0f);
                if (_rightHandWeight < 0.01f) _rightHandVelocity = 0f;
            }

            // =================================================================================
            // 3. 头部注视 (LookAt) 处理
            // =================================================================================
            float targetLookW = _data.WantsLookAtIK ? 1f : 0f;
            _lookAtWeight = Mathf.SmoothDamp(_lookAtWeight, targetLookW, ref _lookAtVelocity, 0.2f); // 0.2f 可提取配置

            if (_lookAtWeight > 0.01f)
            {
                // LookAt 使用 Vector3 坐标 (不需要旋转)
                // RuntimeData.LookAtPoint 通常由 AimIntentProcessor 计算
                _ikSource.SetIKTarget(
                    IKTarget.HeadLook,
                    _data.LookAtPosition, // 注意：确保 RuntimeData 里有这个字段 (Vector3)
                    Quaternion.identity,
                    _lookAtWeight
                );
            }
            else
            {
                _ikSource.UpdateIKWeight(IKTarget.HeadLook, 0f);
                if (_lookAtWeight < 0.01f) _lookAtVelocity = 0f;
            }

            // UAR 不需要显式的 OnAnimatorIK 或 OnUpdateIK 调用，
            // 只要设置了 Target 和 Weight，Constraint 就会自动生效。
        }
    }
}

