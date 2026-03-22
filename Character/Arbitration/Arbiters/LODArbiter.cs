using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// LOD 性能仲裁器 
    /// 以低开销在主线程进行降频裁决 仲裁角色当前的表现层级 并下发给底层 Animator
    /// </summary>
    public class LODArbiter
    {
        private readonly PlayerController _player;
        private readonly PlayerRuntimeData _data;
        private readonly PlayerSO _config;

        private float _timeSinceLastArbitration;
        private CharacterLOD _lastEnforcedLOD = CharacterLOD.High;

        public LODArbiter(PlayerController player)
        {
            _player = player;
            _data = player.RuntimeData;
            _config = player.Config;

            // 初始随机错峰 防止多角色同时检测
            if (_config != null && _config.Core != null)
            {
                _timeSinceLastArbitration = Random.Range(0f, _config.Core.LODCheckInterval);
            }
        }

        public void Arbitrate()
        {
            if (_data.CameraTransform == null || _config == null || _config.Core == null) return;

            _timeSinceLastArbitration += Time.deltaTime;
            if (_timeSinceLastArbitration < _config.Core.LODCheckInterval) return;

            _timeSinceLastArbitration = 0f;
            ExecuteArbitrationLogic();
        }

        private void ExecuteArbitrationLogic()
        {
            float sqrDist = (_player.transform.position - _data.CameraTransform.position).sqrMagnitude;

            float medDistSqr = _config.Core.MediumLODDistance * _config.Core.MediumLODDistance;
            float lowDistSqr = _config.Core.LowLODDistance * _config.Core.LowLODDistance;

            CharacterLOD targetLOD = CharacterLOD.High;

            if (sqrDist > lowDistSqr)
            {
                targetLOD = CharacterLOD.Low;
            }
            else if (sqrDist > medDistSqr)
            {
                targetLOD = CharacterLOD.Medium;
            }

            if (targetLOD != _lastEnforcedLOD)
            {
                _lastEnforcedLOD = targetLOD;
                EnforceArbitration(targetLOD);
            }
        }

        // 强制执行仲裁结果
        private void EnforceArbitration(CharacterLOD lod)
        {
            if (_player.animator == null) return;

            // 更新运行时仲裁标志，其他系统根据这些标志做只读判断
            // 原实现中：CurrentLOD > High 时会对一些系统进行降级处理
            // 这里将相同策略以仲裁标志的形式下发：当 lod != High 时视为降级
            bool isDegraded = (lod != CharacterLOD.High);
            _data.Arbitration.BlockIK = isDegraded;
            _data.Arbitration.BlockFacial = isDegraded;

            // 根据 LOD 级别调整 Animator 的实际工作状态
            // High: 完全启用，所有系统工作
            // Medium: 禁用动画更新，但保持结构 (避免频繁启用/禁用导致的抖动)
            // Low: 完全禁用 Animator，仅保留位移
            switch (lod)
            {
                case CharacterLOD.High:
                    _player.animator.enabled = true;
                    _player.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    break;

                case CharacterLOD.Medium:
                    // 禁用 Animator 的更新以节省 CPU
                    // 保留当前动画姿态，不进行任何混合或参数计算
                    _player.animator.enabled = false;
                    break;

                case CharacterLOD.Low:
                    // 最严格的降级：彻底关闭 Animator
                    _player.animator.enabled = false;
                    _player.animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                    break;
            }
        }
    }
}