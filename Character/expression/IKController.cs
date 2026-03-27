using UnityEngine;

namespace BBBNexus
{
    // IK 结算控制器
    // 负责处理左手/右手的 IK 权重 平滑追踪 Aim IK 与 Warp IK 的拦截逻辑
    // 它从运行时黑板读取意图并将目标与权重下发到 IPlayerIKSource
    public class IKController
    {
        private BBBCharacterController _player;
        private PlayerRuntimeData _data;
        private PlayerSO _config;

        private Vector3 _currentLookAtPosition;
        private Vector3 _lookAtPositionVelocity;
        private float _lookAtPositionSmoothTime;

        private IPlayerIKSource _ikSource => _player.IKSource;
        private Transform _lastAimReference = null;

        private float _leftHandWeight;
        private float _leftHandVelocity;

        private float _rightHandWeight;
        private float _rightHandVelocity;

        private float _lookAtWeight;
        private float _lookAtVelocity;

        private bool _isIKActive = true;

        public IKController(BBBCharacterController player)
        {
            _player = player;
            _data = player.RuntimeData;
            _config = player.Config;
            _lookAtPositionSmoothTime = _config.Aiming.AimIkChaseSmoothTime;
        }

        // 优先级：LOD拦截 -> Warp IK 拦截 -> Aim 基准点更新 -> 左手 IK -> 右手 IK -> 头部注视
        public void Update()
        {
            if (_ikSource == null) return;

            // 物品切换/对象池回收场景下：AimReference 可能指向已失活的 muzzle
            // 这里做一次检查 保证底层 IK 不会持续追踪无效 Transform (不然角色会跟开了挂一样转起来)
            SanitizeAimReference();

            if (_data.Arbitration.BlockIK)
            {
                if (_isIKActive)
                {
                    ResetAllIKWeights();
                    _ikSource.DisableAllIK();
                    _isIKActive = false;
                }
                return;
            }

            if (!_isIKActive)
            {
                _ikSource.EnableAllIK();
                _isIKActive = true;
            }

            // 翻越/攀爬WarpIK更新
            if (_data.IsWarping)
            {
                float warpHandWeight = _data.ActiveWarpData.HandIKWeightCurve.Evaluate(_data.NormalizedWarpTime);
                if (warpHandWeight > 0.01f)
                {
                    _ikSource.SetIKTarget(IKTarget.LeftHand, _data.WarpIKTarget_LeftHand, _data.WarpIKRotation_Hand, warpHandWeight);
                    _ikSource.SetIKTarget(IKTarget.RightHand, _data.WarpIKTarget_RightHand, _data.WarpIKRotation_Hand, warpHandWeight);
                    return; // 阻断普通 IK
                }
                return;
            }

            // AimIK 基准点更新（注：从瞄准切到非瞄准时必须清理）
            if (_data.IsAiming && _data.WantsLookAtIK && _data.CurrentAimReference != null)
            {
                if (_data.CurrentAimReference != _lastAimReference)
                {
                    _ikSource.SetIKTarget(IKTarget.AimReference, _data.CurrentAimReference, 1f);
                    _lastAimReference = _data.CurrentAimReference;
                }
            }
            else
            {
                // 非瞄准/无引用：确保 AimReference 权重归零 并清掉缓存引用
                if (_lastAimReference != null)
                {
                    _ikSource.UpdateIKWeight(IKTarget.AimReference, 0f);
                    _lastAimReference = null;
                }
            }

            // 左手 IK 处理
            float targetLeftW = _data.WantsLeftHandIK ? 1f : 0f;
            _leftHandWeight = Mathf.SmoothDamp(_leftHandWeight, targetLeftW, ref _leftHandVelocity, 0.15f);

            if (_leftHandWeight > 0.01f && _data.LeftHandGoal != null && _data.WantsLeftHandIK)
                _ikSource.SetIKTarget(IKTarget.LeftHand, _data.LeftHandGoal, _leftHandWeight);
            else
            {
                _ikSource.UpdateIKWeight(IKTarget.LeftHand, 0f);
                if (_leftHandWeight < 0.01f) _leftHandVelocity = 0f;
            }

            // 右手 IK 处理
            float targetRightW = _data.WantsRightHandIK ? 1f : 0f;
            _rightHandWeight = Mathf.SmoothDamp(_rightHandWeight, targetRightW, ref _rightHandVelocity, 0.15f);
                
            if (_rightHandWeight > 0.01f && _data.RightHandGoal != null)
                _ikSource.SetIKTarget(IKTarget.RightHand, _data.RightHandGoal, _rightHandWeight);
            else
            {
                _ikSource.UpdateIKWeight(IKTarget.RightHand, 0f);
                if (_rightHandWeight < 0.01f) _rightHandVelocity = 0f;
            }

            // 头部注视 / 武器瞄准IK统一处理
            float targetLookW = _data.WantsLookAtIK ? 1f : 0f;
            _lookAtWeight = Mathf.SmoothDamp(_lookAtWeight, targetLookW, ref _lookAtVelocity, 0.2f);

            if (_lookAtWeight > 0.01f)
            {
                Vector3 desiredTarget = _data.TargetAimPoint;

                _currentLookAtPosition = Vector3.SmoothDamp(
                    _currentLookAtPosition,
                    desiredTarget,
                    ref _lookAtPositionVelocity,
                    _lookAtPositionSmoothTime
                );

                _ikSource.SetIKTarget(
                    IKTarget.HeadLook,
                    _currentLookAtPosition,
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
                    _currentLookAtPosition = _player.transform.position + _player.transform.forward * 5f;
                }
            }
        }

        private void SanitizeAimReference()
        {
            Transform current = _data.CurrentAimReference;

            if (_data.CurrentAimReference != null) return;

            // 清理黑板，防止后续系统继续写入旧引用
            _data.CurrentAimReference = null;
            _data.WantsLookAtIK = false;

            // 同步清理 IKController 内部缓存 + 底层权重
            if (_lastAimReference != null)
            {
                _ikSource.UpdateIKWeight(IKTarget.AimReference, 0f);
                _lastAimReference = null;
            }
        }

        private void ResetAllIKWeights()
        {
            // 如果已经被彻底关干净了，就不重复执行
            if (_leftHandWeight == 0f && _rightHandWeight == 0f && _lookAtWeight == 0f) return;

            // 内部平滑速度和当前权重立刻清零 
            _leftHandWeight = 0f;
            _rightHandWeight = 0f;
            _lookAtWeight = 0f;
            _leftHandVelocity = 0f;
            _rightHandVelocity = 0f;
            _lookAtVelocity = 0f;

            // 向底层传达归零指令（确保本系统在被瘫痪的前一刻，内部参数已经归零）
            _ikSource.UpdateIKWeight(IKTarget.LeftHand, 0f);
            _ikSource.UpdateIKWeight(IKTarget.RightHand, 0f);
            _ikSource.UpdateIKWeight(IKTarget.HeadLook, 0f);
            _ikSource.UpdateIKWeight(IKTarget.AimReference, 0f);
        }
    }
}