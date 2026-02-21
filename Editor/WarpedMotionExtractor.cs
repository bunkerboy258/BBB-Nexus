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

        [MenuItem("Tools/BBB-Nexus/Warped Motion 全量烘焙器 (终极切片版)")]
        public static void ShowWindow()
        {
            GetWindow<WarpedMotionExtractor>("Warped 烘焙");
        }

        private void OnGUI()
        {
            GUILayout.Label("Warped Motion 全量自动化烘焙", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("一键扫描并烘焙 PlayerSO 中所有 WarpedMotionData。\n提取 X,Y,Z,Yaw 速度曲线，并自动计算 WarpPoints 的切片积分位移。", MessageType.Info);

            GUILayout.Space(10);

            _targetPrefab = (GameObject)EditorGUILayout.ObjectField("Character Prefab", _targetPrefab, typeof(GameObject), false);
            _targetPlayerSO = (PlayerSO)EditorGUILayout.ObjectField("Player Config (SO)", _targetPlayerSO, typeof(PlayerSO), false);
            _sampleRate = EditorGUILayout.IntSlider("Sample Rate", _sampleRate, 30, 120);

            GUILayout.Space(20);

            bool canBake = _targetPrefab != null && _targetPlayerSO != null;
            GUI.backgroundColor = canBake ? new Color(0.6f, 1f, 0.6f) : Color.white;
            GUI.enabled = canBake;

            if (GUILayout.Button("一键全量烘焙", GUILayout.Height(40)))
            {
                BakeAllWarpedDataInSO();
            }

            GUI.enabled = true;
            GUI.backgroundColor = Color.white;
        }

        private void BakeAllWarpedDataInSO()
        {
            var fields = typeof(PlayerSO).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => f.FieldType == typeof(WarpedMotionData)).ToList();

            if (fields.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "未找到任何 WarpedMotionData 字段。", "确定");
                return;
            }

            int successCount = 0;
            bool anyChange = false;

            for (int i = 0; i < fields.Count; i++)
            {
                FieldInfo fieldInfo = fields[i];
                WarpedMotionData originalData = (WarpedMotionData)fieldInfo.GetValue(_targetPlayerSO);

                EditorUtility.DisplayProgressBar("全量烘焙中", $"处理: {fieldInfo.Name}", (float)i / fields.Count);

                if (originalData == null || originalData.Clip == null || originalData.Clip.Clip == null)
                {
                    Debug.LogWarning($"[跳过] '{fieldInfo.Name}' 为 Null 或未配置动画 Clip。");
                    continue;
                }

                if (originalData.WarpPoints == null || originalData.WarpPoints.Count == 0)
                {
                    Debug.LogWarning($"[跳过] '{fieldInfo.Name}' 未配置任何 WarpPoints。请至少添加一个点！");
                    continue;
                }

                AnimationClip animClip = originalData.Clip.Clip;

                // 创建全新副本以确保修改被 Unity 序列化系统捕获
                WarpedMotionData bakedData = new WarpedMotionData();
                bakedData.Clip = originalData.Clip;
                bakedData.EndTime = originalData.EndTime;
                bakedData.EndPhase = originalData.EndPhase;
                bakedData.BakedDuration = animClip.length;

                // 深度拷贝 WarpPoints 定义
                bakedData.WarpPoints = originalData.WarpPoints.Select(wp => new WarpPointDef
                {
                    PointName = wp.PointName,
                    NormalizedTime = wp.NormalizedTime
                }).ToList();

                bool success = BakeSingleWarpedData(bakedData, animClip);

                if (success)
                {
                    fieldInfo.SetValue(_targetPlayerSO, bakedData);
                    successCount++;
                    anyChange = true;
                    Debug.Log($"<color=green>[成功] '{fieldInfo.Name}' ({animClip.name}) 烘焙完成！</color>");
                }
            }

            EditorUtility.ClearProgressBar();

            if (anyChange)
            {
                EditorUtility.SetDirty(_targetPlayerSO);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("烘焙完成", $"成功烘焙 {successCount} 个动画！\n特征点切片积分已写入。", "确定");
            }
        }

        private bool BakeSingleWarpedData(WarpedMotionData warpData, AnimationClip clip)
        {
            GameObject tempInstance = Instantiate(_targetPrefab, Vector3.zero, Quaternion.identity);
            tempInstance.hideFlags = HideFlags.HideAndDontSave;

            Animator animator = tempInstance.GetComponent<Animator>();
            if (!animator || animator.runtimeAnimatorController == null) { DestroyImmediate(tempInstance); return false; }

            AnimatorOverrideController overrideCtrl = new AnimatorOverrideController(animator.runtimeAnimatorController);
            animator.runtimeAnimatorController = overrideCtrl;
            var clips = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            foreach (var c in overrideCtrl.animationClips) clips.Add(new KeyValuePair<AnimationClip, AnimationClip>(c, clip));
            overrideCtrl.ApplyOverrides(clips);

            animator.applyRootMotion = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Update(0f); // Init

            float deltaTime = 1f / _sampleRate;
            int totalFrames = Mathf.CeilToInt(clip.length * _sampleRate);

            AnimationCurve curveX = new AnimationCurve();
            AnimationCurve curveY = new AnimationCurve();
            AnimationCurve curveZ = new AnimationCurve();
            AnimationCurve curveRotY = new AnimationCurve();

            // 【关键】确保 WarpPoints 严格按时间升序排列，否则积分切片会错乱
            warpData.WarpPoints = warpData.WarpPoints.OrderBy(wp => wp.NormalizedTime).ToList();

            int currentWarpIndex = 0;
            Vector3 currentSegmentAccumulatedOffset = Vector3.zero;
            Vector3 totalLocalOffset = Vector3.zero;

            for (int i = 0; i <= totalFrames; i++)
            {
                float time = i * deltaTime;
                float normalizedTime = Mathf.Clamp01(time / clip.length);

                animator.Update(deltaTime);
                if (i < 2) continue; // Skip first dirty frames

                // 1. 获取位移
                Vector3 worldDelta = animator.deltaPosition;
                Quaternion worldDeltaRot = animator.deltaRotation;

                // 2. 转为局部速度
                Vector3 localDelta = tempInstance.transform.InverseTransformVector(worldDelta);
                Vector3 localVelocity = localDelta / deltaTime;

                // 3. 计算局部 Y 轴旋转速度
                float localRotVelY = worldDeltaRot.eulerAngles.y;
                if (localRotVelY > 180f) localRotVelY -= 360f;
                localRotVelY /= deltaTime;

                // 4. 写入速度曲线
                curveX.AddKey(normalizedTime, localVelocity.x);
                curveY.AddKey(normalizedTime, localVelocity.y);
                curveZ.AddKey(normalizedTime, localVelocity.z);
                curveRotY.AddKey(normalizedTime, localRotVelY);

                // 5. 【核心：切片积分累加】
                currentSegmentAccumulatedOffset += localDelta;
                totalLocalOffset += localDelta;

                // 6. 检查是否跨越了一个 Warp Point
                if (currentWarpIndex < warpData.WarpPoints.Count)
                {
                    var warpPoint = warpData.WarpPoints[currentWarpIndex];
                    if (normalizedTime >= warpPoint.NormalizedTime)
                    {
                        // 记录这段动画原本产生的局部偏移量
                        warpPoint.BakedLocalOffset = currentSegmentAccumulatedOffset;
                        // 记录此时刻的局部旋转 (相对于上一段起点)
                        warpPoint.BakedLocalRotation = Quaternion.Euler(0, tempInstance.transform.localEulerAngles.y, 0);

                        warpData.WarpPoints[currentWarpIndex] = warpPoint;

                        // 【关键】清空累加器，重新开始积下一段的分
                        currentSegmentAccumulatedOffset = Vector3.zero;
                        currentWarpIndex++;
                    }
                }

                // 更新角色基准系，确保下一帧的 InverseTransformVector 正确
                tempInstance.transform.Translate(worldDelta, Space.World);
                tempInstance.transform.Rotate(worldDeltaRot.eulerAngles, Space.World);
            }

            // 处理动画末尾可能因浮点精度漏掉的最后一个点
            while (currentWarpIndex < warpData.WarpPoints.Count)
            {
                var wp = warpData.WarpPoints[currentWarpIndex];
                wp.BakedLocalOffset = currentSegmentAccumulatedOffset;
                warpData.WarpPoints[currentWarpIndex] = wp;
                currentSegmentAccumulatedOffset = Vector3.zero;
                currentWarpIndex++;
            }

            // --- 数据组装 ---
            warpData.LocalVelocityX = curveX;
            warpData.LocalVelocityY = curveY;
            warpData.LocalVelocityZ = curveZ;
            warpData.LocalRotationY = curveRotY;
            warpData.TotalBakedLocalOffset = totalLocalOffset;

            DestroyImmediate(tempInstance);
            return true;
        }
    }
}

