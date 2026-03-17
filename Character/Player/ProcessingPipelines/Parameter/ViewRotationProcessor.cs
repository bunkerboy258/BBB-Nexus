using UnityEngine;

namespace BBBNexus
{
    // 视角旋转处理器 负责维护视觉朝向的权威方向
    public class ViewRotationProcessor
    {
        private readonly PlayerRuntimeData _data;
        private readonly PlayerSO _config;

        public ViewRotationProcessor(PlayerRuntimeData data, PlayerSO config)
        {
            _data = data;
            _config = config;
        }

        public void Update(in ProcessedInputData input)
        {
            // 拿到这一帧的原始增量
            Vector2 lookInput = input.Look;

            if (lookInput.sqrMagnitude > 0.000001f)
            {
                // 把增量累加进 Yaw 和 Pitch 里
                _data.ViewYaw += lookInput.x * _config.Core.LookSensitivity.x;
                _data.ViewPitch += lookInput.y * _config.Core.LookSensitivity.y;

                // 钳制 Pitch 并让 Yaw 在 360 度内循环
                _data.ViewPitch = Mathf.Clamp(_data.ViewPitch, _config.Core.PitchLimits.x, _config.Core.PitchLimits.y);
                _data.ViewYaw = Mathf.Repeat(_data.ViewYaw, 360f);
            }

            // 更新黑板
            _data.LookInput = lookInput;
            _data.MoveInput = input.Move;
            _data.AuthorityYaw = _data.ViewYaw;
            _data.AuthorityPitch = _data.ViewPitch;
            _data.AuthorityRotation = Quaternion.Euler(_data.AuthorityPitch, _data.AuthorityYaw, 0f);
        }
    }
}