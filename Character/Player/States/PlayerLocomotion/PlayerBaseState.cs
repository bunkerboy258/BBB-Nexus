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
        /// 计算世界坐标系下的移动方向（基于相机）
        /// </summary>
        protected UnityEngine.Vector3 CalculateWorldMoveDir()
        {
            // 1. 输入角度 (相对于屏幕/相机)
            float inputAngle = UnityEngine.Mathf.Atan2(data.MoveInput.x, data.MoveInput.y) * UnityEngine.Mathf.Rad2Deg;

            // 2. 叠加相机角度
            if (data.CameraTransform != null)
            {
                inputAngle += data.CameraTransform.eulerAngles.y;
            }

            // 3. 转为向量
            return UnityEngine.Quaternion.Euler(0f, inputAngle, 0f) * UnityEngine.Vector3.forward;
        }

        // 重载版本
        protected UnityEngine.Vector3 CalculateWorldMoveDir(Vector2 inputVector)
        {
            float inputAngle = UnityEngine.Mathf.Atan2(inputVector.x, inputVector.y) * UnityEngine.Mathf.Rad2Deg;
            if (data.CameraTransform != null) inputAngle += data.CameraTransform.eulerAngles.y;
            return UnityEngine.Quaternion.Euler(0f, inputAngle, 0f) * UnityEngine.Vector3.forward;
        }

        /// <summary>
        /// 计算相对于角色当前朝向的本地角度 (-180 ~ 180)
        /// </summary>
        protected float CalculateLocalAngle()
        {
            if (!HasMoveInput) return 0f;

            // 1. 获取目标世界方向
            Vector3 worldMoveDir = CalculateWorldMoveDir();

            // 2. 转为本地空间 (相对于 Player.transform)
            Vector3 localDir = player.transform.InverseTransformDirection(worldMoveDir);

            // 3. 计算角度
            return Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
        }

        // 重载版本
        protected float CalculateLocalAngle(Vector2 inputVector)
        {
            if (inputVector.sqrMagnitude < 0.001f) return 0f;
            Vector3 worldMoveDir = CalculateWorldMoveDir(inputVector);
            Vector3 localDir = player.transform.InverseTransformDirection(worldMoveDir);
            return Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
        }
    }
}