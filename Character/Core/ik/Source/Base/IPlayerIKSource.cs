using UnityEngine;
public enum IKTarget
{
    LeftHand,
    RightHand,
    LeftFoot,
    RightFoot,
    HeadLook,
    AimReference
}

namespace BBBNexus
{
    public interface IPlayerIKSource
    {
        /// <summary>
        /// 设置一个 IK 目标的 Transform。
        /// </summary>
        /// <param name="target">身体部位</param>
        /// <param name="targetTransform">目标 Transform</param>
        /// <param name="weight">IK 权重 (0-1)</param>
        void SetIKTarget(IKTarget target, Transform targetTransform, float weight);

        /// <summary>
        /// 设置一个 IK 目标的世界坐标(通过vector3设置)
        /// </summary>
        /// <param name="target">身体部位</param>
        /// <param name="position">世界坐标</param>
        /// <param name="rotation">世界旋转</param>
        /// <param name="weight">IK 权重 (0-1)</param>
        void SetIKTarget(IKTarget target, Vector3 position, Quaternion rotation, float weight);

        /// <summary>
        /// 仅更新指定 IK 目标的权重 
        /// </summary>
        void UpdateIKWeight(IKTarget target, float weight);

        /// <summary>
        /// 一键开启所有底层 IK 组件 用于从低 LOD 恢复到高 LOD 时唤醒解算器 
        /// </summary>
        void EnableAllIK();

        /// <summary>
        /// 一键关闭所有底层 IK 组件 用于在低 LOD 下彻底瘫痪解算器以节省 CPU 开销 
        /// </summary>
        void DisableAllIK();
    }

}

