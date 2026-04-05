using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BBBNexus
{
    [CustomEditor(typeof(AudioNormalizationProfile))]
    public class AudioNormalizationProfileEditor : UnityEditor.Editor
    {
        [Tooltip("只扫描这些目录下的 AudioClip（相对 Assets/，留空扫描整个 Assets）")]
        private string _scanFolder = "Assets";

        public override void OnInspectorGUI()
        {
            var profile = (AudioNormalizationProfile)target;

            // 目标 RMS 字段
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("TargetRms"));
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("扫描工具", EditorStyles.boldLabel);

            _scanFolder = EditorGUILayout.TextField("扫描目录（相对 Assets）", _scanFolder);

            EditorGUILayout.HelpBox(
                "扫描目录内的所有 AudioClip，计算 RMS 并生成归一化增益（≤1，只降不升）。\n" +
                "运行时 WeaponAudioUtil.PlayAt 自动查表。",
                MessageType.Info);

            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.55f, 0.9f, 0.55f);

            if (GUILayout.Button("扫描并生成增益表", GUILayout.Height(32)))
            {
                RunScan(profile);
            }

            GUI.backgroundColor = oldBg;

            EditorGUILayout.Space(8);

            // 只显示 Entries（只读，防止误操作）
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.LabelField($"已收录 {profile.Entries.Count} 个音效", EditorStyles.miniLabel);
            EditorGUI.EndDisabledGroup();
        }

        private void RunScan(AudioNormalizationProfile profile)
        {
            string folder = string.IsNullOrWhiteSpace(_scanFolder) ? "Assets" : _scanFolder.Trim();

            // 找出目录内所有 AudioClip GUID
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { folder });
            if (guids.Length == 0)
            {
                EditorUtility.DisplayDialog("归一化扫描", $"在 {folder} 内未找到 AudioClip。", "OK");
                return;
            }

            Undo.RecordObject(profile, "Normalize Audio Clips");

            var entries = new List<AudioNormalizationProfile.Entry>(guids.Length);
            int processed = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) continue;

                EditorUtility.DisplayProgressBar(
                    "音效归一化扫描",
                    $"{clip.name} ({processed + 1}/{guids.Length})",
                    (float)processed / guids.Length);

                float rms  = ComputeRms(clip);
                float gain = rms > 0.0001f
                    ? Mathf.Clamp01(profile.TargetRms / rms)
                    : 1f;

                entries.Add(new AudioNormalizationProfile.Entry
                {
                    Clip = clip,
                    Gain = gain,
                });

                processed++;
            }

            EditorUtility.ClearProgressBar();

            profile.Entries = entries;
            profile.InvalidateLookup();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            Debug.Log($"[AudioNormalization] 扫描完成：{processed} 个 AudioClip，" +
                      $"目标 RMS = {profile.TargetRms:F3}（≈ {20f * Mathf.Log10(profile.TargetRms):F1} dBFS）");
        }

        /// <summary>计算 AudioClip 的 RMS（均方根电平）。</summary>
        private static float ComputeRms(AudioClip clip)
        {
            // GetData 要求 clip 处于 Decompress On Load 或已加载状态
            // 编辑器下 LoadAssetAtPath 会自动解压
            int sampleCount = clip.samples * clip.channels;
            if (sampleCount <= 0) return 0f;

            float[] samples = new float[sampleCount];
            if (!clip.GetData(samples, 0)) return 0f;

            double sum = 0.0;
            foreach (float s in samples)
                sum += s * (double)s;

            return Mathf.Sqrt((float)(sum / sampleCount));
        }
    }
}
