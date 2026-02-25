using UnityEngine;

namespace Characters.Player.Data
{
    /// <summary>
    /// Player 总配置文件。
    /// 作为所有功能模块的根引用。
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerConfig_Main", menuName = "Player/PlayerConfig (Main)")]
    public class PlayerSO : ScriptableObject
    {
        [Header("--- 核心功能模块 (Core Modules) ---")]
        [Tooltip("基础物理、视角、耐力等")]
        public CoreModuleSO Core;

        [Tooltip("基础移动动画集 (走/跑/跳/起步/停止)")]
        public LocomotionAnimSetSO LocomotionAnims;

        [Tooltip("跳跃、二段跳、落地系统")]
        public JumpModuleSO JumpAndLanding;

        [Tooltip("瞄准系统参数")]
        public AimingModuleSO Aiming;

        [Header("--- 高级动作模块 (Optional) ---")]
        [Tooltip("翻越系统 (可选, 为 null 则禁用)")]
        public VaultingSO Vaulting;

        [Tooltip("闪避系统 (可选, 为 null 则禁用)")]
        public DodgingSO Dodging;
    }
}
