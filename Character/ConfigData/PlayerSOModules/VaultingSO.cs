using UnityEngine;

namespace BBBNexus
{
    // 翻越系统配置模块 它负责管理越过障碍物的所有参数 包括检测 动画 IK等 
    // 翻越是复杂的多层协调动作 改这里的参数时要同时改对应的动画数据 不然会断手断脚 
    [CreateAssetMenu(fileName = "VaultingSO", menuName = "BBBNexus/Player/Modules/VaultingSO")]
    public class VaultingSO : ScriptableObject
    {
        [Header("翻越检测 (Vault Detection) - 如何识别可翻越的障碍物")]
        
        [Tooltip("障碍物层级掩码 只有这些层的物体才会被视为可翻越的对象")]
        public LayerMask ObstacleLayers;

        [Tooltip("前向射线长度 米 向前探测多远距离才算遇到障碍 太短容易漏过 太长容易提前触发")]
        public float VaultForwardRayLength = 1.5f;

        [Tooltip("前向射线高度 米 从角色多高的位置发射前向射线 用于判断墙的位置")]
        public float VaultForwardRayHeight = 1.0f;

        [Tooltip("下向射线偏移 米 从前向射线命中点向下这个距离发射下向射线 寻找墙顶部的落脚点")]
        public float VaultDownwardRayOffset = 0.5f;

        [Tooltip("下向射线长度 米 向下探测多远才能找到落脚点 超出此距离说明墙太高")]
        public float VaultDownwardRayLength = 2.0f;

        [Space]
        [Tooltip("双手抓握点宽度 米 两只手要隔多远 太近容易抖 太宽不自然")]
        public float VaultHandSpread = 0.4f;

        [Tooltip("落脚点搜索距离 米 在墙顶下方多远范围内搜索平的落脚点")]
        public float VaultLandDistance = 1.5f;

        [Tooltip("落脚点射线长度 米 向下探测多远来确认落脚点 确保有稳定的地面")]
        public float VaultLandRayLength = 3.0f;

        [Tooltip("是否需要墙后有地面 如果启用 则墙后没有地面的地方无法翻越(防止翻到悬崖)")]
        public bool RequireGroundBehindWall = true;

        [Header("翻越高度分级 (Vault Height Classification) - 不同高度用不同的翻越方式")]
        
        [Tooltip("低翻越最小高度 米 障碍物低于此值不能用翻越 会直接走过去 或者摔跤")]
        public float LowVaultMinHeight = 0.5f;
        
        [Tooltip("低翻越最大高度 米 超过此值必须用高翻越 否则无法通过")]
        public float LowVaultMaxHeight = 1.2f;

        [Space]
        [Tooltip("高翻越最小高度 米 低于此值不需要高翻越")]
        public float HighVaultMinHeight = 1.2f;
        
        [Tooltip("高翻越最大高度 米 超过此值无法翻越 只能找其他路")]
        public float HighVaultMaxHeight = 2.5f;

        [Header("翻越动画数据 (Vault Animation Data) - 带根运动与IK的翻越动画")]
        
        [Tooltip("翻越结束后到待机状态的淡入参数")]
        public AnimPlayOptions VaultToIdleOptions = AnimPlayOptions.Default;
        
        [Tooltip("翻越结束后到移动循环的淡入参数")]
        public AnimPlayOptions VaultToMoveOptions = AnimPlayOptions.Default;

        [Tooltip("低翻越使用的带根运动的翻越动画数据 包含IK目标点与播放速率")]
        public WarpedMotionData lowVaultAnim;

        [Tooltip("高翻越使用的带根运动的翻越动画数据 通常比低翻越要花的时间更长")]
        public WarpedMotionData highVaultAnim;

        [Header("IK 偏移 (Hand IK Offsets) - 以 ledge 朝向为基准的本地偏移，可以用来微调握点位置/朝向")]
        [Tooltip("左手 IK 目标在 ledge 局部空间下的偏移（以 ledge 朝向/竖直为基准）")]
        public Vector3 LeftHandIKOffset = Vector3.zero;

        [Tooltip("右手 IK 目标在 ledge 局部空间下的偏移（以 ledge 朝向/竖直为基准）")]
        public Vector3 RightHandIKOffset = Vector3.zero;

        [Tooltip("手部朝向的欧拉角偏移（度），将在计算 HandRot 后乘以该偏移）")]
        public Vector3 HandRotationOffsetEuler = Vector3.zero;
    }
}
