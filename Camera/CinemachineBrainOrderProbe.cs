using System;
using System.Reflection;
using UnityEngine;

namespace Core.CameraSystem
{
    /// <summary>
    /// 调试用：探测 CinemachineBrain 的 LateUpdate 执行时序。
    /// 
    /// 用法：
    /// 1) 挂到 Main Camera（CinemachineBrain 所在对象）上。
    /// 2) 同时在 CameraRigDriver 勾选 Debug。
    /// 3) 观察 Console：比较同一帧内 "CameraRigDriver.LateUpdate" 与 "CinemachineBrainProbe.LateUpdate" 的先后顺序。
    /// </summary>
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
            // 避免对 Cinemachine 程序集的编译时依赖（用反射获取）。
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
                        // 虚拟相机通常有 Name 属性（ICinemachineCamera.Name）。
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
