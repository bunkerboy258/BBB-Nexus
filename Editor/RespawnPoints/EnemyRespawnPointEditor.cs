using UnityEditor;
using UnityEngine;
using BBBNexus;

namespace BBBNexus.Editor
{
    [CustomEditor(typeof(EnemyRespawnPoint))]
    [CanEditMultipleObjects]
    public class EnemyRespawnPointEditor : UnityEditor.Editor
    {
        private SerializedProperty _prefabProperty;
        private SerializedProperty _spawnOnStartProperty;
        private SerializedProperty _useObjectPoolProperty;
        private SerializedProperty _brainTypeProperty;
        private SerializedProperty _tacticalConfigIdProperty;
        private SerializedProperty _logSpawnProperty;

        private void OnEnable()
        {
            _prefabProperty = serializedObject.FindProperty("_prefab");
            _spawnOnStartProperty = serializedObject.FindProperty("_spawnOnStart");
            _useObjectPoolProperty = serializedObject.FindProperty("_useObjectPool");
            _brainTypeProperty = serializedObject.FindProperty("_brainType");
            _tacticalConfigIdProperty = serializedObject.FindProperty("_tacticalConfigId");
            _logSpawnProperty = serializedObject.FindProperty("_logSpawn");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_prefabProperty, new GUIContent("敌人预制体", "拖拽包含BBBCharacterController的预制体"));
            ValidatePrefab();

            EditorGUILayout.PropertyField(_spawnOnStartProperty, new GUIContent("启动时生成", "Start()时自动生成敌人"));
            EditorGUILayout.PropertyField(_useObjectPoolProperty, new GUIContent("使用对象池", "使用SimpleObjectPoolSystem管理实例"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("AI 配置", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_brainTypeProperty, new GUIContent("AI 大脑类型", "如: Simple, Patrol, Combat"));
            EditorGUILayout.PropertyField(_tacticalConfigIdProperty, new GUIContent("战术配置ID", "可选，从AIManager加载战术配置"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("调试", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_logSpawnProperty, new GUIContent("生成日志", "生成敌人时在控制台输出日志"));

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(12f);

            DrawActionButtons();
        }

        private void ValidatePrefab()
        {
            if (_prefabProperty.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("请指定一个包含BBBCharacterController的预制体", MessageType.Warning);
                return;
            }

            var prefab = _prefabProperty.objectReferenceValue as GameObject;
            if (prefab == null)
            {
                return;
            }

            var controller = prefab.GetComponent<BBBCharacterController>() ??
                           prefab.GetComponentInChildren<BBBCharacterController>(true);
            if (controller == null)
            {
                EditorGUILayout.HelpBox("预制体不包含BBBCharacterController组件", MessageType.Error);
            }
        }

        private void DrawActionButtons()
        {
            var point = (EnemyRespawnPoint)target;

            // 预览按钮
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("预览生成", GUILayout.Height(28f)))
                {
                    point.GeneratePreviewAsChild();
                }

                if (GUILayout.Button("清除预览", GUILayout.Height(28f)))
                {
                    point.ClearPreviewInstance();
                }
            }

            // 运行时按钮
            EditorGUILayout.Space(4f);
            if (Application.isPlaying)
            {
                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("立即生成", GUILayout.Height(24f)))
                    {
                        point.SpawnNow();
                    }

                    if (GUILayout.Button("刷新生成", GUILayout.Height(24f)))
                    {
                        point.RefreshSpawn();
                    }

                    if (GUILayout.Button("清除怪物", GUILayout.Height(24f)))
                    {
                        point.ClearCurrentActor();
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("运行时按钮在游戏运行后可用", MessageType.Info);
            }

            // 显示当前预览状态
            if (point.PreviewInstance != null)
            {
                EditorGUILayout.HelpBox($"当前预览：{point.PreviewInstance.name}", MessageType.None);
            }
        }
    }
}