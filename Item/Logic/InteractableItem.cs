using UnityEngine;

namespace Items.Logic
{
    /// <summary>
    /// 挂载在物品 Prefab 上的组件。
    /// 用于定义该物品在场景中的具体属性（IK点、生成偏移等）。
    /// </summary>
    public class InteractableItem : MonoBehaviour
    {
        [Header("IK 握持锚点")]
        [Tooltip("左手 IK 目标 (护木/握把)")]
        public Transform LeftHandGrip;

        [Tooltip("右手 IK 目标 (扳机/手柄)")]
        public Transform RightHandGrip;

        [Header("生成偏移 (Constraint Offset)")]
        [Tooltip("相对于右手骨骼的位置偏移")]
        public Vector3 SpawnPosOffset;

        [Tooltip("相对于右手骨骼的旋转偏移")]
        public Vector3 SpawnRotOffset;

        // 如果未来有枪口逻辑，可以在这里加
        // public Transform MuzzlePoint;

        public void Initialize()
        {
            // 初始化逻辑，比如重置特效
        }
    }
}
