using System.Linq;
using UnityEditor;
using UnityEngine;
using BBBNexus;

namespace BBBNexus.Editor
{
    public static class EnemyRespawnPointSceneTools
    {
        private const string MenuRoot = "GameObject/BBBNexus/Enemy Respawn Points/";

        [MenuItem(MenuRoot + "场景全部预览生成", priority = 40)]
        public static void GenerateAllPreviewsInLoadedScenes()
        {
            var points = GetAllPoints();
            if (points.Length == 0)
            {
                Debug.Log("[EnemyRespawnPoint] 当前已加载场景中没有 EnemyRespawnPoint。");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Generate Enemy Respawn Previews");

            int generated = 0;
            foreach (var point in points)
            {
                if (point == null)
                {
                    continue;
                }

                point.GeneratePreviewAsChild();
                generated++;
            }

            Debug.Log($"[EnemyRespawnPoint] 已为 {generated} 个点位生成预览。");
        }

        [MenuItem(MenuRoot + "场景全部清除预览", priority = 41)]
        public static void ClearAllPreviewsInLoadedScenes()
        {
            var points = GetAllPoints();
            if (points.Length == 0)
            {
                Debug.Log("[EnemyRespawnPoint] 当前已加载场景中没有 EnemyRespawnPoint。");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Clear Enemy Respawn Previews");

            int cleared = 0;
            foreach (var point in points)
            {
                if (point == null || point.PreviewInstance == null)
                {
                    continue;
                }

                point.ClearPreviewInstance();
                cleared++;
            }

            Debug.Log($"[EnemyRespawnPoint] 已清除 {cleared} 个点位的预览。");
        }

        [MenuItem(MenuRoot + "选中物体预览生成", priority = 42)]
        public static void GenerateSelectedPreviews()
        {
            var points = Selection.gameObjects
                .SelectMany(go => go.GetComponentsInChildren<EnemyRespawnPoint>(true))
                .Distinct()
                .ToArray();

            if (points.Length == 0)
            {
                Debug.Log("[EnemyRespawnPoint] 当前选中物体里没有 EnemyRespawnPoint。");
                return;
            }

            foreach (var point in points)
            {
                point.GeneratePreviewAsChild();
            }

            Debug.Log($"[EnemyRespawnPoint] 已为选中的 {points.Length} 个点位生成预览。");
        }

        [MenuItem(MenuRoot + "选中物体清除预览", priority = 43)]
        public static void ClearSelectedPreviews()
        {
            var points = Selection.gameObjects
                .SelectMany(go => go.GetComponentsInChildren<EnemyRespawnPoint>(true))
                .Distinct()
                .ToArray();

            if (points.Length == 0)
            {
                Debug.Log("[EnemyRespawnPoint] 当前选中物体里没有 EnemyRespawnPoint。");
                return;
            }

            int cleared = 0;
            foreach (var point in points)
            {
                if (point.PreviewInstance == null)
                {
                    continue;
                }

                point.ClearPreviewInstance();
                cleared++;
            }

            Debug.Log($"[EnemyRespawnPoint] 已清除选中的 {cleared} 个点位预览。");
        }

        private static EnemyRespawnPoint[] GetAllPoints()
        {
            return Object.FindObjectsByType<EnemyRespawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }
    }
}