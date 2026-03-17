using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BBBNexus
{
    public enum CharacterLOD
    {
        High,
        Medium,
        Low 
    }
    #region Movement & Direction Enums

    /// <summary>
    /// 离散化的角色意图方向（8方向）。
    /// 这是将连续的摇杆输入量化成8个离散方向 用于选择对应的启动动画与根运动方向
    /// </summary>
    public enum DesiredDirection
    {
        None,
        Forward,
        Backward,
        Left,
        Right,
        ForwardLeft,
        ForwardRight,
        BackwardLeft,
        BackwardRight
    }

    /// <summary>
    /// 下半身的运动状态分类 控制动画混合树的输入源
    /// </summary>
    public enum LocomotionState
    {
        Idle = 0,   // 静止待机
        Walk = 1,   // 行走 低速
        Jog = 2,    // 慢跑 中速
        Sprint = 3, // 冲刺 高速
    }

    /// <summary>
    /// 二段跳的方向分类 决定空中第二次起跳的轨迹
    /// </summary>
    public enum DoubleJumpDirection
    {
        Up = 0,   // 竖直向上跳
        Left = 1, // 向左跳
        Right = 2, // 向右跳
    }

    #endregion

    #region Obstacle & Vaulting Data

    /// <summary>
    /// 翻越障碍物的信息结构 存储从检测射线得到的所有IK与动画驱动数据
    /// </summary>
    public struct VaultObstacleInfo
    {
        [Tooltip("此次翻越数据是否有效 只有所有检测都通过才能设为true")]
        public bool IsValid;

        [Tooltip("墙面的击中点 世界坐标 用于判断手部IK目标")]
        public Vector3 WallPoint;

        [Tooltip("墙面法线方向 用于计算IK手部的朝向")]
        public Vector3 WallNormal;

        [Tooltip("墙的高度 米 用于选择低翻越还是高翻越")]
        public float Height;

        [Tooltip("墙顶的着陆点 世界坐标 角色翻过去后会落在这个位置")]
        public Vector3 LedgePoint;

        [Tooltip("左手IK目标点 世界坐标 动画播放时会持续驱动左手向这里靠近")]
        public Vector3 LeftHandPos;

        [Tooltip("右手IK目标点 世界坐标")]
        public Vector3 RightHandPos;

        [Tooltip("手部IK的朝向 确保两只手指向同一个方向")]
        public Quaternion HandRot;

        [Tooltip("翻越后的预期着陆点 用于最终的根运动变形修正")]
        public Vector3 ExpectedLandPoint;
    }

    #endregion
}
