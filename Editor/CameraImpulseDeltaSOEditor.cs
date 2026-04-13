using UnityEditor;
using UnityEngine;

#if BBBNEXUS_HAS_CINEMACHINE
using Cinemachine;
#endif

namespace BBBNexus
{
    [CustomEditor(typeof(CameraImpulseDeltaSO))]
    public class CameraImpulseDeltaSOEditor : UnityEditor.Editor
    {
#if BBBNEXUS_HAS_CINEMACHINE
        private CinemachineVirtualCamera _sourceVcam;

        private static readonly Color GreenBtn  = new Color(0.55f, 0.9f, 0.55f);
        private static readonly Color YellowBtn = new Color(0.95f, 0.85f, 0.45f);
#endif

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

#if BBBNEXUS_HAS_CINEMACHINE
            var so = (CameraImpulseDeltaSO)target;

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("编辑器工具", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "「叠加预览」将当前场景 VirtualCamera 的值与 Δ 相加后写回，让你直接看到冲击叠加效果。\n完成后可用 Ctrl+Z 撤销。",
                MessageType.Info);

            _sourceVcam = (CinemachineVirtualCamera)EditorGUILayout.ObjectField(
                "目标 VirtualCamera", _sourceVcam,
                typeof(CinemachineVirtualCamera), true);

            if (_sourceVcam == null && GUILayout.Button("自动查找场景中的 VirtualCamera", GUILayout.Height(22)))
                _sourceVcam = FindFirstObjectByType<CinemachineVirtualCamera>();

            EditorGUI.BeginDisabledGroup(_sourceVcam == null);

            var oldBg = GUI.backgroundColor;

            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = GreenBtn;
            if (GUILayout.Button("叠加预览到 VirtualCamera", GUILayout.Height(30)))
            {
                var tpf = _sourceVcam.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
                Undo.RecordObject(_sourceVcam, "Preview CameraImpulseDelta to VirtualCamera");
                if (tpf != null) Undo.RecordObject(tpf, "Preview CameraImpulseDelta to VirtualCamera");
                ApplyDeltaToVcam(so, _sourceVcam);
                EditorUtility.SetDirty(_sourceVcam);
                if (tpf != null) EditorUtility.SetDirty(tpf);
                Debug.Log($"[CameraImpulseDelta] '{so.name}' Δ 已叠加预览到 '{_sourceVcam.name}'（Ctrl+Z 可撤销）");
            }

            GUI.backgroundColor = YellowBtn;
            if (GUILayout.Button("从 VirtualCamera 读取基础值（调试用）", GUILayout.Height(30)))
            {
                var tpf = _sourceVcam.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
                string info = $"VCam '{_sourceVcam.name}' 当前基础值：\n";
                info += $"  FOV = {_sourceVcam.m_Lens.FieldOfView:F2}  →  叠加后 = {_sourceVcam.m_Lens.FieldOfView + so.FovDelta:F2}\n";
                if (tpf != null)
                {
                    info += $"  CameraDistance = {tpf.CameraDistance:F3}  →  叠加后 = {tpf.CameraDistance + so.CameraDistanceDelta:F3}\n";
                    info += $"  ShoulderOffset = {tpf.ShoulderOffset}  →  叠加后 = {tpf.ShoulderOffset + so.ShoulderOffsetDelta}";
                }
                Debug.Log(info);
            }

            GUI.backgroundColor = oldBg;
            EditorGUILayout.EndHorizontal();

            EditorGUI.EndDisabledGroup();

            if (_sourceVcam == null)
                EditorGUILayout.HelpBox("拖入场景中的 CinemachineVirtualCamera 以启用叠加预览。", MessageType.Info);
            else
            {
                var tpf = _sourceVcam.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
                if (tpf == null)
                    EditorGUILayout.HelpBox("未找到 Cinemachine3rdPersonFollow 组件，Body 字段 Δ 将无法预览。", MessageType.Warning);
            }
#else
            EditorGUILayout.HelpBox("未检测到 Cinemachine（缺少 BBBNEXUS_HAS_CINEMACHINE 宏），工具不可用。", MessageType.Warning);
#endif
        }

#if BBBNEXUS_HAS_CINEMACHINE
        private static void ApplyDeltaToVcam(CameraImpulseDeltaSO so, CinemachineVirtualCamera vcam)
        {
            vcam.m_Lens.FieldOfView += so.FovDelta;

            var tpf = vcam.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
            if (tpf != null)
            {
                tpf.CameraDistance    += so.CameraDistanceDelta;
                tpf.ShoulderOffset    += so.ShoulderOffsetDelta;
                tpf.VerticalArmLength += so.VerticalArmLengthDelta;
            }
        }
#endif
    }
}
