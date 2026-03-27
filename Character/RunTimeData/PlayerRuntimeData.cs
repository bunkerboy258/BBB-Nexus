using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 玩家运行时数据：用于在输入、物理、动画与 IK 之间共享的帧级黑板
    /// 仅承载状态与意图 不包含行为逻辑
    /// 
    /// 关键外部类型：
    /// - OverrideContext: 覆盖上下文，储存强制状态请求
    /// - ArbitrationFlags: 仲裁标志，控制各系统是否被阻断
    /// - ActionArbitrationContext: 动作仲裁上下文，管理高优先级动作请求
    /// - PlayerSfxEventQueue: 音效事件队列，存储帧级音频意图
    /// - WarpedMotionData: 变形运动数据，根运动曲线与 IK 参数
    /// - VaultObstacleInfo: 翻越障碍信息，IK 目标与物理参数
    /// - ItemInstance: 物品实例，动态装备状态
    /// - AnimPlayOptions: 动画播放选项，动画过渡参数
    /// - LocomotionState: 下半身运动状态枚举
    /// </summary>
    public class PlayerRuntimeData
    {
        public OverrideContext Override;
        public ArbitrationFlags Arbitration;
        public ActionArbitrationContext ActionArbitration;

        public PlayerRuntimeData(BBBCharacterController player)
        {
            CurrentHealth = player.Config.Core.MaxHealth;
            CameraTransform = player.PlayerCamera;
            CurrentStamina = player.Config.Core.MaxStamina;
            Override.Clear();
            Arbitration.Clear();
            ActionArbitration.Clear();

            SfxQueue = new PlayerSfxEventQueue();
        }

        #region 核心生存状态
        /// <summary>当前血量</summary>
        public float CurrentHealth;
        /// <summary>是否已死亡</summary>
        public bool IsDead;
        #endregion

        #region 输入状态
        /// <summary>相机视角输入 (X=水平, Y=竖直, -1~1)</summary>
        public Vector2 LookInput;
        /// <summary>移动摇杆输入 (X=前后, Y=左右, -1~1)</summary>
        public Vector2 MoveInput;
        #endregion

        #region 视角与旋转
        /// <summary>视角水平角(度)</summary>
        public float ViewYaw;
        /// <summary>视角俯仰角(度)</summary>
        public float ViewPitch;
        /// <summary>权威朝向水平角 供IK/上身使用</summary>
        public float AuthorityYaw;
        /// <summary>权威朝向俯仰角 供瞄准使用</summary>
        public float AuthorityPitch;
        /// <summary>权威旋转(四元数) 代表相机期望朝向</summary>
        public Quaternion AuthorityRotation;
        /// <summary>角色当前朝向水平角 平滑跟随</summary>
        public float CurrentYaw;
        /// <summary>旋转速度 用于平滑转向</summary>
        public float RotationVelocity;
        #endregion

        #region 物理与地面状态
        /// <summary>是否接地</summary>
        public bool IsGrounded;
        /// <summary>是否处于闪避中</summary>
        public bool IsDodgeing;
        /// <summary>竖直速度(m/s)</summary>
        public float VerticalVelocity;
        /// <summary>刚着陆的瞬间(仅一帧)</summary>
        public bool JustLanded;
        /// <summary>刚离地的瞬间(仅一帧)</summary>
        public bool JustLeftGround;
        #endregion

        #region 下半身运动状态
        /// <summary>是否瞄准 影响上身/动画树</summary>
        public bool IsAiming;
        /// <summary>上一帧运动状态</summary>
        public LocomotionState LastLocomotionState = LocomotionState.Idle;
        /// <summary>当前运动状态 用于动画混合</summary>
        public LocomotionState CurrentLocomotionState = LocomotionState.Idle;
        /// <summary>期望移动方向(世界空间单位向量)</summary>
        public Vector3 DesiredWorldMoveDir;
        /// <summary>期望移动角度相对于身体(度)</summary>
        public float DesiredLocalMoveAngle;
        /// <summary>当前水平速度(m/s)</summary>
        public float CurrentSpeed;
        #endregion

        #region 装备与指向基准
        /// <summary>快捷栏装备意图 -1无意图 >=0对应槽位</summary>
        public int WantsToEquipHotbarIndex = -1;
        /// <summary>当前装备物品 null为空手</summary>
        public ItemInstance CurrentItem;
        /// <summary>指向基准Transform </summary>
        public Transform CurrentAimReference;
        #endregion

        #region 帧级意图标志 (每帧清理)
        /// <summary>瞄准目标点(世界坐标)</summary>
        public Vector3 TargetAimPoint;
        /// <summary>相机朝向向量 用于上身/动画</summary>
        public Vector3 CameraLookDirection;
        
        /// <summary>本帧是否想跑</summary>
        public bool WantToRun;
        /// <summary>本帧是否想闪避</summary>
        public bool WantsToDodge;
        /// <summary>本帧是否想翻滚</summary>
        public bool WantsToRoll;
        /// <summary>本帧是否想跳跃</summary>
        public bool WantsToJump;
        /// <summary>本帧是否想二段跳</summary>
        public bool WantsDoubleJump;
        /// <summary>二段跳方向</summary>
        public DoubleJumpDirection DoubleJumpDirection = DoubleJumpDirection.Up;
        
        /// <summary>本帧是否想翻越</summary>
        public bool WantsToVault;
        /// <summary>是否低翻越</summary>
        public bool WantsLowVault;
        /// <summary>是否高翻越</summary>
        public bool WantsHighVault;
        /// <summary>有效的翻越障碍物信息</summary>
        public VaultObstacleInfo CurrentVaultInfo;
        /// <summary>量化的移动方向(8向)</summary>
        public DesiredDirection QuantizedDirection;
        
        /// <summary>本帧是否进入下落状态</summary>
        public bool WantsToFall;
        /// <summary>本帧是否想开火</summary>
        public bool WantsToFire;
        
        /// <summary>表情1意图</summary>
        public bool WantsExpression1;
        /// <summary>表情2意图</summary>
        public bool WantsExpression2;
        /// <summary>表情3意图</summary>
        public bool WantsExpression3;
        /// <summary>表情4意图</summary>
        public bool WantsExpression4;
        /// <summary>是否想执行动作(Action)</summary>
        public bool WantsToAction;
        #endregion

        #region 根运动变形与翻越
        /// <summary>是否处于根运动变形中</summary>
        public bool IsWarping;
        /// <summary>是否处于翻越状态</summary>
        public bool IsVaulting;
        /// <summary>当前激活的变形数据</summary>
        public WarpedMotionData ActiveWarpData;
        /// <summary>变形的归一化时间(0~1)</summary>
        public float NormalizedWarpTime;
        /// <summary>变形期间左手IK目标(世界坐标)</summary>
        public Vector3 WarpIKTarget_LeftHand;
        /// <summary>变形期间右手IK目标(世界坐标)</summary>
        public Vector3 WarpIKTarget_RightHand;
        /// <summary>变形期间手部IK朝向(四元数)</summary>
        public Quaternion WarpIKRotation_Hand;
        #endregion

        #region 动画混合参数
        /// <summary>前后混合(-1后退, 1前进)</summary>
        public float CurrentAnimBlendX;
        /// <summary>左右混合(-1左, 1右)</summary>
        public float CurrentAnimBlendY;
        /// <summary>跑步循环时间 用于脚步判定</summary>
        public float CurrentRunCycleTime;
        /// <summary>预期脚相 用于选择动画过渡</summary>
        public FootPhase ExpectedFootPhase;
        /// <summary>下落高度等级 用于选取落地表现</summary>
        public int FallHeightLevel;
        #endregion

        #region 动画播放选项覆盖 (下一次生效)
        /// <summary>下半身播放选项覆写 null使用默认</summary>
        public AnimPlayOptions? NextStatePlayOptions = null;
        /// <summary>上半身播放选项覆写 null使用默认</summary>
        public AnimPlayOptions? NextUpperBodyStatePlayOptions = null;
        #endregion

        #region IK驱动目标
        /// <summary>启用左手IK</summary>
        public bool WantsLeftHandIK;
        /// <summary>启用右手IK</summary>
        public bool WantsRightHandIK;
        /// <summary>启用头部LookAt IK</summary>
        public bool WantsLookAtIK;
        /// <summary>左手目标Transform</summary>
        public Transform LeftHandGoal;
        /// <summary>右手目标Transform</summary>
        public Transform RightHandGoal;
        /// <summary>头部注视点(世界坐标)</summary>
        public Vector3 LookAtPosition;
        #endregion

        #region 体力与追踪状态
        /// <summary>当前体力值</summary>
        public float CurrentStamina;
        /// <summary>体力枯竭标志</summary>
        public bool IsStaminaDepleted;
        /// <summary>本次空中是否已使用二段跳</summary>
        public bool HasPerformedDoubleJumpInAir;
        /// <summary>玩家相机Transform 用于计算视角与朝向</summary>
        public Transform CameraTransform;
        #endregion

        #region 音效队列 (帧级)
        /// <summary>本帧待播放的角色音效事件队列</summary>
        public PlayerSfxEventQueue SfxQueue;
        #endregion

        #region 表情意图（帧级，最后一个覆盖）

        /// <summary>
        /// 本帧请求播放的表情事件（仅状态机写入）。
        /// 同帧多次写入：最后一个覆盖。
        /// </summary>
        public PlayerFacialEvent FacialEventRequest;

        #endregion

        #region 方法

        public PlayerRuntimeData()
        {
            CurrentLocomotionState = LocomotionState.Idle;
        }

        /// <summary>
        /// 清除所有帧级意图标志
        /// </summary>
        public void ResetIntetnt()
        {
            WantsToVault = false;
            WantToRun = false;
            WantsToJump = false;
            WantsDoubleJump = false;
            WantsToDodge = false;
            WantsToRoll = false;
            WantsLowVault = false;
            WantsHighVault = false;
            WantsToFire = false;
            WantsToAction = false;

            WantsExpression1 = false;
            WantsExpression2 = false;
            WantsExpression3 = false;
            WantsExpression4 = false;

            // 表情事件：在 LateUpdate 清理（FacialController 在 Update 消费）
            FacialEventRequest = PlayerFacialEvent.None;

            // 每帧清理帧仲裁请求
            ActionArbitration.Clear();

            // 注意 音频事件不在这里清理
            // AudioController 在 Update 消费（而 ResetIntetnt 在 LateUpdate）
            // 若在此清理，会导致本帧刚写入的音频事件在消费前丢失
        }

        #endregion
    }
}
