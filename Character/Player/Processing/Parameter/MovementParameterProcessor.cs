using UnityEngine;
using Characters.Player.Data;

namespace Characters.Player.Processing
{
    // 动画参数处理器 只负责计算 X Y 动画混合参数（单位圆方向）并驱动下落检测与意图生成
    public class MovementParameterProcessor
    {
        private PlayerSO _config;
        private PlayerRuntimeData _data;
        private Transform _playerTransform;

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

        // 新增：记录上一次瞄准状态
        private bool _lastAiming = false;

        public MovementParameterProcessor(PlayerController player)
        {
            _config = player.Config;
            _data = player.RuntimeData;
            _playerTransform = player.transform;

            _currentAnimBlendX = 0f;
            _xBlendVelocity = 0f;
            _currentAnimBlendY = 0f;
            _yBlendVelocity = 0f;

            _apexY = _playerTransform.position.y;
            _wasGroundedLastFrame = _data.IsGrounded;
            _airborneTime = 0f;
        }

        // 每帧更新 依次计算方向 下落高度 下落意图 
        public void Update()
        {
            // 维护 DesiredLocalMoveAngle：世界方向与角色正前的夹角
            UpdateDesiredLocalMoveAngleFromWorldDir();

            // 平滑方向并输出 Mixer 参数
            UpdateDirectionBlend();

            // 持续计算下落高度等级 
            UpdateFallHeight();

            // 计算下落意图 
            UpdateFallIntent();
        }

        // 新增：根据世界移动方向和角色朝向维护 DesiredLocalMoveAngle
        private void UpdateDesiredLocalMoveAngleFromWorldDir()
        {
            Vector3 worldDir = _data.DesiredWorldMoveDir;
            if (worldDir.sqrMagnitude < 0.0001f)
            {
                _data.DesiredLocalMoveAngle = 0f;
                return;
            }
            // 角色正前
            Vector3 forward = _playerTransform.forward;
            worldDir.y = 0f;
            forward.y = 0f;
            if (worldDir.sqrMagnitude < 0.0001f || forward.sqrMagnitude < 0.0001f)
            {
                _data.DesiredLocalMoveAngle = 0f;
                return;
            }
            worldDir.Normalize();
            forward.Normalize();
            float angle = Vector3.SignedAngle(forward, worldDir, Vector3.up);
            _data.DesiredLocalMoveAngle = angle;
        }

        // 计算动画混合参数 X Y 
        // 简化逻辑：将 MoveInput 映射到单位圆边上，再平滑写入黑板
        private void UpdateDirectionBlend()
        {
            Vector2 input = _data.MoveInput;
            Vector2 circle;
            if (input.sqrMagnitude < 0.0001f)
            {
                circle = Vector2.zero;
            }
            else
            {
                circle = input.normalized; // 单位圆边上
            }

            // 检查瞄准状态切换，切换时重置平滑速度，防止跳变
            bool aimingNow = _data.IsAiming;
            if (aimingNow != _lastAiming)
            {
                _xBlendVelocity = 0f;
                _yBlendVelocity = 0f;
            }
            _lastAiming = aimingNow;

            // 直接平滑写入
            float xSmoothTime, ySmoothTime;
            if (_config.Aiming == null)
            {
                xSmoothTime = _config.Core.XAnimBlendSmoothTime;
                ySmoothTime = _config.Core.YAnimBlendSmoothTime;
            }
            else
            {
                xSmoothTime = Mathf.Max(0.0001f, aimingNow ? _config.Aiming.AimXAnimBlendSmoothTime : _config.Core.XAnimBlendSmoothTime);
                ySmoothTime = Mathf.Max(0.0001f, aimingNow ? _config.Aiming.AimYAnimBlendSmoothTime : _config.Core.YAnimBlendSmoothTime);
            }

            _currentAnimBlendX = Mathf.SmoothDamp(
                _currentAnimBlendX,
                circle.x,
                ref _xBlendVelocity,
                xSmoothTime,
                Mathf.Infinity,
                Time.deltaTime
            );
            _data.CurrentAnimBlendX = _currentAnimBlendX;

            _currentAnimBlendY = Mathf.SmoothDamp(
                _currentAnimBlendY,
                circle.y,
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
