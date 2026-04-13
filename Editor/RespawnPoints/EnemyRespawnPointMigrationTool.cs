using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using BBBNexus;
using System.Reflection;
using System.Linq;

namespace BBBNexus.Editor
{
    public static class EnemyRespawnPointMigrationTool
    {
        private const string OldTypeName = "EnemyRespawnPoint";
        private const string OldNamespace = ""; // 旧版本没有命名空间

        [MenuItem("Tools/BBBNexus/迁移 EnemyRespawnPoint (PackId → Prefab)")]
        public static void MigrateAllRespawnPoints()
        {
            // 查找所有旧的 EnemyRespawnPoint 组件
            var allComponents = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var oldComponents = new List<MonoBehaviour>();

            foreach (var component in allComponents)
            {
                if (component == null) continue;

                var type = component.GetType();
                // 检查是否是旧版本（没有命名空间或属于旧命名空间）
                if (type.Name == OldTypeName && string.IsNullOrEmpty(type.Namespace))
                {
                    oldComponents.Add(component);
                }
            }

            if (oldComponents.Count == 0)
            {
                EditorUtility.DisplayDialog("迁移完成", "未找到需要迁移的旧版本 EnemyRespawnPoint 组件。", "确定");
                return;
            }

            int migratedCount = 0;
            int failedCount = 0;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Migrate EnemyRespawnPoint Components");

            foreach (var oldComponent in oldComponents)
            {
                if (oldComponent == null) continue;

                var gameObject = oldComponent.gameObject;

                try
                {
                    // 复制字段值
                    var oldType = oldComponent.GetType();

                    // 获取旧组件的字段值
                    var spawnOnStart = GetFieldValue<bool>(oldComponent, "_spawnOnStart");
                    var brainType = GetFieldValue<string>(oldComponent, "_brainType");
                    var tacticalConfigId = GetFieldValue<string>(oldComponent, "_tacticalConfigId");
                    var logSpawn = GetFieldValue<bool>(oldComponent, "_logSpawn");
                    var packId = GetFieldValue<string>(oldComponent, "_packId");

                    // 销毁旧组件
                    Undo.DestroyObjectImmediate(oldComponent);

                    // 添加新组件
                    var newComponent = Undo.AddComponent<EnemyRespawnPoint>(gameObject);

                    // 设置字段值
                    SetFieldValue(newComponent, "_spawnOnStart", spawnOnStart);
                    SetFieldValue(newComponent, "_brainType", brainType);
                    SetFieldValue(newComponent, "_tacticalConfigId", tacticalConfigId);
                    SetFieldValue(newComponent, "_logSpawn", logSpawn);

                    // 设置 useObjectPool 为 true（推荐）
                    SetFieldValue(newComponent, "_useObjectPool", true);

                    // 提示用户需要手动设置预制体
                    Debug.LogWarning($"[EnemyRespawnPoint迁移] GameObject '{gameObject.name}' 已迁移。原 PackId: '{packId}'。请手动设置预制体字段。", gameObject);

                    migratedCount++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[EnemyRespawnPoint迁移] 迁移 GameObject '{gameObject.name}' 失败: {ex.Message}", gameObject);
                    failedCount++;
                }
            }

            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());

            string message = $"迁移完成！\n" +
                           $"成功: {migratedCount} 个\n" +
                           $"失败: {failedCount} 个\n" +
                           $"\n" +
                           $"注意：需要手动为每个迁移的组件设置预制体字段。\n" +
                           $"原 PackId 信息可在控制台日志中查看。";

            EditorUtility.DisplayDialog("迁移完成", message, "确定");
            Debug.Log($"[EnemyRespawnPoint迁移] {message}");
        }

        [MenuItem("Tools/BBBNexus/检查 EnemyRespawnPoint 状态")]
        public static void CheckRespawnPointStatus()
        {
            var allComponents = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var oldComponents = new List<MonoBehaviour>();
            var newComponents = new List<EnemyRespawnPoint>();
            var mixedObjects = new List<GameObject>();

            foreach (var component in allComponents)
            {
                if (component == null) continue;

                var type = component.GetType();

                // 检查新旧版本
                if (type == typeof(EnemyRespawnPoint))
                {
                    newComponents.Add((EnemyRespawnPoint)component);
                }
                else if (type.Name == OldTypeName && string.IsNullOrEmpty(type.Namespace))
                {
                    oldComponents.Add(component);
                }
            }

            // 检查是否有GameObject同时包含新旧组件
            foreach (var oldComp in oldComponents)
            {
                if (oldComp == null) continue;

                var gameObject = oldComp.gameObject;
                var hasNewComponent = gameObject.GetComponent<EnemyRespawnPoint>() != null;

                if (hasNewComponent)
                {
                    mixedObjects.Add(gameObject);
                }
            }

            string report = $"EnemyRespawnPoint 状态报告:\n" +
                          $"----------------------------------------\n" +
                          $"旧版本 (PackId): {oldComponents.Count} 个\n" +
                          $"新版本 (Prefab): {newComponents.Count} 个\n" +
                          $"混合对象 (新旧共存): {mixedObjects.Count} 个\n" +
                          $"\n" +
                          $"建议操作:\n";

            if (oldComponents.Count > 0)
            {
                report += $"1. 运行迁移工具将旧版本转换为新版本\n";
            }

            if (mixedObjects.Count > 0)
            {
                report += $"2. 清理混合对象（手动删除旧组件）\n";
            }

            if (newComponents.Count > 0)
            {
                var missingPrefabs = newComponents.Count(c => c.Prefab == null);
                report += $"3. {missingPrefabs} 个新版本组件缺少预制体引用\n";
            }

            EditorUtility.DisplayDialog("状态检查", report, "确定");
            Debug.Log($"[EnemyRespawnPoint检查] {report}");
        }

        private static T GetFieldValue<T>(object obj, string fieldName)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null && field.FieldType == typeof(T))
            {
                return (T)field.GetValue(obj);
            }
            return default(T);
        }

        private static void SetFieldValue<T>(object obj, string fieldName, T value)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null && field.FieldType == typeof(T))
            {
                field.SetValue(obj, value);
            }
        }

    }
}