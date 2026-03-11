using Characters.Player.Data;
using UnityEngine;

namespace Characters.Player.Processing
{
    // 跳跃与翻越意图处理器 它是高段移动的决策中枢 
    // 负责检测翻越障碍物 仲裁跳跃 翻越 二段跳的优先级 
    public class JumpOrVaultIntentProcessor
    {
        private PlayerController _player;
        private PlayerSO _config;
        private LayerMask _obstacleMask;

        // 缓存有效的翻越信息 使得按键时能快速获取最新的障碍物数据 
        private VaultObstacleInfo _lastValidLowVaultInfo;
        private float _lastValidLowVaultTime;
        private VaultObstacleInfo _lastValidHighVaultInfo;
        private float _lastValidHighVaultTime;

        public JumpOrVaultIntentProcessor(PlayerController player)
        {
            _player = player;
            _config = player.Config;
            _obstacleMask = _config.Vaulting.ObstacleLayers;
        }

        // 每帧的环境扫描与输入检测 
        // 即使不按键也要持续扫描周围障碍 为按键时提供最新数据 
        public void Update()
        {
            var data = _player.RuntimeData;

            // 实时扫描环境并更新缓存 但只在地面上进行以节省性能
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

            // 直接读取输入状态并检测跳跃按下 
            if (_player.InputReader != null)
            {
                var inputFrame = _player.InputReader.Current;
                if (inputFrame.JumpPressed)
                {
                    HandleJumpIntent(data);
                    _player.InputReader.ConsumeJump();
                }
            }
        }

        // 跳跃意图处理 仲裁是否跳跃 翻越或二段跳 
        // 优先级依次为 低翻越 高翻越 地面跳跃 空中二段跳 
        private void HandleJumpIntent(PlayerRuntimeData data)
        {
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
            // 允许在地面上检测 也允许在已执行过二段跳的空中情况检测翻越 便于二段跳结束后接翻越
            if (!data.IsGrounded && !data.HasPerformedDoubleJumpInAir) return false;

            // 按下按键时 调用静默模式的检测 获取最新数据
            return DetectObstacle(out info, minHeight, maxHeight, true);
        }

        // 纯粹的数学和物理检测逻辑 无绘制开销 
        private bool DetectObstacle(out VaultObstacleInfo info, float minHeight, float maxHeight, bool isSilent)
        {
            info = new VaultObstacleInfo { IsValid = false };

            Transform root = _player.transform;
            Vector3 rayStart = root.position + Vector3.up * _config.Vaulting.VaultForwardRayHeight;
            Vector3 forward = root.forward;

            // 第一步 向前找墙 
            if (Physics.Raycast(rayStart, forward, out RaycastHit wallHit, _config.Vaulting.VaultForwardRayLength, _obstacleMask))
            {
                if (Vector3.Dot(wallHit.normal, Vector3.up) > 0.1f) return false;

                // 第二步 向下找墙沿 
                Vector3 downRayStart = wallHit.point + Vector3.up * _config.Vaulting.VaultDownwardRayLength + forward * _config.Vaulting.VaultDownwardRayOffset;

                if (Physics.Raycast(downRayStart, Vector3.down, out RaycastHit ledgeHit, _config.Vaulting.VaultDownwardRayLength, _obstacleMask))
                {
                    if (Vector3.Dot(ledgeHit.normal, Vector3.up) < 0.9f) return false;

                    float height = ledgeHit.point.y - root.position.y;
                    if (height < minHeight || height > maxHeight) return false;

                    // 第三步 寻找墙后落地点 
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

                    // 第四步 组装数据 修复手部握持方向
                    info.IsValid = true;
                    info.WallPoint = wallHit.point;
                    info.WallNormal = wallHit.normal;
                    info.Height = height;
                    info.ExpectedLandPoint = finalLandPoint;

                    Vector3 ledgeEdge = new Vector3(wallHit.point.x, ledgeHit.point.y, wallHit.point.z);
                    info.LedgePoint = ledgeEdge;

                    // 注意：手部左右的“正方向”必须稳定
                    // 之前用 (ledge->root) 与 rightDir 点乘做翻转 在某些角度/贴墙位置会抖动导致左右手互换(总感觉怪怪的找了半天)
                    // 这里改成用“角色右方向”作为符号判定 只要角色朝向不突变 左右就不会反掉

                    // 用墙法线构建横向轴：沿墙面水平方向（避免 Y 分量污染）
                    Vector3 wallNormalFlat = new Vector3(wallHit.normal.x, 0f, wallHit.normal.z);
                    if (wallNormalFlat.sqrMagnitude < 0.0001f) return false;

                    // rightDir = wallRight（沿墙面的右方向）
                    Vector3 rightDir = Vector3.Cross(Vector3.up, wallNormalFlat).normalized;

                    // 让 rightDir 的符号与角色的右方向一致 这样左/右手永远不会互换
                    Vector3 characterRight = new Vector3(root.right.x, 0f, root.right.z).normalized;
                    if (characterRight.sqrMagnitude > 0.0001f)
                    {
                        if (Vector3.Dot(rightDir, characterRight) < 0f)
                            rightDir = -rightDir;
                    }

                    float halfSpread = _config.Vaulting.VaultHandSpread * 0.5f;
                    info.LeftHandPos = ledgeEdge - rightDir * halfSpread;
                    info.RightHandPos = ledgeEdge + rightDir * halfSpread;

                    // 手部朝向：沿着墙面法线的反方向（朝向墙）
                    info.HandRot = Quaternion.LookRotation(-wallNormalFlat.normalized, Vector3.up);

                    return true;
                }
            }
            return false;
        }
    }
}
