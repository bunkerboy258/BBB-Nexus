using Characters.Player;
using UnityEngine;

namespace Core.CameraSystem
{
    /// <summary>
    /// CameraRigDriver
    /// 职责：
    /// 1. 将 PlayerRuntimeData 中的“权威方向源(AuthorityRotation)”同步到场景独立的 CameraRig。
    /// 2. (新增) 计算真实的物理瞄准点，并反向推送到 PlayerRuntimeData，供 IK 和逻辑使用。
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

        [Header("Aiming (Data Push)")]
        [Tooltip("是否计算并向 Player 输送权威瞄准点数据")]
        [SerializeField] private bool _pushAimData = true;
        [Tooltip("准星射线检测的最大距离")]
        [SerializeField] private float _aimRaycastDistance = 100f;
        [Tooltip("哪些层可以被准星击中？(千万要排除 Player 自身所在的 Layer！否则准星会打在自己后脑勺上)")]
        [SerializeField] private LayerMask _aimCollisionMask = ~0; // 默认 everything

        [Header("Debug")]
        [Tooltip("开启后绘制射线并打印信息")]
        [SerializeField] private bool _debugExecutionOrder = false;
        [SerializeField] private int _debugLogEveryNFrames = 10;

        private Camera _mainCamera;

        private void Awake()
        {
            // 缓存主摄像机
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                Debug.LogWarning("[CameraRigDriver] 场景中未找到 MainCamera！瞄准点计算将失效。");
            }
        }

        private void LateUpdate()
        {
            if (_player == null) return;
            var data = _player.RuntimeData;

            // ==========================================
            // 1. Rig 跟随与旋转同步 (原有逻辑)
            // ==========================================
            Transform target = _followTarget != null ? _followTarget : _player.transform;
            transform.position = target.position + _followOffset;

            if (_syncPitch)
            {
                transform.rotation = data.AuthorityRotation;
            }
            else
            {
                transform.rotation = Quaternion.Euler(0f, data.AuthorityYaw, 0f);
            }

            // ==========================================
            // 2. 瞄准点计算与数据推送 (新增逻辑)
            // ==========================================
            if (_pushAimData && _mainCamera != null)
            {
                CalculateAndPushAimPoint(data);
            }

            // ==========================================
            // 3. Debug
            // ==========================================
            if (_debugExecutionOrder)
            {
                int n = Mathf.Max(1, _debugLogEveryNFrames);
                if (Time.frameCount % n == 0)
                {
                    //Debug.Log($"[CamDebug] F{Time.frameCount} CameraRigDriver.LateUpdate rigYaw={transform.eulerAngles.y:0.00}");
                }
            }
        }

        /// <summary>
        /// 从屏幕中心发射射线，寻找实际的物理交点，并写入黑板。
        /// </summary>
        private void CalculateAndPushAimPoint(Characters.Player.Data.PlayerRuntimeData data)
        {
            // 获取屏幕正中心的射线 (即准星位置)
            Ray screenRay = _mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 finalAimPoint;

            // 执行射线检测
            if (Physics.Raycast(screenRay, out RaycastHit hitInfo, _aimRaycastDistance, _aimCollisionMask))
            {
                // 打中实体，瞄准点就是击中点
                finalAimPoint = hitInfo.point;

                if (_debugExecutionOrder)
                    Debug.DrawLine(screenRay.origin, hitInfo.point, Color.red);
            }
            else
            {
                // 没打中任何东西，瞄准点就是射线尽头的一个虚拟点
                finalAimPoint = screenRay.GetPoint(_aimRaycastDistance);

                if (_debugExecutionOrder)
                    Debug.DrawLine(screenRay.origin, finalAimPoint, Color.yellow);
            }

            // 【核心解耦点】：将计算结果推入黑板
            data.TargetAimPoint = finalAimPoint;
            data.CameraLookDirection = _mainCamera.transform.forward;
        }
    }
}
