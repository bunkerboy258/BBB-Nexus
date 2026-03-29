using System;
using Newtonsoft.Json;
using NekoGraph;
using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 玩家 equipment pack 的静态 VFS 工具。
    /// 负责在已 boot 的 equipment pack 中写入 / 删除 .equipid 文件。
    /// </summary>
    public static class EquipmentPackVfs
    {
        public const string EquipmentPackId = "equipment";
        public const string MainSlotDir = "/mainslot";
        public const string OtherSlotDir = "/otherslot";
        public const string HidePackDir = "/hidepack";
        public const string VirtualPackDir = "/virtualpack";
        public const string MainSlotOccupierId = "__mainslotoccupier_mainhand__";

        public static BasePackData GetEquipmentPack(BBBCharacterController owner = null, UserModel user = null)
        {
            if (ShouldUseLocalBackend(owner))
            {
                owner.EnsureLocalEquipmentPackReady();
                var localPack = FindPackByPackID(owner.LocalPackDataDict, EquipmentPackId);
                if (localPack == null)
                {
                    throw new InvalidOperationException($"Required local pack '{EquipmentPackId}' was not found.");
                }

                return localPack;
            }

            user ??= MainModel.Instance?.CurrentUser;
            if (user == null)
            {
                throw new InvalidOperationException("Current user is null.");
            }

            SaveBootstrapRegistry.EnsureDefaultPacks(user);
            var pack = user?.FindPackByPackID(EquipmentPackId);
            if (pack == null)
            {
                throw new InvalidOperationException($"Required pack '{EquipmentPackId}' was not found in UserModel.PackDataDict.");
            }

            return pack;
        }

        public static void EnsureLayout(BBBCharacterController owner = null, UserModel user = null)
        {
            var analyser = GetEquipmentPackAnalyser(owner, user);
            analyser.CreateDirectory(EquipmentPackId, MainSlotDir, PackAccessSubjects.SystemMin);
            analyser.CreateDirectory(EquipmentPackId, OtherSlotDir, PackAccessSubjects.SystemMin);
            analyser.CreateDirectory(EquipmentPackId, HidePackDir, PackAccessSubjects.SystemMin);
            analyser.CreateDirectory(EquipmentPackId, VirtualPackDir, PackAccessSubjects.SystemMin);
            WriteEquipIdFile(GetMainSlotOccupierVirtualPath(), MainSlotOccupierId, owner, user);
        }

        public static void SetMainSlotItem(int index, string itemId, BBBCharacterController owner = null, UserModel user = null)
        {
            EnsureLayout(owner, user);
            WriteEquipIdFile($"{MainSlotDir}/{GetMainSlotFileName(index)}.equipid", itemId, owner, user);
        }

        public static void ClearMainSlotItem(int index, BBBCharacterController owner = null, UserModel user = null)
        {
            EnsureLayout(owner, user);
            DeletePath($"{MainSlotDir}/{GetMainSlotFileName(index)}.equipid", owner, user);
        }

        public static void SetOtherSlot(EquipmentSlot slot, string itemId, BBBCharacterController owner = null, UserModel user = null)
        {
            EnsureLayout(owner, user);
            WriteEquipIdFile($"{OtherSlotDir}/{GetOtherSlotFileName(slot)}.equipid", itemId, owner, user);
        }

        public static void ClearOtherSlot(EquipmentSlot slot, BBBCharacterController owner = null, UserModel user = null)
        {
            EnsureLayout(owner, user);
            DeletePath($"{OtherSlotDir}/{GetOtherSlotFileName(slot)}.equipid", owner, user);
        }

        public static void SetHideSlot(EquipmentSlot slot, string itemId, BBBCharacterController owner = null, UserModel user = null)
        {
            EnsureLayout(owner, user);
            WriteEquipIdFile($"{HidePackDir}/{GetOtherSlotFileName(slot)}.equipid", itemId, owner, user);
        }

        public static void ClearHideSlot(EquipmentSlot slot, BBBCharacterController owner = null, UserModel user = null)
        {
            EnsureLayout(owner, user);
            DeletePath($"{HidePackDir}/{GetOtherSlotFileName(slot)}.equipid", owner, user);
        }

        public static string BuildEquipIdJson(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                throw new ArgumentException("Item id cannot be empty.", nameof(itemId));
            }

            return JsonConvert.SerializeObject(new EquipIdData { Id = itemId }, Formatting.Indented);
        }

        public static bool TryGetOtherSlotItemId(EquipmentSlot slot, out string itemId, BBBCharacterController owner = null, UserModel user = null)
        {
            return TryReadEquipIdFile($"{OtherSlotDir}/{GetOtherSlotFileName(slot)}.equipid", out itemId, owner, user);
        }

        public static bool TryGetMainSlotItemId(int index, out string itemId, BBBCharacterController owner = null, UserModel user = null)
        {
            return TryReadEquipIdFile($"{MainSlotDir}/{GetMainSlotFileName(index)}.equipid", out itemId, owner, user);
        }

        public static bool TryGetHideSlotItemId(EquipmentSlot slot, out string itemId, BBBCharacterController owner = null, UserModel user = null)
        {
            return TryReadEquipIdFile($"{HidePackDir}/{GetOtherSlotFileName(slot)}.equipid", out itemId, owner, user);
        }

        public static void SetVirtualSlot(string ownerItemId, EquipmentSlot slot, string itemId, BBBCharacterController owner = null, UserModel user = null)
        {
            EnsureLayout(owner, user);
            WriteEquipIdFile(BuildVirtualSlotPath(ownerItemId, slot), itemId, owner, user);
        }

        public static void ClearVirtualSlot(string ownerItemId, EquipmentSlot slot, BBBCharacterController owner = null, UserModel user = null)
        {
            EnsureLayout(owner, user);
            DeletePath(BuildVirtualSlotPath(ownerItemId, slot), owner, user);
        }

        public static bool TryGetVirtualSlotItemId(string ownerItemId, EquipmentSlot slot, out string itemId, BBBCharacterController owner = null, UserModel user = null)
        {
            return TryReadEquipIdFile(BuildVirtualSlotPath(ownerItemId, slot), out itemId, owner, user);
        }

        public static bool TryTakeVirtualSlotItemId(string ownerItemId, EquipmentSlot slot, out string itemId, BBBCharacterController owner = null, UserModel user = null)
        {
            if (!TryGetVirtualSlotItemId(ownerItemId, slot, out itemId, owner, user))
            {
                return false;
            }

            ClearVirtualSlot(ownerItemId, slot, owner, user);
            return true;
        }

        public static bool TryGetOccupiedMainSlotIndex(out int index, BBBCharacterController owner = null, UserModel user = null)
        {
            index = -1;
            EnsureLayout(owner, user);

            for (int i = 1; i <= 5; i++)
            {
                if (TryGetMainSlotItemId(i, out var itemId, owner, user) &&
                    string.Equals(itemId, MainSlotOccupierId, StringComparison.Ordinal))
                {
                    index = i;
                    return true;
                }
            }

            return false;
        }

        public static bool SwapMainHandWithMainSlot(int index, BBBCharacterController owner = null, UserModel user = null)
        {
            EnsureLayout(owner, user);

            if (!TryGetMainSlotItemId(index, out var targetItemId, owner, user) ||
                string.IsNullOrWhiteSpace(targetItemId) ||
                string.Equals(targetItemId, MainSlotOccupierId, StringComparison.Ordinal))
            {
                return false;
            }

            TryGetOtherSlotItemId(EquipmentSlot.MainHand, out var currentMainhandItemId, owner, user);
            TryGetOccupiedMainSlotIndex(out var occupiedIndex, owner, user);

            SwapEquipIdPaths(
                $"{MainSlotDir}/{GetMainSlotFileName(index)}.equipid",
                $"{OtherSlotDir}/{GetOtherSlotFileName(EquipmentSlot.MainHand)}.equipid",
                MainSlotOccupierId,
                owner,
                user);

            if (!string.IsNullOrWhiteSpace(currentMainhandItemId) && occupiedIndex > 0 && occupiedIndex != index)
            {
                SetMainSlotItem(occupiedIndex, currentMainhandItemId, owner, user);
            }

            return true;
        }

        public static bool ReturnMainHandToOccupiedMainSlot(BBBCharacterController owner = null, UserModel user = null)
        {
            EnsureLayout(owner, user);

            if (!TryGetOtherSlotItemId(EquipmentSlot.MainHand, out var currentMainhandItemId, owner, user) ||
                string.IsNullOrWhiteSpace(currentMainhandItemId))
            {
                return false;
            }

            if (!TryGetOccupiedMainSlotIndex(out var occupiedIndex, owner, user))
            {
                return false;
            }

            SetMainSlotItem(occupiedIndex, currentMainhandItemId, owner, user);
            ClearOtherSlot(EquipmentSlot.MainHand, owner, user);
            return true;
        }

        public static void SwapEquipIdPaths(string leftPath, string rightPath, string emptyFallbackForLeft = null, BBBCharacterController owner = null, UserModel user = null)
        {
            EnsureLayout(owner, user);

            TryReadEquipIdFile(leftPath, out var leftItemId, owner, user);
            TryReadEquipIdFile(rightPath, out var rightItemId, owner, user);

            WriteOrDeleteEquipIdFile(leftPath, string.IsNullOrWhiteSpace(rightItemId) ? emptyFallbackForLeft : rightItemId, owner, user);
            WriteOrDeleteEquipIdFile(rightPath, leftItemId, owner, user);
        }

        private static void WriteEquipIdFile(string path, string itemId, BBBCharacterController owner, UserModel user)
        {
            var analyser = GetEquipmentPackAnalyser(owner, user);
            if (!analyser.WriteFile(EquipmentPackId, path, BuildEquipIdJson(itemId), PackAccessSubjects.SystemMin))
            {
                throw new InvalidOperationException($"Failed to write equipment VFS file: {path}");
            }
        }

        private static void WriteOrDeleteEquipIdFile(string path, string itemId, BBBCharacterController owner, UserModel user)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                DeletePath(path, owner, user);
                return;
            }

            WriteEquipIdFile(path, itemId, owner, user);
        }

        private static void DeletePath(string path, BBBCharacterController owner, UserModel user)
        {
            var analyser = GetEquipmentPackAnalyser(owner, user);
            analyser.Delete(EquipmentPackId, path, PackAccessSubjects.SystemMin);
        }

        private static bool TryReadEquipIdFile(string path, out string itemId, BBBCharacterController owner, UserModel user)
        {
            itemId = null;
            var analyser = GetEquipmentPackAnalyser(owner, user);
            var node = analyser.GetNode(EquipmentPackId, path, PackAccessSubjects.SystemMin) as VFSNodeData;
            if (node == null || string.IsNullOrWhiteSpace(node.DataJson))
            {
                return false;
            }

            var data = JsonConvert.DeserializeObject<EquipIdData>(node.DataJson);
            if (data == null || string.IsNullOrWhiteSpace(data.Id))
            {
                return false;
            }

            itemId = data.Id;
            return true;
        }

        private static GraphAnalyser GetEquipmentPackAnalyser(BBBCharacterController owner, UserModel user)
        {
            if (ShouldUseLocalBackend(owner))
            {
                owner.EnsureLocalEquipmentPackReady();
                GetEquipmentPack(owner, user);
                owner.LocalGraphHub.Analyser.RebuildIndex();
                return owner.LocalGraphHub.Analyser;
            }

            user ??= MainModel.Instance?.CurrentUser;
            if (user == null)
            {
                throw new InvalidOperationException("Current user is null.");
            }

            SaveBootstrapRegistry.EnsureDefaultPacks(user);
            GetEquipmentPack(owner, user);
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

            if (!ReferenceEquals(context.PackDataDict, user.GetPlayerPackDict()))
            {
                context.SetPackDataDict(user.GetPlayerPackDict());
            }

            context.Analyser.RebuildIndex();
            return context.Analyser;
        }

        private static bool ShouldUseLocalBackend(BBBCharacterController owner)
        {
            return owner != null && owner.UseLocalEquipmentBackend;
        }

        private static BasePackData FindPackByPackID(System.Collections.Generic.Dictionary<string, BasePackData> packs, string packId)
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

        private static string GetOtherSlotFileName(EquipmentSlot slot)
        {
            return slot switch
            {
                EquipmentSlot.MainHand => "mainhand",
                EquipmentSlot.OffHand => "offhand",
                _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, "Only MainHand and OffHand are supported.")
            };
        }

        private static string GetMainSlotFileName(int index)
        {
            if (index <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "Main slot index must start at 1.");
            }

            return index.ToString();
        }

        private static string BuildVirtualSlotPath(string ownerItemId, EquipmentSlot slot)
        {
            if (string.IsNullOrWhiteSpace(ownerItemId))
            {
                throw new ArgumentException("Owner item id cannot be empty.", nameof(ownerItemId));
            }

            return $"{VirtualPackDir}/{GetOtherSlotFileName(slot)}/{NormalizeSegment(ownerItemId)}.equipid";
        }

        private static string GetMainSlotOccupierVirtualPath()
        {
            return $"{VirtualPackDir}/mainslotoccupier/mainhand.equipid";
        }

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
