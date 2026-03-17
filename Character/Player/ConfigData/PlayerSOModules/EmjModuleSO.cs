using UnityEngine;
using Animancer;

namespace BBBNexus
{
    // 表情系统配置模块 负责管理角色的脸部表情动画 包括基础循环与瞬时特殊表情 
    // 表情是独立的动画层 不会干扰身体动作 可以随时打断和叠加 
    [CreateAssetMenu(fileName = "EmjModule", menuName = "BBBNexus/Player/Modules/Emj Module")]
    public class EmjModuleSO : ScriptableObject
    {
        [Header("基础表情 (Base Expression) - 常态表情的循环动画")]
        
        [Tooltip("基础表情动画 循环播放 是玩家的常态脸部表情 通常是中立表情或者柔和笑容")]
        public ClipTransition BaseExpression;

        [Header("特殊表情 (Special Expressions) - 特定事件触发的瞬时表情")]
        
        [Tooltip("特殊表情1")]
        public ClipTransition SpecialExpression1;

        [Tooltip("特殊表情2")]
        public ClipTransition SpecialExpression2;

        [Tooltip("特殊表情3")]
        public ClipTransition SpecialExpression3;

        [Tooltip("特殊表情4")]
        public ClipTransition SpecialExpression4;
    }
}