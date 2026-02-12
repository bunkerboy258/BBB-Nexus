using Characters.Player.Data;
using UnityEngine;

namespace Characters.Player.Parameters
{
    /// <summary>
    /// 视角旋转处理器（权威方向源生成器）。
    /// 职责：
    /// - 从 RuntimeData.LookInput（鼠标/右摇杆 delta）累加得到 ViewYaw/ViewPitch；
    /// - 将 ViewYaw/ViewPitch 同步为 AuthorityYaw/AuthorityPitch，并计算 AuthorityRotation；
    /// - 消费 LookInput（清零），防止后续系统重复叠加。
    /// 
    /// 备注：本处理器不直接旋转相机/角色；仅维护“权威参考系数据”。
    /// </summary>
    public class ViewRotationProcessor
    {
        private readonly PlayerController _player;
        private readonly PlayerRuntimeData _data;
        private readonly PlayerSO _config;

        public ViewRotationProcessor(PlayerController player)
        {
            _player = player;
            _data = player.RuntimeData;
            _config = player.Config;
        }

        public void Update()
        {
            // 读取并消费输入：权威方向源只应由这里维护（无论是否瞄准）。
            Vector2 lookDelta = _data.LookInput;
            _data.LookInput = Vector2.zero;

            if (lookDelta.sqrMagnitude > 0.000001f)
            {
                // 注意：LookInput 绑定的是 Mouse/delta（每帧增量）。
                // 增量输入不应再乘 Time.deltaTime，否则会产生帧率相关的非线性缩放并引入抖动感。
                _data.ViewYaw += lookDelta.x * _config.LookSensitivity.x;

                _data.ViewPitch += lookDelta.y * _config.LookSensitivity.y;
                _data.ViewPitch = Mathf.Clamp(_data.ViewPitch, _config.PitchLimits.x, _config.PitchLimits.y);

                _data.ViewYaw = Mathf.Repeat(_data.ViewYaw, 360f);
            }

            // 权威方向源：始终等于 View
            _data.AuthorityYaw = _data.ViewYaw;
            _data.AuthorityPitch = _data.ViewPitch;
            _data.AuthorityRotation = Quaternion.Euler(_data.AuthorityPitch, _data.AuthorityYaw, 0f);
        }
    }
}
