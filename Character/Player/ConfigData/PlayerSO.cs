using System.Collections.Generic;
using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// Player 总配置文件 - 游戏角色系统的中枢骨架
    /// 这里集合了玩家角色所有关键子系统的配置引用 
    /// 就像编织一根绳子 一端是意图管线 另一端是物理驱动 这个SO就是把所有线头扎在一起的地方
    /// 别瞎改引用 不然状态机会散架
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerConfig_Main", menuName = "BBBNexus/Player/PlayerConfig (Main)")]
    public class PlayerSO : ScriptableObject
    {
        [Header("--- 核心功能模块 (Core Modules) - 缺一不可")]

        [Tooltip("角色状态机与全局打断逻辑注册表 存储所有可用的玩家状态与拦截器 这是状态机的灵魂")]
        public PlayerBrainSO Brain;

        [Tooltip("基础物理参数模块 包含重力 速度 视角灵敏度 体力系统等 改这里的参数会影响整个游戏的手感")]
        public CoreModuleSO Core;

        [Tooltip("基础移动动画集合 包含行走 跑步 冲刺的所有启动 循环 停止动画 以及8方向启动根运动 游戏流畅度的直接来源")]
        public LocomotionAnimSetSO LocomotionAnims;

        [Tooltip("跳跃与落地系统 管理跳跃力度 二段跳 5级高度的落地缓冲动画等 没这个模块就像在地面粘着走")]
        public JumpModuleSO JumpAndLanding;

        [Tooltip("瞄准系统参数 控制瞄准时的灵敏度 移动速度 动画混合树等 射击游戏必须的组件")]
        public AimingModuleSO Aiming;

        [Header("--- 高级动作模块 (Optional - 为 null 则禁用) - 可选功能")]

        [Tooltip("翻越系统模块 障碍物检测 IK手部对齐 根运动变形等 为空则禁用所有翻越动作 让关卡设计少了很多花样")]
        public VaultingSO Vaulting;

        [Tooltip("闪避系统模块 8方向闪避的根运动与体力消耗 为空则玩家无法躲避 战斗会变得很艰难")]
        public DodgingSO Dodging;

        [Tooltip("翻滚系统模块 与闪避类似但更耗时耗力 为空则禁用翻滚 有些设计会用翻滚代替闪避")]
        public RollSO Rolling;

        [Header("--- 表情系统 (Optional - 为 null 则使用 Core 默认表情)")]

        [Tooltip("表情模块 包含1个基础循环表情与4个瞬时特殊表情 为空则自动使用CoreModule中的BlinkAnim和HurtFaceAnim 不影响战斗但会让角色表现更生动")]
        public EmjModuleSO Emj;
    }
}
