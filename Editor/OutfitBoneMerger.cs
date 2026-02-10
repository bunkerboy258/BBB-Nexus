using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class OutfitBoneMerger : EditorWindow
{
    private GameObject _baseAvatar;
    private GameObject _outfitInScene; // 改为引用场景中的物体

    private List<SkinnedMeshRenderer> _outfitRenderers = new List<SkinnedMeshRenderer>();
    private bool _isAnalyzed = false;

    [MenuItem("Tools/Outfit Bone Merger (骨骼合并工具)")]
    public static void ShowWindow()
    {
        GetWindow<OutfitBoneMerger>("Bone Merger");
    }

    private void OnGUI()
    {
        GUILayout.Label("二次元角色换装骨骼合并工具 (v2.0)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("请先将素体和衣服都拖入场景中，然后将它们拖到下面的槽位里。", MessageType.Info);
        EditorGUILayout.Space();

        EditorGUILayout.BeginVertical("box");
        _baseAvatar = (GameObject)EditorGUILayout.ObjectField("Base Avatar (场景中的素体)", _baseAvatar, typeof(GameObject), true);
        _outfitInScene = (GameObject)EditorGUILayout.ObjectField("Outfit (场景中的衣服)", _outfitInScene, typeof(GameObject), true);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();

        if (GUILayout.Button("1. 分析衣服结构", GUILayout.Height(30)))
        {
            AnalyzeOutfit();
        }

        if (_isAnalyzed && _outfitRenderers.Count > 0)
        {
            EditorGUILayout.LabelField($"检测到 {_outfitRenderers.Count} 个 SkinnedMeshRenderer", EditorStyles.helpBox);

            // ... (可以加一个滚动视图显示所有 renderer)

            EditorGUILayout.Space();

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

    private void AnalyzeOutfit()
    {
        if (_outfitInScene == null)
        {
            EditorUtility.DisplayDialog("错误", "请先将场景中的衣服物体拖入 'Outfit' 槽位。", "OK");
            return;
        }
        _outfitRenderers.Clear();
        var renderers = _outfitInScene.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        _outfitRenderers.AddRange(renderers);
        _isAnalyzed = true;
        Debug.Log($"分析完成，在 '{_outfitInScene.name}' 上找到 {_outfitRenderers.Count} 个网格。");
    }

    private void MergeBones()
    {
        if (_baseAvatar == null || _outfitInScene == null)
        {
            EditorUtility.DisplayDialog("错误", "请先设置素体和衣服！", "OK");
            return;
        }

        // --- 【核心修复：克隆与替换】 ---

        // 1. 创建一个与原 Prefab 实例完全无关的“干净”克隆体
        GameObject outfitInstance = Instantiate(_outfitInScene, _outfitInScene.transform.parent);
        outfitInstance.transform.SetPositionAndRotation(_outfitInScene.transform.position, _outfitInScene.transform.rotation);
        outfitInstance.name = _outfitInScene.name + "_Merged";

        // 2. 在新的克隆体上重新获取 Renderers
        var clonedRenderers = outfitInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true);

        // 3. 构建素体骨骼字典
        Dictionary<string, Transform> bodyBoneMap = new Dictionary<string, Transform>();
        foreach (var t in _baseAvatar.GetComponentsInChildren<Transform>(true))
        {
            if (!bodyBoneMap.ContainsKey(t.name)) bodyBoneMap.Add(t.name, t);
        }

        // 4. 遍历克隆体上的 Renderers 并执行合并
        List<string> errorLogs = new List<string>();
        foreach (var renderer in clonedRenderers)
        {
            Transform[] oldBones = renderer.bones;
            Transform[] newBones = new Transform[oldBones.Length];

            for (int i = 0; i < oldBones.Length; i++)
            {
                if (oldBones[i] == null) continue;
                string boneName = oldBones[i].name;

                if (bodyBoneMap.TryGetValue(boneName, out Transform targetBone))
                {
                    newBones[i] = targetBone;
                }
                else
                {
                    errorLogs.Add($"Mesh [{renderer.name}] 丢失骨骼: {boneName}");
                    newBones[i] = oldBones[i]; // 保留旧骨骼以供排查
                }
            }

            renderer.bones = newBones;

            // 修正 RootBone
            var bodyRootBone = _baseAvatar.GetComponentInChildren<SkinnedMeshRenderer>()?.rootBone;
            if (bodyRootBone != null) renderer.rootBone = bodyRootBone;

            // 修正父子关系 (现在可以成功了！)
            renderer.transform.SetParent(_baseAvatar.transform, true);
        }

        // --- 5. 清理工作 ---

        // 删除原始的、未合并的衣服实例
        Undo.DestroyObjectImmediate(_outfitInScene);

        // 找到克隆体中残留的空骨架并删除
        Transform oldArmature = outfitInstance.transform.Find("Armature");
        if (oldArmature == null) oldArmature = outfitInstance.transform.Find("Root");
        if (oldArmature != null) Undo.DestroyObjectImmediate(oldArmature.gameObject);

        // 最后， outfitInstance 本身可能也是个空的根节点，如果它下面没东西了，也删掉
        if (outfitInstance.transform.childCount == 0)
        {
            Undo.DestroyObjectImmediate(outfitInstance);
        }

        // 6. 结果反馈
        if (errorLogs.Count > 0)
        {
            string msg = "合并完成，但发现部分骨骼丢失！\n这部分骨骼的网格已被移动到素体下，但蒙皮可能不正确。\n请查看 Console 获取详细列表。";
            EditorUtility.DisplayDialog("警告", msg, "OK");
            foreach (var err in errorLogs) Debug.LogError(err);
        }
        else
        {
            EditorUtility.DisplayDialog("成功", "骨骼完美匹配，并已将衣服网格移动到素体层级下！", "OK");
        }

        // 重置工具状态
        _isAnalyzed = false;
        _outfitInScene = null;
    }
}
