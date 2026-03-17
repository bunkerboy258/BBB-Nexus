using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 性能优化驱动器
    /// 根据 SO 配置的频率 检测角色与相机的距离 裁决当前lod
    /// </summary>
    public class OptimizationDriver
    {
        private readonly PlayerController _player;
        private float _checkTimer;

        public OptimizationDriver(PlayerController player)
        {
            _player = player;

            // 注: 加入随机初始偏移
            // 确保同屏刷多个敌人时 不会一起执行距离计算 防止CPU峰值
            float interval = _player.Config.Core.LODCheckInterval;
            _checkTimer = Random.Range(0f, interval);
        }

        public void Update()
        {
            // 如果黑板上还没有相机引用，直接挂机
            if (_player.RuntimeData.CameraTransform == null)
            {
                Debug.Log("没有设置主相机 lod分级无法工作");
                return;
            }

            _checkTimer += Time.deltaTime;

            // 读取 SO 中的自定义间隔
            if (_checkTimer >= _player.Config.Core.LODCheckInterval)
            {
                _checkTimer = 0f;
                UpdateLODState();
            }
        }

        private void UpdateLODState()
        {
            // 注:使用 sqrMagnitude (平方距离)，省去极其昂贵的开平方(Mathf.Sqrt)运算
            float sqrDist = (_player.transform.position - _player.RuntimeData.CameraTransform.position).sqrMagnitude;

            // 将 SO 里的距离也平方后进行比较
            float medDistSqr = _player.Config.Core.MediumLODDistance * _player.Config.Core.MediumLODDistance;
            float lowDistSqr = _player.Config.Core.LowLODDistance * _player.Config.Core.LowLODDistance;

            CharacterLOD newLOD = CharacterLOD.High;

            if (sqrDist > lowDistSqr)
            {
                newLOD = CharacterLOD.Low;
            }
            else if (sqrDist > medDistSqr)
            {
                newLOD = CharacterLOD.Medium;
            }

            // 只有当 LOD 发生实质性跨越时，才去改黑板和底层组件
            if (newLOD != _player.RuntimeData.CurrentLOD)
            {
                _player.RuntimeData.CurrentLOD = newLOD;
                ApplyEngineCulling(newLOD);
            }
        }

        private void ApplyEngineCulling(CharacterLOD lod)
        {
            // 当处于 Low 级别时，通知 Animator 直接放弃计算骨骼的 Transform 矩阵
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