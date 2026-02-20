using Characters.Player.Data;
using DrawXXL;
using UnityEngine;

namespace Characters.Player.Processing
{
    public class JumpOrVaultIntentProcessor
    {
        private PlayerController _player;
        private PlayerSO _config;

        // [建议放入 PlayerSO 的配置项，这里为了直观暂时硬编码]
        private float _forwardRayLength = 1.5f; // 前方检测距离
        private float _forwardRayHeight = 1.0f; // 前方射线的起点高度 (胸部位置)
        private float _downwardRayOffset = 0.5f; // 向下射线距离墙面的偏移量 (越过墙沿多少距离开始往下打)
        private float _downwardRayLength = 2.0f; // 向下射线长度
        private float _handSpread = 0.4f; // 双手在墙沿上的间距 (米)
        private LayerMask _obstacleMask; // 哪些层被认为是障碍物

        public JumpOrVaultIntentProcessor(PlayerController player)
        {
            _player = player;
            _config = player.Config;
            _obstacleMask = LayerMask.GetMask("Default", "Environment"); // 根据您的项目修改
        }

        /// <summary>
        /// 每帧更新：监听 Jump 按键并在 Jump 按下时仲裁 Vault/Jump/DoubleJump 意图。
        /// 如果 Jump 未按下，则清理跳跃/翻越意图。
        /// </summary>
        public void Update()
        {
            var data = _player.RuntimeData;

            if (_player.InputReader != null && _player.InputReader.IsJumpPressed)
            {
                // 先检测是否可以翻越
                if (TryGetVaultIntent(data, out VaultObstacleInfo info))
                {
                    data.WantsToVault = true;
                    data.CurrentVaultInfo = info;

                    // 确保普通跳跃意图被拦截
                    data.WantsToJump = false;
                    data.WantsDoubleJump = false;
                    return;
                }

                // 地面跳跃
                if (data.IsGrounded)
                {
                    data.WantsToJump = true;
                    data.WantsToVault = false;
                    data.WantsDoubleJump = false;
                    return;
                }

                // 空中二段跳
                if (!data.IsGrounded && !data.HasPerformedDoubleJumpInAir)
                {
                    data.DoubleJumpDirection = DoubleJumpDirection.Up;
                    data.WantsDoubleJump = true;
                    data.WantsToJump = false;
                    data.WantsToVault = false;
                    return;
                }

                // 如果按下Jump但未满足任何条件，保持意图为 false
                data.WantsToVault = false;
                data.WantsToJump = false;
                data.WantsDoubleJump = false;
            }
            else
            {
                // 未按下 Jump：重置跳跃与翻越相关的一次性意图
                data.WantsToVault = false;
                data.WantsToJump = false;
                data.WantsDoubleJump = false;
            }
        }

        /// <summary>
        /// 检测当前是否可触发翻越。
        /// </summary>
        public bool TryGetVaultIntent(PlayerRuntimeData data, out VaultObstacleInfo info)
        {
            info = new VaultObstacleInfo { IsValid = false };

            // 默认规则：必须在地面才允许触发翻越（跑酷可放宽）
            if (!data.IsGrounded) return false;

            return DetectObstacle(out info);
        }

        private bool DetectObstacle(out VaultObstacleInfo info)
        {
            info = new VaultObstacleInfo { IsValid = false };

            Transform root = _player.transform;
            Vector3 rayStart = root.position + Vector3.up * _forwardRayHeight;
            Vector3 forward = root.forward;

            DrawBasics.Ray(rayStart, forward, Color.yellow);

            // --- 第一步：向前打射线找墙 ---
            if (Physics.Raycast(rayStart, forward, out RaycastHit wallHit, _forwardRayLength, _obstacleMask))
            {
                // 确保打中的是一堵墙（法线比较水平）
                if (Vector3.Dot(wallHit.normal, Vector3.up) > 0.1f) return false;

                DrawBasics.Point(wallHit.point, "Wall Hit", Color.red, 0.05f, 0.0f, Color.red);
                DrawBasics.Vector(wallHit.point, wallHit.point + wallHit.normal * 0.5f, Color.red, 0.0f, "Wall Normal");

                // --- 第二步：从墙面上方，往墙后稍微偏移一点，向下打射线找墙沿 ---
                // 起点：击中点往上抬起一定高度，并沿着角色正前方(越过墙沿)偏移
                Vector3 downRayStart = wallHit.point + Vector3.up * _downwardRayLength + forward * _downwardRayOffset;

                DrawBasics.Ray(downRayStart, Vector3.down, Color.cyan);

                if (Physics.Raycast(downRayStart, Vector3.down, out RaycastHit ledgeHit, _downwardRayLength, _obstacleMask))
                {
                    // 确保打中的是水平面（墙顶）
                    if (Vector3.Dot(ledgeHit.normal, Vector3.up) < 0.9f) return false;

                    // 计算墙高 (Ledge的Y 减去 角色脚底的Y)
                    float height = ledgeHit.point.y - root.position.y;

                    // 检查高度是否在允许翻越的范围内 (比如 0.5m ~ 2.5m)
                    if (height < 0.5f || height > 2.5f) return false;

                    DrawBasics.Point(ledgeHit.point, "Ledge Hit", Color.green, 0.05f);

                    // --- 第三步：计算最终数据 ---
                    info.IsValid = true;
                    info.WallPoint = wallHit.point;
                    info.WallNormal = wallHit.normal;
                    info.Height = height;

                    // 墙沿边线点：把向下射线的击中点(LedgeHit)，沿着墙面法线往回推，推到和墙面(WallHit)平齐的位置
                    // 这是为了防止手抓得太深
                    Vector3 ledgeEdge = new Vector3(wallHit.point.x, ledgeHit.point.y, wallHit.point.z);
                    info.LedgePoint = ledgeEdge;

                    // 计算双手 IK 目标点
                    // 沿着墙沿（即法线的叉乘方向）左右分开
                    Vector3 rightDir = Vector3.Cross(Vector3.up, wallHit.normal).normalized;

                    info.LeftHandPos = ledgeEdge + rightDir * (_handSpread / 2f);
                    info.RightHandPos = ledgeEdge - rightDir * (_handSpread / 2f);

                    // 计算双手旋转：手掌朝下，手指朝前（也就是墙的法线反方向）
                    // 假设模型的 Z 轴是手背，Y 轴是手指 (这取决于您的具体骨骼朝向，可能需要调整)
                    info.HandRot = Quaternion.LookRotation(-wallHit.normal, Vector3.up);

                    // Draw IK target points
                    // 修复：DrawShapes.Sphere 的预期签名与传入参数不匹配，改为使用 DrawBasics.Point（接受 Vector3 位置）
                    DrawBasics.Point(info.LeftHandPos, "L_IK", Color.magenta, 0.03f);
                    DrawBasics.Point(info.RightHandPos, "R_IK", Color.magenta, 0.03f);

                    // Draw coordinate system using Vector drawing
                    Vector3 xAxis = info.HandRot * Vector3.right * 0.2f;
                    Vector3 yAxis = info.HandRot * Vector3.up * 0.2f;
                    Vector3 zAxis = info.HandRot * Vector3.forward * 0.2f;

                    DrawBasics.Vector(info.LeftHandPos, info.LeftHandPos + xAxis, Color.red, 0.0f);
                    DrawBasics.Vector(info.LeftHandPos, info.LeftHandPos + yAxis, Color.green, 0.0f);
                    DrawBasics.Vector(info.LeftHandPos, info.LeftHandPos + zAxis, Color.blue, 0.0f);

                    return true;
                }
            }

            return false;
        }
    }
}
