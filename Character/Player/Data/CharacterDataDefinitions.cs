using UnityEngine;
using Animancer;
using System.Collections.Generic;

namespace Characters.Player.Data
{
    #region Enums (枚举定义)

    /// <summary>
    /// Foot phase (left or right foot down) - 脚相位（左脚或右脚着地）
    /// </summary>
    public enum FootPhase { LeftFootDown, RightFootDown }

    /// <summary>
    /// Motion type for animation driving - 动画驱动类型
    /// </summary>
    [System.Serializable]
    public enum MotionType
    {
        InputDriven,   // Driven by input vector (loop) - 由输入向量控制（循环）
        CurveDriven,   // Driven by baked curve (start, stop, etc) - 由烘焙曲线控制（启动、停止等）
        Mixed          // Curve to input switch - 曲线到输入切换
    }

    /// <summary>
    /// Warped Motion Type for auto-baking feature points - 扭曲动作类型，用于自动烘焙特征点
    /// </summary>
    public enum WarpedType
    {
        None,           // Manual mode, respects user-defined points - 手动模式，使用用户配置的点
        Vault,          // Auto-detects Y-axis peak (apex) - 自动探测Y轴极大值（顶点）
        Dodge,          // Auto-detects XZ-plane max distance - 自动探测XZ平面最大位移点
        Simple          // Auto-generates a single end point at 1.0 - 仅生成1.0的终点
    }

    #endregion

    #region Serializable Data Wrappers (可序列化数据容器)

    /// <summary>
    /// Animation clip data for standard, curve-driven motion (e.g., Starts, Stops).
    /// 适用于标准、曲线驱动的运动动画片段数据（如起步、停止）。
    /// </summary>
    [System.Serializable]
    public class MotionClipData
    {
        [Header("Animation Source - 动画资源")]
        public ClipTransition Clip;
        public MotionType Type = MotionType.CurveDriven;

        [Header("Playback Settings - 播放设置")]
        public float TargetDuration = 0f;
        public float EndTime = 0f;

        [Header("!Abandoned! 局部方向矫正（弃用，改为WarpedMotionData的局部速度曲线）")]
        public bool AllowBakeTargetLocalDirection;
        public Vector3 TargetLocalDirection;

        [Header("Baked Runtime Data - 烘焙运行时数据")]
        public FootPhase EndPhase = FootPhase.LeftFootDown;
        public float PlaybackSpeed = 1f;
        public AnimationCurve SpeedCurve;
        public AnimationCurve RotationCurve;
        public float RotationFinishedTime = 0f;

        public MotionClipData()
        {
            SpeedCurve = new AnimationCurve();
            RotationCurve = new AnimationCurve();
        }
    }

    /// <summary>
    /// Defines a special moment (warp point) in an animation for spatial alignment.
    /// 定义动画中用于空间对齐的特征时刻（Warp点）。
    /// </summary>
    [System.Serializable]
    public class WarpPointDef
    {
        [Tooltip("Feature point name for identification - 特征点识别名称")]
        public string PointName;

        [Tooltip("Normalized time to trigger this point (0-1) - 触发该特征点的动画归一化时间 (0-1)")]
        [Range(0f, 1f)]
        public float NormalizedTime;

        [Tooltip("Local offset applied to the runtime target at this point - 在此时刻，对运行时目标点施加的局部坐标偏移")]
        public Vector3 TargetPositionOffset;

        [Header("Baking Results - 烘焙结果")]
        [Tooltip("[ReadOnly] Baked local offset from previous point to this moment - 从上个点到此时刻的烘焙局部位移")]
        public Vector3 BakedLocalOffset;

        [Tooltip("[ReadOnly] Baked local rotation at this moment - 此时刻的烘焙局部旋转")]
        public Quaternion BakedLocalRotation = Quaternion.identity;
    }

    /// <summary>
    /// Animation data for advanced motion warping (e.g., Vault, Dodge).
    /// 用于高级空间扭曲（如翻越、闪避）的动画数据。
    /// </summary>
    [System.Serializable]
    public class WarpedMotionData
    {
        [Header("Animation Source - 动画资源")]
        public ClipTransition Clip;

        [Header("Timing Control - 时序控制")]
        public float EndTime = 0f;
        public FootPhase EndPhase = FootPhase.LeftFootDown;

        [Header("Baking & Warping Config - 烘焙与扭曲配置")]
        [Tooltip("Defines how the baker should auto-detect feature points - 定义烘焙器如何自动探测特征点")]
        public WarpedType Type = WarpedType.None;

        [Tooltip("Warp points in time order - 空间对齐特征点序列，需按时间升序排列")]
        public List<WarpPointDef> WarpPoints = new List<WarpPointDef>();

        [Tooltip("IK weight curve during this motion - 动作期间手部 IK 的权重曲线")]
        public AnimationCurve HandIKWeightCurve = new AnimationCurve();

        [Header("Baked Curves (ReadOnly) - 烘焙曲线（只读）")]
        public float BakedDuration;
        public AnimationCurve LocalVelocityX = new AnimationCurve();
        public AnimationCurve LocalVelocityY = new AnimationCurve();
        public AnimationCurve LocalVelocityZ = new AnimationCurve();
        public AnimationCurve LocalRotationY = new AnimationCurve();

        [HideInInspector]
        public Vector3 TotalBakedLocalOffset;
    }

    #endregion
}
