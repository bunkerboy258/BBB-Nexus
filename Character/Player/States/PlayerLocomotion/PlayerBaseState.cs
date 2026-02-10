using Core.StateMachine;
using Characters.Player.Data;
using UnityEngine;

namespace Characters.Player.States
{
    /// <summary>
    /// 玩家基础状态抽象类
    /// 作用：所有玩家状态（空闲、移动、转身等）的基类，封装通用的字段、属性和辅助方法，避免重复代码。
    /// 核心封装：
    /// 1. 玩家控制器、运行时数据、配置文件的通用引用；
    /// 2. 移动输入判定、世界/本地移动方向/角度计算的通用方法。
    /// </summary>
    public abstract class PlayerBaseState : BaseState
    {
        /// <summary>玩家核心控制器引用（访问动画、运动驱动、状态机等核心模块）</summary>
        protected PlayerController player;
        /// <summary>玩家运行时数据（读写输入、状态标记、动画参数等动态数据）</summary>
        protected PlayerRuntimeData data;
        /// <summary>玩家配置文件（读取动画、数值、阈值等静态配置）</summary>
        protected PlayerSO config;

        /// <summary>
        /// 构造函数：初始化通用引用（玩家控制器、运行时数据、配置文件）
        /// </summary>
        /// <param name="player">玩家核心控制器实例</param>
        protected PlayerBaseState(PlayerController player)
        {
            this.player = player;
            this.data = player.RuntimeData;
            this.config = player.Config;
        }

        /// <summary>
        /// 检测是否有有效移动输入（避免浮点精度问题）
        /// sqrMagnitude > 0.001f 替代 magnitude > 0：减少浮点误差导致的无效判定
        /// </summary>
        protected bool HasMoveInput => data.MoveInput.sqrMagnitude > 0.001f;

        /// <summary>
        /// 计算世界坐标系下的移动方向（带相机旋转偏移）
        /// 返回值：世界坐标系中，玩家应朝向的移动方向（基于输入+相机视角）
        /// </summary>
        /// <returns>世界坐标系下的移动方向向量（Vector3）</returns>
        protected UnityEngine.Vector3 CalculateWorldMoveDir()
        {
            // 1. 基于输入向量计算基础角度（Atan2(x,y)：根据输入的x/y分量计算弧度角）
            // 数学逻辑：Atan2(input.x, input.y) 以Y轴为前、X轴为右，计算输入方向的弧度角
            // 示例：输入(1,0)（右）→ 90°；输入(0,1)（前）→ 0°；输入(-1,0)（左）→ -90°
            float targetAngle = UnityEngine.Mathf.Atan2(data.MoveInput.x, data.MoveInput.y) * UnityEngine.Mathf.Rad2Deg;

            // 2. 叠加相机的Y轴旋转（让移动方向跟随相机视角，第三人称视角核心逻辑）
            // CameraTransform.eulerAngles.y：相机绕Y轴的旋转角度（视角朝向）
            if (data.CameraTransform != null) targetAngle += data.CameraTransform.eulerAngles.y;

            // 3. 将角度转换为世界坐标系下的方向向量（forward为基础，绕Y轴旋转targetAngle）
            return UnityEngine.Quaternion.Euler(0f, targetAngle, 0f) * UnityEngine.Vector3.forward;
        }

        /// <summary>
        /// 计算玩家本地坐标系下的移动角度（重载1：使用当前MoveInput）
        /// 返回值：相对于玩家自身forward的偏移角度（-180 ~ 180°），用于转身/动画混合判定
        /// </summary>
        /// <returns>本地坐标系下的移动角度（°），无输入时返回0</returns>
        protected float CalculateLocalAngle()
        {
            // 无有效移动输入时，直接返回0（避免无效计算）
            if (!HasMoveInput) return 0f;

            // 1. 获取世界坐标系下的移动方向
            Vector3 worldDir = CalculateWorldMoveDir();

            // 2. 转换为玩家本地坐标系方向（将世界方向转换为“相对于玩家自身”的方向）
            // InverseTransformDirection：世界→本地，以玩家transform的forward为前、right为右
            Vector3 localDir = player.transform.InverseTransformDirection(worldDir);

            // 3. 计算本地方向的偏移角度（Atan2(x,z)：以Z轴为前、X轴为右，计算偏移角）
            // localDir.x：左右偏移；localDir.z：前后偏移 → 结果为相对于玩家forward的角度
            return Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// 计算玩家本地坐标系下的移动角度（重载2：自定义输入向量）
        /// 用途：短按转身等场景，使用“上一帧有效输入”而非当前输入计算角度
        /// </summary>
        /// <param name="inputVector">自定义输入向量（如LastNonZeroMoveInput）</param>
        /// <returns>本地坐标系下的移动角度（°），输入无效时返回0</returns>
        protected float CalculateLocalAngle(Vector2 inputVector)
        {
            // 输入向量长度过小（浮点精度问题），视为无有效输入，返回0
            if (inputVector.sqrMagnitude < 0.001f) return 0f;

            // 1. 基于自定义输入向量计算世界移动方向（逻辑同CalculateWorldMoveDir）
            float targetAngle = Mathf.Atan2(inputVector.x, inputVector.y) * Mathf.Rad2Deg;
            if (data.CameraTransform != null) targetAngle += data.CameraTransform.eulerAngles.y;
            Vector3 worldDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

            // 2. 转换为玩家本地坐标系方向
            Vector3 localDir = player.transform.InverseTransformDirection(worldDir);

            // 3. 计算本地方向的偏移角度
            return Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
        }
    }
}