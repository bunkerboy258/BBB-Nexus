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
            // 1. 计算两个独立的平滑值
            UpdateIntensityLogic();
            UpdateAngleLogic();

            // 2. [核心] 将极坐标转为笛卡尔坐标
            float rad = _currentAngle * Mathf.Deg2Rad;
            float x = Mathf.Sin(rad) * _currentIntensity;
            float y = Mathf.Cos(rad) * _currentIntensity;

            // 3. 写入 Data 供 Mixer 使用
            _data.CurrentAnimBlendX = x;
            _data.CurrentAnimBlendY = y;
            //Debug.Log($"[MovementParameterProcessor] Updated Blend - X: {x:F2}, Y: {y:F2}");
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
            float targetAngle = 0f;

            // 仅当有输入时计算目标角度，否则保持 0 (或者保持上一帧角度？通常归零)
            if (_data.MoveInput.sqrMagnitude > 0.001f)
            {
                // 世界空间方向
                float worldAngle = Mathf.Atan2(_data.MoveInput.x, _data.MoveInput.y) * Mathf.Rad2Deg;
                if (_data.CameraTransform != null)
                    worldAngle += _data.CameraTransform.eulerAngles.y;

                // 本地空间方向
                Vector3 worldDir = Quaternion.Euler(0f, worldAngle, 0f) * Vector3.forward;
                Vector3 localDir = _playerTransform.InverseTransformDirection(worldDir);
                targetAngle = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
            }

            // 角度平滑 (解决 180 度跳变的核心)
            _currentAngle = Mathf.SmoothDampAngle(
                _currentAngle,
                targetAngle,
                ref _angleVelocity,
                _config.XAnimBlendSmoothTime
            );
        }
    }
}
