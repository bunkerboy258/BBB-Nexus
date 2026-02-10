using UnityEngine;

namespace Characters.Player.Core.IK
{
    /// <summary>
    /// IK 系统的抽象接口。
    /// 无论底层是 Unity 原生 IK 还是 Final IK，都要实现这个接口。
    /// </summary>
    public interface IPlayerIKSource
    {
        /// <summary>
        /// 每帧更新 (通常在 LateUpdate 或 OnAnimatorIK 中调用)
        /// </summary>
        void OnUpdateIK(int layerIndex);

        /// <summary>
        /// 设置左手 IK 目标和权重
        /// </summary>
        /// <param name="target">抓取点 Transform (如果是 null 则不生效)</param>
        /// <param name="weight">权重 (0-1)</param>
        void SetLeftHandIK(Transform target, float weight);

        /// <summary>
        /// 设置右手 IK 目标和权重 (通常右手不需要 IK 因为它绑定在枪上，但为了大炮可能需要)
        /// </summary>
        void SetRightHandIK(Transform target, float weight);

        /// <summary>
        /// 设置全身/脊柱的瞄准目标 (Look At)
        /// </summary>
        void SetLookAtTarget(Vector3 targetPosition, float weight);
    }
}
