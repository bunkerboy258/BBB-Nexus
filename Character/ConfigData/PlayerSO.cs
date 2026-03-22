using System.Collections.Generic;
using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// Player 总配置文件 - 游戏角色系统的中枢骨架
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerConfig_Main", menuName = "BBBNexus/Player/PlayerConfig (Main)")]
    public class PlayerSO : ScriptableObject
    {
        [Header("--- 核心功能模块 (Core Modules) - 缺一不可")]

        [Tooltip("角色状态机与全局打断逻辑注册表")]
        public PlayerBrainSO Brain;

        [Tooltip("基础物理参数模块")]
        public CoreSO Core;

        [Tooltip("基础移动动画集合")]
        public LocomotionSO LocomotionAnims;

        [Tooltip("跳跃与落地系统")]
        public JumpSO JumpAndLanding;

        [Tooltip("瞄准系统参数")]
        public AimingSO Aiming;

        [Header("--- 高级动作模块 (Optional)")]
        public VaultingSO Vaulting;
        public DodgingSO Dodging;
        public RollSO Rolling;

        [Header("--- Action 系统 (Optional)")]
        [Tooltip("Action 模块：提供 8 个全身动作动画（接管模式）。")]
        public ActionSO Action;

        [Header("--- 音频系统 (Optional)")]
        [Tooltip("音频模块：编号到音效的映射（为空则不播放任何音效）。")]
        public AudioSO Audio;

        [Header("--- 表情系统 (Optional)")]
        public EmjSO Emj;
    }
}
