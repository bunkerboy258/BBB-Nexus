using UnityEngine;

namespace BBBNexus
{
    // 摄像机刚体驱动 它是摄像机表现层与角色数据的桥接器
    // 负责把角色黑板中的“权威朝向(AuthorityRotation)”同步到场景中的 CameraRig
    // 并计算场景中的真实瞄准点反向写回黑板供 IK 与逻辑使用
    [DefaultExecutionOrder(-200)]
    public class CameraRigDriver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BBBCharacterController _player;

        [Header("Follow")]
        [Tooltip("跟随的目标 为空时默认使用 Player.transform")]
        [SerializeField] private Transform _followTarget;
        [Tooltip("跟随偏移 建议用于把 Rig 放到角色胸口/骨盆/头部附近的稳定点（世界空间偏移）")]
        [SerializeField] private Vector3 _followOffset = Vector3.zero;

        [Header("Rotation")]
        [Tooltip("是否同步 Pitch 若关闭 仅同步 Yaw（常用于某些第三人称探索模式）")]
        [SerializeField] private bool _syncPitch = true;

        [Header("Aiming (Data Push)")]
        [Tooltip("是否计算并向 Player 输送权威瞄准点数据")]
        [SerializeField] private bool _pushAimData = true;
        [Tooltip("准星射线检测的最大距离")]
        [SerializeField] private float _aimRaycastDistance = 100f;
        [Tooltip("哪些层可以被准星击中？(千万要排除 Player 自身所在的 Layer！否则准星会打在角色后脑勺上)")]
        [SerializeField] private LayerMask _aimCollisionMask = ~0; // 默认 everything

        [Header("Debug")]
        [Tooltip("开启后绘制射线并打印信息")]
        [SerializeField] private bool _debugExecutionOrder = false;
        [SerializeField] private int _debugLogEveryNFrames = 10;

        private Camera _mainCamera;


        private readonly RaycastHit[] _raycastHits = new RaycastHit[1];

        private void Awake()
        {
            // 缓存主摄像机 避免每帧查找导致开销
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                Debug.LogWarning("[BBBNexus] CameraRigDriver场景中未找到 MainCamera！瞄准点计算将失效。");
            }
        }

        private void LateUpdate()
        {
            if (_player == null) return;
            var data = _player.RuntimeData;

            // Rig 跟随与旋转同步
            Transform target = _followTarget != null ? _followTarget : _player.transform;
            transform.position = target.position + _followOffset;

            if (_syncPitch)
            {
                // 完整同步权威朝向
                transform.rotation = data.AuthorityRotation;
            }
            else
            {
                // 仅同步 Yaw 保持 Rig 在水平平面旋转
                transform.rotation = Quaternion.Euler(0f, data.AuthorityYaw, 0f);
            }

            // 瞄准点计算与数据推送（可选）
            if (_pushAimData && _mainCamera != null)
            {
                CalculateAndPushAimPoint(data);
            }

            // 调试输出
            if (_debugExecutionOrder)
            {
                int n = Mathf.Max(1, _debugLogEveryNFrames);
                if (Time.frameCount % n == 0)
                {
                    // Debug.Log($"[CamDebug] F{Time.frameCount} CameraRigDriver.LateUpdate rigYaw={transform.eulerAngles.y:0.00}");
                }
            }
        }

        // 从屏幕中心发射射线 寻找实际的物理交点 并写入黑板（RuntimeData）
        private void CalculateAndPushAimPoint(PlayerRuntimeData data)
        {
            // 获取屏幕正中心的射线（即准星位置）
            Ray screenRay = _mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 finalAimPoint;

            // 执行射线检测（NonAlloc）
            int hitCount = Physics.RaycastNonAlloc(screenRay, _raycastHits, _aimRaycastDistance, _aimCollisionMask);
            if (hitCount > 0)
            {
                finalAimPoint = _raycastHits[0].point;
            }
            else
            {
                finalAimPoint = screenRay.GetPoint(_aimRaycastDistance);
            }

            // 将计算结果写回黑板 供角色逻辑/IK 使用
            data.TargetAimPoint = finalAimPoint;
            data.CameraLookDirection = _mainCamera.transform.forward;
        }
    }
}
