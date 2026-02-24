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

        private Vector3 _currentLookAtPosition;
        private Vector3 _lookAtPositionVelocity;
        private float _lookAtPositionSmoothTime = 0.1f;

        // 引用 PlayerController 中的 IK 源 (MonoBehaviour)
        private IPlayerIKSource _ikSource => _player.IKSource;

        // 上一次的基准点，避免每帧重复调用 SetIKTarget 消耗性能
        private Transform _lastAimReference=null;

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
            // 1. 通用 Warp IK 拦截 (最高优先级)
            // =================================================================================
            if (_data.IsWarping)
            {
                // Debug.Log(_data.ActiveWarpData.Clip.Name);
                // 直接从当前的 Warp 数据表里抽取权重
                float warpHandWeight = _data.ActiveWarpData.HandIKWeightCurve.Evaluate(_data.NormalizedWarpTime);

                if (warpHandWeight > 0.01f)
                {
                    // 无脑把手放到 RuntimeData 里指定的点上
                    _ikSource.SetIKTarget(IKTarget.LeftHand, _data.WarpIKTarget_LeftHand, _data.WarpIKRotation_Hand, warpHandWeight);
                    _ikSource.SetIKTarget(IKTarget.RightHand, _data.WarpIKTarget_RightHand, _data.WarpIKRotation_Hand, warpHandWeight);
                    //Debug.Log(_data.WarpIKTarget_LeftHand);
                    //if(warpHandWeight>0.1) Debug.Log(warpHandWeight);

                    // 只要 Warp IK 处于激活状态，直接 return 阻断普通的持枪 IK
                    return;
                }

                return;
            }

            if (_data.IsAiming)
            {
                if (_ikSource == null) return;


                // 同步指向基准物
                // 只有当玩家换了物品，或者基准点发生变化时，才通知底层 IK
                if (_data.CurrentAimReference != _lastAimReference)
                {
                    // 将黑板上的基准 Transform 发送给底层 (FinalIKSource 收到后会把它赋给 AimIK.solver.transform)
                    _ikSource.SetIKTarget(IKTarget.AimReference, _data.CurrentAimReference, 1f);
                    _lastAimReference = _data.CurrentAimReference;
                }

                // =================================================================================
                // 1. 上半身指向 (LookAt / Aim) 处理
                // =================================================================================
                // 这里的逻辑保持不变，依然是消费 TargetAimPoint (Vector3)
                float targetLookw = _data.WantsLookAtIK ? 1f : 0f;
                _lookAtWeight = Mathf.SmoothDamp(_lookAtWeight, targetLookw, ref _lookAtVelocity, 0.2f);

                if (_lookAtWeight > 0.01f)
                {
                    // 底层（FinalIKSource）现在已经知道该用“谁”去指向这个坐标了！
                    _ikSource.SetIKTarget(
                        IKTarget.HeadLook,
                        _data.TargetAimPoint, // 之前由摄像机驱动器算出的精确世界坐标
                        Quaternion.identity,
                        _lookAtWeight
                    );
                }
                else
                {
                    _ikSource.UpdateIKWeight(IKTarget.HeadLook, 0f);
                    if (_lookAtWeight < 0.01f) _lookAtVelocity = 0f;
                }
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
            _lookAtWeight = Mathf.SmoothDamp(_lookAtWeight, targetLookW, ref _lookAtVelocity, 0.2f);

            if (_lookAtWeight > 0.01f)
            {
                // 【核心修改】：平滑注视点的位置
                // 这样即使相机准星坐标突变，角色的头也会有一个“转过去”的过程
                _currentLookAtPosition = Vector3.SmoothDamp(
                    _currentLookAtPosition,
                    _data.LookAtPosition, // 这是黑板上瞬间更新的目标点
                    ref _lookAtPositionVelocity,
                    _lookAtPositionSmoothTime
                );

                _ikSource.SetIKTarget(
                    IKTarget.HeadLook,
                    _currentLookAtPosition, // 传入平滑后的坐标
                    Quaternion.identity,
                    _lookAtWeight
                );
            }
            else
            {
                _ikSource.UpdateIKWeight(IKTarget.HeadLook, 0f);
                if (_lookAtWeight < 0.01f)
                {
                    _lookAtVelocity = 0f;
                    // 当取消瞄准时，可以将当前注视点重置为前方，防止下次瞄准时头从奇怪的地方转过来
                    _currentLookAtPosition = _player.transform.position + _player.transform.forward * 5f;
                }
            }

            // UAR 不需要显式的 OnAnimatorIK 或 OnUpdateIK 调用，
            // 只要设置了 Target 和 Weight，Constraint 就会自动生效。
        }
    }
}

