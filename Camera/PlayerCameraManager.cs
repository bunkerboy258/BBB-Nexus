using UnityEngine;
#if BBBNEXUS_HAS_CINEMACHINE
using Cinemachine;
#endif

namespace BBBNexus
{
    // 玩家摄像机管理器 它是摄像机行为的协调者 负责虚拟相机切换与运行时鼠标控制
    // 同时负责屏幕中央准星的简单绘制 仅作为演示/调试用
    public class PlayerCameraManager : MonoBehaviour
    {
        [Header("监听对象")]
        [SerializeField] private BBBCharacterController _player;

#if BBBNEXUS_HAS_CINEMACHINE
        [Header("虚拟相机")]
        [SerializeField] private CinemachineVirtualCamera _freeLookCam; // 探索
        [SerializeField] private CinemachineVirtualCamera _aimCam; // 瞄准
#endif

        [Header("探索模式缩放 (鼠标滚轮)")]
        [Tooltip("是否允许探索模式使用鼠标滚轮拉近拉远")] 
        public bool EnableFreeLookZoom = true;
        [Tooltip("滚轮缩放速度（每次滚轮增量影响的 FOV 值）")]
        public float FreeLookZoomSpeed = 8f;
        [Tooltip("探索相机最小 FOV（越小越近）")]
        public float FreeLookMinFov = 25f;
        [Tooltip("探索相机最大 FOV（越大越远）")]
        public float FreeLookMaxFov = 70f;
        [Tooltip("缩放平滑时间，越小越快（秒）")]
        public float FreeLookZoomSmoothTime = 0.12f;

        [Header("鼠标控制 (运行时)")]
        [Tooltip("进入运行模式时是否隐藏并锁定鼠标 退出时会自动恢复")]
        public bool HideCursorOnPlay = true;
        [Tooltip("锁定模式：Locked 会锁在窗口中心 Confined 限制在窗口内 None 不锁定")] 
        public CursorLockMode CursorLock = CursorLockMode.Locked;

        [Header("准星 (Screen HUD)")]
        [Tooltip("在屏幕中央显示的准星贴图 仅在运行时绘制 可为空以隐藏")]
        public Texture2D CrosshairTexture;
        [Tooltip("是否显示准星（仅影响运行时显示）")]
        public bool ShowCrosshair = true;
        [Tooltip("准星在屏幕上的像素大小（正方形）")]
        public float CrosshairSize = 32f;

        // 缓存目标 FOV 与速度 以实现平滑过渡
        private float _targetFov;
        private float _fovVelocity;
        private bool _wasAiming = false;

        private void Start()
        {
            // 进入运行时根据配置隐藏并锁定鼠标
            if (HideCursorOnPlay)
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLock;
            }

#if BBBNEXUS_HAS_CINEMACHINE
            // 初始化平滑缩放的目标值为当前相机 FOV
            if (_freeLookCam != null)
            {
                _targetFov = _freeLookCam.m_Lens.FieldOfView;
                _fovVelocity = 0f;
            }
#endif

            _wasAiming = _player != null && _player.RuntimeData != null && _player.RuntimeData.IsAiming;
        }

        private void Update()
        {
            if (_player == null) return;

            // 检测瞄准状态切换：进入/退出瞄准时重置目标 FOV 避免瞬移
            bool isAiming = _player.RuntimeData.IsAiming;
#if BBBNEXUS_HAS_CINEMACHINE
            if (_freeLookCam != null && isAiming != _wasAiming)
            {
                _targetFov = _freeLookCam.m_Lens.FieldOfView;
                _fovVelocity = 0f;
            }
#endif
            _wasAiming = isAiming;

#if BBBNEXUS_HAS_CINEMACHINE
            // 探索模式滚轮缩放：修改目标 FOV（即时） 实际应用采用平滑过渡
            // 仅在非瞄准状态启用 避免与瞄准镜/开火视角冲突
            if (EnableFreeLookZoom && !isAiming && _freeLookCam != null)
            {
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(scroll) > 0.0001f)
                {
                    _targetFov -= scroll * FreeLookZoomSpeed;
                    _targetFov = Mathf.Clamp(_targetFov, FreeLookMinFov, FreeLookMaxFov);
                }

                // 平滑过渡到目标 FOV
                float currentFov = _freeLookCam.m_Lens.FieldOfView;
                float smoothTime = Mathf.Max(0.0001f, FreeLookZoomSmoothTime);
                float newFov = Mathf.SmoothDamp(currentFov, _targetFov, ref _fovVelocity, smoothTime);
                _freeLookCam.m_Lens.FieldOfView = newFov;
            }

            // 优先级切换在 Update 中完成 以确保 CinemachineBrain 在 LateUpdate 做最终选择前已拥有正确优先级
            if (isAiming)
            {
                if (_aimCam != null) _aimCam.Priority = 20;
                if (_freeLookCam != null) _freeLookCam.Priority = 10;
            }
            else
            {
                if (_aimCam != null) _aimCam.Priority = 10;
                if (_freeLookCam != null) _freeLookCam.Priority = 20;
            }
#endif
        }

        // 在 Game 窗口绘制简单的准星 HUD（仅运行时）
        private void OnGUI()
        {
            if (!ShowCrosshair) return;
            if (CrosshairTexture == null) return;
            if (!Application.isPlaying) return; // 仅在运行时绘制（编辑器场景视图也会触发 OnGUI）

            // 屏幕中心坐标
            float x = Screen.width * 0.5f;
            float y = Screen.height * 0.5f;

            float size = Mathf.Max(1f, CrosshairSize);
            Rect r = new Rect(x - size * 0.5f, y - size * 0.5f, size, size);
            GUI.DrawTexture(r, CrosshairTexture);
        }

        // 确保在脚本停用或应用退出时恢复鼠标状态 避免编辑器或系统丢失光标
        private void OnDisable()
        {
            if (HideCursorOnPlay)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }

        private void OnApplicationQuit()
        {
            if (HideCursorOnPlay)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }
    }
}
