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

        [MenuItem("Tools/BBB-Nexus/Warped Motion 全量烘焙器 (特征点自动探测)")]
        public static void ShowWindow()
        {
            GetWindow<WarpedMotionExtractor>("Warped 烘焙");
        }

        private void OnGUI()
        {
            GUILayout.Label("Warped Motion 全量自动化烘焙", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("一键烘焙 PlayerSO 中所有 WarpedMotionData 字段。\n自动探测 Vault (Y轴极值) 和 Dodge (XZ轴极值) 特征点。", MessageType.Info);

            GUILayout.Space(10);

            _targetPrefab = (GameObject)EditorGUILayout.ObjectField("Character Prefab", _targetPrefab, typeof(GameObject), false);
            _targetPlayerSO = (PlayerSO)EditorGUILayout.ObjectField("Player Config (SO)", _targetPlayerSO, typeof(PlayerSO), false);
            _sampleRate = EditorGUILayout.IntSlider("Sample Rate", _sampleRate, 30, 120);

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

        private void BakeAllWarpedDataInSO()
        {
            Undo.RecordObject(_targetPlayerSO, "Bake All Warped Motion Data");

            var fields = typeof(PlayerSO).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => f.FieldType == typeof(WarpedMotionData)).ToList();

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

                // 覆盖模式：创建全新副本
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

                bool success = BakeSingleWarpedData(bakedData, animClip);

                if (success)
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
            // 1. 初始化模拟环境
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

            // 2. 准备计算变量
            float deltaTime = 1f / _sampleRate;
            int totalFrames = Mathf.CeilToInt(clip.length * _sampleRate);

            AnimationCurve curveX = new AnimationCurve(), curveY = new AnimationCurve(), curveZ = new AnimationCurve(), curveRotY = new AnimationCurve();

            Vector3[] absolutePositions = new Vector3[totalFrames + 1];
            Vector3 totalOffset = Vector3.zero;

            // 3. 第一遍模拟：提取曲线，并记录绝对轨迹
            for (int i = 0; i <= totalFrames; i++)
            {
                float time = i * deltaTime;
                float normalizedTime = Mathf.Clamp01(time / clip.length);
                animator.Update(deltaTime);

                if (i < 2)
                {
                    absolutePositions[i] = Vector3.zero;
                    continue;
                }

                // 计算位移与旋转
                Vector3 worldDelta = animator.deltaPosition;
                Quaternion worldDeltaRot = animator.deltaRotation;
                Vector3 localDelta = tempInstance.transform.InverseTransformVector(worldDelta);
                Vector3 localVel = localDelta / deltaTime;
                float rotVelY = worldDeltaRot.eulerAngles.y;
                if (rotVelY > 180f) rotVelY -= 360f;
                float localRotVelY = rotVelY / deltaTime;

                // 写入曲线
                curveX.AddKey(normalizedTime, localVel.x);
                curveY.AddKey(normalizedTime, localVel.y);
                curveZ.AddKey(normalizedTime, localVel.z);
                curveRotY.AddKey(normalizedTime, localRotVelY);

                totalOffset += localDelta;
                absolutePositions[i] = totalOffset;

                // 移动临时物体
                tempInstance.transform.Translate(worldDelta, Space.World);
                tempInstance.transform.Rotate(worldDeltaRot.eulerAngles, Space.World);
            }

            // 4. 自动特征点探测
            if (warpData.Type != WarpedType.None)
            {
                warpData.WarpPoints.Clear(); // 清空旧点

                if (warpData.Type == WarpedType.Vault)
                {
                    float maxY = -999f; int apexIndex = 0;
                    for (int i = 0; i < absolutePositions.Length; i++) { if (absolutePositions[i].y > maxY) { maxY = absolutePositions[i].y; apexIndex = i; } }

                    warpData.WarpPoints.Add(new WarpPointDef
                    {
                        PointName = "Apex",
                        NormalizedTime = (float)apexIndex / totalFrames,
                        BakedLocalOffset = absolutePositions[apexIndex] // 暂存绝对位置
                    });
                }
                else if (warpData.Type == WarpedType.Dodge)
                {
                    float maxXZ = -999f; int dodgeIndex = 0;
                    for (int i = 0; i < absolutePositions.Length; i++) { float dist = new Vector2(absolutePositions[i].x, absolutePositions[i].z).magnitude; if (dist > maxXZ) { maxXZ = dist; dodgeIndex = i; } }

                    warpData.WarpPoints.Add(new WarpPointDef
                    {
                        PointName = "MaxDodge",
                        NormalizedTime = (float)dodgeIndex / totalFrames,
                        BakedLocalOffset = absolutePositions[dodgeIndex] // 暂存绝对位置
                    });
                }
            }

            // 5. 补上 1.0 的终点
            if (!warpData.WarpPoints.Any(wp => wp.NormalizedTime >= 0.98f))
            {
                warpData.WarpPoints.Add(new WarpPointDef
                {
                    PointName = "End",
                    NormalizedTime = 1.0f,
                    BakedLocalOffset = totalOffset // 终点的绝对位置就是总位移
                });
            }

            // 6. 将绝对位置转换为分段增量
            warpData.WarpPoints = warpData.WarpPoints.OrderBy(wp => wp.NormalizedTime).ToList();
            Vector3 lastAbsPos = Vector3.zero;

            for (int k = 0; k < warpData.WarpPoints.Count; k++)
            {
                var wp = warpData.WarpPoints[k];
                Vector3 currentAbsPos = wp.BakedLocalOffset; // 取出暂存的绝对位置
                wp.BakedLocalOffset = currentAbsPos - lastAbsPos; // 计算增量！
                warpData.WarpPoints[k] = wp;
                lastAbsPos = currentAbsPos;
            }

            // 7. 组装数据
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
