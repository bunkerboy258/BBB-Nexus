using Items.Data;
using Items.Logic;
using UnityEngine;

namespace Characters.Player.Data
{
    public class EquipmentSnapshot
    {
        public ItemDefinitionSO Definition;
        public InteractableItem Instance;
        public DeviceController DeviceLogic;
        public bool HasItem => Instance != null;
        public bool HasDevice => DeviceLogic != null;
    }

    /// <summary>
    /// 玩家运行时数据容器（纯数据，无业务逻辑）
    /// </summary>
    public class PlayerRuntimeData
    {
        #region Input (输入数据)

        public Vector2 LookInput;               // 鼠标/摇杆视角输入
        public Vector2 MoveInput;               // 移动输入 (-1~1)
        public Vector2 LastNonZeroMoveInput;    // 上一帧有效移动输入（用于方向判定）

        #endregion

        #region Character State (角色状态)

        public float CurrentYaw;                // 角色Y轴朝向（度）
        public bool IsAiming;                   // 是否瞄准中
        public bool IsRunning;                  // 是否奔跑中
        public bool IsGrounded;                 // 是否接地
        public float VerticalVelocity;          // 垂直速度（Y轴）
        public float RotationVelocity;          // 旋转速度（用于平滑）

        #endregion

        #region Intent (意图标记)

        public bool WantToRun;                  // 想要奔跑
        public bool WantsToJump;                // 想要跳跃
        public bool WantsToVault;               // 想要翻越
        public bool IsVaulting;                 // 正在翻越中

        #endregion

        #region Animation (动画参数)

        public float CurrentAnimBlendX;         // 混合树X轴（转向角度）
        public float CurrentAnimBlendY;         // 混合树Y轴（移动速度）
        public float CurrentRunCycleTime;       // 跑步循环归一化时间 (0-1)
        public FootPhase ExpectedFootPhase;     // 期望的下一步相位

        #endregion

        #region Equipment (装备系统)

        public ItemDefinitionSO DesiredItemDefinition;      // 期望装备
        public EquipmentSnapshot CurrentEquipment = new EquipmentSnapshot();  // 当前装备

        #endregion

        #region IK (反向运动学)

        public bool WantsLeftHandIK;            // 左手IK意图
        public bool WantsRightHandIK;           // 右手IK意图
        public bool WantsLookAtIK;              // 注视IK意图
        public Transform LeftHandGoal;          // 左手目标
        public Transform RightHandGoal;         // 右手目标
        public Vector3 LookAtPosition;          // 注视目标位置

        #endregion

        #region References (引用)

        public Transform CameraTransform;       // 主摄像机（用于IK注视等）
        public float CurrentStamina;            // 当前耐力值

        #endregion

        public PlayerRuntimeData()
        {
            IsRunning = false;
        }

        public void ResetIntetnt()
        {
            WantsToVault = false;
            WantToRun = false;
            WantsToJump = false;
        }
    }
}