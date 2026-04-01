using System;
using System.Collections.Generic;
using NekoGraph;
using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 统一的 Pack VFS 访问层喵~
    /// 提供统一的 Pack 获取、Analyser 获取、路由逻辑
    /// </summary>
    public static class PackVfs
    {
        /// <summary>
        /// 获取指定 Pack 喵~
        /// 自动根据 owner 是否是玩家决定路由到全局或本地
        /// </summary>
        public static BasePackData GetPack(BBBCharacterController owner, string packId)
        {
            if (owner == null)
            {
                throw new InvalidOperationException("owner cannot be null.");
            }

            if (string.IsNullOrEmpty(packId))
            {
                throw new ArgumentException("packId cannot be empty.", nameof(packId));
            }

            var packDict = GetPackDataDict(owner);
            var pack = FindPackByPackID(packDict, packId);
            
            if (pack == null)
            {
                throw new InvalidOperationException($"Required pack '{packId}' was not found.");
            }

            return pack;
        }

        /// <summary>
        /// 获取 Pack 对应的 Analyser 喵~
        /// </summary>
        public static GraphAnalyser GetAnalyser(BBBCharacterController owner, string packId)
        {
            if (owner == null)
            {
                throw new InvalidOperationException("owner cannot be null.");
            }

            if (string.IsNullOrEmpty(packId))
            {
                throw new ArgumentException("packId cannot be empty.", nameof(packId));
            }

            // 确保 Pack 存在
            EnsurePackExists(owner, packId);

            // 路由到正确的 Analyser
            if (IsPlayer(owner))
            {
                // 全局路由
                var graphHub = GraphHub.Instance;
                if (graphHub == null)
                {
                    throw new InvalidOperationException("GraphHub.Instance is null.");
                }

                var context = graphHub.GetContext(GraphInstanceSlot.Player);
                if (context == null || context.Analyser == null)
                {
                    throw new InvalidOperationException("Player graph context or analyser is unavailable.");
                }

                context.Analyser.RebuildIndex();
                return context.Analyser;
            }
            else
            {
                // 本地路由
                if (owner.LocalGraphHub == null || owner.LocalGraphHub.Analyser == null)
                {
                    throw new InvalidOperationException("LocalGraphHub or Analyser is not initialized.");
                }

                owner.LocalGraphHub.Analyser.RebuildIndex();
                return owner.LocalGraphHub.Analyser;
            }
        }

        /// <summary>
        /// 确保 Pack 存在，不存在则从 MetaLib 加载喵~
        /// </summary>
        public static void EnsurePackExists(BBBCharacterController owner, string packId)
        {
            if (owner == null)
            {
                throw new InvalidOperationException("owner cannot be null.");
            }

            if (string.IsNullOrEmpty(packId))
            {
                throw new ArgumentException("packId cannot be empty.", nameof(packId));
            }

            var packDict = GetPackDataDict(owner);

            // 检查是否已存在
            foreach (var pair in packDict)
            {
                if (pair.Value != null && pair.Value.PackID == packId)
                {
                    return;
                }
            }

            if (!PackDefinitionRegistry.TryCreatePack(packId, out var pack))
            {
                throw new InvalidOperationException($"Failed to create pack '{packId}'. Make sure it is registered in PackDefinitionRegistry.");
            }

            // 加载 VFS 并初始化
            if (IsPlayer(owner))
            {
                var graphHub = GraphHub.Instance;
                var context = graphHub.GetContext(GraphInstanceSlot.Player);
                context.Analyser.LoadVFSFromPack(pack);
                context.Analyser.RebuildIndex();
                context.Runner.OnPackDataDictLoaded(packDict);
            }
            else
            {
                owner.EnsureLocalPackStorageReady();
                owner.LocalGraphHub.SetPackDataDict(owner.LocalPackDataDict);
                owner.LocalGraphHub.Analyser.LoadVFSFromPack(pack);
                owner.LocalGraphHub.Analyser.RebuildIndex();
                owner.LocalGraphHub.Runner.OnPackDataDictLoaded(owner.LocalPackDataDict);
            }
        }

        /// <summary>
        /// 获取 Pack 数据字典喵~
        /// </summary>
        public static Dictionary<string, BasePackData> GetPackDataDict(BBBCharacterController owner)
        {
            if (owner == null)
            {
                throw new InvalidOperationException("owner cannot be null.");
            }

            if (IsPlayer(owner))
            {
                // 玩家：使用全局 GraphHub 的 PackDataDict
                var graphHub = GraphHub.Instance;
                if (graphHub == null)
                {
                    throw new InvalidOperationException("GraphHub.Instance is null.");
                }

                var context = graphHub.GetContext(GraphInstanceSlot.Player);
                if (context == null || context.PackDataDict == null)
                {
                    throw new InvalidOperationException("Player graph context or PackDataDict is unavailable.");
                }

                return context.PackDataDict;
            }
            else
            {
                // 小怪：使用本地 PackDataDict
                owner.EnsureLocalPackStorageReady();
                return owner.LocalPackDataDict;
            }
        }

        /// <summary>
        /// 判断是否是玩家喵~
        /// </summary>
        private static bool IsPlayer(BBBCharacterController owner)
        {
            // 通过标签判断是否是玩家
            return owner.CompareTag("Player");
        }

        /// <summary>
        /// 在字典中查找 Pack 喵~
        /// </summary>
        private static BasePackData FindPackByPackID(Dictionary<string, BasePackData> packs, string packId)
        {
            if (packs == null || string.IsNullOrWhiteSpace(packId))
            {
                return null;
            }

            foreach (var pair in packs)
            {
                if (pair.Value != null && string.Equals(pair.Value.PackID, packId, StringComparison.Ordinal))
                {
                    return pair.Value;
                }
            }

            return null;
        }
    }

    [Serializable]
    public sealed class MaxCoreStateData
    {
        public float MaxHealth = 100f;
        public float MaxSanity = 100f;
    }

    public static class CharMaxStatePackVfs
    {
        public const string CharMaxStatePackId = "charmaxstate";
        public const string MaxCoreStatePath = "/core.maxcorestate";

        public static string BuildMaxCoreStateJson(MaxCoreStateData data)
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented);
        }

        public static void SetMaxCoreState(MaxCoreStateData data, BBBCharacterController owner)
        {
            if (owner == null) throw new InvalidOperationException("owner cannot be null.");
            if (data == null) throw new ArgumentNullException(nameof(data));

            var analyser = PackVfs.GetAnalyser(owner, CharMaxStatePackId);
            if (!analyser.WriteFile(CharMaxStatePackId, MaxCoreStatePath, BuildMaxCoreStateJson(data), PackAccessSubjects.SystemMin))
            {
                throw new InvalidOperationException($"Failed to write char max core state VFS file: {MaxCoreStatePath}");
            }
        }

        public static bool TryGetMaxCoreState(out MaxCoreStateData data, BBBCharacterController owner)
        {
            data = null;
            if (owner == null) throw new InvalidOperationException("owner cannot be null.");

            var analyser = PackVfs.GetAnalyser(owner, CharMaxStatePackId);
            var node = analyser.GetNode(CharMaxStatePackId, MaxCoreStatePath, PackAccessSubjects.SystemMin) as VFSNodeData;
            if (node == null || string.IsNullOrWhiteSpace(node.DataJson))
            {
                return false;
            }

            data = Newtonsoft.Json.JsonConvert.DeserializeObject<MaxCoreStateData>(node.DataJson);
            return data != null;
        }

        public static void EnsureDefaultMaxCoreState(BBBCharacterController owner)
        {
            if (owner == null) throw new InvalidOperationException("owner cannot be null.");
            if (TryGetMaxCoreState(out _, owner))
            {
                return;
            }

            SetMaxCoreState(owner.CreateDefaultMaxCoreStateData(), owner);
        }

        public static bool ApplyToOwner(BBBCharacterController owner, bool refillCurrent = true)
        {
            if (owner == null) throw new InvalidOperationException("owner cannot be null.");
            if (!TryGetMaxCoreState(out var data, owner))
            {
                return false;
            }

            owner.ApplyMaxCoreState(data, refillCurrent);
            return true;
        }
    }

    public static class CharMaxStateExeHandler
    {
        [EXEHandler(".maxcorestate", typeof(MaxCoreStateData))]
        public static void Handle(
            string dataJson,
            SignalContext context,
            BasePackData pack,
            GraphRunner runner,
            string packInstanceID)
        {
            var data = Newtonsoft.Json.JsonConvert.DeserializeObject<MaxCoreStateData>(dataJson);
            if (data == null)
            {
                Debug.LogWarning("[CharMaxStateExeHandler] DataJson 反序列化失败。");
                return;
            }

            var owner = ResolveOwner(context?.Args);
            if (owner == null)
            {
                Debug.LogWarning("[CharMaxStateExeHandler] context.Args 中未找到 BBBCharacterController，跳过应用。");
                return;
            }

            owner.ApplyMaxCoreState(data, refillCurrent: true);
        }

        private static BBBCharacterController ResolveOwner(object args)
        {
            switch (args)
            {
                case BBBCharacterController owner:
                    return owner;
                case GameObject go:
                    return go.GetComponent<BBBCharacterController>() ?? go.GetComponentInChildren<BBBCharacterController>(true);
                case Component component:
                    return component.GetComponent<BBBCharacterController>() ?? component.GetComponentInChildren<BBBCharacterController>(true);
                default:
                    return null;
            }
        }
    }
}
