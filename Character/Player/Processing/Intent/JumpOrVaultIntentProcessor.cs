using Characters.Player.Data;
using UnityEngine;

namespace Characters.Player.Processing
{
    public class JumpOrVaultIntentProcessor
    {
        private PlayerController _player;
        private PlayerSO _config;
        private LayerMask _obstacleMask;

        // --- Debug 缓存变量 ---
        private VaultObstacleInfo _lastValidLowVaultInfo;
        private float _lastValidLowVaultTime;
        private VaultObstacleInfo _lastValidHighVaultInfo;
        private float _lastValidHighVaultTime;

        // 可在 PlayerSO 中配置，这里硬编码为 2 秒，方便您观察
        private float _debugHoldDuration = 2.0f;

        public JumpOrVaultIntentProcessor(PlayerController player)
        {
            _player = player;
            _config = player.Config;
            _obstacleMask = _config.Vaulting. ObstacleLayers;

            // 订阅跳跃按键按下事件
            _player.InputReader.OnJumpPressed += OnJumpPressed;
        }


        /// <summary>
        /// Update 负责每帧的环境扫描（用于 Debug 绘制）和清理残留意图
        /// </summary>
        public void Update()
        {
            var data = _player.RuntimeData;

            // --- 1. 实时扫描环境并更新 Debug 缓存 (即使不按键) ---
            // 只有在地面上才去扫描墙壁，节省性能
            if (data.IsGrounded)
            {
                // 尝试扫描低墙
                if (DetectObstacle(out VaultObstacleInfo lowInfo, _config.Vaulting.LowVaultMinHeight, _config.Vaulting.LowVaultMaxHeight, false))
                {
                    _lastValidLowVaultInfo = lowInfo;
                    _lastValidLowVaultTime = Time.time;
                }

                // 尝试扫描高墙
                if (DetectObstacle(out VaultObstacleInfo highInfo, _config.Vaulting.HighVaultMinHeight, _config.Vaulting.HighVaultMaxHeight, false))
                {
                    _lastValidHighVaultInfo = highInfo;
                    _lastValidHighVaultTime = Time.time;
                }
            }

        }

        /// <summary>
        /// 跳跃键按下回调：仲裁意图
        /// </summary>
        private void OnJumpPressed()
        {
            var data = _player.RuntimeData;

            // 先检测矮翻越
            if (TryGetVaultIntent(data, out VaultObstacleInfo info, _config.Vaulting.LowVaultMinHeight, _config.Vaulting.LowVaultMaxHeight))
            {
                data.WantsToVault = true;
                data.WantsLowVault = true;
                data.CurrentVaultInfo = info;
                return;
            }

            // 再检测高翻越
            if (TryGetVaultIntent(data, out info, _config.Vaulting.HighVaultMinHeight, _config.Vaulting.HighVaultMaxHeight))
            {
                data.WantsToVault = true;
                data.WantsHighVault = true;
                data.CurrentVaultInfo = info;
                return;
            }

            // 地面跳跃
            if (data.IsGrounded)
            {
                data.WantsToJump = true;
                return;
            }

            // 空中二段跳
            if (!data.IsGrounded && !data.HasPerformedDoubleJumpInAir)
            {
                data.DoubleJumpDirection = DoubleJumpDirection.Up;
                data.WantsDoubleJump = true;
                return;
            }
        }

        public bool TryGetVaultIntent(PlayerRuntimeData data, out VaultObstacleInfo info, float minHeight, float maxHeight)
        {
            info = new VaultObstacleInfo { IsValid = false };
            // 允许在地面上检测，也允许在已执行过二段跳的空中情况检测翻越（便于二段跳结束后接翻越）
            if (!data.IsGrounded && !data.HasPerformedDoubleJumpInAir) return false;

            // 按下按键时，调用静默模式(不画线)的检测，获取最新数据
            return DetectObstacle(out info, minHeight, maxHeight, true);
        }

        /// <summary>
        /// 纯粹的数学和物理检测逻辑 (无绘制开销)
        /// </summary>
        private bool DetectObstacle(out VaultObstacleInfo info, float minHeight, float maxHeight, bool isSilent)
        {
            info = new VaultObstacleInfo { IsValid = false };

            Transform root = _player.transform;
            Vector3 rayStart = root.position + Vector3.up * _config.Vaulting.VaultForwardRayHeight;
            Vector3 forward = root.forward;

            // --- 第一步：向前找墙 ---
            if (Physics.Raycast(rayStart, forward, out RaycastHit wallHit, _config.Vaulting.VaultForwardRayLength, _obstacleMask))
            {
                if (Vector3.Dot(wallHit.normal, Vector3.up) > 0.1f) return false;

                // --- 第二步：向下找墙沿 ---
                Vector3 downRayStart = wallHit.point + Vector3.up * _config.Vaulting.VaultDownwardRayLength + forward * _config.Vaulting.VaultDownwardRayOffset;

                if (Physics.Raycast(downRayStart, Vector3.down, out RaycastHit ledgeHit, _config.Vaulting.VaultDownwardRayLength, _obstacleMask))
                {
                    if (Vector3.Dot(ledgeHit.normal, Vector3.up) < 0.9f) return false;

                    float height = ledgeHit.point.y - root.position.y;
                    if (height < minHeight || height > maxHeight) return false;

                    // --- 第三步：寻找墙后落地点 ---
                    Vector3 vaultForwardDir = -wallHit.normal;
                    Vector3 landRayStart = ledgeHit.point + vaultForwardDir * _config.Vaulting.VaultLandDistance + Vector3.up * 0.5f;
                    Vector3 finalLandPoint = Vector3.zero;
                    bool foundGround = false;

                    if (Physics.Raycast(landRayStart, Vector3.down, out RaycastHit landHit, _config.Vaulting.VaultLandRayLength, _obstacleMask))
                    {
                        if (Vector3.Dot(landHit.normal, Vector3.up) >= 0.7f)
                        {
                            finalLandPoint = landHit.point;
                            foundGround = true;
                        }
                    }

                    if (_config.Vaulting.RequireGroundBehindWall && !foundGround) return false;

                    if (!foundGround)
                    {
                        finalLandPoint = landRayStart + Vector3.down * 0.5f;
                    }

                    // --- 第四步：组装数据 ---
                    info.IsValid = true;
                    info.WallPoint = wallHit.point;
                    info.WallNormal = wallHit.normal;
                    info.Height = height;
                    info.ExpectedLandPoint = finalLandPoint;

                    Vector3 ledgeEdge = new Vector3(wallHit.point.x, ledgeHit.point.y, wallHit.point.z);
                    info.LedgePoint = ledgeEdge;

                    Vector3 rightDir = Vector3.Cross(Vector3.up, wallHit.normal).normalized;
                    info.LeftHandPos = ledgeEdge + rightDir * (_config.Vaulting.VaultHandSpread / 2f);
                    info.RightHandPos = ledgeEdge - rightDir * (_config.Vaulting.VaultHandSpread / 2f);
                    info.HandRot = Quaternion.LookRotation(-wallHit.normal, Vector3.up);

                    return true;
                }
            }
            return false;
        }


    }
}
