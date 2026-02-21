using Characters.Player.Data;
using DrawXXL;
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
            _obstacleMask = _config.ObstacleLayers;

            // 订阅跳跃按键按下事件
            _player.InputReader.OnJumpPressed += OnJumpPressed;
        }

        ~JumpOrVaultIntentProcessor()
        {
            if (_player?.InputReader != null)
            {
                _player.InputReader.OnJumpPressed -= OnJumpPressed;
            }
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
                if (DetectObstacle(out VaultObstacleInfo lowInfo, _config.LowVaultMinHeight, _config.LowVaultMaxHeight, false))
                {
                    _lastValidLowVaultInfo = lowInfo;
                    _lastValidLowVaultTime = Time.time;
                }

                // 尝试扫描高墙
                if (DetectObstacle(out VaultObstacleInfo highInfo, _config.HighVaultMinHeight, _config.HighVaultMaxHeight, false))
                {
                    _lastValidHighVaultInfo = highInfo;
                    _lastValidHighVaultTime = Time.time;
                }
            }

            // --- 2. 持续绘制有效的 Debug 信息 ---
            // 如果最近 2 秒内扫描到过低墙
            if (Time.time - _lastValidLowVaultTime < _debugHoldDuration && _lastValidLowVaultInfo.IsValid)
            {
                DrawDebugInfo(_lastValidLowVaultInfo, "Low Vault");
            }
            // 如果最近 2 秒内扫描到过高墙
            else if (Time.time - _lastValidHighVaultTime < _debugHoldDuration && _lastValidHighVaultInfo.IsValid)
            {
                DrawDebugInfo(_lastValidHighVaultInfo, "High Vault");
            }

            // （原有的意图清理逻辑已注释，保持原样）
            //data.WantsLowVault = false;
            //data.WantsHighVault = false;
            //data.WantsToJump = false;
            //data.WantsDoubleJump = false;
        }

        /// <summary>
        /// 跳跃键按下回调：仲裁意图
        /// </summary>
        private void OnJumpPressed()
        {
            var data = _player.RuntimeData;

            // 先检测矮翻越
            if (TryGetVaultIntent(data, out VaultObstacleInfo info, _config.LowVaultMinHeight, _config.LowVaultMaxHeight))
            {
                data.WantsToVault = true;
                data.WantsLowVault = true;
                data.CurrentVaultInfo = info;
                return;
            }

            // 再检测高翻越
            if (TryGetVaultIntent(data, out info, _config.HighVaultMinHeight, _config.HighVaultMaxHeight))
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
            if (!data.IsGrounded) return false;

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
            Vector3 rayStart = root.position + Vector3.up * _config.VaultForwardRayHeight;
            Vector3 forward = root.forward;

            if (!isSilent) DrawBasics.Ray(rayStart, forward, Color.yellow, 0.02f); // 用细黄线表示正在扫描

            // --- 第一步：向前找墙 ---
            if (Physics.Raycast(rayStart, forward, out RaycastHit wallHit, _config.VaultForwardRayLength, _obstacleMask))
            {
                if (Vector3.Dot(wallHit.normal, Vector3.up) > 0.1f) return false;

                // --- 第二步：向下找墙沿 ---
                Vector3 downRayStart = wallHit.point + Vector3.up * _config.VaultDownwardRayLength + forward * _config.VaultDownwardRayOffset;

                if (Physics.Raycast(downRayStart, Vector3.down, out RaycastHit ledgeHit, _config.VaultDownwardRayLength, _obstacleMask))
                {
                    if (Vector3.Dot(ledgeHit.normal, Vector3.up) < 0.9f) return false;

                    float height = ledgeHit.point.y - root.position.y;
                    if (height < minHeight || height > maxHeight) return false;

                    // --- 第三步：寻找墙后落地点 ---
                    Vector3 vaultForwardDir = -wallHit.normal;
                    Vector3 landRayStart = ledgeHit.point + vaultForwardDir * _config.VaultLandDistance + Vector3.up * 0.5f;
                    Vector3 finalLandPoint = Vector3.zero;
                    bool foundGround = false;

                    if (Physics.Raycast(landRayStart, Vector3.down, out RaycastHit landHit, _config.VaultLandRayLength, _obstacleMask))
                    {
                        if (Vector3.Dot(landHit.normal, Vector3.up) >= 0.7f)
                        {
                            finalLandPoint = landHit.point;
                            foundGround = true;
                        }
                    }

                    if (_config.RequireGroundBehindWall && !foundGround) return false;

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
                    info.LeftHandPos = ledgeEdge + rightDir * (_config.VaultHandSpread / 2f);
                    info.RightHandPos = ledgeEdge - rightDir * (_config.VaultHandSpread / 2f);
                    info.HandRot = Quaternion.LookRotation(-wallHit.normal, Vector3.up);

                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 专门负责把有效数据画出来的函数
        /// </summary>
        private void DrawDebugInfo(VaultObstacleInfo info, string vaultType)
        {
            // 在墙上打个字，告诉您现在扫到的是高墙还是矮墙
            DrawBasics.Point(info.WallPoint, vaultType, Color.red, 0.1f, 0.0f, Color.red);
            DrawBasics.Vector(info.WallPoint, info.WallPoint + info.WallNormal * 0.5f, Color.red, 0.02f, "Normal");

            DrawBasics.Point(info.LedgePoint, "Ledge Hit", Color.green, 0.1f);

            // 如果落地点是个坑（虚拟点），画灰色；如果是实地，画蓝色
            Color landColor = (info.ExpectedLandPoint.y > info.LedgePoint.y - 10f) ? Color.blue : Color.gray;
            DrawBasics.Point(info.ExpectedLandPoint, "Land Point", landColor, 0.2f);

            DrawBasics.Point(info.LeftHandPos, "L_IK", Color.magenta, 0.1f);
            DrawBasics.Point(info.RightHandPos, "R_IK", Color.magenta, 0.1f);

            Vector3 xAxis = info.HandRot * Vector3.right * 0.3f;
            Vector3 yAxis = info.HandRot * Vector3.up * 0.3f;
            Vector3 zAxis = info.HandRot * Vector3.forward * 0.3f;

            DrawBasics.Vector(info.LeftHandPos, info.LeftHandPos + xAxis, Color.red, 0.02f);
            DrawBasics.Vector(info.LeftHandPos, info.LeftHandPos + yAxis, Color.green, 0.02f);
            DrawBasics.Vector(info.LeftHandPos, info.LeftHandPos + zAxis, Color.blue, 0.02f);
        }
    }
}
