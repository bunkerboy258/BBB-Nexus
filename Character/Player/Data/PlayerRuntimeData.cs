using Items.Data;
using Items.Logic;
using UnityEngine;

namespace Characters.Player.Data
{
    /// <summary>
    /// 装备快照（“当前身上装备了什么”的运行时结果）。
    /// 
    /// 注意：
    /// - Definition 表示“装备的定义资源”；
    /// - Instance/DeviceLogic 是场景中实例化出来的对象（可为空）。
    /// </summary>
    public class EquipmentSnapshot
    {
        /// <summary>当前装备的定义（ScriptableObject）。为 null 表示空手。</summary>
        public ItemDefinitionSO Definition;

        /// <summary>当前装备在场景中的实例（挂在玩家 WeaponContainer 下）。</summary>
        public InteractableItem Instance;

        /// <summary>装置逻辑控制器（例如枪械开火/换弹）。没有装置则为 null。</summary>
        public DeviceController DeviceLogic;

        /// <summary>是否有物品实例（Instance != null）。</summary>
        public bool HasItem => Instance != null;

        /// <summary>是否有可用设备逻辑（DeviceLogic != null）。</summary>
        public bool HasDevice => DeviceLogic != null;
    }

    /// <summary>
    /// 玩家运行时数据容器（纯数据，无业务逻辑）。
    /// 
    /// 约定（行业常用）：
    /// - Input 区域：由 InputReader 写入；由 MotionDriver/Processors 消费。
    /// - State 区域：由 MotionDriver / Controllers 写入；状态机与动画层读取。
    /// - Intent 区域：由 *IntentProcessor 写入；状态机读取；每帧末 Reset。
    /// - References：启动时注入，运行期间只读。
    /// </summary>
    public class PlayerRuntimeData
    {
        #region View / Input (视角与输入)

        /// <summary>
        /// 视角水平角（度）。
        /// 用途：
        /// - FreeLook：通常代表“相机”的 yaw 参考；
        /// - Aiming：通常会被同步为角色 yaw，保证模式切换不跳变。
        /// </summary>
        public float ViewYaw;

        /// <summary>
        /// 视角俯仰角（度）。
        /// 用途：驱动 CameraRoot pitch 或用于 IK/瞄准仰角等。
        /// </summary>
        public float ViewPitch;

        /// <summary>
        /// 视角输入（鼠标/右摇杆 delta）。
        /// 约定：
        /// - FreeLook 模式下通常由 Cinemachine 消费（本项目 MotionDriver 不消费）；
        /// - Aiming 模式下由 MotionDriver 消费并清零。
        /// </summary>
        public Vector2 LookInput;

        /// <summary>
        /// 移动输入（-1~1）。
        /// X=左右，Y=前后。
        /// </summary>
        public Vector2 MoveInput;

        #endregion

        #region Authority Orientation (权威方向源)

        /// <summary>
        /// 权威水平角（度）。
        /// 约定：所有“以相机为参考系”的逻辑（移动、对齐、相机 root）都应优先使用该值，而不是直接读 CameraTransform。
        /// </summary>
        public float AuthorityYaw;

        /// <summary>
        /// 权威俯仰角（度）。
        /// 用于驱动 CameraRoot pitch 或 IK/瞄准仰角等。
        /// </summary>
        public float AuthorityPitch;

        /// <summary>
        /// 权威旋转（世界空间）。等价于 Quaternion.Euler(AuthorityPitch, AuthorityYaw, 0)。
        /// </summary>
        public Quaternion AuthorityRotation;

        #endregion

        #region Character State (角色状态)

        /// <summary>
        /// 角色当前世界 yaw（度）。
        /// 这是“角色朝向”的权威结果，供动画/相机/对齐逻辑使用。
        /// </summary>
        public float CurrentYaw;

        /// <summary>是否处于瞄准模式（Strafe）。由 AimIntentProcessor 控制。</summary>
        public bool IsAiming;

        /// <summary>是否处于奔跑状态。由 StaminaController/意图系统维护。</summary>
        public bool IsRunning;

        /// <summary>是否接地（CharacterController.isGrounded 的缓存）。由 MotionDriver 写入。</summary>
        public bool IsGrounded;

        /// <summary>
        /// 角色垂直速度（m/s）。
        /// 用途：重力、跳跃、贴地力。
        /// </summary>
        public float VerticalVelocity;

        /// <summary>
        /// SmoothDampAngle 的内部速度缓存。
        /// 重要：模式切换时应清零，避免过冲。
        /// </summary>
        public float RotationVelocity;

        #endregion

        #region Intent (意图标记：每帧生成，每帧清理)

        /// <summary>
        /// 本帧是否“想跑”（输入意图）。
        /// 注意：IsRunning 通常是系统结果；WantToRun 是输入意图。
        /// </summary>
        public bool WantToRun;

        /// <summary>本帧是否请求跳跃。</summary>
        public bool WantsToJump;

        /// <summary>本帧是否请求翻越。</summary>
        public bool WantsToVault;

        /// <summary>是否正在翻越中（持续状态）。</summary>
        public bool IsVaulting;

        #endregion

        #region Animation (动画驱动用参数)

        /// <summary>
        /// 动画混合 X（通常对应“方向/转角”）。由 MovementParameterProcessor 写入。
        /// </summary>
        public float CurrentAnimBlendX;

        /// <summary>
        /// 动画混合 Y（通常对应“速度/走跑权重”）。由 MovementParameterProcessor 写入。
        /// </summary>
        public float CurrentAnimBlendY;

        /// <summary>
        /// 跑步循环相位（0~1）。
        /// 用途：左右脚相位匹配（例如 Stop/Start 选择、Loop_L/Loop_R 选择）。
        /// </summary>
        public float CurrentRunCycleTime;

        /// <summary>
        /// 期望下一步脚相位（用于起步/落地/循环混合器选择）。
        /// </summary>
        public FootPhase ExpectedFootPhase;

        #endregion

        #region Equipment (装备系统)

        /// <summary>
        /// 装备意图：玩家希望切换到的装备定义。
        /// - 为 null 表示“卸载/空手”；
        /// - 不等同于 CurrentEquipment.Definition（后者是实际已装备结果）。
        /// </summary>
        public ItemDefinitionSO DesiredItemDefinition;

        /// <summary>
        /// 当前实际装备快照（由 EquipmentDriver 写入）。
        /// </summary>
        public EquipmentSnapshot CurrentEquipment = new EquipmentSnapshot();

        #endregion

        #region IK (反向运动学：意图 + 目标)

        /// <summary>是否开启左手 IK（通常用于枪械护木握持）。</summary>
        public bool WantsLeftHandIK;

        /// <summary>是否开启右手 IK（通常用于武器握把/扳机手）。</summary>
        public bool WantsRightHandIK;

        /// <summary>是否开启注视 IK（头/眼睛看向目标）。</summary>
        public bool WantsLookAtIK;

        /// <summary>左手 IK 目标点。</summary>
        public Transform LeftHandGoal;

        /// <summary>右手 IK 目标点。</summary>
        public Transform RightHandGoal;

        /// <summary>注视目标点（世界坐标）。</summary>
        public Vector3 LookAtPosition;

        #endregion

        #region References (引用：启动时注入)

        /// <summary>
        /// 主摄像机 Transform 缓存。
        /// 用途：Camera-Relative 移动计算、IK/射线等。
        /// </summary>
        public Transform CameraTransform;

        /// <summary>
        /// 耐力当前值。
        /// 用途：冲刺消耗/恢复。
        /// </summary>
        public float CurrentStamina;

        #endregion

        #region Movement Derived (移动派生数据：统一输出)

        /// <summary>
        /// 期望的世界空间移动方向（已融合 MoveInput + AuthorityYaw）。
        /// 约定：由 MovementParameterProcessor（或专用 MovementDirectionProcessor）每帧更新；
        /// 状态机与动画层只读，避免重复计算导致不一致。
        /// </summary>
        public Vector3 DesiredWorldMoveDir;

        /// <summary>
        /// 期望的本地移动角（度，-180~180）。
        /// 定义：将 DesiredWorldMoveDir 转换到 Player 本地空间后得到的朝向角。
        /// 用途：起步动画方向选择、2D Mixer 方向参数等。
        /// </summary>
        public float DesiredLocalMoveAngle;

        #endregion

        public PlayerRuntimeData()
        {
            IsRunning = false;
        }

        /// <summary>
        /// 每帧末清理“一次性意图”。
        /// 为什么：意图应由 Processor 每帧重新生成，避免状态残留导致误触发。
        /// </summary>
        public void ResetIntetnt()
        {
            WantsToVault = false;
            WantToRun = false;
            WantsToJump = false;
        }
    }
}