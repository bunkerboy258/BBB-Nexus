using UnityEngine;
using Characters.Player.Data;

namespace Characters.Player.Processing
{
    // 动画参数处理器 它把运动意图转换成动画混合参数 
    // 负责计算 X Y 动画混合参数 并驱动下落检测与意图生成 
    public class MovementParameterProcessor
    {
        private PlayerSO _config;
        private PlayerRuntimeData _data;
        private Transform _playerTransform;

        // 平滑状态变量 强度档位的平滑缓冲 
        private float _blendTimer;
        private float _startIntensity;
        private float _targetIntensity;
        private float _currentIntensity;

        // 方向向量平滑 替代角度平滑避免 -180 180 跳变 
        private Vector3 _currentLocalDir = Vector3.forward;

        // 动画 X Y 参数平滑状态 
        private float _currentAnimBlendX;
        private float _xBlendVelocity;
        private float _currentAnimBlendY;
        private float _yBlendVelocity;

        // 下落距离计算状态 追踪本次空中过程的最高点 
        private float _apexY;
        private bool _wasGroundedLastFrame;

        // 下落意图计算状态 累积空中时间判断是否应该进入下落动画 
        private float _airborneTime;

        public MovementParameterProcessor(PlayerController player)
        {
            _config = player.Config;
            _data = player.RuntimeData;
            _playerTransform = player.transform;

            _currentIntensity = 0.0f;
            _currentLocalDir = Vector3.forward;

            _currentAnimBlendX = 0f;
            _xBlendVelocity = 0f;
            _currentAnimBlendY = 0f;
            _yBlendVelocity = 0f;

            _apexY = _playerTransform.position.y;
            _wasGroundedLastFrame = _data.IsGrounded;
            _airborneTime = 0f;
        }

        // 每帧更新 依次计算强度 方向 下落高度 下落意图 
        public void Update()
        {
            // 根据运动状态更新强度目标
            UpdateIntensityLogic();

            // 平滑方向并输出 Mixer 参数
            UpdateDirectionBlend();

            // 持续计算下落高度等级 
            UpdateFallHeight();

            // 计算下落意图 
            UpdateFallIntent();
        }

        // Y 轴逻辑 强度 速度混合 
        // 根据当前运动状态映射到不同的强度目标值 
        private void UpdateIntensityLogic()
        {
            // 根据运动状态获取目标强度
            float target = GetTargetIntensityForState(_data.CurrentLocomotionState);

            // 检测目标变化 按键切换或运动状态变化
            if (Mathf.Abs(target - _targetIntensity) > 0.01f)
            {
                _blendTimer = 0f;
                _startIntensity = _currentIntensity;
                _targetIntensity = target;
            }

            // 使用配置的曲线平滑强度变化
            float curveTime = 0.3f; 
            if (_config.Core.SprintBlendCurve.length > 0)
            {
                curveTime = _config.Core.SprintBlendCurve.keys[_config.Core.SprintBlendCurve.length - 1].time;
            }

            if (_blendTimer < curveTime)
            {
                _blendTimer += Time.deltaTime;
                float t = _blendTimer / curveTime;
                float factor = _config.Core.SprintBlendCurve.Evaluate(t);
                _currentIntensity = Mathf.Lerp(_startIntensity, _targetIntensity, factor);
            }
            else
            {
                _currentIntensity = _targetIntensity;
            }
        }

        // 根据运动状态获取目标强度值 
        // 返回值范围 0.0 Idle 到 1.0 Sprint
        private float GetTargetIntensityForState(LocomotionState state)
        {
            return state switch
            {
                // Walk 缓慢行走 强度 0.3 0.4
                LocomotionState.Walk => 0.35f,

                // Jog 正常慢跑 强度 0.65 0.75
                LocomotionState.Jog => 0.7f,

                // Sprint 快速冲刺 强度 0.95 1.0
                LocomotionState.Sprint => 0.98f,

                // Idle 站立 停止 强度 0.0
                LocomotionState.Idle => 0.0f,

                _ => 0.0f
            };
        }

        // 计算动画混合参数 X Y 
        // 考虑瞄准模式的特殊性 直接用摇杆输入而非世界方向 
        private void UpdateDirectionBlend()
        {
            // 目标局部方向
            // Aim 模式 直接用 MoveInput 的本地语义 避免引入 AuthorityYaw 带来的二次抖动
            // FreeLook 模式 以 DesiredWorldMoveDir 为准 再转到角色本地
            Vector3 targetLocalDir;

            if (_data.MoveInput.sqrMagnitude < 0.001f)
            {
                // 无输入时回到正前方 防止停下后保留侧向导致混合停在 Lean 上
                targetLocalDir = Vector3.forward;
            }
            else if (_data.IsAiming)
            {
                // Aim 直接使用输入语义作为局部方向
                Vector2 input = _data.MoveInput.normalized;
                targetLocalDir = new Vector3(input.x, 0f, input.y);
            }
            else
            {
                // FreeLook 把相机参考系的世界方向转入角色本地
                Vector3 worldDir = _data.DesiredWorldMoveDir;
                if (worldDir.sqrMagnitude < 0.0001f)
                {
                    targetLocalDir = Vector3.forward;
                }
                else
                {
                    targetLocalDir = _playerTransform.InverseTransformDirection(worldDir);
                    targetLocalDir.y = 0f;
                    if (targetLocalDir.sqrMagnitude > 0.0001f)
                        targetLocalDir.Normalize();
                    else
                        targetLocalDir = Vector3.forward;
                }
            }

            // 注意：这里不应该平滑方向 DesiredLocalMoveAngle 是逻辑层应该瞬时响应
            // 方向本身的平滑应该交给后续表现层的 Blend X Y SmoothDamp 处理
            _currentLocalDir = targetLocalDir;
            _currentLocalDir.y = 0f;
            if (_currentLocalDir.sqrMagnitude > 0.0001f)
                _currentLocalDir.Normalize();
            else
                _currentLocalDir = Vector3.forward;

            // 由方向向量求角度
            float angle = Mathf.Atan2(_currentLocalDir.x, _currentLocalDir.z) * Mathf.Rad2Deg;
            _data.DesiredLocalMoveAngle = angle;

            // 极坐标 笛卡尔坐标 Cartesian Mixer
            float rad = angle * Mathf.Deg2Rad;
            float targetX = Mathf.Sin(rad) * _currentIntensity;
            float targetY = Mathf.Cos(rad) * _currentIntensity;

            float xSmoothTime; float ySmoothTime;
            // X Y 都平滑 避免快速换向时 X 突跳导致权重抖动
            if (_config.Aiming == null)
            {
                xSmoothTime = _config.Core.XAnimBlendSmoothTime;
                ySmoothTime = _config.Core.YAnimBlendSmoothTime;
            }
            else
            {
                xSmoothTime = Mathf.Max(0.0001f, _data.IsAiming ? _config.Aiming.AimXAnimBlendSmoothTime : _config.Core.XAnimBlendSmoothTime);
                ySmoothTime = Mathf.Max(0.0001f, _data.IsAiming ? _config.Aiming.AimYAnimBlendSmoothTime : _config.Core.YAnimBlendSmoothTime);
            }

            _currentAnimBlendX = Mathf.SmoothDamp(
                _currentAnimBlendX,
                targetX,
                ref _xBlendVelocity,
                xSmoothTime,
                Mathf.Infinity,
                Time.deltaTime
            );
            _data.CurrentAnimBlendX = _currentAnimBlendX;

            _currentAnimBlendY = Mathf.SmoothDamp(
                _currentAnimBlendY,
                targetY,
                ref _yBlendVelocity,
                ySmoothTime,
                Mathf.Infinity,
                Time.deltaTime
            );
            _data.CurrentAnimBlendY = _currentAnimBlendY;
        }

        // 持续下落等级计算 每帧更新
        // 接地 重置 apex 将 FallHeightLevel 置为 0 避免残留
        // 离地 持续维护 apex 最高点 用 apex current 计算当前已下落距离
        // 这样 LandState 进入时可以直接消费最后一帧算出来的 FallHeightLevel
        private void UpdateFallHeight()
        {
            bool isGrounded = _data.IsGrounded;
            float currentY = _playerTransform.position.y;

            // 边沿检测 落地 离地
            bool justLanded = !_wasGroundedLastFrame && isGrounded;
            bool justLeftGround = _wasGroundedLastFrame && !isGrounded;

            // 写入边沿事件 供状态机在 LogicUpdate 时消费
            _data.JustLanded = justLanded;
            _data.JustLeftGround = justLeftGround;

            if (isGrounded)
            {
                // 在地面上 为下一次离地预热
                _apexY = currentY;

                // 仅在非刚落地的情况下清零 FallHeightLevel
                // 目的是让 LandState 在 Enter 时能消费 上一次空中帧 的 FallHeightLevel
                if (!justLanded)
                {
                    _data.FallHeightLevel = 0;
                }
            }
            else
            {
                // 刚离地 把 apex 初始化为离地那一刻的高度
                if (justLeftGround)
                {
                    _apexY = currentY;
                }

                // 上升阶段刷新最高点
                if (currentY > _apexY)
                {
                    _apexY = currentY;
                }

                // 持续计算 从最高点到当前点 的已下落距离 上升阶段为 0
                float fallHeight = Mathf.Max(0f, _apexY - currentY);
                // 每帧写入等级 持续计算
                CalculateFallHeightLevel(fallHeight);
            }

            _wasGroundedLastFrame = isGrounded;
        }

        // 计算下落意图 当空中时间超过阈值时 WantsToFall 为真 
        private void UpdateFallIntent()
        {
            bool isGrounded = _data.IsGrounded;

            if (isGrounded)
            {
                // 落地 重置空中时间
                _airborneTime = 0f;
                _data.WantsToFall = false;
            }
            else
            {
                // 空中 累积时间
                _airborneTime += Time.deltaTime;

                // 当空中时间超过阈值时 设置 WantsToFall 为真
                float fallTimeThreshold = _config.LocomotionAnims.AirborneTimeThresholdForFall;
                _data.WantsToFall = _airborneTime >= fallTimeThreshold;
            }
        }

        // 纯粹的数学计算 无状态副作用
        private void CalculateFallHeightLevel(float height)
        {
            if (height < _config.JumpAndLanding.LandHeight_Level1) _data.FallHeightLevel = 0;
            else if (height < _config.JumpAndLanding.LandHeight_Level2) _data.FallHeightLevel = 1;
            else if (height < _config.JumpAndLanding.LandHeight_Level3) _data.FallHeightLevel = 2;
            else if (height < _config.JumpAndLanding.LandHeight_Level4) _data.FallHeightLevel = 3;
            else _data.FallHeightLevel = 4;
        }
    }
}
