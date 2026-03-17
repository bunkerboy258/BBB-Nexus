using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace BBBNexus
{
    // 角色骨骼合并工具 用于将换装衣服的蒙皮集成到素体骨架
    // 核心目标 让衣服网格跟随素体骨骼运动 确保换装后的形象一致性
    public class OutfitBoneMerger : EditorWindow
    {
        // 目标素体预制体 提供标准骨骼体系
        private GameObject _baseAvatar;
        // 待合并的衣服模型 需要在场景中才能操作
        private GameObject _outfitInScene;

        // 衣服模型上找到的所有网格渲染器 每个都需要骨骼重映射
        private List<SkinnedMeshRenderer> _outfitRenderers = new List<SkinnedMeshRenderer>();
        // 分析状态标志 用于 UI 联动显示结果面板
        private bool _isAnalyzed = false;

        [MenuItem("Tools/BBB-Nexus/Outfit Bone Merger (骨骼合并工具 有问题待修复)")]
        public static void ShowWindow()
        {
            GetWindow<OutfitBoneMerger>("Bone Merger");
        }

        private void OnGUI()
        {
            GUILayout.Label("角色换装骨骼合并工具 (v2.0)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("请先将素体和衣服都拖入场景中，然后将它们拖到下面的槽位里。", MessageType.Info);
            EditorGUILayout.Space();

            // 资源槽位 支持场景中的实例引用
            EditorGUILayout.BeginVertical("box");
            _baseAvatar = (GameObject)EditorGUILayout.ObjectField("Base Avatar (场景中的素体)", _baseAvatar, typeof(GameObject), true);
            _outfitInScene = (GameObject)EditorGUILayout.ObjectField("Outfit (场景中的衣服)", _outfitInScene, typeof(GameObject), true);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // 第一步 扫描衣服模型找出所有网格
            if (GUILayout.Button("1. 分析衣服结构", GUILayout.Height(30)))
            {
                AnalyzeOutfit();
            }

            // 第二步 执行合并 仅在分析完成后显示
            if (_isAnalyzed && _outfitRenderers.Count > 0)
            {
                // 显示发现的网格数量
                EditorGUILayout.LabelField($"检测到 {_outfitRenderers.Count} 个 SkinnedMeshRenderer", EditorStyles.helpBox);

                EditorGUILayout.Space();

                // 合并按钮 绿色背景强调这是核心操作
                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("2. 执行骨骼合并 (Merge Bones)", GUILayout.Height(40)))
                {
                    MergeBones();
                }
                GUI.backgroundColor = Color.white;
            }
            else if (_outfitInScene != null)
            {
                EditorGUILayout.HelpBox("点击分析按钮来查找衣服上的网格。", MessageType.Info);
            }
        }

        // 第一阶段 分析衣服模型结构 列举所有需要合并的网格
        private void AnalyzeOutfit()
        {
            // 验证必要的输入
            if (_outfitInScene == null)
            {
                EditorUtility.DisplayDialog("错误", "请先将场景中的衣服物体拖入 'Outfit' 槽位。", "OK");
                return;
            }

            // 清空之前的分析结果
            _outfitRenderers.Clear();
            // 递归搜索衣服物体及其所有子物体的网格渲染器
            var renderers = _outfitInScene.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            _outfitRenderers.AddRange(renderers);
            // 标记分析完成 UI 会据此显示第二步按钮
            _isAnalyzed = true;
            Debug.Log($"分析完成，在 '{_outfitInScene.name}' 上找到 {_outfitRenderers.Count} 个网格。");
        }

        // 第二阶段 核心合并逻辑 重映射衣服网格的骨骼引用至素体
        private void MergeBones()
        {
            // 验证双方都已正确指定
            if (_baseAvatar == null || _outfitInScene == null)
            {
                EditorUtility.DisplayDialog("错误", "请先设置素体和衣服！", "OK");
                return;
            }

            // 克隆衣服模型 创建独立副本避免操作污染原始资源
            // 这样即使失败也不会破坏场景中的原物体
            GameObject outfitInstance = Instantiate(_outfitInScene, _outfitInScene.transform.parent);
            outfitInstance.transform.SetPositionAndRotation(_outfitInScene.transform.position, _outfitInScene.transform.rotation);
            outfitInstance.name = _outfitInScene.name + "_Merged";

            // 在新克隆体上重新获取网格渲染器 原数据绑定会失效 需要重新扫描
            var clonedRenderers = outfitInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            // 构建素体骨骼映射表 快速查询骨骼名称到实际 Transform 的映射
            // 这样合并时不需要逐次遍历素体的骨骼树 性能优势明显
            Dictionary<string, Transform> bodyBoneMap = new Dictionary<string, Transform>();
            foreach (var t in _baseAvatar.GetComponentsInChildren<Transform>(true))
            {
                // 避免重复键名 通常不会有同名骨骼但防御一下
                if (!bodyBoneMap.ContainsKey(t.name)) bodyBoneMap.Add(t.name, t);
            }

            // 遍历衣服的每个网格 逐个重映射其骨骼
            List<string> errorLogs = new List<string>();
            foreach (var renderer in clonedRenderers)
            {
                // 获取网格当前绑定的骨骼列表 通常与衣服自己的骨架匹配
                Transform[] oldBones = renderer.bones;
                // 创建新骨骼数组 用于存储重映射后的素体骨骼
                Transform[] newBones = new Transform[oldBones.Length];

                // 遍历每个影响网格的骨骼
                for (int i = 0; i < oldBones.Length; i++)
                {
                    // 跳过空引用 避免报错
                    if (oldBones[i] == null) continue;
                    // 提取骨骼名称 作为查询键
                    string boneName = oldBones[i].name;

                    // 在素体映射表中查找同名骨骼
                    if (bodyBoneMap.TryGetValue(boneName, out Transform targetBone))
                    {
                        // 找到 将衣服的骨骼引用替换为素体的对应骨骼
                        newBones[i] = targetBone;
                    }
                    else
                    {
                        // 未找到 记录错误日志便于后续排查
                        errorLogs.Add($"Mesh [{renderer.name}] 丢失骨骼: {boneName}");
                        // 保留旧骨骼 虽然可能导致蒙皮不完美但至少不会破裂
                        newBones[i] = oldBones[i];
                    }
                }

                // 应用新骨骼列表 这是蒙皮重映射的关键步骤
                renderer.bones = newBones;

                // 修正根骨骼 这决定了网格的基准坐标系
                // 通常应该跟素体的根骨骼一致
                var bodyRootBone = _baseAvatar.GetComponentInChildren<SkinnedMeshRenderer>()?.rootBone;
                if (bodyRootBone != null) renderer.rootBone = bodyRootBone;

                // 修正层级关系 将衣服网格的父物体改为素体
                // 这样衣服网格就会跟随素体的骨骼动画运动
                renderer.transform.SetParent(_baseAvatar.transform, true);
            }

            // 清理工作 删除不再需要的物体

            // 删除原始的衣服实例 因为已经有合并好的克隆体了
            Undo.DestroyObjectImmediate(_outfitInScene);

            // 查找衣服克隆体上可能残留的旧骨骼结构 通常名字叫 Armature 或 Root
            Transform oldArmature = outfitInstance.transform.Find("Armature");
            if (oldArmature == null) oldArmature = outfitInstance.transform.Find("Root");
            // 删除骨骼结构 因为网格已经重映射到素体骨骼了 不需要再保留衣服的骨架
            if (oldArmature != null) Undo.DestroyObjectImmediate(oldArmature.gameObject);

            // 如果衣服的根节点现在已经空了 也一起删除
            if (outfitInstance.transform.childCount == 0)
            {
                Undo.DestroyObjectImmediate(outfitInstance);
            }

            // 结果反馈
            if (errorLogs.Count > 0)
            {
                // 有骨骼丢失 通常是因为衣服使用的骨骼名称与素体不匹配
                string msg = "合并完成，但发现部分骨骼丢失！\n这部分骨骼的网格已被移动到素体下，但蒙皮可能不正确。\n请查看 Console 获取详细列表。";
                EditorUtility.DisplayDialog("警告", msg, "OK");
                // 输出详细错误信息 帮助开发者快速定位问题
                foreach (var err in errorLogs) Debug.LogError(err);
            }
            else
            {
                // 完美匹配 所有衣服骨骼都在素体上找到了对应
                EditorUtility.DisplayDialog("成功", "骨骼完美匹配，并已将衣服网格移动到素体层级下！", "OK");
            }

            // 重置工具状态 准备下一次操作
            _isAnalyzed = false;
            _outfitInScene = null;
        }
    }
}
