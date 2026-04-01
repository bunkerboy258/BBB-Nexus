using System;
using Newtonsoft.Json;
using NekoGraph;
using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 单位初始化状态 Pack VFS 工具喵~
    /// 负责在单位的本地 Pack 中写入/读取初始化数据
    /// </summary>
    public static class InitStatePackVfs
    {
        public const string InitStatePackId = "initstate";
        public const string UnitInitFile = "/unitinit";

        /// <summary>
        /// 构建初始化数据 JSON 喵~
        /// </summary>
        public static string BuildUnitInitJson(UnitInitData data)
        {
            return JsonConvert.SerializeObject(data, Formatting.Indented);
        }

        /// <summary>
        /// 设置单位初始化数据喵~
        /// </summary>
        public static void SetUnitInitData(BBBCharacterController owner, UnitInitData data)
        {
            var analyser = PackVfs.GetAnalyser(owner, InitStatePackId);
            if (!analyser.WriteFile(InitStatePackId, UnitInitFile, BuildUnitInitJson(data), PackAccessSubjects.SystemMin))
            {
                throw new InvalidOperationException($"Failed to write unit init data to VFS file: {UnitInitFile}");
            }
        }

        /// <summary>
        /// 读取单位初始化数据喵~
        /// </summary>
        public static bool TryGetUnitInitData(BBBCharacterController owner, out UnitInitData data)
        {
            data = null;
            var analyser = PackVfs.GetAnalyser(owner, InitStatePackId);
            var node = analyser.GetNode(InitStatePackId, UnitInitFile, PackAccessSubjects.SystemMin) as VFSNodeData;
            
            if (node == null || string.IsNullOrWhiteSpace(node.DataJson))
            {
                return false;
            }

            data = JsonConvert.DeserializeObject<UnitInitData>(node.DataJson);
            return data != null;
        }

        /// <summary>
        /// 确保初始化 Pack 存在喵~
        /// </summary>
        public static void EnsureInitStatePackExists(BBBCharacterController owner)
        {
            PackVfs.EnsurePackExists(owner, InitStatePackId);
        }
    }
}
