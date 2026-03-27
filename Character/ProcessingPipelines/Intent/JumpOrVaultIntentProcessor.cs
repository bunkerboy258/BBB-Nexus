using UnityEngine;

namespace BBBNexus
{
    // 跳跃与翻越意图处理器
    public class JumpOrVaultIntentProcessor
    {
        private readonly PlayerRuntimeData _data;
        private readonly PlayerSO _config;
        private readonly Transform _playerTransform;
        private readonly LayerMask _obstacleMask;

        public JumpOrVaultIntentProcessor(PlayerRuntimeData data, PlayerSO config, Transform playerTransform)
        {
            _data = data;
            _config = config;
            _playerTransform = playerTransform;
            _obstacleMask = _config.Vaulting.ObstacleLayers;
        }

        // 注： 彻底移除了早期版本每帧空转的射线探测缓存机制
        // 只有当接收到明确的跳跃指令时，才瞬间进行物理环境扫描
        public bool Update(in ProcessedInputData input)
        {
            // 只有按键按下的那一帧，才启动极其昂贵的射线检测逻辑
            if (input.JumpPressed)
            {
                if (HandleJumpIntent(_data))
                {
                    return true;
                }
            }

            return false;
        }

        // 跳跃意图处理与优先级仲裁
        private bool HandleJumpIntent(PlayerRuntimeData data)
        {
            // 低位翻越检测 (在这里才真正发射射线)
            if (TryGetVaultIntent(data, out VaultObstacleInfo info, _config.Vaulting.LowVaultMinHeight, _config.Vaulting.LowVaultMaxHeight))
            {
                data.WantsToVault = true;
                data.WantsLowVault = true;
                data.CurrentVaultInfo = info;
                return true;
            }

            // 高位翻越检测
            if (TryGetVaultIntent(data, out info, _config.Vaulting.HighVaultMinHeight, _config.Vaulting.HighVaultMaxHeight))
            {
                data.WantsToVault = true;
                data.WantsHighVault = true;
                data.CurrentVaultInfo = info;
                return true;
            }

            // 普通地面跳跃
            if (data.IsGrounded)
            {
                data.WantsToJump = true;
                return true;
            }

            // 空中二段跳
            if (!data.IsGrounded && !data.HasPerformedDoubleJumpInAir)
            {
                data.DoubleJumpDirection = DoubleJumpDirection.Up;
                data.WantsDoubleJump = true;
                return true;
            }

            return false;
        }

        public bool TryGetVaultIntent(PlayerRuntimeData data, out VaultObstacleInfo info, float minHeight, float maxHeight)
        {
            return DetectObstacle(out info, minHeight, maxHeight, true);
        }

        private bool DetectObstacle(out VaultObstacleInfo info, float minHeight, float maxHeight, bool isSilent)
        {
            info = new VaultObstacleInfo { IsValid = false };

            Transform root = _playerTransform;

            Vector3 rayStart = root.position + Vector3.up * _config.Vaulting.VaultForwardRayHeight;
            // 从角色上方偏移 VaultForwardRayHeight 作为前方探测的起点

            Vector3 forward = root.forward;
            // forward = 角色面向 用于朝前的射线方向

            if (Physics.Raycast(rayStart, forward, out RaycastHit wallHit, _config.Vaulting.VaultForwardRayLength, _obstacleMask))
            {
                // 命中前方物体 wallHit 包含击中点与法线

                if (Vector3.Dot(wallHit.normal, Vector3.up) > 0.1f) return false;
                // 如果命中面的法线接近上向（点积大） 说明是地面或缓坡 退出判断

                Vector3 downRayStart = wallHit.point + Vector3.up * _config.Vaulting.VaultDownwardRayLength + forward * _config.Vaulting.VaultDownwardRayOffset;
                // 在墙面点上方并向前偏移 作为向下搜索 ledge 的起点

                if (Physics.Raycast(downRayStart, Vector3.down, out RaycastHit ledgeHit, _config.Vaulting.VaultDownwardRayLength, _obstacleMask))
                {
                    // 找到墙顶或台阶的顶面 ledgeHit

                    if (Vector3.Dot(ledgeHit.normal, Vector3.up) < 0.9f) return false;
                    // ledge 的法线必须接近上向 保证为可站立的平面

                    float height = ledgeHit.point.y - root.position.y;
                    // 计算台阶顶面相对于角色根点的高度

                    if (height < minHeight || height > maxHeight) return false;
                    // 高度不在可翻越区间时退出（用于区分低翻越/高翻越）

                    Vector3 vaultForwardDir = -wallHit.normal;
                    // 翻越的前向为墙法线的反方向（远离墙）

                    Vector3 landRayStart = ledgeHit.point + vaultForwardDir * _config.Vaulting.VaultLandDistance + Vector3.up * 0.5f;
                    // 从 ledge 前方偏移 VaultLandDistance 并上移 0.5m 作为落点检测起点

                    Vector3 finalLandPoint = Vector3.zero;
                    bool foundGround = false;

                    if (Physics.Raycast(landRayStart, Vector3.down, out RaycastHit landHit, _config.Vaulting.VaultLandRayLength, _obstacleMask))
                    {
                        // 向下检测墙后地面
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
                        // 未找到地面时使用兜底点（下移0.5m）作为预估落点 保证数据可用
                    }

                    info.IsValid = true;
                    info.WallPoint = wallHit.point;
                    info.WallNormal = wallHit.normal;
                    info.Height = height;
                    info.ExpectedLandPoint = finalLandPoint;

                    Vector3 ledgeEdge = new Vector3(wallHit.point.x, ledgeHit.point.y, wallHit.point.z);
                    info.LedgePoint = ledgeEdge;

                    Vector3 wallNormalFlat = new Vector3(wallHit.normal.x, 0f, wallHit.normal.z);
                    if (wallNormalFlat.sqrMagnitude < 0.0001f) return false;

                    Vector3 rightDir = Vector3.Cross(Vector3.up, wallNormalFlat).normalized;
                    Vector3 characterRight = new Vector3(root.right.x, 0f, root.right.z).normalized;

                    if (characterRight.sqrMagnitude > 0.0001f)
                    {
                        if (Vector3.Dot(rightDir, characterRight) < 0f)
                            rightDir = -rightDir;
                    }

                    float halfSpread = _config.Vaulting.VaultHandSpread * 0.5f;
                    // 计算默认握点（不含偏移）
                    Vector3 baseLeft = ledgeEdge - rightDir * halfSpread;
                    Vector3 baseRight = ledgeEdge + rightDir * halfSpread;

                    // 将 vaultForwardDir 与 world up 作为 ledge 局部空间基准
                    Quaternion ledgeBasis = Quaternion.LookRotation(vaultForwardDir, Vector3.up);

                    // 应用 VaultingSO 中的可配置偏移（以 ledgeBasis 变换到世界空间）
                    Vector3 leftOffsetWorld = ledgeBasis * _config.Vaulting.LeftHandIKOffset;
                    Vector3 rightOffsetWorld = ledgeBasis * _config.Vaulting.RightHandIKOffset;

                    info.LeftHandPos = baseLeft + leftOffsetWorld;
                    info.RightHandPos = baseRight + rightOffsetWorld;

                    // 手朝向：以墙面法线为基准的朝向，再叠加可配置的欧拉偏移
                    Quaternion baseHandRot = Quaternion.LookRotation(-wallNormalFlat.normalized, Vector3.up);
                    Quaternion handRotOffset = Quaternion.Euler(_config.Vaulting.HandRotationOffsetEuler);
                    info.HandRot = baseHandRot * handRotOffset;

                    return true;
                }
            }
            return false;
        }
    }
}