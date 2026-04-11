using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace BBBNexus
{
    /// <summary>
    /// Player 总配置文件 - 游戏角色系统的中枢骨架
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerConfig_Main", menuName = "BBBNexus/Player/PlayerConfig (Main)")]
    public class PlayerSO : ScriptableObject
    {
        [Header("核心功能模块")]

        [Tooltip("角色状态机与全局打断逻辑注册表")]
        public PlayerBrainSO Brain;

        [Tooltip("Locomotion 域仲裁配置。当前为 Brain 的语义别名，避免旧资源断引用。")]
        public PlayerBrainSO LocomotionBrain => Brain;

        [Tooltip("基础物理参数模块")]
        public CoreSO Core;

        [Tooltip("基础移动动画集合")]
        public LocomotionSO LocomotionAnims;

        [Tooltip("跳跃与落地系统")]
        public JumpSO JumpAndLanding;

        [FormerlySerializedAs("Aiming")]
        [Tooltip("战术持枪下半身基座参数")]
        public TacticalMotionBaseSO TacticalMotionBase;

        [System.Obsolete("Use TacticalMotionBase instead.")]
        public TacticalMotionBaseSO Aiming => TacticalMotionBase;

        [Header("被动反应")]
        [Tooltip("受击僵直状态 SO（HealthArbiter 在结算伤害时自动施加）")]
        public StatusEffectSO HitReaction;

        [Header("高级模块 ")]
        public VaultingSO Vaulting;
        public DodgingSO Dodging;
        public RollSO Rolling;
        public ActionSO Action;
        [Tooltip("Action 域仲裁配置。当前可为空，逐步承接从 OverrideState 迁出的动作规则。")]
        public ActionArbiterSO ActionArbiterConfig;
        [Tooltip("Status 域仲裁配置。当前可为空，逐步承接受击/僵直等控制规则。")]
        public StatusArbiterSO StatusArbiterConfig;
        public LocomotionAudioSO Audio;
        public EmjSO Emj;

        [Header("装备系统")]
        [Tooltip("角色可用的装备槽位列表配置")]
        public EquipmentSlotRegistrySO SlotRegistry;
    }
}
