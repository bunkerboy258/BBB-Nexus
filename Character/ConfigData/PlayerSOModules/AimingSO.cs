using Animancer;
using UnityEngine;

namespace BBBNexus
{
    // 瞄准系统配置模块 它独立管理瞄准状态下的所有参数 包括灵敏度 移动速度 混合树等 
    // 瞄准时会从常规移动状态切到瞄准移动状态 所有参数都独立设置 
    [CreateAssetMenu(fileName = "AimingSO", menuName = "BBBNexus/Player/Modules/AimingSO")]
    public class AimingSO : ScriptableObject
    {
        [Header("瞄准灵敏度 (Aiming Sensitivity) - 瞄准模式下的视角反应")]
        
        [Tooltip("瞄准时的鼠标灵敏度倍数 通常比常规灵敏度低(0.5~0.8) 让玩家能更精准瞄准")]
        public float AimSensitivity = 1f;
        
        [Header("瞄准移动速度 (Aiming Movement Speeds) - 瞄准时的移动受限")]
        
        [Tooltip("瞄准时行走速度 m/s 通常低于常规行走速度 防止走火")]
        public float AimWalkSpeed = 1.5f;
        
        [Tooltip("瞄准时慢跑速度 m/s")]
        public float AimJogSpeed = 2.5f;
        
        [Tooltip("瞄准时冲刺速度 m/s 建议保持和无瞄准时一样")]
        public float AimSprintSpeed = 5.0f;
        
        [Header("旋转与混合参数 (Rotation & Blending) - 视角与动画的平滑度")]
        
        [Tooltip("瞄准时的旋转平滑时间 秒 通常比常规小 让转向更直接")]
        public float AimRotationSmoothTime = 0.05f;

        [Tooltip("瞄准前后(X)动画参数平滑时间 秒 控制动画过渡的流畅度")]
        public float AimXAnimBlendSmoothTime = 0.2f;
        
        [Tooltip("瞄准左右(Y)动画参数平滑时间 秒")]
        public float AimYAnimBlendSmoothTime = 0.2f;
        
        [Tooltip("瞄准目标IK追踪平滑时间 秒 越小追踪越敏感")]
        public float AimIkChaseSmoothTime = 0.1f;

        [Header("动画资源 (Animation Assets) - 瞄准状态下的动画混合树")]

        [Tooltip("瞄准 Walk 状态的2D混合树 参数 (x,y) 统一映射到半径为1的圆内坐标")]
        public MixerTransition2D AimWalkMixer;

        [Tooltip("瞄准 Jog 状态的2D混合树 参数 (x,y) 统一映射到半径为1的圆内坐标")]
        public MixerTransition2D AimJogMixer;

        [Tooltip("瞄准 Sprint 状态的2D混合树 参数 (x,y) 统一映射到半径为1的圆内坐标")]
        public MixerTransition2D AimSprintMixer;
    }
}