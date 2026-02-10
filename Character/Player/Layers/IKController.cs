using UnityEngine;
using Characters.Player.Core.IK;
using Characters.Player.Data;

namespace Characters.Player.Layers
{
    /// <summary>
    /// IK 系统管理器 (Data-Driven)。
    /// 职责：
    /// 1. 每帧读取 RuntimeData 中的 IK 意图 (WantsHandIK, Targets)。
    /// 2. 计算权重的平滑过渡 (SmoothDamp)。
    /// 3. 将最终数据传递给底层的 IK 策略 (Native/FinalIK)。
    /// </summary>
    public class IKController
    {
        private PlayerController _player;
        private PlayerRuntimeData _data;
        private PlayerSO _config;
        private IPlayerIKSource _ikSource; // 底层策略

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

            // 策略选择：这里使用 Native
            _ikSource = new NativeIKSource(player.animator);
        }

        /// <summary>
        /// 在 PlayerController.Update 中调用。
        /// 负责计算权重的平滑过渡，不涉及具体的 IK 求解。
        /// </summary>
        public void Update()
        {
            // 1. 左手权重平滑
            float targetLeftW = _data.WantsLeftHandIK ? 1f : 0f;
            _leftHandWeight = Mathf.SmoothDamp(_leftHandWeight, targetLeftW, ref _leftHandVelocity, 0.15f);

            // 2. 右手权重平滑
            float targetRightW = _data.WantsRightHandIK ? 1f : 0f; // 假设双手共用一个开关，也可以分开
            _rightHandWeight = Mathf.SmoothDamp(_rightHandWeight, targetRightW, ref _rightHandVelocity, 0.15f);

            // 3. 注视权重平滑
            float targetLookW = _data.WantsLookAtIK ? 1f : 0f;
            _lookAtWeight = Mathf.SmoothDamp(_lookAtWeight, targetLookW, ref _lookAtVelocity, 0.2f);

        }

        /// <summary>
        /// 在 PlayerController.OnAnimatorIK 中调用。
        /// 负责将数据应用到底层 IK 系统。
        /// </summary>
        public void OnAnimatorIK_Internal(int layerIndex)
        {
            // 只有当权重 > 0 时才传递目标，节省开销
            if (_leftHandWeight > 0.01f)
                _ikSource.SetLeftHandIK(_data.LeftHandGoal, _leftHandWeight);
            else
                _ikSource.SetLeftHandIK(null, 0f);

            if (_rightHandWeight > 0.01f)
                _ikSource.SetRightHandIK(_data.RightHandGoal, _rightHandWeight);
            else
                _ikSource.SetRightHandIK(null, 0f);

            if (_lookAtWeight > 0.01f)
                _ikSource.SetLookAtTarget(_data.LookAtPosition, _lookAtWeight);
            else
                _ikSource.SetLookAtTarget(Vector3.zero, 0f);

            // 驱动底层
            _ikSource.OnUpdateIK(layerIndex);
        }
    }
}
