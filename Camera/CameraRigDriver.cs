using Characters.Player;
using UnityEngine;

namespace Core.CameraSystem
{
    /// <summary>
    /// CameraRigDriver
    /// 职责：将 PlayerRuntimeData 中的“权威方向源(AuthorityRotation)”同步到场景独立的 CameraRig(或 CameraRoot)。
    /// 约定：
    /// - 本脚本应挂在场景中的 CameraRig 根物体上（不要作为 Player 子物体）；
    /// - LateUpdate 执行，以便在角色/MotionDriver 更新完权威数据后，再驱动 Rig。
    /// - 执行顺序：需要早于 CinemachineBrain（否则 Brain 可能读到上一帧的 Rig）。
    ///   如项目里显式设置了 CinemachineBrain 的 Script Execution Order，请确保其顺序值大于本脚本。
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class CameraRigDriver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerController _player;

        [Header("Follow")]
        [Tooltip("跟随的目标。为空时默认使用 Player.transform")]
        [SerializeField] private Transform _followTarget;

        [Tooltip("跟随偏移。建议用于把 Rig 放到角色胸口/骨盆/头部附近的稳定点（世界空间偏移）。")]
        [SerializeField] private Vector3 _followOffset = Vector3.zero;

        [Header("Rotation")]
        [Tooltip("是否同步 Pitch。若关闭，仅同步 Yaw（常用于某些第三人称探索模式）。")]
        [SerializeField] private bool _syncPitch = true;

        [Header("Debug")]
        [Tooltip("开启后每隔 N 帧打印一次 CameraRigDriver 的 LateUpdate 执行信息，用于对比 CinemachineBrain 的执行顺序。")]
        [SerializeField] private bool _debugExecutionOrder = false;

        [Tooltip("每隔多少帧打印一次（避免刷屏）。")]
        [SerializeField] private int _debugLogEveryNFrames = 10;

        private void LateUpdate()
        {
            if (_player == null) return;

            Transform target = _followTarget != null ? _followTarget : _player.transform;

            // 位置：只做跟随（不从属），避免继承 Player 的层级旋转/缩放。
            transform.position = target.position + _followOffset;

            // 旋转：来自权威方向源（由 ViewRotationProcessor 维护）。
            var data = _player.RuntimeData;
            if (_syncPitch)
            {
                transform.rotation = data.AuthorityRotation;
            }
            else
            {
                transform.rotation = Quaternion.Euler(0f, data.AuthorityYaw, 0f);
            }

            if (_debugExecutionOrder)
            {
                int n = Mathf.Max(1, _debugLogEveryNFrames);
                if (Time.frameCount % n == 0)
                {
                    Debug.Log($"[CamDebug] F{Time.frameCount} CameraRigDriver.LateUpdate rigYaw={transform.eulerAngles.y:0.00}");
                }
            }
        }
    }
}
