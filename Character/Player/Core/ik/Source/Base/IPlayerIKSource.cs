using UnityEngine;

// [原样保留]
public enum IKTarget
{
    LeftHand,
    RightHand,
    LeftFoot,
    RightFoot,
    HeadLook,
    AimReference
}

namespace Characters.Player.Core
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
        /// 仅更新指定 IK 目标的权重。
        /// </summary>
        void UpdateIKWeight(IKTarget target, float weight);
    }

}

