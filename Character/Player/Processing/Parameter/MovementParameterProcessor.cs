using UnityEngine;
using Characters.Player.Data;

namespace Characters.Player.Processing
{
    /// <summary>
    /// 动画参数处理器：将意图转换为动画混合参数。
    /// 
    /// 职责：
    /// - 计算世界空间移动方向（DesiredWorldMoveDir）
    /// - 计算动画混合参数 X（方向）和 Y（速度强度）
    /// - X 轴：由移动方向和强度合成的极坐标 x 分量（保持不变）
    /// - Y 轴：根据 CurrentLocomotionState 映射到不同强度（Walk/Jog/Sprint）
    /// - 根据垂直速度和重力计算最高点下落距离（内部数据）
    /// </summary>
    public class MovementParameterProcessor
    {
        private PlayerSO _config;
        private PlayerRuntimeData _data;
        private Transform _playerTransform;

        // --- 平滑状态变量 ---
        private float _blendTimer;
        private float _startIntensity;
        private float _targetIntensity;
        private float _currentIntensity; // 当前强度（根据状态）

        // 方向向量平滑（替代角度平滑，避免 -180/180 跳变）
        private Vector3 _currentLocalDir = Vector3.forward;
        private Vector3 _localDirVelocity;

        // 动画 Y 参数平滑状态
        private float _currentAnimBlendY;
        private float _yBlendVelocity;

        // --- 下落距离计算状态（内部数据）---

        private float _apexY; // 本次空中过程的最高点 Y 坐标
        private bool _wasGroundedLastFrame;

        public MovementParameterProcessor(PlayerController player)
        {
            _config = player.Config;
            _data = player.RuntimeData;
            _playerTransform = player.transform;

            _currentIntensity = 0.0f;
            _currentLocalDir = Vector3.forward;
            _localDirVelocity = Vector3.zero;
            _currentAnimBlendY = 0f;
            _yBlendVelocity = 0f;

            _apexY = _playerTransform.position.y;
            _wasGroundedLastFrame = _data.IsGrounded;
        }

        public void Update()
        {
            // 0) 统一计算移动派生数据（状态机/动画共用）
            UpdateMovementDerivedData();

            // 1) 根据运动状态更新强度目标
            UpdateIntensityLogic();

            // 2) 平滑方向并输出 Mixer 参数
            UpdateDirectionBlend();

            // 3) 持续计算下落高度等级（每帧更新，不依赖落地瞬间）
            UpdateFallHeight();
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

        /// <summary>
        /// Y 轴逻辑（强度/速度混合）
        /// 根据 CurrentLocomotionState 映射到不同的强度目标值。
        /// </summary>
        private void UpdateIntensityLogic()
        {
            // 根据运动状态获取目标强度
            float target = GetTargetIntensityForState(_data.CurrentLocomotionState);

            // 检测目标变化（按键切换或运动状态变化）
            if (Mathf.Abs(target - _targetIntensity) > 0.01f)
            {
                _blendTimer = 0f;
                _startIntensity = _currentIntensity;
                _targetIntensity = target;
            }

            // 使用配置的曲线平滑强度变化
            float curveTime = 0.3f; 
            if (_config.SprintBlendCurve.length > 0)
            {
                curveTime = _config.SprintBlendCurve.keys[_config.SprintBlendCurve.length - 1].time;
            }

            if (_blendTimer < curveTime)
            {
                _blendTimer += Time.deltaTime;
                float t = _blendTimer / curveTime;
                float factor = _config.SprintBlendCurve.Evaluate(t);
                _currentIntensity = Mathf.Lerp(_startIntensity, _targetIntensity, factor);
            }
            else
            {
                _currentIntensity = _targetIntensity;
            }
        }

        /// <summary>
        /// 根据运动状态获取目标强度值。
        /// 返回值范围：0.0（Idle）到 1.0（Sprint）
        /// </summary>
        private float GetTargetIntensityForState(LocomotionState state)
        {
            return state switch
            {
                // Walk：缓慢行走（强度 0.3~0.4）
                LocomotionState.Walk => 0.35f,

                // Jog：正常慢跑（强度 0.65~0.75）
                LocomotionState.Jog => 0.7f,

                // Sprint：快速冲刺（强度 0.95~1.0）
                LocomotionState.Sprint => 0.98f,

                // Idle：站立/停止（强度 0.0）
                LocomotionState.Idle => 0.0f,

                _ => 0.0f
            };
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

            // 方向向量平滑（X轴）
            float xSmoothTime = Mathf.Max(0.0001f, _config.XAnimBlendSmoothTime);
            _currentLocalDir = Vector3.SmoothDamp(
                _currentLocalDir,
                targetLocalDir,
                ref _localDirVelocity,
                xSmoothTime,
                Mathf.Infinity,
                Time.deltaTime
            );
            _currentLocalDir.y = 0f;
            if (_currentLocalDir.sqrMagnitude < 0.0001f)
                _currentLocalDir = Vector3.forward;
            else
                _currentLocalDir.Normalize();

            // 由平滑后的向量求角度
            float angle = Mathf.Atan2(_currentLocalDir.x, _currentLocalDir.z) * Mathf.Rad2Deg;
            _data.DesiredLocalMoveAngle = angle;

            // 极坐标 -> 笛卡尔坐标（Cartesian Mixer）
            float rad = angle * Mathf.Deg2Rad;
            float targetX = Mathf.Sin(rad) * _currentIntensity;
            float targetY = Mathf.Cos(rad) * _currentIntensity;

            // X轴直接赋值（方向已平滑）
            _data.CurrentAnimBlendX = targetX;

            // Y轴平滑（速度/强度）
            float ySmoothTime = Mathf.Max(0.0001f, _config.YAnimBlendSmoothTime);
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

        /// <summary>
        /// 持续下落等级计算（每帧更新）：
        /// - 接地：重置 apex，并将 FallHeightLevel 置为 0（避免残留）。
        /// - 离地：持续维护 apex（最高点），并用 (apex - current) 计算当前“已下落距离”。
        /// - 这样 LandState 进入时可以直接消费最后一帧算出来的 FallHeightLevel。
        /// </summary>
        private void UpdateFallHeight()
        {
            bool isGrounded = _data.IsGrounded;
            float currentY = _playerTransform.position.y;

            // 边沿检测：落地/离地
            bool justLanded = !_wasGroundedLastFrame && isGrounded;
            bool justLeftGround = _wasGroundedLastFrame && !isGrounded;

            if (isGrounded)
            {
                // 在地面上：为下一次离地预热。
                _apexY = currentY;

                // 非空中：FallHeightLevel 强制归零，防止上一跳残留。
                _data.FallHeightLevel = 0;

                //（可选）刚落地可以在这里做一次最终校正，但不依赖它。
                if (justLanded)
                {
                    // no-op
                }
            }
            else
            {
                // 刚离地：把 apex 初始化为离地那一刻的高度。
                if (justLeftGround)
                {
                    _apexY = currentY;
                }

                // 上升阶段刷新最高点。
                if (currentY > _apexY)
                {
                    _apexY = currentY;
                }

                // 持续计算“从最高点到当前点”的已下落距离（上升阶段为 0）。
                float fallHeight = Mathf.Max(0f, _apexY - currentY);
                Debug.Log(fallHeight);
                // 每帧写入等级（持续计算）。
                CalculateFallHeightLevel(fallHeight);
            }

            _wasGroundedLastFrame = isGrounded;
        }

        /// <summary>
        /// 纯粹的数学计算，无状态副作用
        /// </summary>
        private void CalculateFallHeightLevel(float height)
        {
            if (height < _config.LandHeightWalkJog_Level1) _data.FallHeightLevel = 0; // 几乎没掉 (如0.5m)
            else if (height < _config.LandHeightWalkJog_Level2) _data.FallHeightLevel = 1; // 轻落地 (如2m)
            else if (height < _config.LandHeightWalkJog_Level3) _data.FallHeightLevel = 2; // 中落地 (如4m)
            else if (height < _config.LandHeightWalkJog_Level4) _data.FallHeightLevel = 3; // 重落地 (如6m)
            else _data.FallHeightLevel = 4; // 摔死/翻滚 (极高)
        }
    }
}
