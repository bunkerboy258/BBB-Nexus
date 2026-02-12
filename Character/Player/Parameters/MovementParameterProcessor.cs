using UnityEngine;
using Characters.Player.Data;

namespace Characters.Player.Parameters
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

        // 角度平滑专用
        private float _currentAngle;
        private float _angleVelocity;

        public MovementParameterProcessor(PlayerController player)
        {
            _config = player.Config;
            _data = player.RuntimeData;
            _playerTransform = player.transform;

            // 初始化
            _currentIntensity = 0.7f;
            _currentAngle = 0f;
        }

        public void Update()
        {
            // 0) 统一出移动派生数据（状态机/动画共用）
            UpdateMovementDerivedData();

            // 1) 计算两个独立的平滑值
            UpdateIntensityLogic();
            UpdateAngleLogic();

            // 2) 极坐标 -> 笛卡尔坐标
            float rad = _currentAngle * Mathf.Deg2Rad;
            float x = Mathf.Sin(rad) * _currentIntensity;
            float y = Mathf.Cos(rad) * _currentIntensity;

            // 3) 写入 Data 供 Mixer 使用
            _data.CurrentAnimBlendX = x;
            _data.CurrentAnimBlendY = y;
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

            // 转为本地角度（-180~180）
            Vector3 localDir = _playerTransform.InverseTransformDirection(_data.DesiredWorldMoveDir);
            _data.DesiredLocalMoveAngle = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
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

        // --- X 轴逻辑 (方向/角度) ---
        private void UpdateAngleLogic()
        {
            // targetAngle 来自统一派生数据，确保与起步/状态判定一致
            float targetAngle = _data.DesiredLocalMoveAngle;

            _currentAngle = Mathf.SmoothDampAngle(
                _currentAngle,
                targetAngle,
                ref _angleVelocity,
                _config.XAnimBlendSmoothTime
            );
        }
    }
}
