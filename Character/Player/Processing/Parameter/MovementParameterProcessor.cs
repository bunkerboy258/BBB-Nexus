using UnityEngine;
using Characters.Player.Data;

namespace Characters.Player.Processing
{
    public class MovementParameterProcessor
    {
        private PlayerSO _config;
        private PlayerRuntimeData _data;
        private Transform _playerTransform;

        // --- 平滑状态变量 ---
        private float _blendTimer;
        private float _startIntensity;

        private float _targetIntensity;
        private float _currentIntensity; // 当前强度 (0.7 ~ 1.0)

        // 方向向量平滑（替代角度平滑，避免 -180/180 跳变）
        private Vector3 _currentLocalDir = Vector3.forward;
        private Vector3 _localDirVelocity;

        public MovementParameterProcessor(PlayerController player)
        {
            _config = player.Config;
            _data = player.RuntimeData;
            _playerTransform = player.transform;

            _currentIntensity = 0.7f;
            _currentLocalDir = Vector3.forward;
            _localDirVelocity = Vector3.zero;
        }

        public void Update()
        {
            // 0) 统一出移动派生数据（状态机/动画共用）
            UpdateMovementDerivedData();

            // 1) 平滑强度
            UpdateIntensityLogic();

            // 2) 平滑方向并输出 Mixer 参数
            UpdateDirectionBlend();
        }

        private void UpdateMovementDerivedData()
        {
            // 默认值
            _data.DesiredWorldMoveDir = Vector3.zero;
            _data.DesiredLocalMoveAngle = 0f;

            Vector2 input = _data.MoveInput;
            if (input.sqrMagnitude < 0.001f) return;

            // 以 AuthorityYaw 为参考系得到世界方向
            float worldAngle = Mathf.Atan2(input.x, input.y) * Mathf.Rad2Deg + _data.AuthorityYaw;
            Vector3 worldDir = Quaternion.Euler(0f, worldAngle, 0f) * Vector3.forward;
            worldDir.y = 0f;
            _data.DesiredWorldMoveDir = worldDir.sqrMagnitude > 0.0001f ? worldDir.normalized : Vector3.zero;

            // 这里不再直接用角度驱动 Mixer（角度会在 ±180 发生跳变），角度在 UpdateDirectionBlend 中由平滑后的向量得到。
        }

        // --- Y 轴逻辑 (强度/速度) ---
        private void UpdateIntensityLogic()
        {
            float target = _data.IsRunning ? 1.0f : 0.7f;

            // 检测目标变化 (按键切换)
            if (Mathf.Abs(target - _targetIntensity) > 0.01f)
            {
                _blendTimer = 0f;
                _startIntensity = _currentIntensity;
                _targetIntensity = target;
            }

            // 曲线平滑
            float curveTime = _config.SprintBlendCurve.keys[_config.SprintBlendCurve.length - 1].time;
            if (_blendTimer < curveTime)
            {
                _blendTimer += Time.deltaTime;
                float t = _blendTimer / curveTime;
                float factor = _config.SprintBlendCurve.Evaluate(t * curveTime);
                _currentIntensity = Mathf.Lerp(_startIntensity, _targetIntensity, factor);
            }
            else
            {
                _currentIntensity = _targetIntensity;
            }
        }

        private void UpdateDirectionBlend()
        {
            // 目标局部方向：由 DesiredWorldMoveDir 转成本地向量
            Vector3 targetLocalDir;
            if (_data.DesiredWorldMoveDir.sqrMagnitude < 0.0001f)
            {
                // 无输入时回到正前方，防止停下后保留侧向导致混合停在 Lean 上
                targetLocalDir = Vector3.forward;
            }
            else
            {
                targetLocalDir = _playerTransform.InverseTransformDirection(_data.DesiredWorldMoveDir);
                targetLocalDir.y = 0f;
                targetLocalDir = targetLocalDir.sqrMagnitude > 0.0001f ? targetLocalDir.normalized : Vector3.forward;
            }

            // SmoothDamp 方向向量：保证连续，不会出现 angle 的 ±180 跳变
            float smoothTime = Mathf.Max(0.0001f, _config.XAnimBlendSmoothTime);
            _currentLocalDir = Vector3.SmoothDamp(
                _currentLocalDir,
                targetLocalDir,
                ref _localDirVelocity,
                smoothTime,
                Mathf.Infinity,
                Time.deltaTime
            );
            _currentLocalDir.y = 0f;

            if (_currentLocalDir.sqrMagnitude < 0.0001f)
            {
                _currentLocalDir = Vector3.forward;
            }
            else
            {
                _currentLocalDir.Normalize();
            }

            // 由平滑后的向量求角度（供状态/调试/起步选择使用）
            float angle = Mathf.Atan2(_currentLocalDir.x, _currentLocalDir.z) * Mathf.Rad2Deg;
            _data.DesiredLocalMoveAngle = angle;

            // 极坐标 -> 笛卡尔坐标（Cartesian Mixer）
            float rad = angle * Mathf.Deg2Rad;
            float x = Mathf.Sin(rad) * _currentIntensity;
            float y = Mathf.Cos(rad) * _currentIntensity;

            _data.CurrentAnimBlendX = x;
            _data.CurrentAnimBlendY = y;
        }
    }
}
