using System;
using Newtonsoft.Json;
using NekoGraph;
using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 弹药 Pack VFS 工具喵~
    /// 负责在 ammo pack 中写入/读取弹药状态文件
    /// 使用 PackVfs 统一层进行路由
    /// </summary>
    public static class AmmoPackVfs
    {
        public const string AmmoPackId = "ammo";

        /// <summary>
        /// 确保 AmmoPack 存在喵~
        /// </summary>
        public static void EnsureAmmoPackExists(BBBCharacterController owner)
        {
            if (owner == null)
            {
                throw new InvalidOperationException("owner cannot be null.");
            }

            PackVfs.EnsurePackExists(owner, AmmoPackId);
        }

        /// <summary>
        /// 构建弹药状态 JSON 喵~
        /// </summary>
        public static string BuildAmmoStateJson(AmmoStateData data)
        {
            return JsonConvert.SerializeObject(data, Formatting.Indented);
        }

        /// <summary>
        /// 构建换弹状态 JSON 喵~
        /// </summary>
        public static string BuildReloadStateJson(ReloadStateData data)
        {
            return JsonConvert.SerializeObject(data, Formatting.Indented);
        }

        /// <summary>
        /// 写入弹药状态文件喵~
        /// </summary>
        public static void SetAmmoState(string weaponSoName, string weaponInstanceId, AmmoStateData data,
            BBBCharacterController owner)
        {
            if (owner == null) throw new InvalidOperationException("owner cannot be null.");
            
            var analyser = PackVfs.GetAnalyser(owner, AmmoPackId);
            var path = $"/{NormalizeSegment(weaponSoName)}/{NormalizeSegment(weaponInstanceId)}.ammo";
            
            if (!analyser.WriteFile(AmmoPackId, path, BuildAmmoStateJson(data), PackAccessSubjects.SystemMin))
            {
                throw new InvalidOperationException($"Failed to write ammo state VFS file: {path}");
            }
        }

        /// <summary>
        /// 读取弹药状态文件喵~
        /// </summary>
        public static bool TryGetAmmoState(string weaponSoName, string weaponInstanceId, out AmmoStateData data,
            BBBCharacterController owner)
        {
            data = null;
            if (owner == null) throw new InvalidOperationException("owner cannot be null.");
            
            var analyser = PackVfs.GetAnalyser(owner, AmmoPackId);
            var path = $"/{NormalizeSegment(weaponSoName)}/{NormalizeSegment(weaponInstanceId)}.ammo";
            
            var node = analyser.GetNode(AmmoPackId, path, PackAccessSubjects.SystemMin) as VFSNodeData;
            if (node == null || string.IsNullOrWhiteSpace(node.DataJson))
            {
                return false;
            }

            data = JsonConvert.DeserializeObject<AmmoStateData>(node.DataJson);
            return data != null;
        }

        /// <summary>
        /// 写入换弹状态文件喵~
        /// </summary>
        public static void SetReloadState(string weaponSoName, string weaponInstanceId, ReloadStateData data,
            BBBCharacterController owner)
        {
            if (owner == null) throw new InvalidOperationException("owner cannot be null.");
            
            var analyser = PackVfs.GetAnalyser(owner, AmmoPackId);
            var path = $"/{NormalizeSegment(weaponSoName)}/{NormalizeSegment(weaponInstanceId)}.reload";
            
            if (!analyser.WriteFile(AmmoPackId, path, BuildReloadStateJson(data), PackAccessSubjects.SystemMin))
            {
                throw new InvalidOperationException($"Failed to write reload state VFS file: {path}");
            }
        }

        /// <summary>
        /// 读取换弹状态文件喵~
        /// </summary>
        public static bool TryGetReloadState(string weaponSoName, string weaponInstanceId, out ReloadStateData data,
            BBBCharacterController owner)
        {
            data = null;
            if (owner == null) throw new InvalidOperationException("owner cannot be null.");
            
            var analyser = PackVfs.GetAnalyser(owner, AmmoPackId);
            var path = $"/{NormalizeSegment(weaponSoName)}/{NormalizeSegment(weaponInstanceId)}.reload";
            
            var node = analyser.GetNode(AmmoPackId, path, PackAccessSubjects.SystemMin) as VFSNodeData;
            if (node == null || string.IsNullOrWhiteSpace(node.DataJson))
            {
                return false;
            }

            data = JsonConvert.DeserializeObject<ReloadStateData>(node.DataJson);
            return data != null;
        }

        // =========================================================
        // 内部辅助方法喵~
        // =========================================================

        private static string NormalizeSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Path segment cannot be empty.", nameof(value));
            }
            return value.Trim()
                .Replace('/', '_')
                .Replace('\\', '_')
                .Replace(':', '_');
        }
    }
}
