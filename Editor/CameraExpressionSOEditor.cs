using UnityEditor;
using UnityEngine;

#if BBBNEXUS_HAS_CINEMACHINE
using Cinemachine;
#endif

namespace BBBNexus
{
    [CustomEditor(typeof(CameraExpressionSO))]
    public class CameraExpressionSOEditor : UnityEditor.Editor
    {
#if BBBNEXUS_HAS_CINEMACHINE
        private CinemachineVirtualCamera _sourceVcam;

        private static readonly Color GreenBtn = new Color(0.55f, 0.9f, 0.55f);
        private static readonly Color RedBtn   = new Color(0.95f, 0.55f, 0.55f);
#endif

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

#if BBBNEXUS_HAS_CINEMACHINE
            var so = (CameraExpressionSO)target;

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("编辑器工具", EditorStyles.boldLabel);

            _sourceVcam = (CinemachineVirtualCamera)EditorGUILayout.ObjectField(
                "源 VirtualCamera", _sourceVcam,
                typeof(CinemachineVirtualCamera), true);

            if (_sourceVcam == null && GUILayout.Button("自动查找场景中的 VirtualCamera", GUILayout.Height(22)))
                _sourceVcam = FindFirstObjectByType<CinemachineVirtualCamera>();

            EditorGUI.BeginDisabledGroup(_sourceVcam == null);

            var oldBg = GUI.backgroundColor;

            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = GreenBtn;
            if (GUILayout.Button("从 VirtualCamera 抓取", GUILayout.Height(30)))
            {
                Undo.RecordObject(so, "Capture CameraExpression from VirtualCamera");
                CaptureFromVcam(so, _sourceVcam);
                EditorUtility.SetDirty(so);
                Debug.Log($"[CameraExpressionSO] '{so.name}' ← 从 '{_sourceVcam.name}' 抓取完成");
            }

            GUI.backgroundColor = RedBtn;
            if (GUILayout.Button("预览到 VirtualCamera", GUILayout.Height(30)))
            {
                var tpf = _sourceVcam.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
                Undo.RecordObject(_sourceVcam, "Preview CameraExpression to VirtualCamera");
                if (tpf != null) Undo.RecordObject(tpf, "Preview CameraExpression to VirtualCamera");
                PreviewToVcam(so, _sourceVcam);
                EditorUtility.SetDirty(_sourceVcam);
                if (tpf != null) EditorUtility.SetDirty(tpf);
                Debug.Log($"[CameraExpressionSO] '{so.name}' → 预览到 '{_sourceVcam.name}'");
            }

            GUI.backgroundColor = oldBg;
            EditorGUILayout.EndHorizontal();

            EditorGUI.EndDisabledGroup();

            if (_sourceVcam == null)
                EditorGUILayout.HelpBox("拖入场景中的 CinemachineVirtualCamera 以启用抓取 / 预览。", MessageType.Info);
            else
            {
                var tpf = _sourceVcam.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
                if (tpf == null)
                    EditorGUILayout.HelpBox("未找到 Cinemachine3rdPersonFollow 组件，Body 字段将无法抓取/预览。", MessageType.Warning);
            }
#else
            EditorGUILayout.HelpBox("未检测到 Cinemachine（缺少 BBBNEXUS_HAS_CINEMACHINE 宏），工具不可用。", MessageType.Warning);
#endif
        }

#if BBBNEXUS_HAS_CINEMACHINE
        private static void CaptureFromVcam(CameraExpressionSO so, CinemachineVirtualCamera vcam)
        {
            so.TargetFov = vcam.m_Lens.FieldOfView;

            var tpf = vcam.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
            if (tpf != null)
            {
                so.Damping           = tpf.Damping;
                so.ShoulderOffset    = tpf.ShoulderOffset;
                so.VerticalArmLength = tpf.VerticalArmLength;
                so.CameraSide        = tpf.CameraSide;
                so.CameraDistance    = tpf.CameraDistance;
                so.CameraRadius      = tpf.CameraRadius;
                so.DampingIntoCollision = tpf.DampingIntoCollision;
                so.DampingFromCollision = tpf.DampingFromCollision;
            }
        }

        private static void PreviewToVcam(CameraExpressionSO so, CinemachineVirtualCamera vcam)
        {
            vcam.m_Lens.FieldOfView = so.TargetFov;

            var tpf = vcam.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
            if (tpf != null)
            {
                tpf.Damping           = so.Damping;
                tpf.ShoulderOffset    = so.ShoulderOffset;
                tpf.VerticalArmLength = so.VerticalArmLength;
                tpf.CameraSide        = so.CameraSide;
                tpf.CameraDistance    = so.CameraDistance;
                tpf.CameraRadius      = so.CameraRadius;
                tpf.DampingIntoCollision = so.DampingIntoCollision;
                tpf.DampingFromCollision = so.DampingFromCollision;
            }
        }
#endif
    }
}
