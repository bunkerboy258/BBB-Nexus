using UnityEngine;
using Characters.Player.Data;

namespace Characters.Player.Core
{
    /// <summary>
    /// LOD 性能仲裁器 (直属 Core 层)
    /// 以低开销在主线程进行降频裁决 仲裁角色当前的表现层级 并下发给底层 Animator
    /// </summary>
    public class LODArbiter
    {
        private readonly PlayerController _player;
        private readonly PlayerRuntimeData _data;
        private readonly PlayerSO _config;

        private float _timeSinceLastArbitration;

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

            if (targetLOD != _data.CurrentLOD)
            {
                _data.CurrentLOD = targetLOD;
                EnforceArbitration(targetLOD);
            }
        }

        // 强制执行仲裁结果
        private void EnforceArbitration(CharacterLOD lod)
        {
            if (_player.animator == null) return;

            if (lod == CharacterLOD.Low)
            {
                _player.animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            }
            else
            {
                _player.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
        }
    }
}