using UnityEngine;
using UnityEditor;
using Characters.Player.Data;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using Animancer;

namespace Editors
{
    public class WarpedMotionExtractor : EditorWindow
    {
        [Header("Global Settings")]
        private GameObject _targetPrefab;
        private PlayerSO _targetPlayerSO;
        private int _sampleRate = 60;

        // 批量设置的目标类型
        private WarpedType _batchTargetType = WarpedType.Simple;

        [MenuItem("Tools/BBB-Nexus/Warped Motion 全量烘焙器")]
        public static void ShowWindow()
        {
            GetWindow<WarpedMotionExtractor>("Warped 烘焙");
        }

        private void OnGUI()
        {
            GUILayout.Label("Warped Motion 全量自动化烘焙", EditorStyles.boldLabel);

            _targetPrefab = (GameObject)EditorGUILayout.ObjectField("Character Prefab", _targetPrefab, typeof(GameObject), false);
            _targetPlayerSO = (PlayerSO)EditorGUILayout.ObjectField("Player Config (SO)", _targetPlayerSO, typeof(PlayerSO), false);
            _sampleRate = EditorGUILayout.IntSlider("Sample Rate", _sampleRate, 30, 120);

            GUILayout.Space(15);

            // --- 批量操作面板 ---
            GUI.backgroundColor = new Color(0.8f, 0.8f, 1f);
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Batch Operations (批量操作)", EditorStyles.miniBoldLabel);
            _batchTargetType = (WarpedType)EditorGUILayout.EnumPopup("Target Type", _batchTargetType);

            if (GUILayout.Button("一键设置所有字段为此类型"))
            {
                SetAllFieldsToType(_batchTargetType);
            }
            EditorGUILayout.EndVertical();
            GUI.backgroundColor = Color.white;

            GUILayout.Space(20);

            bool canBake = _targetPrefab != null && _targetPlayerSO != null;
            GUI.backgroundColor = canBake ? new Color(0.6f, 1f, 0.6f) : Color.white;
            GUI.enabled = canBake;
            if (GUILayout.Button("一键全量烘焙 (自动探测 + 覆盖)", GUILayout.Height(40)))
            {
                BakeAllWarpedDataInSO();
            }
            GUI.enabled = true;
            GUI.backgroundColor = Color.white;
        }

        /// <summary>
        /// 批量修改 SO 中所有 WarpedMotionData 的 Type
        /// </summary>
        private void SetAllFieldsToType(WarpedType targetType)
        {
            if (_targetPlayerSO == null) return;

            Undo.RecordObject(_targetPlayerSO, "Batch Set Warped Type");
            var fields = GetWarpedFields();

            foreach (var field in fields)
            {
                WarpedMotionData data = (WarpedMotionData)field.GetValue(_targetPlayerSO);
                if (data != null)
                {
                    data.Type = targetType;
                }
            }

            EditorUtility.SetDirty(_targetPlayerSO);
            Debug.Log($"<color=blue>[Extractor] 已将 {fields.Count} 个动作类型一键设为: {targetType}</color>");
        }

        private List<FieldInfo> GetWarpedFields()
        {
            if (_targetPlayerSO == null) return new List<FieldInfo>();
            return typeof(PlayerSO).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => f.FieldType == typeof(WarpedMotionData)).ToList();
        }

        private void BakeAllWarpedDataInSO()
        {
            Undo.RecordObject(_targetPlayerSO, "Bake All Warped Motion Data");
            var fields = GetWarpedFields();

            if (fields.Count == 0) return;

            int successCount = 0;
            bool anyChange = false;

            for (int i = 0; i < fields.Count; i++)
            {
                FieldInfo fieldInfo = fields[i];
                WarpedMotionData originalData = (WarpedMotionData)fieldInfo.GetValue(_targetPlayerSO);
                EditorUtility.DisplayProgressBar("烘焙中", $"处理: {fieldInfo.Name}", (float)i / fields.Count);

                if (originalData == null || originalData.Clip == null || originalData.Clip.Clip == null) continue;

                // 只有在自动模式下，或者手动模式但有配置点时，才继续
                if (originalData.Type == WarpedType.None && (originalData.WarpPoints == null || originalData.WarpPoints.Count == 0)) continue;

                AnimationClip animClip = originalData.Clip.Clip;
                WarpedMotionData bakedData = new WarpedMotionData();
                bakedData.Clip = originalData.Clip;
                bakedData.EndTime = originalData.EndTime;
                bakedData.EndPhase = originalData.EndPhase;
                bakedData.Type = originalData.Type;
                bakedData.BakedDuration = animClip.length;
                bakedData.HandIKWeightCurve = new AnimationCurve(originalData.HandIKWeightCurve.keys);

                if (originalData.Type == WarpedType.None)
                {
                    bakedData.WarpPoints = originalData.WarpPoints.Select(wp => new WarpPointDef
                    {
                        PointName = wp.PointName,
                        NormalizedTime = wp.NormalizedTime,
                        TargetPositionOffset = wp.TargetPositionOffset
                    }).ToList();
                }

                if (BakeSingleWarpedData(bakedData, animClip))
                {
                    fieldInfo.SetValue(_targetPlayerSO, bakedData);
                    successCount++;
                    anyChange = true;
                }
            }

            EditorUtility.ClearProgressBar();
            if (anyChange)
            {
                EditorUtility.SetDirty(_targetPlayerSO);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("烘焙完成", $"成功更新了 {successCount} 个动画的数据！", "确定");
            }
        }

        private bool BakeSingleWarpedData(WarpedMotionData warpData, AnimationClip clip)
        {
            GameObject tempInstance = Instantiate(_targetPrefab, Vector3.zero, Quaternion.identity);
            tempInstance.hideFlags = HideFlags.HideAndDontSave;
            Animator animator = tempInstance.GetComponent<Animator>();
            if (!animator || animator.runtimeAnimatorController == null) { DestroyImmediate(tempInstance); return false; }

            var overrideCtrl = new AnimatorOverrideController(animator.runtimeAnimatorController);
            animator.runtimeAnimatorController = overrideCtrl;
            var clips = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            foreach (var c in overrideCtrl.animationClips) clips.Add(new KeyValuePair<AnimationClip, AnimationClip>(c, clip));
            overrideCtrl.ApplyOverrides(clips);

            animator.applyRootMotion = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Update(0f);

            float deltaTime = 1f / _sampleRate;
            int totalFrames = Mathf.CeilToInt(clip.length * _sampleRate);
            AnimationCurve curveX = new AnimationCurve(), curveY = new AnimationCurve(), curveZ = new AnimationCurve(), curveRotY = new AnimationCurve();
            Vector3[] absolutePositions = new Vector3[totalFrames + 1];
            Vector3 totalOffset = Vector3.zero;

            for (int i = 0; i <= totalFrames; i++)
            {
                float time = i * deltaTime;
                float normalizedTime = Mathf.Clamp01(time / clip.length);
                animator.Update(deltaTime);
                if (i < 2) { absolutePositions[i] = Vector3.zero; continue; }

                Vector3 worldDelta = animator.deltaPosition;
                Quaternion worldDeltaRot = animator.deltaRotation;
                Vector3 localDelta = tempInstance.transform.InverseTransformVector(worldDelta);
                Vector3 localVel = localDelta / deltaTime;

                float rotVelY = worldDeltaRot.eulerAngles.y;
                if (rotVelY > 180f) rotVelY -= 360f;

                curveX.AddKey(normalizedTime, localVel.x);
                curveY.AddKey(normalizedTime, localVel.y);
                curveZ.AddKey(normalizedTime, localVel.z);
                curveRotY.AddKey(normalizedTime, rotVelY / deltaTime);

                totalOffset += localDelta;
                absolutePositions[i] = totalOffset;

                tempInstance.transform.Translate(worldDelta, Space.World);
                tempInstance.transform.Rotate(worldDeltaRot.eulerAngles, Space.World);
            }

            if (warpData.Type != WarpedType.None)
            {
                warpData.WarpPoints.Clear();

                if (warpData.Type == WarpedType.Vault)
                {
                    float maxY = -999f; int apexIndex = 0;
                    for (int i = 0; i < absolutePositions.Length; i++) { if (absolutePositions[i].y > maxY) { maxY = absolutePositions[i].y; apexIndex = i; } }
                    warpData.WarpPoints.Add(new WarpPointDef { PointName = "Apex", NormalizedTime = (float)apexIndex / totalFrames, BakedLocalOffset = absolutePositions[apexIndex] });
                }
                else if (warpData.Type == WarpedType.Dodge)
                {
                    float maxXZ = -999f; int dodgeIndex = 0;
                    for (int i = 0; i < absolutePositions.Length; i++) { float dist = new Vector2(absolutePositions[i].x, absolutePositions[i].z).magnitude; if (dist > maxXZ) { maxXZ = dist; dodgeIndex = i; } }
                    warpData.WarpPoints.Add(new WarpPointDef { PointName = "MaxDodge", NormalizedTime = (float)dodgeIndex / totalFrames, BakedLocalOffset = absolutePositions[dodgeIndex] });
                }
            }

            if (!warpData.WarpPoints.Any(wp => wp.NormalizedTime >= 0.98f))
            {
                warpData.WarpPoints.Add(new WarpPointDef { PointName = "End", NormalizedTime = 1.0f, BakedLocalOffset = totalOffset });
            }

            warpData.WarpPoints = warpData.WarpPoints.OrderBy(wp => wp.NormalizedTime).ToList();
            Vector3 lastAbsPos = Vector3.zero;
            for (int k = 0; k < warpData.WarpPoints.Count; k++)
            {
                var wp = warpData.WarpPoints[k];
                Vector3 currentAbsPos = wp.BakedLocalOffset;
                wp.BakedLocalOffset = currentAbsPos - lastAbsPos;
                warpData.WarpPoints[k] = wp;
                lastAbsPos = currentAbsPos;
            }

            warpData.LocalVelocityX = curveX;
            warpData.LocalVelocityY = curveY;
            warpData.LocalVelocityZ = curveZ;
            warpData.LocalRotationY = curveRotY;
            warpData.TotalBakedLocalOffset = totalOffset;

            DestroyImmediate(tempInstance);
            return true;
        }
    }
}
