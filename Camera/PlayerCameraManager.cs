using UnityEngine;
using Cinemachine;
using Characters.Player; // 引用 PlayerController

namespace Core.CameraSystem
{
    public class PlayerCameraManager : MonoBehaviour
    {
        [Header("监听对象")]
        [SerializeField] private PlayerController _player;

        [Header("虚拟相机")]
        [SerializeField] private CinemachineVirtualCamera _freeLookCam; // 探索
        [SerializeField] private CinemachineVirtualCamera _aimCam; // 瞄准

        private void Update()
        {
            if (_player == null) return;

            // 优先级切换放在 Update：确保 CinemachineBrain 在 LateUpdate 选择机位前就已确定优先级。
            if (_player.RuntimeData.IsAiming)
            {
                _aimCam.Priority = 20;
                _freeLookCam.Priority = 10;
            }
            else
            {
                _aimCam.Priority = 10;
                _freeLookCam.Priority = 20;
            }
        }
    }
}
