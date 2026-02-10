using UnityEngine;
using Characters.Player.Data;

namespace Characters.Player.Core
{
    public class MotionDriver
    {
        private PlayerController _player;
        private CharacterController _cc;
        private PlayerRuntimeData _data;
        private PlayerSO _config;

        public MotionDriver(PlayerController player)
        {
            _player = player;
            _cc = player.CharController;
            _data = player.RuntimeData;
            _config = player.Config;
        }

        /// <summary>
        /// 核心驱动函数。
        /// </summary>
        /// <param name="clipData">当前动画数据 (Loop时为null)</param>
        /// <param name="stateTime">状态已持续时间 (真实时间，无需乘倍速)</param>
        /// <param name="startYaw">状态进入时的初始朝向</param>
        public void UpdateMotion(MotionClipData clipData, float stateTime, float startYaw)
        {
            Vector3 horizontalVelocity = Vector3.zero;

            // 1. 决策：使用哪种驱动模式
            if (clipData == null || clipData.Type == MotionType.InputDriven)
            {
                horizontalVelocity = CalculateInputDrivenVelocity();
            }
            else if (clipData.Type == MotionType.CurveDriven)
            {
                horizontalVelocity = CalculateCurveDrivenVelocity(clipData, stateTime, startYaw);
            }
            else if(clipData.Type == MotionType.mixed)
            {
                if (stateTime < clipData.RotationFinishedTime)
                {
                    horizontalVelocity = CalculateCurveDrivenVelocity(clipData, stateTime, startYaw);
                }
                else
                {
                    horizontalVelocity = CalculateInputDrivenVelocity();
                }
            }

            // 2. 垂直速度 (重力)
            Vector3 verticalVelocity = CalculateGravity();

            // 3. 应用
            _cc.Move((horizontalVelocity + verticalVelocity) * Time.deltaTime);
        }

        /// <summary>
        /// 无参重载：仅计算并应用重力（垂直速度），不处理水平移动
        /// </summary>
        public void UpdateMotion()
        {
            // 水平速度置零（仅保留重力带来的垂直速度）
            Vector3 horizontalVelocity = Vector3.zero;
            // 计算垂直速度（重力逻辑）
            Vector3 verticalVelocity = CalculateGravity();
            // 仅应用重力移动
            _cc.Move((horizontalVelocity + verticalVelocity) * Time.deltaTime);
        }

        public void UpdateAimMotion(float speedMult)
        {
            // 1. 旋转：强制身体面向相机前方 (Strafing 核心)
            if (_data.CameraTransform != null)
            {
                // 获取相机水平前方
                Vector3 camFwd = _data.CameraTransform.forward;
                camFwd.y = 0; // 忽略垂直分量
                if (camFwd.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(camFwd.normalized);

                    // 使用极快的平滑 (AimRotationSmoothTime)
                    float angle = Mathf.SmoothDampAngle(
                        _player.transform.eulerAngles.y,
                        targetRot.eulerAngles.y,
                        ref _data.RotationVelocity,
                        _config.AimRotationSmoothTime
                    );
                    _player.transform.rotation = Quaternion.Euler(0f, angle, 0f);
                }
            }

            // 2. 移动：基于 Input 的笛卡尔坐标移动
            // 在 Strafing 模式下，Input.y 直接对应 Forward，Input.x 直接对应 Right
            // 不需要再 Atan2 算角度了，直接平移

            Vector3 moveDir = _player.transform.right * _data.MoveInput.x + _player.transform.forward * _data.MoveInput.y;

            // 速度选择
            float baseSpeed = _data.IsRunning ? _config.RunSpeed : _config.MoveSpeed;

            // 应用重力和移动
            Vector3 verticalVelocity = CalculateGravity();
            _cc.Move((moveDir.normalized * baseSpeed * speedMult + verticalVelocity) * Time.deltaTime);
        }
        private Vector3 CalculateCurveDrivenVelocity(MotionClipData data, float time, float startYaw)
        {
            // 1. 旋转
            // 直接读取曲线 (X轴已经是缩放后的时间)
            float curveAngle = data.RotationCurve.Evaluate(time*data.PlaybackSpeed);

            // 四元数叠加
            Quaternion startRot = Quaternion.Euler(0f, startYaw, 0f);
            Quaternion offsetRot = Quaternion.Euler(0f, curveAngle, 0f);
            Quaternion targetRot = startRot * offsetRot;

            // 微平滑
            _player.transform.rotation = Quaternion.Slerp(_player.transform.rotation, targetRot, Time.deltaTime * 30f);

            // 2. 速度
            // 直接读取曲线 (Y轴已经是缩放后的速度)
            float speed = data.SpeedCurve.Evaluate(time * data.PlaybackSpeed);

            // 沿当前朝向
            return _player.transform.forward * speed;
        }

        private Vector3 CalculateInputDrivenVelocity()
        {
            if (_data.MoveInput.sqrMagnitude < 0.001f)
            {
                return Vector3.zero;
            }
            // 1. 计算目标
            float targetAngle = Mathf.Atan2(_data.MoveInput.x, _data.MoveInput.y) * Mathf.Rad2Deg;
            if (_data.CameraTransform != null) targetAngle += _data.CameraTransform.eulerAngles.y;

            // 2. 旋转
            float angle = Mathf.SmoothDampAngle(
                _player.transform.eulerAngles.y,
                targetAngle,
                ref _data.RotationVelocity,
                _config.RotationSmoothTime
            );
            _player.transform.rotation = Quaternion.Euler(0f, angle, 0f);

            // 3. 速度
            float baseSpeed = _data.IsRunning ? _config.RunSpeed : _config.MoveSpeed;
            if (!_data.IsGrounded) baseSpeed *= _config.AirControl;
            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

            return moveDir.normalized * baseSpeed;
        }

        private Vector3 CalculateGravity()
        {
            _data.IsGrounded = _cc.isGrounded;
            if (_data.IsGrounded && _data.VerticalVelocity < 0)
            {
                _data.VerticalVelocity = -2f; // 贴地力
            }
            else
            {
                _data.VerticalVelocity += -20f * Time.deltaTime; // 重力
            }
            return new Vector3(0f, _data.VerticalVelocity, 0f);
        }
    }
}
