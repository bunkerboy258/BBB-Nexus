using System;
using System.Reflection;
using UnityEngine;

namespace BBBNexus
{
    // CinemachineBrain 时序探测器
    // 调试用 负责记录 CinemachineBrain 的 LateUpdate 执行时序
    // 用法：贴到 Main Camera（包含 CinemachineBrain）上 并在 CameraRigDriver 打开调试
    [DefaultExecutionOrder(10000)]
    public class CinemachineBrainOrderProbe : MonoBehaviour
    {
        [SerializeField] private bool _enabled = true;
        [SerializeField] private int _logEveryNFrames = 10;

        private Component _brain;
        private PropertyInfo _activeVirtualCameraProp;
        private PropertyInfo _vcamNameProp;

        private void Awake()
        {
            // 使用反射避免对 Cinemachine 程序集的编译时依赖
            _brain = GetComponent("CinemachineBrain");
            if (_brain == null) return;

            _activeVirtualCameraProp = _brain.GetType().GetProperty("ActiveVirtualCamera", BindingFlags.Instance | BindingFlags.Public);
        }

        private void LateUpdate()
        {
            if (!_enabled) return;

            int n = Mathf.Max(1, _logEveryNFrames);
            if (Time.frameCount % n != 0) return;

            string vcamName = "(no brain)";
            if (_brain != null && _activeVirtualCameraProp != null)
            {
                try
                {
                    object vcam = _activeVirtualCameraProp.GetValue(_brain, null);
                    if (vcam != null)
                    {
                        // 虚拟相机通常实现 Name 属性
                        _vcamNameProp ??= vcam.GetType().GetProperty("Name", BindingFlags.Instance | BindingFlags.Public);
                        vcamName = _vcamNameProp != null ? (string)_vcamNameProp.GetValue(vcam, null) : vcam.ToString();
                    }
                    else
                    {
                        vcamName = "(null)";
                    }
                }
                catch (Exception e)
                {
                    vcamName = $"(reflect err: {e.GetType().Name})";
                }
            }

            Debug.Log($"[CamDebug] F{Time.frameCount} CinemachineBrainProbe.LateUpdate camYaw={transform.eulerAngles.y:0.00} vcam={vcamName}");
        }
    }
}
