using Characters.Player.Animation;
using UnityEngine;

namespace Characters.Player.Data
{
    /// <summary>
    /// 翻越系统配置模块 (ScriptableObject)。
    /// 包含所有与翻越相关的探测参数、动画数据和高度阈值。
    /// </summary>
    [CreateAssetMenu(fileName = "VaultingModule", menuName = "Player/Modules/Vaulting Module")]
    public class VaultingSO : ScriptableObject
    {
        [Header("VAULT - DETECTION - 翻越检测")]
        [Tooltip("哪些层被认为是可翻越的障碍物")]
        public LayerMask ObstacleLayers;

        [Tooltip("向前探测墙壁的射线长度")]
        public float VaultForwardRayLength = 1.5f;

        [Tooltip("向前探测射线的起点高度 (相对于角色根节点)")]
        public float VaultForwardRayHeight = 1.0f;

        [Tooltip("向下探测墙沿的射线起点，相对于墙面交点的向前偏移量")]
        public float VaultDownwardRayOffset = 0.5f;

        [Tooltip("向下探测墙沿的射线长度")]
        public float VaultDownwardRayLength = 2.0f;

        [Space]
        [Tooltip("双手在墙沿上的间距")]
        public float VaultHandSpread = 0.4f;

        [Tooltip("寻找墙后落地点的探测距离")]
        public float VaultLandDistance = 1.5f;

        [Tooltip("寻找落地点的射线长度")]
        public float VaultLandRayLength = 3.0f;

        [Tooltip("是否必须找到墙后的地面才能翻越？")]
        public bool RequireGroundBehindWall = true;

        [Header("VAULT - HEIGHT THRESHOLDS - 翻越高度阈值")]
        [Tooltip("低翻越的最小高度")]
        public float LowVaultMinHeight = 0.5f;
        [Tooltip("低翻越的最大高度")]
        public float LowVaultMaxHeight = 1.2f;

        [Space]
        [Tooltip("高翻越的最小高度")]
        public float HighVaultMinHeight = 1.2f;
        [Tooltip("高翻越的最大高度")]
        public float HighVaultMaxHeight = 2.5f;

        [Header("VAULT - ANIMATION DATA - 翻越动画数据")]
        public AnimPlayOptions VaultToIdleOptions = AnimPlayOptions.Default;
        public AnimPlayOptions VaultToMoveOptions = AnimPlayOptions.Default;

        [Tooltip("低翻越使用的 Warped Motion 动画数据")]
        public WarpedMotionData lowVaultAnim;

        [Tooltip("高翻越使用的 Warped Motion 动画数据")]
        public WarpedMotionData highVaultAnim;
    }
}
