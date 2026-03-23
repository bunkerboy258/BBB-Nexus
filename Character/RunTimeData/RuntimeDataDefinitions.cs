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

    public struct OverrideContext
    {
        public bool IsActive;
        public ActionRequest Request;
        public BaseState ReturnState;

        public void Clear()
        {
            IsActive = false;
            Request = default;
            ReturnState = null;
        }
    }

    public struct ArbitrationFlags
    {
        public bool BlockInput;
        public bool BlockUpperBody;
        public bool BlockFacial;
        public bool BlockIK;
        public bool BlockInventory;
        public bool BlockAudio;
        public bool IsDead;
        public bool BlockAction;

        public void Clear()
        {
            BlockInput = false;
            BlockUpperBody = false;
            BlockFacial = false;
            BlockIK = false;
            BlockInventory = false;
            BlockAudio = false;
            IsDead = false;
            BlockAction = false;
        }
    }

    /// <summary>
    /// 动作仲裁请求（帧级）：由各系统写入 ActionArbiter 只读取并应用 
    /// 规则：同一帧内仅保留 Priority 最高的请求 
    /// </summary>
    public struct ActionArbitrationContext
    {
        public bool HasRequest;
        public ActionRequest HighestPriorityRequest;

        public void Clear()
        {
            HasRequest = false;
            HighestPriorityRequest = default;
        }

        public void Submit(in ActionRequest request)
        {
            if (!HasRequest || request.Priority > HighestPriorityRequest.Priority)
            {
                HighestPriorityRequest = request;
                HasRequest = true;
            }
        }
    }

    /// <summary>
    /// 帧级音频事件队列
    /// 用法：RuntimeData.SfxQueue.Enqueue(PlayerSfxEvent.X)
    /// </summary>
    public struct PlayerSfxEventQueue
    {
        private const int Capacity = 16;

        private PlayerSfxEvent _e0;
        private PlayerSfxEvent _e1;
        private PlayerSfxEvent _e2;
        private PlayerSfxEvent _e3;
        private PlayerSfxEvent _e4;
        private PlayerSfxEvent _e5;
        private PlayerSfxEvent _e6;
        private PlayerSfxEvent _e7;
        private PlayerSfxEvent _e8;
        private PlayerSfxEvent _e9;
        private PlayerSfxEvent _e10;
        private PlayerSfxEvent _e11;
        private PlayerSfxEvent _e12;
        private PlayerSfxEvent _e13;
        private PlayerSfxEvent _e14;
        private PlayerSfxEvent _e15;

        private int _count;

        public int Count => _count;

        public void Clear() => _count = 0;

        public void Enqueue(PlayerSfxEvent evt)
        {
            if (_count >= Capacity) return;

            switch (_count)
            {
                case 0: _e0 = evt; break;
                case 1: _e1 = evt; break;
                case 2: _e2 = evt; break;
                case 3: _e3 = evt; break;
                case 4: _e4 = evt; break;
                case 5: _e5 = evt; break;
                case 6: _e6 = evt; break;
                case 7: _e7 = evt; break;
                case 8: _e8 = evt; break;
                case 9: _e9 = evt; break;
                case 10: _e10 = evt; break;
                case 11: _e11 = evt; break;
                case 12: _e12 = evt; break;
                case 13: _e13 = evt; break;
                case 14: _e14 = evt; break;
                case 15: _e15 = evt; break;
            }

            _count++;
        }

        public PlayerSfxEvent Get(int index)
        {
            return index switch
            {
                0 => _e0,
                1 => _e1,
                2 => _e2,
                3 => _e3,
                4 => _e4,
                5 => _e5,
                6 => _e6,
                7 => _e7,
                8 => _e8,
                9 => _e9,
                10 => _e10,
                11 => _e11,
                12 => _e12,
                13 => _e13,
                14 => _e14,
                15 => _e15,
                _ => default,
            };
        }
    }
    #region Movement & Direction Enums

    /// <summary>
    /// 离散化的角色意图方向（8方向） 
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
        Idle = 0, 
        Walk = 1, 
        Jog = 2,
        Sprint = 3,
    }

    /// <summary>
    /// 二段跳的方向分类 决定空中第二次起跳的轨迹(已经废弃二段跳多态 向后兼容)
    /// </summary>
    public enum DoubleJumpDirection
    {
        Up = 0,  
        Left = 1,
        Right = 2,
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

        [Tooltip("墙的高度  用于选择低翻越还是高翻越")]
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
