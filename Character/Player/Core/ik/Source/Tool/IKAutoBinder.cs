using UnityEngine;
using UnityEngine.Animations.Rigging;
using System.Collections.Generic;
using Characters.Player.Core;


#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Characters.Player.IK
{
    /// <summary>
    /// 编辑器辅助工具：一键绑定 IK Prefab 到角色骨骼，并自动配置 RigBuilder。
    /// 挂载在 IK Prefab 根节点上。
    /// </summary>
    [ExecuteInEditMode]
    public class IKAutoBinder : MonoBehaviour
    {
        [Header("--- 目标角色 (Target) ---")]
        [Tooltip("将角色根物体(带Animator)拖到这里")]
        public Animator TargetCharacter;

        [Header("--- 自身组件引用 (Self) ---")]
        [Tooltip("IK 源脚本，用于自动填入引用")]
        public UnityAnimationRiggingSource SourceScript;

        [Space(10)]
        [Header("Constraints (Drag these from Children in Prefab)")]
        public TwoBoneIKConstraint LeftHandIK;
        public TwoBoneIKConstraint RightHandIK;
        public MultiAimConstraint HeadAim;

#if UNITY_EDITOR
        [ContextMenu("Auto Bind Bones & Setup Rig")]
        public void BindToCharacter()
        {
            // 1. 寻找并验证 Animator
            if (TargetCharacter == null)
            {
                TargetCharacter = GetComponentInParent<Animator>();
                if (TargetCharacter == null)
                {
                    Debug.LogError($"[IKAutoBinder] 绑定失败：请先将 TargetCharacter (角色Animator) 拖入槽位，或将此 Prefab 放入角色子层级！", this);
                    return;
                }
                Debug.Log($"[IKAutoBinder] 自动找到父物体角色: {TargetCharacter.name}");
            }

            // 2. 准备 Rig Builder (在角色身上)
            RigBuilder characterRigBuilder = TargetCharacter.GetComponent<RigBuilder>();
            if (characterRigBuilder == null)
            {
                characterRigBuilder = TargetCharacter.gameObject.AddComponent<RigBuilder>();
                Debug.Log($"[IKAutoBinder] 已自动为 {TargetCharacter.name} 添加 RigBuilder 组件。");
            }

            // 3. 记录撤销操作 (Undo Support)
            List<Object> objectsToRecord = new List<Object>
            {
                TargetCharacter.gameObject,
                characterRigBuilder
            };
            if (LeftHandIK != null) objectsToRecord.Add(LeftHandIK);
            if (RightHandIK != null) objectsToRecord.Add(RightHandIK);
            if (HeadAim != null) objectsToRecord.Add(HeadAim);
            if (SourceScript != null) objectsToRecord.Add(SourceScript);
            Undo.RecordObjects(objectsToRecord.ToArray(), "Bind IK System");

            // 4. 自动注册 Rig Layers
            var rigLayers = GetComponentsInChildren<Rig>(true);
            characterRigBuilder.layers.Clear();
            foreach (var rig in rigLayers)
            {
                characterRigBuilder.layers.Add(new RigLayer(rig, true));
            }
            EditorUtility.SetDirty(characterRigBuilder);
            Debug.Log($"[IKAutoBinder] {rigLayers.Length} 个 Rig Layers 已注册到角色 {TargetCharacter.name}。");

            // 5. 绑定左手骨骼
            if (LeftHandIK != null)
            {
                var data = LeftHandIK.data;
                data.root = TargetCharacter.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                data.mid = TargetCharacter.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                data.tip = TargetCharacter.GetBoneTransform(HumanBodyBones.LeftHand);
                LeftHandIK.data = data;
                EditorUtility.SetDirty(LeftHandIK);
                Debug.Log($"[IKAutoBinder] 左手骨骼绑定成功: {data.tip?.name ?? "None"}");
            }
            else Debug.LogWarning("[IKAutoBinder] LeftHandIK 未配置，跳过左手绑定。");

            // 6. 绑定右手骨骼
            if (RightHandIK != null)
            {
                var data = RightHandIK.data;
                data.root = TargetCharacter.GetBoneTransform(HumanBodyBones.RightUpperArm);
                data.mid = TargetCharacter.GetBoneTransform(HumanBodyBones.RightLowerArm);
                data.tip = TargetCharacter.GetBoneTransform(HumanBodyBones.RightHand);
                RightHandIK.data = data;
                EditorUtility.SetDirty(RightHandIK);
                Debug.Log($"[IKAutoBinder] 右手骨骼绑定成功: {data.tip?.name ?? "None"}");
            }
            else Debug.LogWarning("[IKAutoBinder] RightHandIK 未配置，跳过右手绑定。");

            // 7. 绑定头部骨骼
            if (HeadAim != null)
            {
                var data = HeadAim.data;
                data.constrainedObject = TargetCharacter.GetBoneTransform(HumanBodyBones.Head);
                HeadAim.data = data;
                EditorUtility.SetDirty(HeadAim);
                Debug.Log($"[IKAutoBinder] 头部骨骼绑定成功: {data.constrainedObject?.name ?? "None"}");
            }
            else Debug.LogWarning("[IKAutoBinder] HeadAim 未配置，跳过头部绑定。");

            // 8. 自动填入 Source 脚本的引用
            if (SourceScript != null)
            {
                SerializedObject so = new SerializedObject(SourceScript);
                so.Update();

                // 填入 Constraint 引用
                TrySetObjectReference(so, "_leftHandIK", LeftHandIK);
                TrySetObjectReference(so, "_rightHandIK", RightHandIK);
                TrySetObjectReference(so, "_headLookAtIK", HeadAim);

                // 填入 Target 引用
                if (LeftHandIK != null && LeftHandIK.data.target != null)
                    TrySetObjectReference(so, "_leftHandTarget", LeftHandIK.data.target);

                if (RightHandIK != null && RightHandIK.data.target != null)
                    TrySetObjectReference(so, "_rightHandTarget", RightHandIK.data.target);

                if (HeadAim != null && HeadAim.data.sourceObjects.Count > 0)
                    TrySetObjectReference(so, "_lookAtTarget", HeadAim.data.sourceObjects[0].transform);

                if (so.ApplyModifiedProperties())
                {
                    Debug.Log($"[IKAutoBinder] Source 脚本引用已自动填入。");
                }
            }
            else Debug.LogWarning("[IKAutoBinder] SourceScript 未配置，跳过自动填入。");

            // 9. 强制刷新 Rig Builder
            characterRigBuilder.Build();

            Debug.Log($"<color=green>[IKAutoBinder] 全部绑定成功！请检查 Component 数据。</color>", this);
        }

        private bool TrySetObjectReference(SerializedObject so, string propertyName, Object value)
        {
            var property = so.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
                return true;
            }
            else
            {
                Debug.LogWarning($"[IKAutoBinder] 在 {so.targetObject.GetType().Name} 中找不到名为 '{propertyName}' 的字段，请检查字段名是否匹配。");
                return false;
            }
        }
#endif
    }

    // --- 自定义 Inspector 按钮 ---
#if UNITY_EDITOR
    [CustomEditor(typeof(IKAutoBinder))]
    public class IKAutoBinderEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            IKAutoBinder binder = (IKAutoBinder)target;

            GUILayout.Space(10);
            GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
            if (GUILayout.Button("Auto Bind Bones & Setup Rig", GUILayout.Height(35)))
            {
                binder.BindToCharacter();
            }
            GUI.backgroundColor = Color.white;
        }
    }


#endif
}
