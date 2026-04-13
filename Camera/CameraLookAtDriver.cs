using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// Cinemachine LookAt / Follow 靶子的旋转驱动器。
    /// 将 PlayerRuntimeData.AuthorityRotation 每帧同步到此 Transform 的旋转，
    /// 配合 VirtualCamera 的 SameAsFollowTarget Aim 模式，让镜头跟随角色权威朝向。
    ///
    /// 位置、跟随偏移、Damping 全部交给 Cinemachine Transposer / Composer 处理，
    /// 此脚本只负责"唯一 Cinemachine 自己做不到的事"：写入旋转。
    ///
    /// 用法：
    ///   1. 在角色下创建空物体 "CameraTarget"，挂上此脚本
    ///   2. VirtualCamera 的 Follow 和 LookAt 都指向这个物体
    ///   3. VirtualCamera Aim 选 SameAsFollowTarget
    /// </summary>
    public class CameraLookAtDriver : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("BBBCharacterController 引用，为空时自动查找父物体")]
        public BBBCharacterController Player;

        [Header("Settings")]
        [Tooltip("是否同步 Pitch（俯仰角）。关闭后仅同步 Yaw，适合某些探索模式。")]
        public bool SyncPitch = true;

        [Tooltip("Pitch 钳制范围（度）")]
        public Vector2 PitchLimits = new Vector2(-70f, 70f);

        [Header("Debug")]
        [Tooltip("开启后每 60 帧打印一次 Yaw / Pitch / Position 信息")]
        public bool DebugMode = false;

        private int _debugFrameCounter;

        private void Awake()
        {
            if (Player == null)
                Player = GetComponentInParent<BBBCharacterController>();

            if (Player == null)
            {
                Debug.LogError("[CameraLookAtDriver] 未找到 BBBCharacterController，请手动赋值或将此脚本放在角色子物体上。");
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (Player?.RuntimeData == null) return;

            transform.rotation = SyncPitch
                ? Player.RuntimeData.AuthorityRotation
                : Quaternion.Euler(0f, Player.RuntimeData.AuthorityYaw, 0f);

            if (DebugMode)
            {
                _debugFrameCounter++;
                if (_debugFrameCounter % 60 == 0)
                {
                    Debug.Log($"[CameraLookAtDriver] Yaw={Player.RuntimeData.AuthorityYaw:F1}, " +
                              $"Pitch={Player.RuntimeData.AuthorityPitch:F1}, " +
                              $"Position={transform.position:F2}");
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, transform.forward * 2f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, transform.up * 1f);
        }
    }
}
