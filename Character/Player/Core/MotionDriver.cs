using UnityEngine;
using Characters.Player.Data;

namespace Characters.Player.Core
{
    /// <summary>
    /// MotionDriver：
    /// 负责把“输入/动画曲线”转换为 CharacterController.Move 的速度向量，并维护角色与相机参考的旋转数据。
    /// </summary>
    public class MotionDriver
    {
        private readonly PlayerController _player;
        private readonly CharacterController _cc;
        private readonly PlayerRuntimeData _data;
        private readonly PlayerSO _config;

        // 曲线->输入切换：用于对齐 ViewYaw，避免参考系突变。
        private bool _pendingViewYawAlign;

        // 模式切换守卫：用于在 FreeLook<->Aiming 切换时做一次性的 yaw 同步（防止角度跳变）
        private bool _wasAimingLastFrame;

        public MotionDriver(PlayerController player)
        {
            _player = player;
            _cc = player.CharController;
            _data = player.RuntimeData;
            _config = player.Config;

            _wasAimingLastFrame = _data.IsAiming;
        }

        public void UpdateMotion(MotionClipData clipData, float stateTime, float startYaw)
        {
            HandleAimModeTransitionIfNeeded();

            Vector3 horizontalVelocity;

            if (clipData != null)
            {
                if (clipData.Type == MotionType.CurveDriven)
                {
                    _pendingViewYawAlign = true;
                    horizontalVelocity = CalculateCurveDrivenVelocity(clipData, stateTime, startYaw);
                }
                else if (clipData.Type == MotionType.mixed)
                {
                    if (stateTime < clipData.RotationFinishedTime)
                    {
                        _pendingViewYawAlign = true;
                        horizontalVelocity = CalculateCurveDrivenVelocity(clipData, stateTime, startYaw);
                    }
                    else
                    {
                        horizontalVelocity = CalculateMotionFromInput();
                    }
                }
                else
                {
                    _pendingViewYawAlign = false;
                    horizontalVelocity = CalculateMotionFromInput();
                }
            }
            else
            {
                _pendingViewYawAlign = false;
                horizontalVelocity = CalculateMotionFromInput();
            }

            Vector3 verticalVelocity = CalculateGravity();
            _cc.Move((horizontalVelocity + verticalVelocity) * Time.deltaTime);
        }

        public void UpdateMotion()
        {
            Vector3 verticalVelocity = CalculateGravity();
            _cc.Move(verticalVelocity * Time.deltaTime);
        }

        /// <summary>
        /// [兼容接口] 旧的瞄准状态仍在调用 UpdateAimMotion。
        /// </summary>
        public void UpdateAimMotion(float speedMult)
        {
            Vector3 horizontal = CalculateAimDrivenVelocity(speedMult);
            Vector3 vertical = CalculateGravity();
            _cc.Move((horizontal + vertical) * Time.deltaTime);
        }

        private Vector3 CalculateMotionFromInput(float speedMult = 1f)
        {
            AlignViewYawToCharacterIfPending();

            return _data.IsAiming
                ? CalculateAimDrivenVelocity(speedMult)
                : CalculateInputDrivenVelocity(speedMult);
        }

        /// <summary>
        /// 探索模式（FreeLook）：移动使用 CameraTransform 的水平 forward/right 作为参考系。
        /// </summary>
        private Vector3 CalculateInputDrivenVelocity(float speedMult = 1f)
        {
            // CameraRoot 已移除：这里以 RuntimeData.CameraTransform 作为唯一相机参考。
            Transform cam = _data.CameraTransform;

            Vector3 camForward = cam != null ? cam.forward : _player.transform.forward;
            Vector3 camRight = cam != null ? cam.right : _player.transform.right;

            camForward.y = 0f;
            camRight.y = 0f;
            camForward = camForward.sqrMagnitude > 0.0001f ? camForward.normalized : _player.transform.forward;
            camRight = camRight.sqrMagnitude > 0.0001f ? camRight.normalized : _player.transform.right;

            Vector2 input = _data.MoveInput;
            if (input.sqrMagnitude < 0.001f)
            {
                _data.CurrentYaw = _player.transform.eulerAngles.y;
                return Vector3.zero;
            }

            Vector3 moveDir = camRight * input.x + camForward * input.y;
            moveDir = moveDir.sqrMagnitude > 0.0001f ? moveDir.normalized : Vector3.zero;

            float targetYaw = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
            float smoothedYaw = Mathf.SmoothDampAngle(
                _player.transform.eulerAngles.y,
                targetYaw,
                ref _data.RotationVelocity,
                _config.RotationSmoothTime
            );
            _player.transform.rotation = Quaternion.Euler(0f, smoothedYaw, 0f);
            _data.CurrentYaw = smoothedYaw;

            float baseSpeed = _data.IsRunning ? _config.RunSpeed : _config.MoveSpeed;
            if (!_data.IsGrounded) baseSpeed *= _config.AirControl;

            return moveDir * baseSpeed * speedMult;
        }

        /// <summary>
        /// 瞄准模式（Strafing）：旋转由 LookInput.x 驱动角色，移动严格使用角色自身 forward/right。
        /// </summary>
        private Vector3 CalculateAimDrivenVelocity(float speedMult = 1f)
        {
            float lookDeltaX = _data.LookInput.x;
            _data.LookInput = Vector2.zero;

            float targetYaw = _player.transform.eulerAngles.y + lookDeltaX * _config.LookSensitivity.x * _config.AimSensitivity * Time.deltaTime;

            float smoothedYaw = Mathf.SmoothDampAngle(
                _player.transform.eulerAngles.y,
                targetYaw,
                ref _data.RotationVelocity,
                _config.AimRotationSmoothTime
            );

            _player.transform.rotation = Quaternion.Euler(0f, smoothedYaw, 0f);
            _data.CurrentYaw = smoothedYaw;

            // CameraRoot 已移除：这里仅维护数据层的 ViewYaw，使模式切换时 yaw 连续。
            _data.ViewYaw = smoothedYaw;

            Vector2 input = _data.MoveInput;
            if (input.sqrMagnitude < 0.001f) return Vector3.zero;

            Vector3 forward = _player.transform.forward;
            Vector3 right = _player.transform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 moveDir = (right * input.x + forward * input.y);
            moveDir = moveDir.sqrMagnitude > 0.0001f ? moveDir.normalized : Vector3.zero;

            float baseSpeed = _data.IsRunning ? _config.AimRunSpeed : _config.AimWalkSpeed;
            if (!_data.IsGrounded) baseSpeed *= _config.AirControl;

            return moveDir * baseSpeed * speedMult;
        }

        private Vector3 CalculateCurveDrivenVelocity(MotionClipData data, float time, float startYaw)
        {
            float curveAngle = data.RotationCurve.Evaluate(time * data.PlaybackSpeed);

            Quaternion startRot = Quaternion.Euler(0f, startYaw, 0f);
            Quaternion offsetRot = Quaternion.Euler(0f, curveAngle, 0f);
            Quaternion targetRot = startRot * offsetRot;

            _player.transform.rotation = Quaternion.Slerp(_player.transform.rotation, targetRot, Time.deltaTime * 30f);

            _data.CurrentYaw = _player.transform.eulerAngles.y;

            float speed = data.SpeedCurve.Evaluate(time * data.PlaybackSpeed);
            return _player.transform.forward * speed;
        }

        private void AlignViewYawToCharacterIfPending()
        {
            if (!_pendingViewYawAlign) return;
            _pendingViewYawAlign = false;

            float characterYaw = _player.transform.eulerAngles.y;
            _data.ViewYaw = characterYaw;
            _data.CurrentYaw = characterYaw;
        }

        private void HandleAimModeTransitionIfNeeded()
        {
            bool isAiming = _data.IsAiming;
            if (isAiming == _wasAimingLastFrame) return;

            float yaw = _player.transform.eulerAngles.y;
            _data.CurrentYaw = yaw;
            _data.ViewYaw = yaw;

            _data.RotationVelocity = 0f;
            _wasAimingLastFrame = isAiming;
        }

        private Vector3 CalculateGravity()
        {
            _data.IsGrounded = _cc.isGrounded;

            if (_data.IsGrounded && _data.VerticalVelocity < 0)
            {
                _data.VerticalVelocity = -2f;
            }
            else
            {
                _data.VerticalVelocity += _config.Gravity * Time.deltaTime;
            }

            return new Vector3(0f, _data.VerticalVelocity, 0f);
        }
    }
}
