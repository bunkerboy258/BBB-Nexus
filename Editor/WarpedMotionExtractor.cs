using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using Animancer;
using System;

namespace BBBNexus
{
    // 扭曲位移全量烘焙器 负责提取非线性运动特征点 存储为离线物理数据
    public class WarpedMotionExtractor : EditorWindow
    {
        [Header("Global Settings")]
        // 用于动画采样的临时代理模型 必须包含物理骨骼与 Animator
        private GameObject _targetPrefab;
        // 配置注入的根节点 开启递归扫描的入口
        private PlayerSO _targetPlayerSO;
        // 支持任意序列化资源作为扫描目标 增加配置灵活性
        private UnityEngine.Object _targetAsset;
        // 物理采样频率 决定离线速度曲线的平滑度
        private int _sampleRate = 60;

        // 批量设置任务的目标运动类型
        private WarpedType _batchTargetType = WarpedType.Simple;

        // 注册至 Unity 菜单工具栏 开启编辑器入口
        [MenuItem("Tools/BBB-Nexus/WarpedMotionBaker v3.4.1")]
        public static void ShowWindow()
        {
            GetWindow<WarpedMotionExtractor>("Warped 烘焙");
        }

        // 绘制编辑器交互界面 执行配置注入与烘焙调度
        private void OnGUI()
        {
            GUILayout.Label("Warped Motion 特化根运动烘焙器 v3.4.1", EditorStyles.boldLabel);

            _targetPrefab = (GameObject)EditorGUILayout.ObjectField("采样模型预制体", _targetPrefab, typeof(GameObject), false);
            _targetPlayerSO = (PlayerSO)EditorGUILayout.ObjectField("玩家总配置", _targetPlayerSO, typeof(PlayerSO), false);
            _targetAsset = EditorGUILayout.ObjectField(new GUIContent("任意序列化文件", "可选择任意序列化文件进行深度遍历"), _targetAsset, typeof(UnityEngine.Object), false);
            _sampleRate = EditorGUILayout.IntSlider("物理采样频率", _sampleRate, 30, 120);

            GUILayout.Space(15);

            GUI.backgroundColor = new Color(0.8f, 0.8f, 1f);
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("批量全局操作", EditorStyles.miniBoldLabel);
            _batchTargetType = (WarpedType)EditorGUILayout.EnumPopup("同步目标类型", _batchTargetType);

            if (GUILayout.Button("强制同步所有数据节点为此类型"))
            {
                SetAllFieldsToType(_batchTargetType);
            }
            EditorGUILayout.EndVertical();
            GUI.backgroundColor = Color.white;

            GUILayout.Space(20);

            bool canBake = _targetPrefab != null && (_targetAsset != null || _targetPlayerSO != null);
            GUI.backgroundColor = canBake ? new Color(0.6f, 1f, 0.6f) : Color.white;
            GUI.enabled = canBake;
            if (GUILayout.Button("一键执行全量烘焙任务 自动探测并覆盖", GUILayout.Height(40)))
            {
                BakeAllWarpedDataInSO();
            }
            GUI.enabled = true;
            GUI.backgroundColor = Color.white;

            EditorGUILayout.HelpBox("请保证模型预制体的根物体已经配置animator组件(还得有avatar)", MessageType.Info);
            EditorGUILayout.HelpBox("注意 若选择较低的烘焙采样率 要注意结果是否产生了贝塞尔过冲", MessageType.Info);
        }

        // 利用反射机制深度遍历配置树 寻找所有扭曲运动数据实例 支持列表与数组嵌套
        private void ScanWarpedMotionDataRecursive(object target, Action<WarpedMotionData, FieldInfo, object> onFound)
        {
            if (target == null) return;
            var type = target.GetType();
            if (!typeof(UnityEngine.Object).IsAssignableFrom(type) && !type.IsClass) return;

            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                var value = field.GetValue(target);
                if (value == null) continue;

                if (field.FieldType == typeof(WarpedMotionData) || value is WarpedMotionData)
                {
                    onFound((WarpedMotionData)value, field, target);
                }
                else if (value is ScriptableObject so)
                {
                    ScanWarpedMotionDataRecursive(so, onFound);
                }
                else if (typeof(System.Collections.IEnumerable).IsAssignableFrom(field.FieldType) && field.FieldType != typeof(string))
                {
                    var enumerable = value as System.Collections.IEnumerable;
                    if (enumerable != null)
                    {
                        foreach (var item in enumerable)
                        {
                            if (item == null) continue;
                            if (item is WarpedMotionData wmd)
                            {
                                onFound(wmd, field, target);
                            }
                            else if (item is ScriptableObject itemSo)
                            {
                                ScanWarpedMotionDataRecursive(itemSo, onFound);
                            }
                        }
                    }
                }
            }
        }

        // 批量修改指定资源下所有数据节点的驱动类型 保持黑板逻辑一致性
        private void SetAllFieldsToType(WarpedType targetType)
        {
            var root = _targetAsset != null ? _targetAsset : (UnityEngine.Object)_targetPlayerSO;
            if (root == null) return;

            Undo.RecordObject(root, "Batch Set Warped Type");
            int count = 0;
            ScanWarpedMotionDataRecursive(root, (data, field, owner) => {
                if (data != null)
                {
                    data.Type = targetType;
                    count++;
                }
            });

            EditorUtility.SetDirty(root);
            Debug.Log($"烘焙器 已完成批量类型同步 覆盖节点数量 {count}");
        }

        // 执行全量烘焙流程 协调反射扫描 物理采样与离线数据持久化写入
        private void BakeAllWarpedDataInSO()
        {
            var root = _targetAsset != null ? _targetAsset : (UnityEngine.Object)_targetPlayerSO;
            if (root == null) return;

            Undo.RecordObject(root, "Bake All Warped Motion Data");
            var allFields = new List<(WarpedMotionData, FieldInfo, object)>();
            ScanWarpedMotionDataRecursive(root, (data, field, owner) => {
                if (data != null)
                    allFields.Add((data, field, owner));
            });

            if (allFields.Count == 0) return;

            int successCount = 0;
            bool anyChange = false;

            for (int i = 0; i < allFields.Count; i++)
            {
                var (originalData, fieldInfo, ownerObj) = allFields[i];
                EditorUtility.DisplayProgressBar("执行全量烘焙任务", $"正在处理字段 {fieldInfo.Name}", (float)i / allFields.Count);

                if (originalData == null || originalData.Clip == null || originalData.Clip.Clip == null) continue;
                if (originalData.Type == WarpedType.None && (originalData.WarpPoints == null || originalData.WarpPoints.Count == 0)) continue;

                AnimationClip animClip = originalData.Clip.Clip;
                // 创建全新的物理数据实例 隔离原始配置节点
                WarpedMotionData bakedData = new WarpedMotionData();
                bakedData.Clip = originalData.Clip;
                bakedData.EndTime = originalData.EndTime;
                bakedData.EndPhase = originalData.EndPhase;
                bakedData.Type = originalData.Type;
                bakedData.BakedDuration = animClip.length;
                bakedData.HandIKWeightCurve = new AnimationCurve(originalData.HandIKWeightCurve.keys);

                // 根据驱动类型 复制或保留特征点位定义
                if (originalData.Type == WarpedType.None)
                {
                    bakedData.WarpPoints = originalData.WarpPoints.Select(wp => new WarpPointDef
                    {
                        PointName = wp.PointName,
                        NormalizedTime = wp.NormalizedTime,
                        TargetPositionOffset = wp.TargetPositionOffset
                    }).ToList();
                }
                else if (originalData.Type == WarpedType.Custom)
                {
                    bakedData.WarpPoints = originalData.WarpPoints.Select(wp => new WarpPointDef
                    {
                        PointName = wp.PointName,
                        NormalizedTime = wp.NormalizedTime,
                        TargetPositionOffset = wp.TargetPositionOffset
                    }).ToList();
                }

                // 核心采样任务 提取物理轨迹与特征点
                if (BakeSingleWarpedData(bakedData, animClip))
                {
                    fieldInfo.SetValue(ownerObj, bakedData);
                    successCount++;
                    anyChange = true;
                }
            }

            EditorUtility.ClearProgressBar();
            if (anyChange)
            {
                EditorUtility.SetDirty(root);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("任务完毕", $"成功更新了 {successCount} 个运动数据实例", "了解");
            }
        }

        // 核心采样逻辑 通过模拟动画步进 提取局部坐标系下的速度曲线与旋转偏移
        private bool BakeSingleWarpedData(WarpedMotionData warpData, AnimationClip clip)
        {
            // 实例化临时采样代理 禁止保存至场景
            GameObject tempInstance = Instantiate(_targetPrefab, Vector3.zero, Quaternion.identity);
            tempInstance.hideFlags = HideFlags.HideAndDontSave;
            Animator animator = tempInstance.GetComponent<Animator>();
            if (!animator || animator.runtimeAnimatorController == null) { DestroyImmediate(tempInstance); return false; }

            // 构造动画覆盖控制器 强制采样目标剪辑
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

            // 物理步进采样 逐帧提取位移增量
            for (int i = 0; i <= totalFrames; i++)
            {
                float time = i * deltaTime;
                float normalizedTime = Mathf.Clamp01(time / clip.length);
                animator.Update(deltaTime);
                if (i < 2) { absolutePositions[i] = Vector3.zero; continue; }

                // 获取根运动增量 并转换至局部坐标空间
                Vector3 worldDelta = animator.deltaPosition;
                Quaternion worldDeltaRot = animator.deltaRotation;
                Vector3 localDelta = tempInstance.transform.InverseTransformVector(worldDelta);
                Vector3 localVel = localDelta / deltaTime;

                float rotVelY = worldDeltaRot.eulerAngles.y;
                if (rotVelY > 180f) rotVelY -= 360f;

                // 写入离线速度曲线
                curveX.AddKey(normalizedTime, localVel.x);
                curveY.AddKey(normalizedTime, localVel.y);
                curveZ.AddKey(normalizedTime, localVel.z);
                curveRotY.AddKey(normalizedTime, rotVelY / deltaTime);

                totalOffset += localDelta;
                absolutePositions[i] = totalOffset;

                // 更新临时代理位置 模拟真实位移轨迹
                tempInstance.transform.Translate(worldDelta, Space.World);
                tempInstance.transform.Rotate(worldDeltaRot.eulerAngles, Space.World);
            }

            // 根据驱动类型 自动探测关键物理特征点 用于意图管线的对齐解算
            if (warpData.Type != WarpedType.None)
            {
                if (warpData.Type != WarpedType.Custom)
                {
                    warpData.WarpPoints.Clear();

                    if (warpData.Type == WarpedType.Vault)
                    {
                        // 寻找垂直位移最高点 标记为翻越顶点
                        float maxY = -999f; int apexIndex = 0;
                        for (int i = 0; i < absolutePositions.Length; i++) { if (absolutePositions[i].y > maxY) { maxY = absolutePositions[i].y; apexIndex = i; } }
                        warpData.WarpPoints.Add(new WarpPointDef { PointName = "Apex", NormalizedTime = (float)apexIndex / totalFrames, BakedLocalOffset = absolutePositions[apexIndex] });
                    }
                    else if (warpData.Type == WarpedType.Dodge)
                    {
                        // 寻找水平位移最大点 标记为闪避极限
                        float maxXZ = -999f; int dodgeIndex = 0;
                        for (int i = 0; i < absolutePositions.Length; i++) { float dist = new Vector2(absolutePositions[i].x, absolutePositions[i].z).magnitude; if (dist > maxXZ) { maxXZ = dist; dodgeIndex = i; } }
                        warpData.WarpPoints.Add(new WarpPointDef { PointName = "MaxDodge", NormalizedTime = (float)dodgeIndex / totalFrames, BakedLocalOffset = absolutePositions[dodgeIndex] });
                    }
                }
            }

            // 强制补充末尾特征点 保证物理对齐完整性
            if (!warpData.WarpPoints.Any(wp => wp.NormalizedTime >= 0.98f))
            {
                if (warpData.Type != WarpedType.Custom)
                {
                    warpData.WarpPoints.Add(new WarpPointDef { PointName = "End", NormalizedTime = 1.0f, BakedLocalOffset = totalOffset });
                }
            }

            warpData.WarpPoints = warpData.WarpPoints.OrderBy(wp => wp.NormalizedTime).ToList();

            // 计算各特征点之间的相对局部位移 供意图管线进行实时插值补偿
            Vector3 lastAbsPos = Vector3.zero;
            for (int k = 0; k < warpData.WarpPoints.Count; k++)
            {
                var wp = warpData.WarpPoints[k];

                // 针对自定义特征点 自动检索其物理时间点对应的采样位置
                if (warpData.Type == WarpedType.Custom && wp.BakedLocalOffset == Vector3.zero)
                {
                    int frameIndex = Mathf.RoundToInt(wp.NormalizedTime * totalFrames);
                    frameIndex = Mathf.Clamp(frameIndex, 0, absolutePositions.Length - 1);
                    wp.BakedLocalOffset = absolutePositions[frameIndex];
                }

                Vector3 currentAbsPos = wp.BakedLocalOffset;
                wp.BakedLocalOffset = currentAbsPos - lastAbsPos;
                warpData.WarpPoints[k] = wp;
                lastAbsPos = currentAbsPos;
            }

            // 封装离线数据载体
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